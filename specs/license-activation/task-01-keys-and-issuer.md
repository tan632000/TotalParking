# Task 01 — RSA keys, offline issuer, and pre-installed trial license

Status: done

## Outcome
An RSA-2048 keypair exists (private key held by the vendor, never committed),
the app embeds the public key, an offline issuing script produces licenses per
the plan's Signing contract, and a pre-installed trial license valid until
2026-11-07 is both placed in `App_Data` AND included in the csproj so a built
and PUBLISHED app runs normally until that date.

## Scope
- In: keypair generation (offline); issuing script implementing the plan's
  Signing contract (RSA-2048, SHA-256, PKCS#1 v1.5, canonical UTF-8 JSON payload
  `customerId,issuedUtc,expiryUtc`); embed public key in `Web.config`; generate
  the committed trial license (expiry 2026-11-07) AND add it as `<Content Include>`.
- Dev-local (C2-12): document generating a gitignored long-expiry license into
  `App_Data/license/` for dev/CI rebuilds after 2026-11-07. No Debug-gated flag
  (app ships `compilation debug="true"` → would disable the gate in production).
- Out: signature verification in the app (task 02); the gate (task 04).

## Coverage
- CP-04 (license artifact + signing half)

## Ownership
- Create: `tools/license/issue-license.ps1`, `tools/license/README.md`
- Create: `TotalParking/App_Data/license/license.lic` (trial, committed)
- Modify: `TotalParking/Web.config` (add `license:publicKey`, `license:dataDir`)
- Modify: `TotalParking/TotalParking.csproj` (add `<Content Include="App_Data\license\license.lic" />` — C2-02; else Publish drops it like the `.cshtml` Hard-rule-1 case, cf. `csproj:315` `driver_lanes.json`)
- Modify: `.gitignore` (ignore the private key path and `App_Data/license/*.dev.lic`)

## Acceptance
- AC-04 (issuing half): running the issuer with a given expiry produces a license
  whose signature verifies against the committed public key AND the committed
  trial `license.lic` verifies against the committed `Web.config` public key.

## Dependencies
- none

## Verification Plan
- Command: `powershell -NoProfile -File tools/license/issue-license.ps1 -VerifyCommitted -PublicKeyFrom TotalParking/Web.config -License TotalParking/App_Data/license/license.lic`
- Named probe: `issue-license.ps1` `-VerifyCommitted` branch — verifies the COMMITTED trial license against the COMMITTED public key using the contract's `RSA.VerifyData(..., SHA256, Pkcs1)` (cross-artifact, not a self-sign; closes C2-05/C2-07's "always green" gap).
- Reachability: known — PowerShell 5.1 + .NET Framework 4.7.2 RSA on this Windows host.
- Oracle: exit 0 and printed `COMMITTED LICENSE: VALID` with expiry 2026-11-07.
- Counterexample: flipping one payload byte of the committed license, or using a non-matching public key, must print `INVALID` and exit nonzero.
- Artifacts: any `_verify*.lic` scratch files are ephemeral — delete after the run; the committed artifact is `TotalParking/App_Data/license/license.lic`.

## Receipt

Verification: PASS
Command: powershell -NoProfile -ExecutionPolicy Bypass -File tools\license\issue-license.ps1 -VerifyCommitted -PublicKeyFrom TotalParking\Web.config -License TotalParking\App_Data\license\license.lic
Exit: 0
Base: 25336aba49a7512911dd25f01e20f2fec4649b7e
Head: 876e851affea7746843b1ae8c17acf3cc4672cf6
```text
COMMITTED LICENSE: VALID
  expiry: 2026-11-07T00:00:00Z
EXIT: 0
```

Negative control (tampered copy under %TEMP%, not the committed file): flipping one
payload byte → `COMMITTED LICENSE: INVALID` + exit 1. The oracle discriminates
(positive VALID/exit0 vs negative INVALID/exit1), so it is not always-green.

Security: `git check-ignore tools/license/keys/private-key.xml` returns the path →
the private key is git-ignored and will not be committed. Public key stored as
Base64(XML) in `Web.config` `license:publicKey`. Keypair is a DEV key — regenerate
on a secure machine with `-GenerateKeys -Force` before true handover (see README).
