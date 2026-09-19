# Task R3-01: Route service
**Status:** pending

## Outcome

`GET /Monitor/DriverRoute` answers the C1 contract: it reads the newest routing decision inside the D8 freshness window, computes the shortest lane path from the `is_entry` node to that decision's destination block arrival node, and returns state `ROUTE`, `MESSAGE`, or `WAITING`. A new `LaneNetwork` service owns the graph load and the Dijkstra search, so no geometry rule moves into the browser.

## Scope

- **In scope:** the `LaneNetwork` service that loads `lane_node`, `lane_edge`, and `block.lane_node_id` and runs Euclidean-weighted Dijkstra; the `DriverRoute` action and its three response states; the D8 freshness comparison in SQL; adding `block_no` and `route` to the existing `Simulate` response; the companion reachability script.
- **Out of scope:** the lane data itself; the block selection rule; any view markup or JavaScript; `ZoneRouter`; the block map and routing state endpoints, whose current shapes stay unchanged.

## Anchors and Ownership

| ID | Type | Target | Role | Access | Action |
|---|---|---|---|---|---|
| A-R3-01-01 | file | `TotalParking/Services/LaneNetwork.cs` | owner | write | create |
| A-R3-01-02 | file | `TotalParking/Controllers/MonitorController.cs` | owner | write | modify |
| A-R3-01-03 | file | `TotalParking/Database/verify_route_reachability.sql` | proof | write | create |
| A-R3-01-04 | file | `TotalParking/Database/35_lane_network.sql` | consumer | read | read |
| A-R3-01-05 | file | `TotalParking/Database/36_routing_block.sql` | consumer | read | read |

## Changes

- [ ] Load `lane_node`, `lane_edge`, and `block.lane_node_id` in `LaneNetwork` expanding each stored edge in both directions per C3, and run Dijkstra from the `is_entry` node with edge cost equal to the Euclidean distance between endpoint coordinates, resolving equal-cost ties by lower `node_id` so repeated calls agree. _Requirements: 3.1_
- [ ] Add the `DriverRoute` action implementing C1, selecting the newest decision with `NOW(3) - decided_at <= 90 seconds` in SQL, mapping outcomes to states `ROUTE`, `MESSAGE`, and `WAITING`, returning the ordered point array from the `is_entry` node to the destination arrival node, taking the frame from `BlockMapRepository.ViewW` and `ViewH` rather than restating the numbers per I1, and adding the same server-selected `block_no` and `route` to the read-only `Simulate` response. _Requirements: 3.2_
- [ ] Return an explicit no-route result naming the `block_no` when the target is unreachable or its `lane_node_id` is null with no straight-line, axis-aligned, or partial array per D4, return 503 with the exception message when the lane or routing views are missing, and write `verify_route_reachability.sql` using a recursive CTE from the `is_entry` node to list arrival nodes it cannot reach. _Requirements: 3.3_

## Acceptance

- **R3.1:** for a reachable block the returned point sequence has the minimum total Euclidean length among lane paths, and two consecutive calls with an unchanged lane network return the same sequence.
- **R3.2:** the returned `route` is an ordered array whose first point equals the `is_entry` node coordinates and whose last point equals the destination block's arrival node coordinates, in the frame reported by `view_w` and `view_h`.
- **R3.3:** a block whose arrival node has no edge path, or whose `lane_node_id` is null, produces state `MESSAGE` naming that `block_no` with an empty `route`, and the recursive reachability query lists that block.

## Dependencies

- tasks/task-R1-01-lane-network-migration.md
- tasks/task-R2-01-block-allocation.md

## Verification Plan

- **Verification ref:** V3
- **Task role:** subject
- **Command:** `mysql -h 127.0.0.1 -P 3306 -u root total_parking < TotalParking/Database/verify_route_reachability.sql`
- **Expected:** the recursive query lists zero unreachable arrival nodes, and two consecutive `GET /Monitor/DriverRoute` calls against the running site return the same `route` array beginning at the `is_entry` node and ending at the destination block's arrival node.
- **Negative path:** a block whose arrival node is detached from the graph is listed by the query and makes the endpoint answer state `MESSAGE` with an empty `route` instead of any point array.
- **Reachability:** `TotalParking/Controllers/MonitorController.cs` exposes the action at `/Monitor/DriverRoute` on the running site.

<!-- The table above is the only task anchor/ownership table. Access read requires Action read. Access write requires create, modify, or delete. Targets are exact; globs and parent-directory claims are invalid. A verifier must not repeat the subject's Acceptance criterion. Specs-only never creates or updates docs and never fabricates execution proof; record doc impact as a brief recommendation only. -->
