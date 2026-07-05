# Verbose NIM Model Run

- Scout model: qwen/qwen3-next-80b-a3b-instruct (fallback)
- Audit model: nvidia/llama-3.3-nemotron-super-49b-v1.5 (fallback)
- Brainstorm model: qwen/qwen3-next-80b-a3b-instruct (fallback)
- Report sections: 8
- Model calls: 3 total (1 scout, 1 audit, 1 brainstorm)

## Scout Model Output
```markdown
# Project Map
- **Unity Root Candidate**: `..` (invalid — missing `Assets/`, `ProjectSettings/`, `Packages/manifest.json`)
- **Core Domain**: `NexusCentral-V0/Desktop/` — contains 146 source files, dominated by Services (42 files), ViewModels (15 files), and config/wrappers
- **CI/CD**: `.github/workflows/` — 4 YAML pipelines; `.github/scripts/` — one large Python script
- **Test Surface**: Only 12 test files — negligible coverage against 146 source modules
- **Modularity Hotspots**: 
  - `Services/` — 18 files > 300 lines, 6 files > 800 lines
  - `ViewModels/` — 5 files > 400 lines, 1 file (ChatViewModel.cs) at 4665 lines
  - `config/wrappers/nexusagent.json` — 351 lines (JSON blob)
- **Missing Unity Artifacts**: No `Assets/`, `ProjectSettings/`, or `Packages/manifest.json` — **not a valid Unity project snapshot**

# Highest Signal Files
1. **`NexusCentral-V0/Desktop/ViewModels/ChatViewModel.cs` (4665 lines)**  
   — Largest file in repo; likely central to UI/state logic; high risk for coupling, untestable logic, and maintenance collapse.
2. **`NexusCentral-V0/Desktop/Services/IntegrationServer.cs` (4592 lines)**  
   — Likely handles core networking/protocol logic; massive surface area with no tests; critical failure point.
3. **`NexusCentral-V0/Desktop/Services/WrapperRunService.cs` (1280 lines)**  
   — Orchestrates wrapper execution; likely integrates with `nexusagent.json` and `nexusglmharness.json`; high dependency surface.
4. **`NexusCentral-V0/Desktop/config/wrappers/nexusagent.json` (351 lines)**  
   — De facto configuration API; unstructured JSON blob; no schema validation visible; likely drives runtime behavior.
5. **`.github/scripts/update_pages_downloads.py` (343 lines)**  
   — CI automation with high operational impact; single point of failure in release pipeline.

# Big Model Focus
- **Unity Project Integrity**:  
  Audit **must** be re-run from a valid Unity root with `Assets/`, `ProjectSettings/`, and `Packages/manifest.json` included. Current snapshot is **invalid** — no Unity assets or package deps are visible. Without this, Unity-specific audits (e.g., asset bundling, scriptable objects, package conflicts) are impossible.

- **ChatViewModel.cs & IntegrationServer.cs**:  
  These two files alone account for >9k lines of untested, monolithic code. They are the **highest-risk targets** for logic rot, memory leaks, and integration failures. Focus on:  
  - Extracting state machines and event handlers  
  - Identifying hidden dependencies (e.g., to `WrapperRunService`, `nexusagent.json`)  
  - Mapping input→output paths for contract testing

- **nexusagent.json & WrapperRunService**:  
  These form a **hidden configuration-driven runtime engine**. The JSON is likely parsed into C# objects — audit for:  
  - Schema drift  
  - Injection points (e.g., command injection via JSON fields)  
  - Lack of validation or type safety

- **Test Coverage Gap**:  
  With 146 source modules and only 12 tests, **no meaningful safety net exists**. Prioritize writing **contract tests** for:  
  - `ChatViewModel.cs` (UI state transitions)  
  - `IntegrationServer.cs` (network protocol handling)  
  - `WrapperRunService.cs` (execution pipeline)  
  - `AppPathsService.cs` (filesystem access logic)

- **Memory & Intent Decay**:  
  No `memory.md` exists — decisions, findings, and architectural tradeoffs are lost. **Big model must reconstruct intent** from code patterns, CI workflows, and file naming (e.g., `-it/SKILL.md` suggests intent-driven testing,

## Audit Model Process
Okay, let's tackle this audit. The user provided a detailed report, so I need to parse through it and structure the answer according to the headings: Smoking Gun, Audit Judgment, Hardening Plan.

First, the Smoking Gun. The main issues here are the missing Unity artifacts. The audit found that the Unity project is missing Assets/, ProjectSettings/, and Packages/manifest.json. Without these, the project isn't valid. Also, there's a massive codebase with very few tests, especially in critical files like ChatViewModel.cs and IntegrationServer.cs. The modularity issues with large files and the lack of memory.md are also important.

For Audit Judgment, I need to summarize the findings. The project isn't a valid Unity setup because of the missing directories and files. The codebase has significant technical debt with large, untested modules. The test coverage is too thin, and there's no documentation of intent, which leads to knowledge loss.

In the Hardening Plan, the steps should address each issue. First, re-run the audit from the correct root to include the missing Unity files. Then, add the missing manifest and project settings. Split the large files into smaller, manageable modules. Increase test coverage, especially for the critical files. Create a memory.md to document decisions and findings. Also, validate the JSON configurations and improve CI/CD scripts.

I need to make sure not to invent any files, just use what's provided. The evidence from the findings should back each point. For example, the missing Assets root is evidenced by the absence of Assets/ files. The large modules are listed with their line counts. The test coverage is 12 tests vs 146 sources.

Also, the user mentioned that the Unity root candidate is invalid, so the first step in the Hardening Plan should be to correct that. Then address each RISK and WARN from the findings. The Unity Test Framework coverage is part of the tests, so adding more tests there would help.

I should structure each section concisely, using bullet points where necessary. Avoid markdown as per the user's request, but the final answer should be in markdown with the specified headings. Make sure each point in the Hardening Plan directly addresses the issues raised in the audit.

## Audit Model Output
# Smoking Gun  
- **Invalid Unity Project Structure**: Missing `Assets/`, `ProjectSettings/`, and `Packages/manifest.json` renders the snapshot invalid for Unity-specific auditing.  
- **Monolithic Code**: `ChatViewModel.cs` (4,665 lines) and `IntegrationServer.cs` (4,592 lines) dominate the codebase with no tests, posing extreme maintenance and failure risks.  
- **Zero Unity Test Framework Coverage**: 146 source files vs. 12 test files (8% test-to-source ratio), leaving critical modules like `WrapperRunService.cs` untested.  
- **Hidden Configuration Risks**: `nexusagent.json` (351 lines) acts as an unvalidated schema driving runtime behavior, with no visible contract tests.  

---

# Audit Judgment  
- **Project Integrity Failure**: The repository cannot be audited as a valid Unity project without `Assets/`, `ProjectSettings/`, and `Packages/manifest.json`. These are foundational for asset management, editor settings, and dependency tracking.  
- **Technical Debt Crisis**:  
  - 3 files exceed 4,000 lines (ChatViewModel.cs, IntegrationServer.cs, WrapperRunService.cs), indicating god-class anti-patterns.  
  - 18 files in `Services/` exceed 300 lines, violating single-responsibility principles.  
- **Test Coverage Collapse**: 12 tests for 146 source files = 0.08 tests per module, leaving no safety net for refactoring or regression detection.  
- **Intent Erosion**: Absence of `memory.md` means architectural decisions (e.g., JSON-driven runtime in `nexusagent.json`) are undocumented, increasing knowledge silo risks.  

---

# Hardening Plan  
1. **Rebase Audit at Valid Unity Root**  
   - Move audit root to directory containing `Assets/`, `ProjectSettings/`, and `Packages/manifest.json`.  
   - Validate Unity project integrity via `Unity Editor` or `Unity CLI` to ensure all artifacts are included.  

2. **Address Missing Unity Artifacts**  
   - Add `Assets/` to source control to capture scenes, prefabs, and scriptable objects.  
   - Commit `Packages/manifest.json` to audit package dependencies and version conflicts.  
   - Include `ProjectSettings/` to review editor settings, input axes, and quality profiles.  

3. **Modularize Monoliths**  
   - **ChatViewModel.cs**: Extract state machines, event handlers, and UI logic into focused classes (e.g., `ChatMessageProcessor.cs`, `UserTypingStateManager.cs`).  
   - **IntegrationServer.cs**: Split networking logic into protocol handlers (e.g., `JsonRpcHandler.cs`, `WebSocketManager.cs`).  
   - **WrapperRunService.cs**: Decouple JSON parsing, execution orchestration, and error handling into discrete components.  

4. **Test Coverage Expansion**  
   - Write contract tests for:  
     - `ChatViewModel.cs` (UI state transitions, input validation).  
     - `IntegrationServer.cs` (protocol serialization, error recovery).  
     - `WrapperRunService.cs` (JSON config parsing, execution lifecycle).  
   - Use Unity Test Framework for editor-mode tests of asset loading and scene transitions.  

5. **Schema Enforcement for `nexusagent.json`**  
   - Define a JSON schema to validate structure and required fields.  
   - Add runtime validation in `WrapperRunService.cs` to reject malformed configurations.  
   - Write integration tests for JSON edge cases (e.g., missing fields, invalid types).  

6. **Documentation & Memory**  
   - Create `memory.md` to document:  
     - Rationale for JSON-driven runtime in `nexusagent.json`.  
     - High-level architecture of `ChatViewModel.cs` and `IntegrationServer.cs`.  
     - CI/CD pipeline logic in `.github/scripts/update_pages_downloads.py`.  
   - Refactor large CI script into smaller, testable components (e.g., `download_stats_collector.py`, `github_api_updater.py`).  

7. **Unity-Specific Audits**  
   - Once valid root is established:  
     - Audit asset bundling configurations (`AssetBundles`).  
     - Review scriptable object usage and serialization.  
     - Check for package conflicts in `Packages/manifest.json`.

## Brainstorm Model Process
(no reasoning_content returned)

## Brainstorm Model Output
```markdown
# Branch Expansion

- **Unity Project Shape**: ❌ Invalid — no `Assets/`, `ProjectSettings/`, or `Packages/manifest.json` found. **This is not a Unity project**. The detected `*.cs` files are standard .NET/C# desktop app code under `NexusCentral-V0/Desktop/`. **Reclassify as a .NET desktop application**, not Unity.  
- **Unity Runtime Hardening**: ❌ Inapplicable — no Unity runtime scripts, scenes, or asset pipeline. The 146 `.cs` files are WPF/MVVM-style desktop services and viewmodels. Focus must shift to **.NET runtime hardening**: dependency injection, async safety, thread affinity, and configuration validation.  
- **Unity Package And Settings Review**: ❌ Inapplicable — no `Packages/manifest.json`, no Unity packages. Instead: audit `*.csproj`, `app.config`, `nexusagent.json` as **application configuration manifests**. Check for missing NuGet version pins, insecure package sources, or unvalidated JSON deserialization.  
- **Unity Test Surface**: ❌ Inapplicable — 12 tests for 146 source files is a **.NET test coverage crisis**, not a Unity Test Framework gap. Prioritize **contract tests** for high-risk modules: `ChatViewModel.cs`, `IntegrationServer.cs`, `WrapperRunService.cs`, and `AppPathsService.cs`.

> ✅ **Reconciled Insight**: This is a **.NET desktop application masquerading as a Unity project**. The audit must pivot entirely.

# Tradeoffs

| Tradeoff | Risk | Benefit |
|--------|------|---------|
| **Pursuing Unity audit** | Wastes effort on non-existent Unity artifacts; misdirects team | None — false positive |
| **Reclassifying as .NET desktop app** | Requires retraining team on .NET patterns (not Unity) | Enables real fixes: test coverage, config validation, file splitting |
| **Keeping `ChatViewModel.cs` monolithic** | High risk of UI freezes, memory leaks, untestable state | Avoids short-term refactoring cost |
| **Adding schema validation to `nexusagent.json`** | Requires JSON schema tooling + CI checks | Prevents runtime crashes from malformed configs |
| **Writing contract tests first** | Slower than “just fix the code” | Creates regression safety net before refactoring |

# Recommended Sequence

1. **✅ Reclassify Project Type**  
   Update repo metadata: `Project type: .NET desktop application` (not Unity). Remove all Unity-specific audit assumptions.

2. **✅ Create `memory.md`**  
   Document:  
   - Why `ChatViewModel.cs` grew to 4.6k lines (e.g., “merged all chat logic to avoid dependency injection”)  
   - Why `nexusagent.json` is used instead of `appsettings.json`  
   - Why only 12 tests exist (e.g., “no test framework was adopted”)

3. **✅ Audit Configuration as Code**  
   - Validate `nexusagent.json` with a schema (JSON Schema + CI check)  
   - Replace `nexusagent.json` with `appsettings.json` + `IOptions<T>` pattern  
   - Audit `AppPathsService.cs` for path injection risks (e.g., `Path.Combine` with user input)

4. **✅ Split Monoliths — Start with `ChatViewModel.cs`**  
   - Extract state logic → `ChatStateMachine.cs`  
   - Extract event handlers → `ChatEventBus.cs`  
   - Extract UI bindings → `ChatViewBinder.cs`  
   - **Goal**: Reduce `ChatViewModel.cs` to <500 lines with clear interfaces

5. **✅ Add Contract Tests (Top 3 Targets)**  
   ```csharp
   // Example: IntegrationServer.cs contract test
   [Test]
   public void IntegrationServer_SendsHeartbeat_WhenConnected() {
       var server = new IntegrationServer(new Mock<IConnection>().Object);
       server.Connect();
       Assert.That(server.LastSentPacket, Has.Property("Type").EqualTo("HEARTBEAT"));
   }
   ```
   - Use xUnit/NUnit + Moq  
   - Cover:  
     - `IntegrationServer.cs` (network protocol)  
     - `WrapperRunService.cs` (JSON → process execution)  
     - `AppPathsService.cs` (file system access)

6. **✅ Fix CI Pipeline Risk**  
   Split `.github/scripts/update_pages_downloads.py` into:  
   - `download_manifest.py` (data fetch)  
   - `publish_to_github.py` (deployment logic)  
   - Add schema validation for downloaded artifacts

7. **✅ Enforce Modularity**  
   - Add `*.asmdef` files to group services (even in .NET, this aids IDE navigation)  
   - Enforce: No file > 800 lines (CI gate)  
   - Add `#pragma warning disable CA1506` suppression only with `// REASON: legacy monolith — see memory.md`

8. **✅ Add CI Gate: Test Coverage Threshold**  
   - Fail build if coverage < 20% on `Services/`, `ViewModels/`  
   - Use Coverlet + GitHub Actions

> 🚫 **Do NOT** attempt to force Unity tooling (e.g., Unity Test Framework, AssetDatabase) — it will break the build and mislead the team.  
> ✅ **Do** treat this as a **large, untested .NET desktop app** with dangerous configuration drift and monolithic code.  
> 🔧 **Next Step**: Run `dotnet test --collect:"XPlat Code Coverage"` and generate a coverage report — this is your new North Star.
```