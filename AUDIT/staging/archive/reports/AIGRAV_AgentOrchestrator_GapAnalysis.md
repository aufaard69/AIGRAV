# AIGRAV Agent Orchestrator Gap Analysis

**Document Version:** 1.0.0  
**Audit Date:** 2026-10-09  
**Target Environment:** Revit 2026 (.NET 10.0-windows) & FastMCP Python 3.14  

---

## 1. Audit Methodology & Fact vs. Assumption Separation

To maintain engineering integrity, every statement in this analysis is classified into one of two tiers:
* **[VERIFIED FACT]**: Verified directly by inspecting source code files, configurations, and project definitions on disk (`C:\AIGRAV`).
* **[ASSUMPTION / HYPOTHESIS]**: Extrapolated behavior that requires active runtime validation in a live Revit 2026 session.

---

## 2. Comprehensive Capability Matrix

| Capability Area | Existing Codebase State | Status | Gap / Missing Implementation | Proposed Reuse & Resolution |
| :--- | :--- | :--- | :--- | :--- |
| **MCP Server & Tool Registration** | • `MCP/TestServer/server.py` defines `mcp = MCPServer("AIGRAV MCP Server")`.<br>• Tools registered via dictionary loop into FastMCP. | **Partially implemented**<br>*(VERIFIED FACT)* | • All tools take a generic `argument: str = ""`. No strongly typed schemas.<br>• Dynamic external C# tools are not auto-discovered by the Python catalog. | • **Reuse**: Keep `server.py` and its FastMCP stdio transport.<br>• **Resolution**: Add an internal query endpoint in C# (`aigrav_list_all_tools`) to auto-populate schemas. |
| **Tool Discovery & Dynamic Loading** | • `AIGRAVSkillLoader.cs` scans `Skills/*.dll`, shadow-copies to `%TEMP%`, and registers `IAIGRAVSkill` instances.<br>• `AIGRAVSkillManagementSkill.cs` exposes `aigrav_skill_status`, `aigrav_discover_skills`, `aigrav_reload_skills`. | **Fully implemented**<br>*(VERIFIED FACT)* | • Dynamic discovery is currently only reachable by manual invocation of `aigrav_discover_skills`. Python layer lacks automatic discovery refresh. | • **Reuse**: Keep collectible `AssemblyLoadContext` shadow-copy architecture unmodified.<br>• **Resolution**: Wire orchestrator discovery to query existing status tool. |
| **Skill Routing & Priority Handling** | • `AIGRAVSkillRegistry.cs` sorts skills by `Info.Priority DESC`.<br>• Priority 2000 (System), 0-1500 (Domain/External), -1000 (Legacy BIM fallback). | **Fully implemented**<br>*(VERIFIED FACT)* | • No multi-skill pipeline or pre/post hook interceptors (not required for current operations). | • **Reuse**: Keep `AIGRAVSkillRegistry` unmodified as the core router inside Revit. |
| **Revit Bridge & ExternalEvent** | • `AIGRAVBridge.cs` enqueues requests into `AIGRAVRequestManager` `ConcurrentQueue`.<br>• `AIGRAVApp.ExternalEvent.Raise()` wakes Revit UI thread.<br>• `RevitExternalEventHandler.cs` dequeues and calls `AIGRAVSkillRegistry.Execute`. | **Fully implemented**<br>*(VERIFIED FACT)* | • None. Core UI thread transition is complete and functional. | • **Reuse**: Strictly preserve this pipeline. **Zero modifications or bypasses permitted.** |
| **Async Execution & Timeout Handling** | • Python HTTP client timeout is 60s.<br>• C# `AIGRAVBridge.ExecuteToolAsync` has `Task.Delay(30000)` timeout.<br>• `AIGRAVHttpServer.cs` processes requests asynchronously via `Task.Run`. | **Partially implemented**<br>*(VERIFIED FACT)* | • 30-second timeout is hardcoded in C#.<br>• Timed-out requests remain queued and execute silently when Revit UI thread eventually unblocks. | • **Reuse**: Async `TaskCompletionSource<string>` pattern.<br>• **Resolution**: Allow caller-specified timeout argument and cooperative cancellation token before dequeued task runs. |
| **Task Understanding & Planning** | • No LLM or DAG orchestration in `c:\AIGRAV`. Current architecture is strictly tool execution. | **Not implemented**<br>*(VERIFIED FACT)* | • No DAG planner, intent parser, dependency resolver, or structured step representation. | • **Resolution**: Implement lightweight Python orchestrator (`task_understanding.py`, `planner.py`) co-located with `MCP/TestServer`. |
| **Execution State Machine** | • `AIGRAVRequest` tracks transient request state in memory during external event wait. | **Partially implemented**<br>*(VERIFIED FACT)* | • No structured task lifecycle states (`PLANNED`, `READY`, `RUNNING`, `WAITING`, `VERIFYING`, `COMPLETED`, `FAILED`, `CANCELLED`). | • **Resolution**: Implement state machine in `orchestrator/state_machine.py`. |
| **Skill Builder (Dynamic Generation)** | • Project template pattern exists in `AIGRAV.Skills.*` targeting `net10.0-windows` and referencing `AIGRAV.Contracts.dll`.<br>• Hot reload via `AIGRAVSkillLoader.Reload()` is operational. | **Partially implemented**<br>*(VERIFIED FACT)* | • No automated synthesis pipeline to write C# source, invoke `dotnet build`, and trigger hot reload from within an autonomous task. | • **Reuse**: Skill contract `IAIGRAVSkill` and `AIGRAVSkillLoader`.<br>• **Resolution**: Create Python skill builder that generates `.cs` + `.csproj`, runs `dotnet build`, and calls `aigrav_reload_skills`. |
| **Result Verification Engine** | • Diagnostic tools exist in `AIGRAVToolExecutor.cs`: `aigrav_validate_element`, `aigrav_check_model_health`, `aigrav_find_warnings`, etc. | **Partially implemented**<br>*(VERIFIED FACT)* | • No automated post-execution verification loop. Modifying tools return status strings without verifying physical database changes. | • **Reuse**: Existing query/validation tools in `AIGRAVToolExecutor`.<br>• **Resolution**: Implement two-phase verification policy (syntactic ID parse + semantic model read). |
| **Recovery, Retry & Cancellation** | • Basic exception handling in `AIGRAVSkillRegistry` and `RevitExternalEventHandler`.<br>• Clean HTTP shutdown via `CancellationTokenSource`. | **Partially implemented**<br>*(VERIFIED FACT)* | • No automated exponential backoff retry for transient Revit busy states.<br>• No in-queue cooperative cancellation. | • **Reuse**: `TaskCompletionSource` exception propagation.<br>• **Resolution**: Add 3-attempt backoff policy for timeouts and cooperative cancellation flag on `AIGRAVRequest`. |
| **Task History & Checkpoints** | • Engineering markdown reports in `KnowledgeBase/`. | **Partially implemented**<br>*(VERIFIED FACT)* | • No structured machine-readable task execution history or resumption checkpoints. | • **Resolution**: Store JSON task execution traces in `KnowledgeBase/TaskHistory/`. |
| **AI Model Routing** | • None in repository. | **Not implemented**<br>*(VERIFIED FACT)* | • No multi-tier model selection (Flash vs. Pro). | • **Resolution**: Add routing logic in Python orchestrator. |
| **Steel Connection Skills** | • Specialized authoring and reflection skills in `AIGRAV.Skills.SteelConnectionAuthoring` and `ConnectionNativeTestSkill.cs`. | **Unknown until tested**<br>*(ASSUMPTION)* | • Behavior and limitations of native Advance Steel API inside headless Revit execution require live verification. | • **Reuse**: Preserve existing code. **Strict Rule: Do not modify steel connection components during this phase.** |

---

## 3. Detailed Architectural Gaps

### Gap 1: Disconnected Python MCP Catalog and External C# Skills
* **Verified Fact**: In [server.py](file:///C:/AIGRAV/MCP/TestServer/server.py#L133-L635), all tools are hardcoded in a static dictionary `TOOLS`. When a new C# skill (e.g. `AIGRAV.Skills.QTO`) is loaded into Revit, it responds to `aigrav_qto`, but `aigrav_qto` is not automatically registered in `server.py` unless manually added.
* **Impact**: External skills loaded at runtime cannot be called by standard MCP clients without a catalog refresh.

### Gap 2: Lack of Post-Execution Semantic Verification
* **Verified Fact**: Tools in [AIGRAVToolExecutor.cs](file:///C:/AIGRAV/Revit/AIGRAV.RevitBridge/AIGRAV.RevitBridge/AIGRAVToolExecutor.cs) return strings such as `"Wall created successfully. Id: 12345"`. However, the bridge does not verify if the created element satisfies geometry constraints, intersects invalid hosts, or generates Revit warnings.
* **Impact**: Upstream AI agents frequently register a "hallucinated success" when a tool completes its transaction even if Revit generates severe join warnings or places the element incorrectly.

### Gap 3: Missing Orchestration State Layer
* **Verified Fact**: Each request sent to `AIGRAVHttpServer` is completely independent and ephemeral. No state is tracked between step 1 (get level) and step 2 (create column).
* **Impact**: Multi-step workflows cannot safely recover, retry, or roll back if step 3 of a 5-step workflow fails.
