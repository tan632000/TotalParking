<!-- CAFEKIT CORE START -->
# AGENTS.md

## Shared CafeKit instructions

- Deliver exactly what was asked. Do not expand, polish, or add optional work beyond the request. Match existing code style and structure.
- For Specs v2.1, treat `spec.json` as machine authority and Markdown as human projections. Keep task status and plan synchronized with `task_registry`; derive ownership, dependencies, transitions, proof, and parallelism only from typed `coordination.boundaries`. Never invent proof, readiness, approval, or audit state.
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

**Windows-only.** This solution cannot be built on Linux, WSL, or a container: ASP.NET MVC 5 on .NET Framework 4.5 requires MSBuild and IIS Express.

- Restore: `nuget restore TotalParking\TotalParking.sln`
- Build: `msbuild TotalParking\TotalParking.sln /p:Configuration=Debug`
- Run: open `TotalParking\TotalParking.sln` in Visual Studio 2022, press F5. App serves at `https://localhost:44327`.
- Test: **none exist.** There is no test project and no test runner. Never report that tests passed.
- Lint: none configured.

React design source (only when explicitly asked — see "Do not touch"):

- `cd TotalParkingLayout && pnpm install && pnpm dev`

## Do not touch

- `TotalParkingLayout/` — frozen React design source. Never run `pnpm export:mvc` or `pnpm build:mvc`: both regenerate and **overwrite every `Views/Home/*.cshtml`**, destroying hand edits made since the last export. Run only on explicit instruction.
- `TotalParking/SCADALayout/assets/` — stale React bundle from a previous integration, unused.
- `TotalParking/scratch/` — one-off Python geometry scripts, not part of the app.
- `TotalParking/packages/`, `TotalParking/bin/`, `TotalParking/obj/`, `TotalParking/.vs/` — generated or restored.
- `cloudflared-windows-amd64.exe` — 54 MB vendored binary.
- `.claude/`, `.agent/` — CafeKit-managed toolkit. Edit only when the task is explicitly about tooling.

## Slow or expensive

- Do not run builds or publishes. The user verifies with F5 in Visual Studio and owns execution proof.
- Large views — `ZoneDetail.cshtml` (1622 lines), `OperationControl.cshtml` (1578), `Maintenance.cshtml` (1436), `Reports.cshtml` (1402), `Index.cshtml` (1275). Read by line range, never load whole files.
- `TotalParking/Content/scada.css` — 5968 lines of compiled Tailwind. Never read in full; grep for the class you need.

## Language Consistency <!-- cafekit:lang -->

Match the language the user writes in. Technical terms, code identifiers, and file paths may remain in English.
<!-- CAFEKIT CORE END -->

## Project constraints — TotalParking SCADA

Not a CafeKit-managed section. Safe from toolkit upgrades.

### What this codebase actually is

A single ASP.NET MVC 5 project whose UI was exported from React to static Razor views. There is **no database, no authentication, no service layer, and no API**. `HomeController` has 22 actions that all `return View()`. The `Models/` folder is empty. Do not assume any of these exist; do not write code that depends on them without an explicit task to build them.

All runtime behaviour is client-side JavaScript inlined in `.cshtml` files, backed by `localStorage`.

### Hard rules

1. **Adding or removing a `.cshtml` requires updating `TotalParking.csproj`.** Razor compiles views at runtime, so a missing `<Content Include>` still works under F5 but is silently excluded from Publish, producing a 404 in the deployed app. Ten views are already missing: `Assets`, `Backup`, `Cctv`, `Diagnostics`, `Energy`, `Historian`, `OperationControl`, `Queue`, `Safety`, `Tracking`.

2. **These `localStorage` keys are a cross-page contract.** Changing a shape in one view breaks the others, and no test will catch it:

   | Key | Written by | Read by |
   |---|---|---|
   | `activeAlarms` | `Index`, `OperationControl` | `Index`, `OperationControl`, `Diagnostics` |
   | `palletCycleData` | `OperationControl` | `Index` |
   | `occBlockStates`, `occPalletOccupancy` | `OperationControl` | `OperationControl` |
   | `occAdminOverride`, `occActiveRecovery`, `occBypassedSensors` | `OperationControl` | `OperationControl` |

   Any change here must update every reader in the same task, and must say so in the summary.

3. **Views declare their own layout.** `_ViewStart.cshtml` sets `_Layout.cshtml`, but all 21 SCADA views override it with `Layout = "~/Views/Shared/_ScadaLayout.cshtml"`. New SCADA pages must do the same.

4. **`Zones` and `ZoneDetail` routing is deliberately asymmetric.** `Zones(id)` renders the `ZoneDetail` view directly; `ZoneDetail(id)` always redirects to `Zones(id ?? 1)`. Do not "fix" this without a task saying so.

5. **Sidebar active state** is computed in `_ScadaLayout.cshtml` from `ViewContext.RouteData.Values["action"]`. A new page needs its action name added there or the nav highlight breaks.

### Known debt — do not fix opportunistically

Each of these is real, but only address them when a task names them: .NET Framework 4.5 is out of support; `scada.css` is cache-busted per request via `?v=@DateTime.Now.Ticks`; Lucide is loaded from `unpkg.com/lucide@latest` (unpinned CDN, unusable on an isolated OT network); `compilation debug="true"` is committed; Bootstrap and jQuery bundles are registered but unused by SCADA pages; default scaffolding (`About`, `Contact`, `_Layout`) is still present.

### Definition of done

The user runs the app and confirms. Do not claim a change works, is verified, or is done based on reading code alone.
