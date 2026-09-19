# Task R5-01: Operator page alignment
**Status:** done

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

- [x] Delete the client-side candidate block selection in the simulate handler per I2 — the `map.blocks` filter, the `sort` on freshness and free space, and the `cand[0]` pick — then highlight and label the `block_no` returned by the server, replace the two-segment gate-to-target polyline in `driveTo` with the server's `route` point array while keeping the existing car animation along those points, and draw nothing while keeping the outcome message when the response carries no `block_no` or an empty `route` per I3. _Requirements: 5.1_
- [x] Keep the simulate call read-only, changing no write behaviour on the page, and write `verify_simulate_readonly.sql` capturing the `vehicle_event`, `vehicle_profile`, and `vehicle_routing` row counts so they can be compared either side of a simulate call. _Requirements: 5.2_

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

## Receipt

- **Verification:** PASS
- **Command:** `mysql -h 127.0.0.1 -P 3306 -u root total_parking < TotalParking/Database/verify_simulate_readonly.sql`
- **Exit:** 0
- **Base:** 2a57347b0bff0bb6c71e4001937988067cb5e7f1
- **Head:** 2a57347b0bff0bb6c71e4001937988067cb5e7f1
- **Run at:** 2026-09-19, MySQL 8.4.11, database `total_parking` on 127.0.0.1:3306. The
  root password was supplied through `MYSQL_PWD` so it stays out of this file.

Current output:

```text
kiem_tra	so_dong_vehicle_event	so_dong_vehicle_profile	so_dong_vehicle_routing	su_kien_moi_nhat	quyet_dinh_moi_nhat	tong_block_no	dong_mang_event_id_SIM
Chu ky truoc/sau khi goi Simulate	684	682	580	2026-09-19 21:22:59.323	2026-09-19 21:07:31.360	672	0
```

### R5.2 — three Simulate calls wrote nothing

The signature was taken, three calls were made, and the signature was taken again.
Every field is identical, including the two timestamps and the `block_no` checksum,
which would move even if a row were overwritten rather than added:

```text
BEFORE  684 / 682 / 580 | su_kien 2026-09-19 21:22:59.323 | quyet_dinh 2026-09-19 21:07:31.360 | tong_block_no 672 | SIM rows 0

  GET /Monitor/Simulate?weight=2200KG  -> ROUTED zone 1 block 112 route_pts 14
  GET /Monitor/Simulate?weight=THUONG  -> ROUTED zone 1 block 112 route_pts 14
  GET /Monitor/Simulate?weight=2600KG  -> NO_CAPACITY zone None block None route_pts 0

AFTER   684 / 682 / 580 | su_kien 2026-09-19 21:22:59.323 | quyet_dinh 2026-09-19 21:07:31.360 | tong_block_no 672 | SIM rows 0
```

Counting rows alone would not have been enough: the routing write is an
`INSERT ... ON DUPLICATE KEY UPDATE` on `event_id`, so overwriting a row leaves the
count unchanged. The timestamps and the `block_no` sum are what close that hole, and
the `SIM` row count proves the endpoint's placeholder `event_id` never reached the table.

### R5.1 — the operator map draws the server's decision

Headless Chrome opened `/Home/Routing`, clicked the 2200KG button, captured the
`Simulate` response off the wire, and then read the DOM. The two agree field for field:

```text
server:
 outcome ROUTED | zone_id 1 | block_no 112 | route_points 14
 first {"x":1052,"y":1165}   last {"x":1026,"y":1139}

dom:
 route_path_exists    true
 route_path_vertices  14
 route_path_head      "M1052,1165 L1094,1167 L1136,1167 L1178,1"
 label_text           "BLOCK 112"
 highlighted_blocks   ["112"]
 outcome_box          "ZONE 1 · block 112hạng tải 2200KG · còn 126 chỗ · gần cổng hạng 0"
```

Re-run after the frame re-crop of R1-01. Every coordinate moved with the frame and the
DOM followed the server field for field, which is the point: nothing on this page
derives geometry of its own.

The drawn path has the same 14 vertices as the server's array and starts at the
server's first point, so it is the lane route rather than the old two-segment
gate-to-target line. Screenshot:
`.claude/chrome-devtools/screenshots/routing-operator.png`

### Negative path — no candidate selection left in the view

```text
$ grep -n "map.blocks.filter\|cand.sort\|cand\[0\]" TotalParking/Views/Home/Routing.cshtml
(no match, exit 1)
```

What was removed: the `map.blocks` filter on zone and occupancy, the `sort` on
`online` then free space, and the `cand[0]` pick. `driveTo` now takes the server's
`block_no` and `route` instead of deriving a polyline from `map.gate` and the block's
own coordinates, and it draws nothing when the route has fewer than two points.

### Two consequences of dropping the client-side pick

- The result box used to read `target.block_no` and `target.online`, where `target`
  came from the client's own pick. It now reads `d.block_no` and `d.occupancy_verified`
  straight off the response. Without that change a destination the server chose but the
  map cannot plot — `kind = 'Ground'`, blocks 901 to 906, which have no `map_x` — would
  have shown a blank block number.
- That case now says so explicitly, with `block này không có toạ độ trên sơ đồ`, rather
  than silently drawing nothing.

### Limitations

- **The route sits at the bottom of the operator map and needs scrolling.** The map
  container keeps its existing `aspect-ratio: 1200 / 1139`, so on a 1600x1100 viewport
  the ramp end of the plan is below the fold. The page's existing layout is out of this
  task's scope, and unlike the driver TV this page is scrollable. The screenshot above
  was taken scrolled to the route.
- **`Routing.cshtml` still hardcodes the frame**, now as `viewBox="0 0 1594 1300"` and
  `aspect-ratio: 1594 / 1300`. I1 wants it to come from the API, and the `Simulate`
  response now carries `view_w` and `view_h`, but the literals predate this work and this
  task's Changes do not name them. They had to be edited by hand both times the frame was
  re-cropped, which is exactly the cost I1 exists to avoid; the driver view needed no
  edit because it reads the frame from the response.
- **The caption `mô phỏng minh hoạ — không phải vị trí xe thật` was left as it is.** The
  drawn path is now the real computed route, but the animated car is still an animation,
  so the caption remains true of what it labels.
- **`2600KG` returns `NO_CAPACITY` in the run above.** That is `ZoneRouter`'s existing
  behaviour: `block.column_count` is null across the board, so `total_tier0` sums to zero
  and no zone qualifies. Unchanged by this task and recorded in D5 as out of scope.

<!-- The table above is the only task anchor/ownership table. Access read requires Action read. Access write requires create, modify, or delete. Targets are exact; globs and parent-directory claims are invalid. A verifier must not repeat the subject's Acceptance criterion. Specs-only never creates or updates docs and never fabricates execution proof; record doc impact as a brief recommendation only. -->
