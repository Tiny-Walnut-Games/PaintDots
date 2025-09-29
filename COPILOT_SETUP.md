# Copilot Agent Setup and Project Rules

This project uses an automated editor-time rules checker to enforce agent mandates.

**Non-negotiable rules (enforced/checked by the scanner)**:

- No hard-coded parameters: Move constants to config, injected components, or blob assets.
- No refs in Burst code: Use value types and explicit copies.
- No nulls: Treat nullability as disabled; avoid 'null' literals and checks.
- Seal new symbols: All newly added types must be 'sealed'.
- No NativeArray: Prefer DynamicBuffer for runtime array-like data.
- Use ECBs (EntityCommandBuffer) where possible instead of direct EntityManager mutations.
- Minimize sync points: Favor multithreading and isolate synchronization.

## How to use

1. In the Unity Editor, open Tools -> Copilot Rules -> Run Scan.
2. The scanner will create a default config at Assets/Editor/CopilotRules/copilot_config.json if missing.
3. Review results in the scanner window and the Console. Fix violations and re-run.

## Notes

- The scanner is intentionally conservative and reports potential issues. It is not a replacement for code review.
- The scanner looks for patterns: 'null', 'NativeArray<', classes not marked 'sealed', and '[BurstCompile]' occurrences followed by 'ref' parameters.
- The scanner only reads .cs files under Assets/ and ignores folders listed in the config.

If you want additional checks added (e.g., ECB vs EntityManager usage, DynamicBuffer patterns), open an issue or edit the scanner under Assets/Editor/CopilotRules/CopilotRulesChecker.cs.
