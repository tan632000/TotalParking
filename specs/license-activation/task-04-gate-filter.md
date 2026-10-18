# Task 04 — LicenseGateFilter + registration + whitelist

Status: done

## Outcome
A global action filter enforces the license on every MVC request: when
`LicenseService` reports LOCKED, navigations redirect to the Activation screen
and JSON requests get HTTP 403; when VALID, nothing changes. A minimal
controller-name whitelist keeps Activation and the loopback ingest endpoints
reachable; the filter always lets the Activation screen render.

## Scope
- In: `LicenseGateFilter`, its registration in `FilterConfig`, and the whitelist.
- Discriminator (C2-04): classify a request as JSON by `Accept: application/json`
  OR the action returning `JsonResult` → HTTP 403; everything else (navigation)
  → 302 redirect to Activation. Do NOT use `Request.IsAjaxRequest()` (always
  false here — native `fetch`, no `X-Requested-With`).
- Whitelist (C2-11): match by CONTROLLER NAME — `Activation` and `Ingest` only.
  Drop "static assets" (IIS serves them; they never enter the MVC filter). `Ingest`
  is safe to whitelist because every action self-guards `IsLoopbackClient`
  (`IngestController.cs:58,67,299-311`).
- Fail-safe (C2-09): the filter must always allow the Activation controller even
  if license evaluation errors; `LicenseService` returns LOCKED (not throws) so
  the gate never 500s the whole site.
- Out: verification logic (task 02); Activation UI (task 03).

## Coverage
- CP-01, CP-02, CP-03, CP-05

## Ownership
- Create: `TotalParking/Filters/LicenseGateFilter.cs`
- Modify: `TotalParking/App_Start/FilterConfig.cs` (register after `HandleErrorAttribute`)
- Modify: `TotalParking/TotalParking.csproj` (`<Compile Include>` for the filter — C2-03)
- Read: `TotalParking/App_Start/RouteConfig.cs:18-28`, `TotalParking/Controllers/IngestController.cs:58,67,299-311`

## Acceptance
- AC-01: VALID state → no redirect on any page.
- AC-02: LOCKED state → navigation to a non-whitelisted controller redirects to Activation; a JSON request (Accept/JsonResult) returns 403.
- AC-03: LOCKED state → `Activation`, `/health`, `/vehicle` still return normally.
- AC-06: background hosts start/run regardless of filter state (filter touches only the MVC pipeline; hosts start at `Global.asax.cs:26-60`).

## Dependencies
- task-03-activation-screen.md

## Verification Plan
- Command: `"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" TotalParking\TotalParking.sln /t:Build /p:Configuration=Debug`
- Named probe: compilation of `LicenseGateFilter` + its registration line in `FilterConfig.cs`; exercised live at C3.
- Reachability: known — `FilterConfig.RegisterGlobalFilters` runs at `Global.asax.cs:19`.
- Oracle: build Exit 0; live@C3 (clock advanced past 2026-11-07) — Dashboard/DriverGuide navigations redirect to Activation; a JSON poll (e.g. `/Alarm/Summary` with `Accept: application/json`) returns 403; `/health`, `/vehicle`, and Activation still return 200; PLC/LED/PGS hosts keep logging.
- Counterexample: a LOCKED non-whitelisted request that is neither redirected nor 403; a whitelisted `/vehicle` that gets blocked; or a JSON poll that gets a 302 HTML body — each fails the oracle.
- Artifacts: build output — ephemeral.

## Receipt

Verification: PASS
Command: & "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" "TotalParking\TotalParking.sln" /t:Build /p:Configuration=Debug
Exit: 0
Base: 25336aba49a7512911dd25f01e20f2fec4649b7e
Head: 11ef39b7ad8c5adcaabcbe10d176c94ec7b776d6
```text
MSBUILD EXIT: 0
-- filter registered (FilterConfig.cs) --
filters.Add(new TotalParking.Filters.LicenseGateFilter());
-- compile include (csproj) --
<Compile Include="Filters\LicenseGateFilter.cs" />
```

Named probe satisfied: build Exit 0 AND the filter is registered in
`FilterConfig.RegisterGlobalFilters` (runs at `Global.asax.cs:19`) AND its `<Compile>`
is in the csproj. LIMITATION: the live oracle (locked → Home redirects to Activation,
JSON controllers return 403, `/health` `/vehicle` + Activation stay 200, PLC/LED/PGS
hosts keep running) is NOT executed here — it is a C3 run-the-app check. Build proves
compilation/registration only.
