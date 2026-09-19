# Task R4-01: Driver guidance view
**Status:** pending

## Outcome

A driver stopped at the barrier can look at a TV showing `Home/DriverGuide`: the plan image at the routing frame, one drawn path from the ramp to the destination, and the block number in large type. The page polls the route endpoint every 2 seconds, shows a waiting state when nothing is current, and is registered in the project file so it survives Publish.

## Scope

- **In scope:** the `DriverGuide` view and its polling script, the `DriverGuide` action on `HomeController`, the `<Content Include>` entry in the project file, and the sidebar active-state entry; the companion state-transition script.
- **Out of scope:** the route endpoint and its contract; the lane data; the block selection rule; the operator page; authentication; any change to the existing sidebar items or to `_ScadaLayout` beyond adding this action name.

## Anchors and Ownership

| ID | Type | Target | Role | Access | Action |
|---|---|---|---|---|---|
| A-R4-01-01 | file | `TotalParking/Views/Home/DriverGuide.cshtml` | owner | write | create |
| A-R4-01-02 | file | `TotalParking/Controllers/HomeController.cs` | owner | write | modify |
| A-R4-01-03 | file | `TotalParking/TotalParking.csproj` | owner | write | modify |
| A-R4-01-04 | file | `TotalParking/Views/Shared/_ScadaLayout.cshtml` | owner | write | modify |
| A-R4-01-05 | file | `TotalParking/Database/verify_driver_route_state.sql` | proof | write | create |
| A-R4-01-06 | file | `TotalParking/Controllers/MonitorController.cs` | consumer | read | read |

## Changes

- [ ] Render the plan image with an SVG overlay whose `viewBox` comes from the endpoint's `view_w` and `view_h` rather than literals per I1, drawing the returned `route` as one path and the destination block number as a large label sized for a wall-mounted screen. _Requirements: 4.1_
- [ ] Poll the route endpoint every 2 seconds per D10, swap to the newest `ROUTE` payload on arrival, and leave the last rendered state untouched when a poll fails. _Requirements: 4.2_
- [ ] Render the waiting state on `WAITING` and clear any previously drawn path and label rather than leaving them on screen, per I3. _Requirements: 4.3_
- [ ] Render the outcome reason text on `MESSAGE`, draw no path and no destination label, and write `verify_driver_route_state.sql` seeding `decided_at` values around the 90-second boundary and different outcomes so the resolved state of each seeded row is reported. _Requirements: 4.4_
- [ ] Add the `DriverGuide` action returning its view, create the view with `Layout = "~/Views/Shared/_ScadaLayout.cshtml"`, add the view as a `<Content Include>` entry in the project file, and add its action name to the sidebar active-state expression. _Requirements: 4.5_

## Acceptance

- **R4.1:** with a current `ROUTE` payload the page shows the plan image, exactly one drawn path, and the destination block number as a text label.
- **R4.2:** a decision persisted while the page is open is drawn within 3 seconds, and when two `ROUTED` decisions sit inside the window the one with the later `decided_at` is drawn.
- **R4.3:** a seeded decision aged 91 seconds resolves to `WAITING`, and the page shows the waiting state with no path element and no destination label left in the DOM.
- **R4.4:** a seeded decision with outcome `REJECTED`, `NO_CAPACITY`, `NO_DATA`, or `MANUAL` inside the window shows its reason text with no path element and no destination label.
- **R4.5:** `Views\Home\DriverGuide.cshtml` appears as a `<Content Include>` entry in the project file, the view sets the SCADA layout, and its action name appears in the sidebar active-state expression.

## Dependencies

- tasks/task-R3-01-route-service.md

## Verification Plan

- **Verification ref:** V4
- **Task role:** subject
- **Command:** `mysql -h 127.0.0.1 -P 3306 -u root total_parking < TotalParking/Database/verify_driver_route_state.sql`
- **Expected:** the seeded row aged 89 seconds resolves to `ROUTE`, the row aged 91 seconds resolves to `WAITING`, a seeded `NO_CAPACITY` row inside the window resolves to `MESSAGE`; opening `Home/DriverGuide` against the running site then shows the plan image with one drawn path and the destination block number, and `grep DriverGuide TotalParking/TotalParking.csproj` returns the Content Include line.
- **Negative path:** the seeded `REJECTED` row renders its reason text with no path element and no destination label, and the drawn path is cleared when the window lapses instead of staying on screen.
- **Reachability:** `TotalParking/Controllers/HomeController.cs` serves the view at `/Home/DriverGuide` on the running site.

<!-- The table above is the only task anchor/ownership table. Access read requires Action read. Access write requires create, modify, or delete. Targets are exact; globs and parent-directory claims are invalid. A verifier must not repeat the subject's Acceptance criterion. Specs-only never creates or updates docs and never fabricates execution proof; record doc impact as a brief recommendation only. -->
