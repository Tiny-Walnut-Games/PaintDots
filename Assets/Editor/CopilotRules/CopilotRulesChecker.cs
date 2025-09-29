using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Editor helper for enforcing the project's Copilot rules.
// - Sealed class
// - No nulls reported (scanner only)
// - Detects NativeArray usage, ref in Burst methods, and unsealed classes
// This intentionally performs read-only scans and emits Console messages.
public sealed class CopilotRulesChecker : EditorWindow
{
    private const string ConfigPath = "Assets/Editor/CopilotRules/copilot_config.json";
    private static readonly string[] DefaultIgnoreFolders = new[] {"Library", "Temp", "Packages", "Build", "obj"};

    private Vector2 _scroll;
    private List<string> _results = new();
    private CopilotConfig _config;

    [MenuItem("Tools/Copilot Rules/Run Scan")]
    public static void RunScanMenu()
    {
        // Run a scan non-interactively and save to a stable "latest" file. The window is optional.
        var runner = CreateInstance<CopilotRulesChecker>();
        runner.LoadConfig();
        runner.RunScan();
        SaveScanLogLatest(runner._results);
        // clean up temporary instance
        ScriptableObject.DestroyImmediate(runner);
    }

    private string GetScanLog()
    {
        var log = new System.Text.StringBuilder();
        log.AppendLine("<CopilotScanLog>");
        foreach (var result in _results)
        {
            log.AppendLine($"  <Result>{System.Security.SecurityElement.Escape(result)}</Result>");
        }
        log.AppendLine("</CopilotScanLog>");
        return log.ToString();
    }

    // Save the current scan log to Assets/.debug. Creates the folder if missing.
    private void SaveScanLog()
    {
        try
        {
            // Ensure there is at least a recent scan
            if (_results == null || _results.Count == 0)
                RunScan();

            var assetsPath = Application.dataPath; // .../Project/Assets
            // Save inside the Assets folder under .debug so it shows up in project (matches existing Assets/.debug/console.txt)
            var debugDir = Path.GetFullPath(Path.Combine(assetsPath, ".debug"));
            if (!Directory.Exists(debugDir))
                Directory.CreateDirectory(debugDir);

            var fileName = "copilot_scanlog_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
            var fullPath = Path.Combine(debugDir, fileName);
            File.WriteAllText(fullPath, GetScanLog());
            // Refresh the AssetDatabase so the file appears in the Editor immediately
            AssetDatabase.Refresh();
            Debug.Log("Saved Copilot scan log to: " + fullPath);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to save copilot scan log: " + e.Message);
        }
    }

    // Static helper for menu-run path: writes a stable "latest" file using provided results.
    private static void SaveScanLogLatest(List<string> results)
    {
        try
        {
            var assetsPath = Application.dataPath;
            var debugDir = Path.GetFullPath(Path.Combine(assetsPath, ".debug"));
            if (!Directory.Exists(debugDir))
                Directory.CreateDirectory(debugDir);

            var fullPath = Path.Combine(debugDir, "copilot_scanlog_latest.txt");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<CopilotScanLog>");
            if (results != null)
            {
                foreach (var r in results)
                    sb.AppendLine($"  <Result>{System.Security.SecurityElement.Escape(r)}</Result>");
            }
            sb.AppendLine("</CopilotScanLog>");

            File.WriteAllText(fullPath, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log("Saved Copilot latest scan log to: " + fullPath);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to save copilot latest scan log: " + e.Message);
        }
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Run Scan Now"))
        {
            RunScan();
        }

        if (GUILayout.Button("Save Scan Log"))
        {
            SaveScanLog();
        }

        if (GUILayout.Button("Open Config"))
        {
            if (File.Exists(ConfigPath))
                EditorUtility.OpenWithDefaultApp(Path.GetFullPath(ConfigPath));
            else
                Debug.LogWarning("Copilot config not found: " + ConfigPath);
        }

        // GUI toggle to persist ExemptEditorNullChecks in copilot_config.json
        GUILayout.Space(6);
        if (_config == null) LoadConfig();
        EditorGUI.BeginChangeCheck();
        var newExempt = GUILayout.Toggle(_config.ExemptEditorNullChecks, "Exempt files under '/Editor/' from null checks");
        if (EditorGUI.EndChangeCheck())
        {
            _config.ExemptEditorNullChecks = newExempt;
            SaveConfig();
        }

        GUILayout.Space(6);
        // Quick hint for developers: use an inline comment marker to suppress the scanner for a specific line.
        GUILayout.Label("Inline ignore: add a comment like '// copilot-ignore' or '// 🚫 copilot-ignore' on the same line to skip it.");
    GUILayout.Label("Story comment: add a line above public Systems/Components/MonoBehaviours with '// Story: <one sentence>'");

        GUILayout.Label("Scan Results:");
        _scroll = GUILayout.BeginScrollView(_scroll);
        foreach (var r in _results)
        {
            GUILayout.Label(r);
        }
        GUILayout.EndScrollView();
    }

    // Helper: check if a source line contains an inline ignore marker (ASCII or emoji)
    private static bool LineHasIgnoreMarker(string line)
    {
        if (string.IsNullOrEmpty(line)) return false;
        if (line.IndexOf("copilot-ignore", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (line.Contains("🚫") || line.Contains("🛑")) return true;
        return false;
    }

    private void LoadConfig()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                _config = CopilotConfig.Default();
                var dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(ConfigPath, JsonUtility.ToJson(_config, true));
                AssetDatabase.Refresh();
                Debug.Log("Created default copilot_config.json at " + ConfigPath);
                return;
            }

            var json = File.ReadAllText(ConfigPath);
            var parsed = JsonUtility.FromJson<CopilotConfig>(json);
            _config = parsed != null ? parsed : CopilotConfig.Default();
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to load copilot config: " + e.Message);
            _config = CopilotConfig.Default();
        }
    }

    // Persist the runtime config to disk
    private void SaveConfig()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(ConfigPath, JsonUtility.ToJson(_config, true));
            AssetDatabase.Refresh();
            Debug.Log("Saved Copilot config to " + ConfigPath);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to save copilot config: " + e.Message);
        }
    }

    private void RunScan()
    {
        _results.Clear();
        LoadConfig();

        var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
        var files = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories)
            .Where(p => !_config.ShouldIgnorePath(p)).ToArray();

    var nativeArrayPattern = new Regex(@"\bNativeArray\s*<", RegexOptions.Compiled);
    var nullPattern = new Regex(@"\bnull\b|==\s*null|=\s*null", RegexOptions.Compiled);
    var entityNullPattern = new Regex(@"\bEntity\.Null\b", RegexOptions.Compiled);
    var classPattern = new Regex(@"^\s*(public|internal|private|protected)?\s*(partial\s+)?(sealed\s+)?(static\s+)?(abstract\s+)?class\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled | RegexOptions.Multiline);
        var burstAttrPattern = new Regex(@"\[BurstCompile\b", RegexOptions.Compiled);

        foreach (var file in files)
        {
            var relPath = MakeRelative(projectRoot, file);
            // Avoid scanning the scanner's own implementation to prevent self-flagging
            if (relPath.IndexOf("Editor/CopilotRules", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            string[] lines;
            try
            {
                lines = File.ReadAllLines(file);
            }
            catch (Exception e)
            {
                _results.Add($"[Error] Could not read {relPath}: {e.Message}");
                continue;
            }

            var text = string.Join("\n", lines);

            // NativeArray usage
            if (_config.CheckNativeArray && nativeArrayPattern.IsMatch(text))
            {
                foreach (Match m in nativeArrayPattern.Matches(text))
                {
                    var idx = m.Index;
                    var (ln, col) = GetLineCol(lines, idx);
                    _results.Add($"[NativeArray] {relPath}:{ln}:{col} -> consider DynamicBuffer or BlobAsset configuration");
                }
            }

            // null usages
            if (_config.CheckNulls)
            {
                // Optionally exempt Editor folder files from null checks to allow editor-only nullable patterns
                if (!(_config.ExemptEditorNullChecks && relPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    if (nullPattern.IsMatch(text))
                    {
                        foreach (Match m in nullPattern.Matches(text))
                        {
                            var idx = m.Index;
                            var (ln, col) = GetLineCol(lines, idx);
                            // skip if the source line contains an inline ignore marker
                            if (LineHasIgnoreMarker(lines[ln - 1]))
                                continue;
                            _results.Add($"[Null] {relPath}:{ln}:{col} -> 'null' found. Policy: no nulls allowed.");
                        }
                    }
                }
            }

            // Unsealed classes
            if (_config.CheckSealedClasses)
            {
                foreach (Match m in classPattern.Matches(text))
                {
                    var sealedGroup = m.Groups[3];
                    var staticGroup = m.Groups[4];
                    // treat static classes as sealed-equivalent
                    if (!sealedGroup.Success && !staticGroup.Success)
                    {
                        var name = m.Groups[6].Value;
                        var idx = m.Index;
                        var (ln, col) = GetLineCol(lines, idx);
                        _results.Add($"[Unsealed Class] {relPath}:{ln}:{col} -> class '{name}' is not sealed.");
                    }
                }
            }

            // Entity.Null usages
            if (_config.CheckEntityNull && entityNullPattern.IsMatch(text))
            {
                foreach (Match m in entityNullPattern.Matches(text))
                {
                    var idx = m.Index;
                    var (ln, col) = GetLineCol(lines, idx);
                    // skip if the source line contains an inline ignore marker
                    if (LineHasIgnoreMarker(lines[ln - 1]))
                        continue;
                    _results.Add($"[Entity.Null] {relPath}:{ln}:{col} -> usage of Entity.Null found. Replace with explicit sentinel or valid Entity reference.");
                }
            }

            // Heuristic: detect unused private fields and methods (declaration with no other references)
            if (_config.CheckUnusedPrivateSymbols)
            {
                // Simple field decl: private [modifiers] Type name;
                var fieldPattern = new Regex(@"\bprivate\b[^;\n=]*\b([A-Za-z_][A-Za-z0-9_]*)\s*(=\s*[^;]+)?;", RegexOptions.Compiled);
                foreach (Match m in fieldPattern.Matches(text))
                {
                    var name = m.Groups[1].Value;
                    // search the whole project for usages of the name
                    var usageCount = 0;
                    foreach (var f in files)
                    {
                        var fileText = File.ReadAllText(f);
                        var occurrences = Regex.Matches(fileText, "\b" + Regex.Escape(name) + "\b").Count;
                        usageCount += occurrences;
                        if (usageCount > 1) break; // more than declaration
                    }
                    if (usageCount <= 1)
                    {
                        var idx = m.Index;
                        var (ln, col) = GetLineCol(lines, idx);
                        _results.Add($"[Unused Private Field] {relPath}:{ln}:{col} -> private field '{name}' appears unused.");
                    }
                }

                // Simple method decl: private [modifiers] ReturnType Name(
                var methodPattern = new Regex(@"\bprivate\b[^\n\{]*\b([A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.Compiled);
                foreach (Match m in methodPattern.Matches(text))
                {
                    var name = m.Groups[1].Value;
                    var usageCount = 0;
                    foreach (var f in files)
                    {
                        var fileText = File.ReadAllText(f);
                        var occurrences = Regex.Matches(fileText, "\b" + Regex.Escape(name) + "\b").Count;
                        usageCount += occurrences;
                        if (usageCount > 1) break;
                    }
                    if (usageCount <= 1)
                    {
                        var idx = m.Index;
                        var (ln, col) = GetLineCol(lines, idx);
                        _results.Add($"[Unused Private Method] {relPath}:{ln}:{col} -> private method '{name}' appears unused.");
                    }
                }
            }
            // Burst + ref parameter detection
            if (_config.CheckBurstRef)
            {
                for (var i = 0; i < lines.Length; ++i)
                {
                    if (burstAttrPattern.IsMatch(lines[i]))
                    {
                        // Look ahead up to 20 lines for method signature containing 'ref '
                        var end = Math.Min(lines.Length, i + 20);
                        for (var j = i + 1; j < end; ++j)
                        {
                            if (lines[j].Contains("ref "))
                            {
                                // skip if line intentionally marked to ignore
                                if (LineHasIgnoreMarker(lines[j]))
                                    continue;
                                // Allow the common ISystem signature 'ref SystemState' - it's expected and safe
                                if (lines[j].IndexOf("ref SystemState", StringComparison.Ordinal) >= 0)
                                    continue;
                                _results.Add($"[Burst+ref] {relPath}:{j + 1}:1 -> 'ref' parameter near [BurstCompile] attribute. Avoid refs in Burst.");
                            }
                            // stop when we hit opening brace of method body or attribute for next member
                            if (lines[j].TrimStart().StartsWith("{") || lines[j].TrimStart().StartsWith("[") )
                                break;
                        }
                    }
                }
            }

            // Story comment enforcement for public Systems/Components/Authoring types
            if (_config.CheckStoryComments)
            {
                // Matches: public [partial] (class|struct) Name [rest of line]
                var publicTypePattern = new Regex("^\\s*public\\s+(partial\\s+)?(struct|class)\\s+([A-Za-z_][A-Za-z0-9_]*)([^\\n\\r]*)", RegexOptions.Compiled | RegexOptions.Multiline);
                var matches = publicTypePattern.Matches(text);
                var interestingTokens = new[] { "SystemBase", "ISystem", "ComponentSystemBase", "IComponentData", "ISharedComponentData", "IBufferElementData", "MonoBehaviour", "Baker", "Authoring", "IBaker" };
                foreach (Match m in matches)
                {
                    var declIdx = m.Index;
                    var (ln, col) = GetLineCol(lines, declIdx);
                    var declRemainder = m.Groups[4].Value ?? string.Empty;
                    var found = false;
                    // check remainder and a few following lines for tokens indicating a System/Component/Authoring
                    if (interestingTokens.Any(t => declRemainder.IndexOf(t, StringComparison.Ordinal) >= 0))
                        found = true;
                    else
                    {
                        var end = Math.Min(lines.Length, ln - 1 + 3);
                        for (var k = ln; k <= end; ++k)
                        {
                            if (interestingTokens.Any(t => lines[k - 1].IndexOf(t, StringComparison.Ordinal) >= 0))
                            {
                                found = true;
                                break;
                            }
                        }
                    }

                    if (!found)
                        continue;

                    // If the declaration line has an inline ignore marker, skip enforcing
                    if (LineHasIgnoreMarker(lines[ln - 1]))
                        continue;

                    // Look up for a preceding non-attribute, non-blank line - allow attributes ([...]) between story comment and decl
                    var prev = ln - 2; // zero-based index to check lines[prev]
                    var storyLineIndex = -1;
                    while (prev >= 0 && ln - prev <= 6) // don't search forever; limit to 6 lines above
                    {
                        var s = lines[prev].TrimStart();
                        if (string.IsNullOrEmpty(s))
                        {
                            prev--; continue;
                        }
                        if (s.StartsWith("["))
                        {
                            prev--; continue; // attribute, keep searching above
                        }
                        // First non-attribute, non-empty line
                        if (s.StartsWith("//"))
                        {
                            if (s.IndexOf("Story:", StringComparison.OrdinalIgnoreCase) >= 0)
                                storyLineIndex = prev;
                        }
                        break;
                    }

                    if (storyLineIndex == -1)
                    {
                        // missing story comment
                        // Respect Editor exemptions
                        if (_config.ExemptEditorNullChecks && relPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                            continue;

                        _results.Add($"[Story] {relPath}:{ln}:{col} -> public system/component/authoring type missing leading '// Story: ...' comment.");
                    }
                    else
                    {
                        // Enforce minimal length for the story sentence
                        var txt = lines[storyLineIndex].Trim();
                        var idx = txt.IndexOf("Story:", StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0)
                        {
                            var after = txt.Substring(idx + "Story:".Length).Trim();
                            if (after.Length < 20)
                            {
                                var (sln, scol) = (storyLineIndex + 1, 1);
                                _results.Add($"[Story] {relPath}:{sln}:{scol} -> 'Story' comment too short (min 20 chars).");
                            }
                        }
                    }
                }
            }
        }

        if (_results.Count == 0)
        {
            _results.Add("No rule violations found.");
            Debug.Log("Copilot Rules: No rule violations found.");
        }
        else
        {
            Debug.Log($"Copilot Rules: Found {_results.Count} potential issues. Open 'Tools/Copilot Rules/Run Scan' to view.");
            foreach (var r in _results)
                Debug.Log(r);
        }
    }

    private static (int line, int col) GetLineCol(string[] lines, int index)
    {
        var running = 0;
        for (var i = 0; i < lines.Length; ++i)
        {
            var len = lines[i].Length + 1; // include newline
            if (running + len > index)
            {
                return (i + 1, Math.Max(1, index - running + 1));
            }
            running += len;
        }
        return (lines.Length, 1);
    }

    private static string MakeRelative(string basePath, string fullPath)
    {
        if (fullPath.StartsWith(basePath))
            return fullPath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath;
    }

    [Serializable]
    private sealed class CopilotConfig
    {
        public bool CheckNativeArray = true;
        public bool CheckNulls = true;
    // When true, files under any '/Editor/' path will be exempt from null checks
    public bool ExemptEditorNullChecks = true;
        public bool CheckSealedClasses = true;
        public bool CheckBurstRef = true;
            public bool CheckEntityNull = true;
        public bool CheckStoryComments = true;
        public bool CheckUnusedPrivateSymbols = false;
        public string[] IgnoreFolderTokens = new string[0];

        public static CopilotConfig Default()
        {
            var cfg = new CopilotConfig();
            cfg.IgnoreFolderTokens = DefaultIgnoreFolders;
            return cfg;
        }

        public bool ShouldIgnorePath(string path)
        {
            var p = path.Replace('\\', '/');
            if (IgnoreFolderTokens != null)
            {
                foreach (var token in IgnoreFolderTokens)
                {
                    if (string.IsNullOrEmpty(token))
                        continue;
                    if (p.IndexOf("/" + token + "/", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            return false;
        }
    }
}
