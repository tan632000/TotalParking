<!-- CAFEKIT CORE START -->
# AGENTS.md

## Shared CafeKit instructions

- Deliver exactly what was asked. Do not expand, polish, or add optional work beyond the request. Match existing code style and structure.
- For process-first Specs, `plan.md` and flat `task-NN-*.md` files are
  canonical, hand-editable state. Each task has exactly one `Status:` field and
  keeps canonical execution proof in its final inline `## Receipt`.
- Specs uses three user decisions: C1 for scope, C2 for adversarial findings,
  and C3 for completion. Planning never starts implementation; implementation
  requires a new explicit user invocation.
- Synchronize only observed task state with surgical edits. Never invent proof,
  readiness, approval, review independence, or completed work.
- `NO_TESTS` and `0 tests + exit 0` do not pass when the task requires automated tests.
- When a hook blocks an action, that is an instruction boundary — do not work around it.
- Use conventional commits. Do not add AI attribution unless requested.

## Response style

Keep replies focused and brief. Match written-file length to task needs. Do not add filler sections or redundant summaries. Lead with the outcome.

## Uncertainty

Say when you are unsure and what would settle it. Label inferences instead of presenting them as verified facts.

## Delegation

Do the work yourself when it takes a handful of tool calls. Delegate genuinely independent parallel tracks. Verification comes from the project's hooks and validators, not from spawning more agents.

## Runtime ownership

- This `CORE` block is runtime-neutral and safe for every runtime.
- Runtime-specific instructions live in that runtime's own managed block, not in `CORE`.
- In a combined install, consume `CORE` plus your native block only. Ignore managed blocks not owned by your runtime. If ownership is unclear, treat the file as `CORE`-only (fail-safe).

## Commands

<!-- Add project-specific install, test, lint, and build commands here. Keep commands executable. -->

## Do not touch

<!-- List files, directories, generated artifacts, or secrets that tasks must leave unchanged. -->

## Slow or expensive

<!-- Note commands, environments, or operations that need explicit planning before running. -->

## Language Consistency <!-- cafekit:lang -->

Always respond in **Tiếng Việt**. Technical terms, code identifiers, and file paths may remain in English, but explanations, comments directed at the user, and structured output must be in Tiếng Việt.


<!-- CAFEKIT CORE END -->

## Project constraints — TotalParking SCADA

Not a CafeKit-managed section. Safe from toolkit upgrades.

### What this codebase actually is

A single ASP.NET MVC 5 project whose UI was exported from React to static Razor views. There is **no database, no authentication, no service layer, and no API**. `HomeController` has 22 actions that all `return View()`. The `Models/` folder is empty. Do not assume any of these exist; do not write code that depends on them without an explicit task to build them.

All runtime behaviour is client-side JavaScript inlined in `.cshtml` files, backed by `localStorage`.

### Hard rules

1. **Adding or removing a `.cshtml` requires updating `TotalParking.csproj`.** Razor compiles views at runtime, so a missing `<Content Include>` still works under F5 but is silently excluded from Publish, producing a 404 in the deployed app.

   All 24 `Views/Home/*.cshtml` and `Views/Shared/_ScadaLayout.cshtml` are currently listed. An earlier version of this rule named ten views as missing (`Assets`, `Backup`, `Cctv`, `Diagnostics`, `Energy`, `Historian`, `OperationControl`, `Queue`, `Safety`, `Tracking`); they have since been added, so do not treat that list as a live gap. Verified 25/09/2026 with `grep -F 'Include="Views\Home\' TotalParking/TotalParking.csproj` — re-run that rather than trusting this sentence.

2. **These `localStorage` keys are a cross-page contract.** Changing a shape in one view breaks the others, and no test will catch it:

   | Key | Written by | Read by |
   |---|---|---|
   | `activeAlarms` | `Index`, `OperationControl` | `Index`, `OperationControl`, `Diagnostics`, `Safety` |
   | `palletCycleData` | `OperationControl`, `Reports` | `Index`, `OperationControl`, `Reports` |
   | `occBlockStates` | `OperationControl` | `OperationControl`, `Safety` |
   | `occPalletOccupancy` | `OperationControl` | `OperationControl` |
   | `occAdminOverride`, `occActiveRecovery` | `OperationControl` | `OperationControl` |
   | `occBypassedSensors` | `OperationControl`, **`Safety`** | `OperationControl`, `Safety` |

   Any change here must update every reader in the same task, and must say so in the summary.

   Two keys have **two writers**: `palletCycleData` (`OperationControl` and `Reports`) and `occBypassedSensors` (`OperationControl` and `Safety`). Last write wins and neither side merges, so a shape change must land in both writers at once.

   `Safety` is easy to miss because it reads and writes without being an "OCC" page. Verified 25/09/2026 by grepping every `localStorage.(get|set|remove)Item` call in `Views/**/*.cshtml`; re-run that grep rather than trusting this table after a view is added.

   Four further keys are single-page state, not a contract: `dispatchEntryQueue`, `dispatchExitQueue`, `dispatchRejectedList` (`Queue` only) and `occBypassLogs` (`Safety` only).

3. **Views declare their own layout.** `_ViewStart.cshtml` sets `_Layout.cshtml`, but all 21 SCADA views override it with `Layout = "~/Views/Shared/_ScadaLayout.cshtml"`. New SCADA pages must do the same.

4. **`Zones` and `ZoneDetail` routing is deliberately asymmetric.** `Zones(id)` renders the `ZoneDetail` view directly; `ZoneDetail(id)` always redirects to `Zones(id ?? 1)`. Do not "fix" this without a task saying so.

5. **Sidebar active state** is computed in `_ScadaLayout.cshtml` from `ViewContext.RouteData.Values["action"]`. A new page needs its action name added there or the nav highlight breaks.

### Known debt — do not fix opportunistically

Each of these is real, but only address them when a task names them: .NET Framework 4.5 is out of support; `scada.css` is cache-busted per request via `?v=@DateTime.Now.Ticks`; Lucide is loaded from `unpkg.com/lucide@latest` (unpinned CDN, unusable on an isolated OT network); `compilation debug="true"` is committed; Bootstrap and jQuery bundles are registered but unused by SCADA pages; default scaffolding (`About`, `Contact`, `_Layout`) is still present.

### Definition of done

The user runs the app and confirms. Do not claim a change works, is verified, or is done based on reading code alone.
