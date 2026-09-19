# Task R5-01: Operator page alignment
**Status:** pending

## Outcome

The Điều hướng xe map draws the destination the server chose and the lane route the server computed. The block-candidate filter, sort, and pick currently written in the view's JavaScript are gone, so the operator page can no longer show a different block from the one recorded in `vehicle_routing`.

## Scope

- **In scope:** the simulation map block of `Routing.cshtml` — removing the client-side candidate selection, drawing the server's `block_no`, and replacing the two-segment polyline with the server's `route`; the companion read-only check script.
- **Out of scope:** the driver view; the route endpoint and the `Simulate` response shape; the zone density, LED sign, and device-status panels on the same page; the page's existing 30-second block-map refresh.

## Anchors and Ownership

| ID | Type | Target | Role | Access | Action |
|---|---|---|---|---|---|
| A-R5-01-01 | file | `TotalParking/Views/Home/Routing.cshtml` | owner | write | modify |
| A-R5-01-02 | file | `TotalParking/Database/verify_simulate_readonly.sql` | proof | write | create |
| A-R5-01-03 | file | `TotalParking/Controllers/MonitorController.cs` | consumer | read | read |

## Changes

- [ ] Delete the client-side candidate block selection in the simulate handler per I2 — the `map.blocks` filter, the `sort` on freshness and free space, and the `cand[0]` pick — then highlight and label the `block_no` returned by the server, replace the two-segment gate-to-target polyline in `driveTo` with the server's `route` point array while keeping the existing car animation along those points, and draw nothing while keeping the outcome message when the response carries no `block_no` or an empty `route` per I3. _Requirements: 5.1_
- [ ] Keep the simulate call read-only, changing no write behaviour on the page, and write `verify_simulate_readonly.sql` capturing the `vehicle_event`, `vehicle_profile`, and `vehicle_routing` row counts so they can be compared either side of a simulate call. _Requirements: 5.2_

## Acceptance

- **R5.1:** the block highlighted and labelled on the operator map equals the `block_no` in the simulate response, and the view file contains no filter, sort, or pick over the block list in JavaScript.
- **R5.2:** the `vehicle_event`, `vehicle_profile`, and `vehicle_routing` row counts taken before and after a simulate call are identical.

## Dependencies

- tasks/task-R3-01-route-service.md

## Verification Plan

- **Verification ref:** V5
- **Task role:** subject
- **Command:** `mysql -h 127.0.0.1 -P 3306 -u root total_parking < TotalParking/Database/verify_simulate_readonly.sql`
- **Expected:** the three row counts taken before and after calling `/Monitor/Simulate?weight=2200KG` are identical, the simulate response carries a server-selected `block_no`, and the operator map highlights that block.
- **Negative path:** `grep -n "map.blocks.filter\|cand.sort\|cand\[0\]" TotalParking/Views/Home/Routing.cshtml` returns no match; any remaining candidate selection in the view fails the check.
- **Reachability:** `TotalParking/Views/Home/Routing.cshtml` is served at `/Home/Routing` and reached from the Điều hướng xe sidebar item.

<!-- The table above is the only task anchor/ownership table. Access read requires Action read. Access write requires create, modify, or delete. Targets are exact; globs and parent-directory claims are invalid. A verifier must not repeat the subject's Acceptance criterion. Specs-only never creates or updates docs and never fabricates execution proof; record doc impact as a brief recommendation only. -->
