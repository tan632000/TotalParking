# Task R1-01: Lane network migration
**Status:** pending

## Outcome

`total_parking` holds a drivable lane network — `lane_node`, `lane_edge`, and a `block.lane_node_id` arrival column — expressed in the routing map frame of `Images/plan_map.jpg`, with exactly one node flagged as the ramp entry. Running `35_lane_network.sql` a second time leaves the same rows, and `verify_lane_network.sql` reports the frame, entry-node, and arrival-node counts.

## Scope

- **In scope:** the `lane_node` and `lane_edge` tables of C3, the `block.lane_node_id` column and its foreign key, the node and edge data for the parking level, the single `is_entry` node of D3, and the companion verification script.
- **Out of scope:** any C# code; the path algorithm; `vehicle_routing`; regenerating `Images/plan_map.jpg`; changing `block.map_x`, `block.map_y`, `block.origin_x`, or `block.origin_y`; changing `BlockMapRepository.GateX` or `GateY`.

## Anchors and Ownership

| ID | Type | Target | Role | Access | Action |
|---|---|---|---|---|---|
| A-R1-01-01 | file | `TotalParking/Database/35_lane_network.sql` | owner | write | create |
| A-R1-01-02 | file | `TotalParking/Database/verify_lane_network.sql` | proof | write | create |

## Changes

- [ ] Create `lane_node` and `lane_edge` exactly as C3 defines them, with `CREATE TABLE IF NOT EXISTS` and foreign keys on both edge endpoints, then derive node and edge coordinates from the drawing using the D2 transform and insert them with explicit `node_id` values inside the frame bounds. _Requirements: 1.1_
- [ ] Insert exactly one node carrying `is_entry = 1` at the ramp head, per D3. _Requirements: 1.2_
- [ ] Add `block.lane_node_id` behind an `information_schema` guard before the `ALTER`, following the guard pattern in `34_block_map_xy.sql`, then set it for every block whose `map_x` is not null. _Requirements: 1.3_
- [ ] Make every insert and update idempotent with `INSERT ... ON DUPLICATE KEY UPDATE` so a second run changes no row count, and write `verify_lane_network.sql` reporting the out-of-frame node count, the `is_entry` count, the count of mapped blocks without an arrival node, and the node and edge totals, naming offending ids rather than only counting them. _Requirements: 1.4_

## Acceptance

- **R1.1:** every `lane_node` row satisfies `0 <= x <= 1200` and `0 <= y <= 1139`; the verification script reports a zero count of rows outside those bounds and prints the `node_id` of any it finds.
- **R1.2:** `SELECT COUNT(*) FROM lane_node WHERE is_entry = 1` returns exactly 1, and the script flags any other count.
- **R1.3:** `SELECT COUNT(*) FROM block WHERE map_x IS NOT NULL AND lane_node_id IS NULL` returns 0, and the script lists the offending `block_no` values when it does not.
- **R1.4:** running `35_lane_network.sql` twice raises no duplicate-key error and leaves the `lane_node`, `lane_edge`, and populated `lane_node_id` counts identical between the two runs.

## Dependencies

- none

## Verification Plan

- **Verification ref:** V1
- **Task role:** subject
- **Command:** `mysql -h 127.0.0.1 -P 3306 -u root total_parking < TotalParking/Database/verify_lane_network.sql`
- **Expected:** the out-of-frame node count is 0, the `is_entry` count is 1, the count of mapped blocks without an arrival node is 0, and the node and edge totals equal the totals printed after the first migration run.
- **Negative path:** inserting a node at `x = 1300`, or a second row with `is_entry = 1`, makes the matching check row report a non-zero count and print the offending `node_id`.
- **Reachability:** `TotalParking/Controllers/MonitorController.cs` serves the same `block` table in the same frame to both map surfaces.

<!-- The table above is the only task anchor/ownership table. Access read requires Action read. Access write requires create, modify, or delete. Targets are exact; globs and parent-directory claims are invalid. A verifier must not repeat the subject's Acceptance criterion. Specs-only never creates or updates docs and never fabricates execution proof; record doc impact as a brief recommendation only. -->
