AIGRAV HOT-RELOAD BATCH 2

Purpose:
Connect the existing AIGRAVToolExecutor to the new Skill Registry without changing the existing BIM tool implementation.

Changes:
1. IAIGRAVSkill now has CanHandle() and Execute().
2. AIGRAVLegacyBimSkill adapts the existing AIGRAVToolExecutor.
3. AIGRAVSkillRegistry is now the execution router.
4. RevitExternalEventHandler routes requests through AIGRAVSkillRegistry.
5. AIGRAVSkillLoader still only discovers DLLs. It does NOT load/unload them yet.

Safety:
AIGRAVToolExecutor.cs is intentionally unchanged.
No external DLL hot loading is enabled in this batch.

Expected execution path:
MCP/HTTP -> AIGRAVBridge -> ExternalEvent -> SkillRegistry -> LegacyBimSkill -> AIGRAVToolExecutor

Next batch:
Introduce the first isolated external skill assembly and controlled reload mechanism.
Do not proceed until this batch builds and runtime tests pass.
