# Task R3-01: Route service
**Status:** done

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
| A-R3-01-04 | file | `TotalParking/Database/37_lane_network.sql` | consumer | read | read |
| A-R3-01-05 | file | `TotalParking/Database/38_routing_block.sql` | consumer | read | read |

## Changes

- [x] Load `lane_node`, `lane_edge`, and `block.lane_node_id` in `LaneNetwork` expanding each stored edge in both directions per C3, and run Dijkstra from the `is_entry` node with edge cost equal to the Euclidean distance between endpoint coordinates, resolving equal-cost ties by lower `node_id` so repeated calls agree. _Requirements: 3.1_
- [x] Add the `DriverRoute` action implementing C1, selecting the newest decision with `NOW(3) - decided_at <= 90 seconds` in SQL, mapping outcomes to states `ROUTE`, `MESSAGE`, and `WAITING`, returning the ordered point array from the `is_entry` node to the destination arrival node, taking the frame from `BlockMapRepository.ViewW` and `ViewH` rather than restating the numbers per I1, and adding the same server-selected `block_no` and `route` to the read-only `Simulate` response. _Requirements: 3.2_
- [x] Return an explicit no-route result naming the `block_no` when the target is unreachable or its `lane_node_id` is null with no straight-line, axis-aligned, or partial array per D4, return 503 with the exception message when the lane or routing views are missing, and write `verify_route_reachability.sql` using a recursive CTE from the `is_entry` node to list arrival nodes it cannot reach. _Requirements: 3.3_

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

## Receipt

- **Verification:** PASS
- **Command:** `mysql -h 127.0.0.1 -P 3306 -u root total_parking < TotalParking/Database/verify_route_reachability.sql`
- **Exit:** 0
- **Base:** 2a57347b0bff0bb6c71e4001937988067cb5e7f1
- **Head:** 2a57347b0bff0bb6c71e4001937988067cb5e7f1
- **Run at:** 2026-09-19, MySQL 8.4.11, database `total_parking` on 127.0.0.1:3306. The
  root password was supplied through `MYSQL_PWD` so it stays out of this file.

Current output:

```text
kiem_tra	so_nut_is_entry	ket_qua	chi_tiet
Co dung mot nut dau doc	1	PASS	313
kiem_tra	so_block_thieu_nut_den	ket_qua	chi_tiet
Block co map_x deu co nut den	0	PASS	-
kiem_tra	so_block_khong_toi_duoc	ket_qua	chi_tiet
R3.3 moi block deu toi duoc tu dau doc	0	PASS	-
kiem_tra	so_block_trung	ket_qua	chi_tiet
Nut den khong trung nut dau doc	0	PASS	-
kiem_tra	nut_toi_duoc	tong_nut	tong_canh	block_co_nut_den
Doi chieu	313	313	341	112
```

### Build

```text
TotalParking -> C:\Users\Admin\source\repos\TotalParking\TotalParking\bin\TotalParking.dll
```

### R3.1 and R3.2 — the live endpoint, checked against an independent search

The site ran on IIS Express at `http://localhost:58349`. An event was posted to
`POST /vehicle`, then `GET /Monitor/DriverRoute` was called twice in a row. Both
calls returned byte-identical `route` arrays:

```text
state ROUTE | block 112 | diem 14 | view 1594x1300
hai lan goi giong nhau: True
dau {'x': 1052, 'y': 1165}   cuoi {'x': 1026, 'y': 1139}
```

The first point `(1052, 1165)` is the `is_entry` node 313 and the last `(1026, 1139)`
is block 112's arrival node 306, both read back from `lane_node`, and the frame is
reported as 1594 by 1300 rather than restated in the view.

These coordinates are the ones that followed the frame re-crop recorded in R1-01. The
endpoint needed no change for it: the frame comes from `BlockMapRepository`, which is
what I1 asks for.

Minimality was checked against a second, independently written Dijkstra reading the
same stored rows:

```text
do dai endpoint 474.15 px | Dijkstra doc lap 474.15 px | nut dich 306
entry node 313 (1052, 1165)
```

The length is the same figure recorded before the re-crop, which is the expected
result: translating every node by a constant cannot change a distance.

### C1 states — all three observed live

```text
WAITING  (no decision inside the 90 s window)
{"view_w":1200,"view_h":1139,"state":"WAITING","event_id":null,
 "outcome":null,"reason":null,"zone_id":null,"block_no":null,
 "occupancy_verified":false,"route":[]}
         (captured before the frame re-crop; view_w/view_h now report 1594 x 1300)

ROUTE    (shown above)

MESSAGE  (destination block has a null lane_node_id)
{"state":"MESSAGE","event_id":"r301-1789826777","outcome":"ROUTED",
 "reason":"Block 901 chua duoc gan nut lan duong.","zone_id":1,
 "block_no":null,"occupancy_verified":true,"route":[]}
```

### R3.3 negative path — a detached arrival node

Block 43's arrival node 89 was cut from the graph by deleting its two edges, the
endpoint was called, the reachability script was run, and the edges were then put
back. The endpoint named the block and drew nothing; the script listed it:

```text
endpoint:
{"state":"MESSAGE","reason":"Khong co duong lan noi tu dau doc toi block 43.",
 "block_no":null,"route":[]}

script:
R3.3 moi block deu toi duoc tu dau doc	1	FAIL	43@node89

after restoring edges (88,89) and (89,90):
R3.3 moi block deu toi duoc tu dau doc	0	PASS	-
nut 313 | canh 341 | block_co_nut 112
```

### 503 when the lane tables are missing

`lane_node` was renamed away, the endpoint was called, and the table was renamed
back. All nine foreign keys referencing `lane_node` and `block` were confirmed
intact afterwards, and the row counts were unchanged:

```text
HTTP 503
{"error":"Table 'total_parking.lane_node' doesn't exist"}

after restore: nut 313 | canh 341 | block_co_nut 112
fk_block_lane_node, fk_lane_edge_from, fk_lane_edge_to  -> lane_node(node_id)
```

### Simulate stays read-only and now carries the same decision

```text
GET /Monitor/Simulate?weight=2200KG
outcome ROUTED zone 1 block_no 112 occ_verified True
route points 14  first {'x':910,'y':1132}  last {'x':884,'y':1106}
view 1200 1139

row counts vehicle_event/vehicle_profile/vehicle_routing
before two Simulate calls: 678/676/574
after:                     678/676/574
```

### Deviation from the ownership table

`GetCurrentDecision` was added to `TotalParking/Services/VehicleRoutingRepository.cs`,
which is not listed in this task's anchors. The D8 query reads `vehicle_routing`, and
every other query against that table lives in that repository; the alternatives were a
raw `MySqlConnection` inside a controller, which no controller in this project does, or
a `vehicle_routing` reader hidden inside the lane-network file. The user chose the
repository.

### Limitations

- **The route to block 112 is 474 px long for a block 24 px from the ramp.** It is the
  true minimum on this lane graph, confirmed twice above, but it is long because block
  112's arrival node sits on its left-hand side and that side is reached only from the
  aisle above the 110-112 row. Arrival nodes are assigned by nearest lane node, which is
  proximity, not the side a block is actually entered from. Where those differ the route
  is longer than it needs to be. It is never wrong in the dangerous direction: no edge
  crosses a block footprint. Confirming the real access sides is a site question.
- **The freshness comparison is literal.** `TIMESTAMPDIFF(MICROSECOND, decided_at, NOW(3))
  <= 90 s` is what D8 specifies, and a row dated in the future satisfies it. D8 fixes the
  clock source as `NOW(3)` on the single host that runs both ingest and site, so that case
  needs a clock moving backwards to occur; no guard against it was added.
- **The graph is read on every call.** 313 nodes and 341 edges are two small queries
  against MySQL on the same host, and at the 2-second poll of D10 that is four queries a
  second. No cache was added, so an edit to the lane data takes effect on the next poll
  rather than after a restart.
- **Test rows were left in the database.** `verify-r201-…` and `r301-…` in
  `vehicle_event`, `vehicle_profile`, and `vehicle_routing` are the evidence quoted in
  this receipt and in R2-01's.

<!-- The table above is the only task anchor/ownership table. Access read requires Action read. Access write requires create, modify, or delete. Targets are exact; globs and parent-directory claims are invalid. A verifier must not repeat the subject's Acceptance criterion. Specs-only never creates or updates docs and never fabricates execution proof; record doc impact as a brief recommendation only. -->
