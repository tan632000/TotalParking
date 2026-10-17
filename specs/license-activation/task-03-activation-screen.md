# Task 03 — Activation screen (paste & apply a license)

Status: done

## Outcome
A locked-state screen the operator reaches when the app is locked: it explains
the software needs activation, accepts a pasted license, calls `LicenseService`
to verify and persist it, and on success restores normal access (on failure
shows a clear error and stays locked).

## Scope
- In: `ActivationController` (GET locked page, POST apply license), the view,
  and the `TotalParking.csproj` `<Content>`/`<Compile>` entries for the new files.
- The controller must remain reachable while LOCKED (task 04 whitelists it) and
  must render even when `LicenseService` evaluates LOCKED/errors (C2-09).
- Out: the gate that forces users here (task 04); verification logic (task 02).

## Coverage
- CP-04 (activation entry + success/failure outcomes)

## Ownership
- Create: `TotalParking/Controllers/ActivationController.cs`
- Create: `TotalParking/Views/Activation/Index.cshtml`
- Modify: `TotalParking/TotalParking.csproj` (`<Content Include>` for the view AND `<Compile Include>` for the controller — Hard rule 1 / C2-03)

## Acceptance
- AC-04: posting a valid future-dated license (per the Signing contract) returns success and the app is no longer locked.
- AC-05: posting an invalid/expired/corrupt license re-renders the screen with an error and the app stays locked.

## Dependencies
- task-02-license-service.md

## Verification Plan
- Command: `"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" TotalParking\TotalParking.sln /t:Build /p:Configuration=Debug`
- Named probe: compilation of `ActivationController`, plus `grep -F 'Views\Activation\Index.cshtml' TotalParking/TotalParking.csproj` returns the include (C2-02/C2-03 discipline).
- Reachability: known — MVC route `{controller}/{action}` reaches `/Activation`; confirmed live at C3.
- Oracle: build Exit 0 and the csproj grep is non-empty; live@C3 — pasting the renewal license unlocks; pasting an invalid one shows the error and stays locked.
- Counterexample: a view missing from the csproj makes the grep empty (404 on Publish — Hard rule 1).
- Artifacts: build output — ephemeral.

## Receipt

Verification: PASS
Command: & "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" "TotalParking\TotalParking.sln" /t:Build /p:Configuration=Debug
Exit: 0
Base: 25336aba49a7512911dd25f01e20f2fec4649b7e
Head: 776439608b0353ce1f168b49ef2e2ddff43f6ce0
```text
MSBUILD EXIT: 0
-- csproj includes (Select-String) --
<Compile Include="Controllers\ActivationController.cs" />
<Content Include="Views\Activation\Index.cshtml" />
```

Named probe satisfied: build Exit 0 AND both the controller `<Compile>` and the view
`<Content>` are present in `TotalParking.csproj` (C2-02/C2-03 — else the view 404s on
Publish). Live oracle (pasting a valid license unlocks; invalid shows error and stays
locked) is a C3 run-the-app check. The Activation view uses `Layout = null` so it does
not depend on `_ScadaLayout`'s `/Alarm/Summary` poll, which the gate 403s while locked.
