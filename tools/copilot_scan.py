#!/usr/bin/env python3
import os, re, json, sys
from datetime import datetime

root = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
assets = os.path.join(root, 'Assets')
config_path = os.path.join(assets, 'Editor', 'CopilotRules', 'copilot_config.json')

# defaults
cfg = {
    'CheckNativeArray': True,
    'CheckNulls': True,
    'CheckSealedClasses': True,
    'CheckBurstRef': True,
    'CheckEntityNull': True,
    'CheckStoryComments': True,
    'CheckUnusedPrivateSymbols': False,
    'IgnoreFolderTokens': ['Library','Temp','Packages','Build','obj'],
    'ExemptEditorNullChecks': True
}

if os.path.exists(config_path):
    try:
        with open(config_path, 'r', encoding='utf-8') as f:
            parsed = json.load(f)
            cfg.update(parsed)
    except Exception as e:
        print('Failed to parse config:', e, file=sys.stderr)

patterns = {
    'NativeArray': re.compile(r"\bNativeArray\s*<"),
    'Null': re.compile(r"\bnull\b|==\s*null|=\s*null"),
    'EntityNull': re.compile(r"\bEntity\.Null\b"),
    'Class': re.compile(r"^\s*(public|internal|private|protected)?\s*(partial\s+)?(sealed\s+)?(static\s+)?(abstract\s+)?class\s+([A-Za-z_][A-Za-z0-9_]*)", re.M),
    'Burst': re.compile(r"\[BurstCompile\b"),
}

# inline ignore markers: ASCII and emoji
ignore_markers = ['copilot-ignore', '🚫', '🛑']

results = []

for dirpath, dirnames, filenames in os.walk(assets):
    # skip ignored tokens
    rel = os.path.relpath(dirpath, root).replace('\\','/')
    skip = False
    for tok in cfg.get('IgnoreFolderTokens', []):
        if tok and ('/' + tok + '/' in ('/' + rel + '/')):
            skip = True
            break
    if skip: continue

    for fn in filenames:
        if not fn.lower().endswith('.cs'): continue
        full = os.path.join(dirpath, fn)
        relPath = os.path.relpath(full, root).replace('\\','/')
        # avoid scanning the scanner implementation itself to prevent self-flagging
        if 'Assets/Editor/CopilotRules' in relPath:
            continue
        try:
            with open(full, 'r', encoding='utf-8') as f:
                text = f.read()
        except Exception as e:
            results.append(f"[Error] Could not read {relPath}: {e}")
            continue

        if cfg.get('CheckNativeArray') and patterns['NativeArray'].search(text):
            for m in patterns['NativeArray'].finditer(text):
                idx = m.start()
                ln = text.count('\n', 0, idx) + 1
                col = idx - text.rfind('\n', 0, idx)
                results.append(f"[NativeArray] {relPath}:{ln}:{col} -> consider DynamicBuffer or BlobAsset configuration")

        if cfg.get('CheckNulls'):
            exempt_editor = cfg.get('ExemptEditorNullChecks', True) and ('/Editor/' in ('/' + relPath))
            if not exempt_editor:
                for m in patterns['Null'].finditer(text):
                    idx = m.start()
                    ln = text.count('\n', 0, idx) + 1
                    col = idx - text.rfind('\n', 0, idx)
                    line = text.splitlines()[ln - 1]
                    if any(tok in line for tok in ignore_markers):
                        continue
                    results.append(f"[Null] {relPath}:{ln}:{col} -> 'null' found. Policy: no nulls allowed.")

        if cfg.get('CheckSealedClasses'):
            for m in patterns['Class'].finditer(text):
                sealed = m.group(3)
                staticg = m.group(4)
                if not sealed and not staticg:
                    name = m.group(6)
                    idx = m.start()
                    ln = text.count('\n', 0, idx) + 1
                    results.append(f"[Unsealed Class] {relPath}:{ln}:1 -> class '{name}' is not sealed.")

        if cfg.get('CheckEntityNull') and patterns['EntityNull'].search(text):
            for m in patterns['EntityNull'].finditer(text):
                idx = m.start(); ln = text.count('\n', 0, idx) + 1
                line = text.splitlines()[ln - 1]
                if any(tok in line for tok in ignore_markers):
                    continue
                results.append(f"[Entity.Null] {relPath}:{ln}:1 -> usage of Entity.Null found. Replace with explicit sentinel or valid Entity reference.")

        if cfg.get('CheckBurstRef'):
            lines = text.splitlines()
            for i,l in enumerate(lines):
                if patterns['Burst'].search(l):
                    end = min(len(lines), i+20)
                    for j in range(i+1, end):
                        if 'ref ' in lines[j]:
                            line = lines[j]
                            if any(tok in line for tok in ignore_markers):
                                continue
                            # allow 'ref SystemState' signatures used by ISystem methods
                            if 'ref SystemState' in line:
                                continue
                            results.append(f"[Burst+ref] {relPath}:{j+1}:1 -> 'ref' parameter near [BurstCompile] attribute. Avoid refs in Burst.")
                        if lines[j].lstrip().startswith('{') or lines[j].lstrip().startswith('['):
                            break

            # Story comment enforcement for public Systems/Components/Authoring types
            if cfg.get('CheckStoryComments'):
                public_type_pattern = re.compile(r"^\s*public\s+(partial\s+)?(struct|class)\s+([A-Za-z_][A-Za-z0-9_]*)([^\n\r]*)", re.M)
                interesting_tokens = ["SystemBase", "ISystem", "ComponentSystemBase", "IComponentData", "ISharedComponentData", "IBufferElementData", "MonoBehaviour", "Baker", "Authoring", "IBaker"]
                for m in public_type_pattern.finditer(text):
                    decl_idx = m.start()
                    ln = text.count('\n', 0, decl_idx) + 1
                    col = decl_idx - text.rfind('\n', 0, decl_idx)
                    decl_remainder = m.group(4) or ''
                    found = False
                    if any(t in decl_remainder for t in interesting_tokens):
                        found = True
                    else:
                        # check the next two lines for tokens
                        lines = text.splitlines()
                        end = min(len(lines), ln - 1 + 3)
                        for k in range(ln, end + 1):
                            if any(t in lines[k - 1] for t in interesting_tokens):
                                found = True; break

                    if not found:
                        continue

                    # check inline ignore on decl line
                    decl_line = text.splitlines()[ln - 1]
                    if any(tok in decl_line for tok in ignore_markers):
                        continue

                    # search up for a story comment line (skip attributes)
                    story_idx = -1
                    prev = ln - 2
                    lines = text.splitlines()
                    while prev >= 0 and ln - prev <= 6:
                        s = lines[prev].lstrip()
                        if s == '':
                            prev -= 1; continue
                        if s.startswith('['):
                            prev -= 1; continue
                        if s.startswith('//') and 'Story:' in s:
                            story_idx = prev; break
                        break

                    if story_idx == -1:
                        # respect editor exemption
                        if cfg.get('ExemptEditorNullChecks', True) and ('/Editor/' in ('/' + relPath)):
                            continue
                        results.append(f"[Story] {relPath}:{ln}:{col} -> public system/component/authoring type missing leading '// Story: ...' comment.")
                    else:
                        txt = lines[story_idx].strip()
                        idx = txt.lower().find('story:')
                        if idx >= 0:
                            after = txt[idx + len('story:'):].strip()
                            if len(after) < 20:
                                results.append(f"[Story] {relPath}:{story_idx+1}:1 -> 'Story' comment too short (min 20 chars).")

# prepare output
if not results:
    results = ["No rule violations found."]

debug_dir = os.path.join(assets, '.debug')
if not os.path.exists(debug_dir): os.makedirs(debug_dir)
out_path = os.path.join(debug_dir, 'copilot_scanlog_latest.txt')
with open(out_path, 'w', encoding='utf-8') as f:
    for r in results:
        f.write(r + '\n')

print(f"Copilot Rules: Found {len(results)} potential issues. Saved to {out_path}")
for r in results:
    print(r)

sys.exit(0)
