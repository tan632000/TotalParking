# Task 02 — LicenseService (verify, state, storage, bounded clock guard)

Status: done

## Outcome
A `LicenseService` the app can call to: verify an RSA-signed license per the
plan's Signing contract, read the active license from `App_Data`, compute state
(VALID vs LOCKED) from its expiry against a bounded monotonic clock, persist the
active license on activation, and fail closed (LOCKED, never crash) on any
corrupt/missing input. A runnable PowerShell probe exercises this logic.

## Scope
- In: signature verification (`RSA.VerifyData(..., SHA256, Pkcs1)`, .NET 4.7.2);
  expiry comparison; active-license atomic load/save (temp+rename) in `App_Data`;
  bounded clock-rollback guard with a sidecar high-water file.
- Clock guard (C2-01 + C2-07): store highest observed UTC in a dedicated sidecar;
  evaluate validity against `max(now, highWater)`; the high-water mark advances
  only within a bounded window (e.g. not beyond active-license `expiryUtc` + a
  fixed drift margin) so a spurious far-future clock cannot be recorded; a valid
  new license supersedes the baseline and deleting the sidecar is the documented
  offline reset. Writes are atomic; a torn/corrupt sidecar → treat as LOCKED, not crash.
- Fail-closed (C2-09): any parse/IO/crypto exception → return LOCKED (never throw to caller).
- Out: the HTTP gate (task 04); the Activation UI (task 03); license issuing (task 01).

## Coverage
- CP-04 (verify half), CP-06 (bounded clock guard)

## Ownership
- Create: `TotalParking/Services/License/LicenseService.cs`
- Create: `TotalParking/Services/License/License.cs` (parsed model + state enum)
- Create: `TotalParking/App_Data/license/clock-highwater.dat` (sidecar; runtime-written)
- Create: `tools/license/verify-license-logic.ps1` (logic probe)
- Modify: `TotalParking/TotalParking.csproj` (`<Compile Include>` for the two `.cs`; `<Content Include>` for the sidecar if seeded — C2-03)
- Read: `TotalParking/Web.config` (`license:publicKey`, `license:dataDir`), `TotalParking/App_Data/license/license.lic`

## Acceptance
- AC-01/AC-04: a valid, future-dated license → VALID.
- AC-05: invalid signature, past expiry, or corrupt/missing file → LOCKED, no exception thrown.
- AC-07: a clock earlier than the sidecar high-water → evaluated at high-water; high-water never advances beyond the bounded window; deleting the sidecar restores evaluation to the real clock.

## Dependencies
- task-01-keys-and-issuer.md

## Verification Plan
- Command: `powershell -NoProfile -File tools/license/verify-license-logic.ps1`
- Named probe: `verify-license-logic.ps1` loads the Debug-built `TotalParking.dll` (+ deps from `bin/`) via reflection and drives `LicenseService` across five cases: valid-future → VALID; past-expiry → LOCKED; tampered-signature → LOCKED; missing/corrupt file → LOCKED (no throw); clock rolled back below high-water → evaluated at high-water. Asserts each, exits nonzero on any mismatch. (Closes C2-06; requires a prior Debug build via the full MSBuild path.)
- Reachability: known — run after `"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" TotalParking\TotalParking.sln /t:Build /p:Configuration=Debug`.
- Oracle: exit 0 and `5/5 cases PASS`.
- Counterexample: a build where a tampered or expired license reports VALID, or a missing file throws, must fail the probe (nonzero exit).
- Artifacts: probe writes only to a temp high-water path it cleans up; `bin/` output is ephemeral.

## Receipt

Verification: PASS
Command: powershell -NoProfile -ExecutionPolicy Bypass -File tools\license\verify-license-logic.ps1
Exit: 0
Base: 25336aba49a7512911dd25f01e20f2fec4649b7e
Head: 737f4e4a7496ef74e4f00d5261accd05ca29e7f3
```text
1 valid-future : PASS
2 past-expiry  : PASS
3 tampered     : PASS
4 missing      : PASS
5 rollback-guard: PASS
5/5 cases PASS
PROBE EXIT: 0
```

Prerequisite: Debug build via the full VS2019 MSBuild path exited 0 before the probe
(the probe loads `TotalParking/bin/TotalParking.dll`). The probe exercises real
behavior (not just compilation), closing C2-06; case 3 proves the C# verifier accepts
only PS-issued-and-signed licenses (cross-tool contract, C2-05); case 4 proves
fail-closed-no-throw (C2-09); case 5 proves the bounded clock guard blocks rollback
evasion (C2-07) while the >90-day jump bound (code) prevents the C2-01 false-positive lock.
