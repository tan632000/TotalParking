# License Activation (web gate)
Specs-Contract: process-first-ready-v1

## Scope decision (C1 — 2026-10-17)
- Existing: warning banner already shipped in `TotalParking/Views/Shared/_ScadaLayout.cshtml` (Part 1); global filter hook `TotalParking/App_Start/FilterConfig.cs:8-11`; ingest routes `TotalParking/App_Start/RouteConfig.cs:18-28` (`Ingest` controller, each action self-guards `IsLoopbackClient` `TotalParking/Controllers/IngestController.cs:58,67,299-311`); background hosts start HTTP-independently `TotalParking/Global.asax.cs:26-60` (six: PlcHost, LedHost, PgsHost, PgsSensorStateHost, CanhBaoCamBienService, SlotScanHost); no existing license code (grep of `*.cs` empty).
- Environment (C2-10/C2-08): target is **.NET Framework 4.7.2** (`TotalParking/TotalParking.csproj:17`), classic non-SDK web project → every new `.cs` needs an explicit `<Compile Include>` and every runtime data file needs `<Content Include>` or Publish drops it (Hard rule 1). `msbuild` is NOT on PATH; build with the full VS2019 MSBuild path, never `dotnet build` (empty `$(VSToolsPath)` skips `WebApplication.targets`). Use the 4.7.2 RSA API `RSA.VerifyData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)` — not the legacy CSP workaround.
- Minimum change: a public, contract-based license gate on the operator web app — normal before the embedded expiry, fully locked (redirect to an Activation screen) after it, re-opened by pasting a valid RSA-signed license with a later expiry. Physical/automation layer untouched.
- Expansion signals: ~11 touched files (over 8); one cohesive subsystem, not 3 independent ones → one Specs packet.
- User decision: KEEP — minimum set, license stored in `App_Data` (no DB migration), RSA offline signing, bounded clock-rollback guard.

## Out of scope
- Any change to PLC/LED/PGS automation or the physical safety layer (E-Stop, PLC interlocks). The gate only affects the ASP.NET MVC request pipeline.
- Covert kill switch, remote backdoor, or persistence designed to defeat the machine owner. Explicitly excluded.
- Online/phone-home activation (OT network is isolated). Offline signed license only.
- Full automated test harness / xUnit project (none exists). task-02 ships a focused PowerShell probe for the crypto/expiry/clock logic; broader test infra is a separate EXPAND.
- Locking loopback machine-to-machine ingest (`/health`, `/vehicle`) — must stay open so the camera keeps feeding automation.
- A Debug-gated unlock flag — REJECTED: the app ships `compilation debug="true"` (CLAUDE.md known-debt), so a Debug flag would disable the gate in production. Dev rebuilds after the trial date use a locally generated, gitignored long-expiry license instead.

## Signing contract (C2-05, binds task-01 issuer ↔ task-02 verifier)
- Algorithm: RSA 2048, signature `RSA.VerifyData(..., HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)`.
- Canonical payload: UTF-8 JSON, keys in fixed order `customerId, issuedUtc, expiryUtc`, dates ISO-8601 `yyyy-MM-ddTHH:mm:ssZ` InvariantCulture, no trailing newline. Signature over exactly those bytes.
- License file: the canonical payload + Base64 signature; public key in `Web.config` `license:publicKey`; private key never committed.

## Coverage profile
| ID | Outcome | Change kinds | Material surfaces | Ambiguity/action | Risk/evidence | Required proof |
|---|---|---|---|---|---|---|
| CP-01 | Before expiry, every web page serves normally (no redirect) | add | Runtime/deploy; Time/retention | none | elevated — `Global.asax.cs:19` filter pipeline | build+probe; live@C3 |
| CP-02 | After expiry/no license, non-whitelisted request is locked (navigation→redirect Activation, JSON→403 by `Accept`/`JsonResult`, not `IsAjaxRequest`) | add | Async/state; API | none | critical — gate on money/access | build; live@C3 |
| CP-03 | While locked, Activation + loopback `/health` `/vehicle` still served (whitelist by controller name; static files are IIS-served, outside the filter) | add | Filesystem/security; Integration | none | critical — automation must keep ingesting | build; live@C3 |
| CP-04 | Valid future-dated signed license activates & restores access; invalid/expired rejected; committed trial verifies against committed public key | add | API; Data/schema | none | critical — RSA verify + Publish inclusion | build+probe+cross-verify; live@C3 |
| CP-05 | Automation hosts + physical layer run regardless of license state | add | Runtime/deploy | none | critical — safety | source (`Global.asax.cs:26-60`, no gate in host path); live@C3 |
| CP-06 | Bounded clock-rollback guard: tamper does not grant validity AND a genuine clock error cannot permanently brick (bounded advance + offline recovery) | add | Time/retention; Async/state | none | critical — false-positive brick | build+probe; live@C3 |

## Acceptance criteria
| ID | EARS criterion | Proof |
|---|---|---|
| AC-01 | While the installed license expiry is in the future, the system shall serve every requested web page without redirecting to Activation. | build + live@C3 |
| AC-02 | While no valid unexpired license is installed, if a non-whitelisted request arrives, the system shall redirect navigations to Activation and return HTTP 403 for requests whose `Accept` is JSON (or whose action returns `JsonResult`). | build + live@C3 |
| AC-03 | While locked, the system shall still serve the Activation controller and the loopback `/health` and `/vehicle` endpoints (whitelist matched by controller name). | build + live@C3 |
| AC-04 | When a license with a valid RSA signature (per the signing contract) and a future expiry is submitted, the system shall persist it to `App_Data` and restore normal access; the committed trial license shall verify against the committed public key. | build + probe + live@C3 |
| AC-05 | If a submitted license has an invalid signature, a past expiry, or is corrupt/missing, the system shall reject it (or evaluate LOCKED), keep the system locked, show an error, and still render Activation. | build + probe + live@C3 |
| AC-06 | The six background hosts (`Global.asax.cs:26-60`) shall start and run independently of license state. | source + live@C3 |
| AC-07 | If the system clock is earlier than the recorded high-water mark, the system shall evaluate validity against the high-water mark; the high-water mark shall not advance beyond a bounded window, and an offline reset path shall exist so a genuine clock error cannot permanently lock a valid license. | build + probe + live@C3 |

## Tasks
| # | Task | Criteria | Primary ownership | Dependencies | Status |
|---|---|---|---|---|---|
| 01 | RSA keypair + offline issuer (signing contract) + pre-installed trial license (expiry 2026-11-07, `<Content Include>`) + embedded public key | AC-04 | `tools/license/`, `TotalParking/App_Data/license/license.lic`, `TotalParking/Web.config`, `TotalParking/TotalParking.csproj`, `.gitignore` | - | done |
| 02 | LicenseService: verify (contract), compute state, store/load, bounded clock guard + sidecar; PowerShell logic probe | AC-01, AC-04, AC-05, AC-07 | `TotalParking/Services/License/`, `TotalParking/App_Data/license/clock-highwater.dat`, `tools/license/verify-license-logic.ps1`, `TotalParking/TotalParking.csproj` | task-01-keys-and-issuer.md | done |
| 03 | Activation screen (controller + view) to paste & apply a license | AC-04, AC-05 | `TotalParking/Controllers/ActivationController.cs`, `TotalParking/Views/Activation/Index.cshtml`, `TotalParking/TotalParking.csproj` | task-02-license-service.md | done |
| 04 | LicenseGateFilter + register + whitelist (controller-name match; fail-closed; always allow Activation) | AC-01, AC-02, AC-03, AC-06 | `TotalParking/Filters/LicenseGateFilter.cs`, `TotalParking/App_Start/FilterConfig.cs`, `TotalParking/TotalParking.csproj` | task-03-activation-screen.md | done |

## Review log
- Round 1 (C2 — 2026-10-17): 12 findings presented, user decision ACCEPT ALL (C2-01…C2-12). C2-12 repair upgraded from "Debug-only flag" to "dev-local gitignored long-expiry license" because the app ships `compilation debug="true"` (would disable the gate in production). Applied across plan + task-01..04.
- Closure (C2 repairs applied):

| ID | Decision | Original counterexample | Repaired at | Proved at | Replay | Closure |
|---|---|---|---|---|---|---|
| C2-01 | accept | CMOS error→clock 2030 recorded→fixed to 2026 stays LOCKED forever | AC-07; task-02 Scope (bounded advance + reset) | pending (probe @ develop) | pending | UNKNOWN |
| C2-02 | accept | Publish drops `license.lic`→deployed app LOCKED day one | task-01 Ownership (`<Content Include>`) | pending (build/publish @ develop) | pending | UNKNOWN |
| C2-03 | accept | New `.cs` not in csproj→not compiled | task-02 & task-04 Ownership (`<Compile Include>`) | pending (build @ develop) | pending | UNKNOWN |
| C2-04 | accept | `IsAjaxRequest()` false→JSON poll gets 302 HTML→`response.json()` throws | AC-02; task-04 Scope (`Accept`/`JsonResult`) | pending (live @ C3) | pending | UNKNOWN |
| C2-05 | accept | Issuer/verifier padding-hash-bytes mismatch→trial fails at runtime | Signing contract §; task-01/02 | pending (cross-verify probe @ develop) | pending | UNKNOWN |
| C2-06 | accept | Build Exit 0 certifies crypto with zero behavior exercised | task-02 probe `verify-license-logic.ps1` | pending (probe @ develop) | pending | UNKNOWN |
| C2-07 | accept | High-water file undefined + cross-process torn write | task-02 Ownership (sidecar) + atomic write | pending (probe @ develop) | pending | UNKNOWN |
| C2-08 | accept | `msbuild` not on PATH→command not Exit 0 | all task Verification (full MSBuild path) | pending (build @ develop) | pending | UNKNOWN |
| C2-09 | accept | Corrupt license→exception→500 everywhere→cannot reach Activation | AC-05; task-02 (exception→LOCKED) + task-04 (always allow Activation) | pending (live @ C3) | pending | UNKNOWN |
| C2-10 | accept | Plan says 4.5; real 4.7.2→wrong/fragile CSP workaround | C1 Environment; Signing contract | fixed (plan edit) | this plan | PASS |
| C2-11 | accept | "static assets" whitelist is dead code in MVC filter | AC-03; task-04 Scope (controller-name predicate) | fixed (plan/task edit) | this plan | PASS |
| C2-12 | accept | Trial 2026-11-07→rebuild after date bricks dev/CI | Out-of-scope note + task-01 (dev-local license) | fixed (plan edit) | this plan | PASS |

- B3 sweep: 5 files reread / 12 deltas / stale references fixed (framework 4.5→4.7.2, msbuild→full path, 4 hosts→6, csproj edits added to task-01/02/04) / 0 conflicts left. Behavioral findings (C2-01..09) remain UNKNOWN until replayed with runtime evidence in `/cf:develop` + C3.
- C3 (2026-10-17): all 4 tasks done with inline receipts. User ran the app and confirmed the live gate: with an expired `license.lic` the web locked and navigation redirected to `/Activation`, and pasting a valid issued license unlocked back to Home (CP-02, CP-04 observed). Finer behaviors not individually observed by the user: JSON-controller 403 vs redirect, `/health` `/vehicle` staying 200 while locked, and hosts continuing to run (CP-03, CP-05) — recommended spot-checks, not blockers. User accepted completion. Pre-handover: swap the DEV signing key for a production key and re-issue `license.lic` at the agreed expiry.
