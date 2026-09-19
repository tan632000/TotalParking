# Task R1-01: Lane network migration
**Status:** done

## Outcome

`total_parking` holds a drivable lane network — `lane_node`, `lane_edge`, and a `block.lane_node_id` arrival column — expressed in the routing map frame of `Images/plan_map.jpg`, with exactly one node flagged as the ramp entry. Running `37_lane_network.sql` a second time leaves the same rows, and `verify_lane_network.sql` reports the frame, entry-node, and arrival-node counts.

## Scope

- **In scope:** the `lane_node` and `lane_edge` tables of C3, the `block.lane_node_id` column and its foreign key, the node and edge data for the parking level, the single `is_entry` node of D3, and the companion verification script.
- **Out of scope:** any C# code; the path algorithm; `vehicle_routing`; regenerating `Images/plan_map.jpg`; changing `block.map_x`, `block.map_y`, `block.origin_x`, or `block.origin_y`; changing `BlockMapRepository.GateX` or `GateY`.

## Anchors and Ownership

| ID | Type | Target | Role | Access | Action |
|---|---|---|---|---|---|
| A-R1-01-01 | file | `TotalParking/Database/37_lane_network.sql` | owner | write | create |
| A-R1-01-02 | file | `TotalParking/Database/verify_lane_network.sql` | proof | write | create |

## Changes

- [x] Create `lane_node` and `lane_edge` exactly as C3 defines them, with `CREATE TABLE IF NOT EXISTS` and foreign keys on both edge endpoints, then derive node and edge coordinates from the drawing using the D2 transform and insert them with explicit `node_id` values inside the frame bounds. _Requirements: 1.1_
- [x] Insert exactly one node carrying `is_entry = 1` at the ramp head, per D3. _Requirements: 1.2_
- [x] Add `block.lane_node_id` behind an `information_schema` guard before the `ALTER`, following the guard pattern in `34_block_map_xy.sql`, then set it for every block whose `map_x` is not null. _Requirements: 1.3_
- [x] Make every insert and update idempotent with `INSERT ... ON DUPLICATE KEY UPDATE` so a second run changes no row count, and write `verify_lane_network.sql` reporting the out-of-frame node count, the `is_entry` count, the count of mapped blocks without an arrival node, and the node and edge totals, naming offending ids rather than only counting them. _Requirements: 1.4_

## Acceptance

- **R1.1:** every `lane_node` row satisfies `0 <= x <= 1200` and `0 <= y <= 1139`; the verification script reports a zero count of rows outside those bounds and prints the `node_id` of any it finds.
- **R1.2:** `SELECT COUNT(*) FROM lane_node WHERE is_entry = 1` returns exactly 1, and the script flags any other count.
- **R1.3:** `SELECT COUNT(*) FROM block WHERE map_x IS NOT NULL AND lane_node_id IS NULL` returns 0, and the script lists the offending `block_no` values when it does not.
- **R1.4:** running `37_lane_network.sql` twice raises no duplicate-key error and leaves the `lane_node`, `lane_edge`, and populated `lane_node_id` counts identical between the two runs.

## Dependencies

- none

## Verification Plan

- **Verification ref:** V1
- **Task role:** subject
- **Command:** `mysql -h 127.0.0.1 -P 3306 -u root total_parking < TotalParking/Database/verify_lane_network.sql`
- **Expected:** the out-of-frame node count is 0, the `is_entry` count is 1, the count of mapped blocks without an arrival node is 0, and the node and edge totals equal the totals printed after the first migration run.
- **Negative path:** inserting a node at `x = 1300`, or a second row with `is_entry = 1`, makes the matching check row report a non-zero count and print the offending `node_id`.
- **Reachability:** `TotalParking/Controllers/MonitorController.cs` serves the same `block` table in the same frame to both map surfaces.

## Receipt

- **Verification:** PASS
- **Command:** `mysql -h 127.0.0.1 -P 3306 -u root total_parking < TotalParking/Database/verify_lane_network.sql`
- **Exit:** 0
- **Base:** 2a57347b0bff0bb6c71e4001937988067cb5e7f1
- **Head:** 2a57347b0bff0bb6c71e4001937988067cb5e7f1
- **Run at:** 2026-09-19, MySQL 8.4.11, database `total_parking` on 127.0.0.1:3306. The
  root password was supplied through `MYSQL_PWD` so it stays out of this file.

Current output:

```text
kiem_tra	so_nut_lech_khung	ket_qua	chi_tiet
R1.1 nut trong khung 1594x1300	0	PASS	-
kiem_tra	so_nut_is_entry	ket_qua	chi_tiet
R1.2 dung mot nut is_entry	1	PASS	313@1052,1165
kiem_tra	so_block_thieu_nut_den	ket_qua	chi_tiet
R1.3 block co map_x deu co nut den	0	PASS	-
kiem_tra	so_tham_chieu_hong	ket_qua	chi_tiet
R1.3 nut den deu ton tai	0	PASS	-
kiem_tra	so_block_dung_nut_cong	ket_qua	chi_tiet
Nut cong khong kiem nut den	0	PASS	-
kiem_tra	so_canh_sai	ket_qua	chi_tiet
Canh vo huong luu mot lan	0	PASS	-
kiem_tra	so_nut	so_canh	so_block_co_nut_den	so_block_co_map_x
R1.4 tong so hang	313	341	112	112
```

### Defect found while building R3-01, fixed here

Block 112 sits at the ramp head, so the nearest lane node to it was the `is_entry`
node itself. A route to that block then held a single point, and C1 requires two
before anything is drawn, so the one block a driver can almost touch would have
rendered as an error string. Its arrival node was moved to node 306, which sits
29 px away on the aisle beside it, and no other block changed. The migration and
`verify_lane_network.sql` both gained a check that no block's arrival node carries
`is_entry`, so the same mistake cannot return silently:

```text
Nut cong khong kiem nut den	0	PASS	-
```

### R1.4 idempotence

`37_lane_network.sql` was applied twice in a row against the same database. Both
runs exited 0 and printed identical totals, so a second run changes no row count:

```text
run 1: so_nut 313 | so_canh 341 | so_block_co_nut_den 112   (exit 0)
run 2: so_nut 313 | so_canh 341 | so_block_co_nut_den 112   (exit 0)
```

### Negative path

Two rejections were observed against the live table, and `lane_node` still held
313 rows afterwards, so neither bad row was stored:

```text
INSERT INTO lane_node (node_id,x,y,is_entry) VALUES (9001,1300,500,0);
ERROR 3819 (HY000) at line 1: Check constraint 'ck_lane_node_frame' is violated.

INSERT INTO lane_node (node_id,x,y,is_entry) VALUES (9002,500,500,1);
ERROR 1062 (23000) at line 1: Duplicate entry '1' for key 'lane_node.uk_lane_node_entry'

SELECT COUNT(*) AS so_nut FROM lane_node;  -> 313
```

Because those constraints refuse the bad rows outright, the FAIL branch of
`verify_lane_network.sql` was exercised separately in a throwaway schema
(`tp_negtest`, created and dropped inside the same run) holding deliberately bad
data. Each check reported FAIL and named the offending ids rather than only
counting them:

```text
R1.1 nut trong khung 1594x1300	2	FAIL	2,3
R1.2 dung mot nut is_entry	2	FAIL	1@100,100 4@200,200
R1.3 block co map_x deu co nut den	2	FAIL	7,8
Nut cong khong kiem nut den	1	FAIL	10
Canh vo huong luu mot lan	3	FAIL	1-2 2-1 3-3
```

### Reachability

Read back from the stored rows, not from the generator, with a recursive walk
outward from the `is_entry` node:

```text
so_nut_toi_duoc            313      (of 313 nodes)
so_block_khong_toi_duoc    0    -   (of 112 blocks with map_x)
```

### The routing frame was re-cropped after this task first closed

The user found the map looked cut off on every side. It was: `plan_map.jpg` had been
cropped to the bounding box of the 112 block dots plus exactly 31 px, so the building
envelope, two zone labels and the bottom vertex all fell outside it.

`plan_map.jpg` was shown to be reproducible from the drawing before anything was
changed — page 1 rendered at zoom 2.0, then cropped at `(620, 230)` to `2731x2592`.
Re-rendering that exact window and differencing it against the committed file gave a
mean difference of 0.38 of 255 and no pixel differing by more than 40:

```text
render day du: 4768 x 3368
cat thu: (2731, 2592)  goc: (2731, 2592)
sai lech trung binh (0-255): 0.38
ty le pixel lech > 40: 0.000%
```

The first widened crop was chosen by eye, and the user was right that it still cut the
map. Measuring settled it. The building outline is the second-largest long dark
component on the page, and its bounding box is exact:

```text
net dam DAI (>=600px) tren trang 1:
  bbox x  145..4707  y   59..3313   (4562x3254)   <- khung ban ve
  bbox x  388..3833  y  245..3022   (3445x2777)   <- duong bao toa nha
```

Against that, the eyeballed crop `(340, 200) 3450x2840` left margins of 48 px left,
45 px top, 18 px bottom — and **minus 43 px on the right**, meaning it cut into the
building. The crop is now derived from the outline instead of guessed:

```text
cat: x 297..3925  y 155..3114  (3628x2959 px)
le (don vi khung): trai 40.0  phai 40.4  tren 39.5  duoi 40.4
```

Because the scale is unchanged, every coordinate moves by a constant:

```text
khung   1200 x 1139  ->  1594 x 1300
toa do  x += 142, y += 33   (112 block, 313 lane node, GateX/GateY)
```

`34_block_map_xy.sql`, `37_lane_network.sql`, `BlockMapRepository.ViewW/ViewH/GateX/GateY`,
and the two frame literals in `Routing.cshtml` were updated together, both migrations
re-applied, and all five verification scripts re-run.

### A second defect the re-crop exposed

`CREATE TABLE IF NOT EXISTS` only creates `ck_lane_node_frame` on a database that does
not yet have `lane_node`. On an existing database the old bounds stayed behind silently
while the migration text said something else — exactly the kind of drift the constraint
is there to prevent. The migration now drops and re-adds the constraint on every run,
before the inserts, so the bounds in the file are always the bounds in the database:

```text
CONSTRAINT_NAME: ck_lane_node_frame
   CHECK_CLAUSE: ((`x` >= 0) and (`x` <= 1594) and (`y` >= 0) and (`y` <= 1300))
```

Widening a frame validates cleanly against existing rows; narrowing one makes that
`ALTER` fail, which is the correct outcome.

### Limitations

- The lane geometry was hand-traced from `Images/plan_map.jpg` under the bounded
  reversal in D2, because aisles are open space rather than linework and page 1
  of the drawing carries over a million detail segments. The checks above prove
  the network is inside the frame, connected, and clear of every block footprint.
  They do not prove it matches site traffic direction, which only a person
  looking at the plan can confirm.
- **`block.map_x`/`map_y` were re-centred after this task closed.** They had held the
  position of the drawing's own number circle, which sits left of a block's centre; they
  now hold the centre of the block's filled shape. The stored arrival nodes were not
  recomputed, so the farthest block-centre-to-arrival-node distance rose from 71 to 74.7
  units. The change and its evidence are recorded in R4-01, which is where it was needed.
- `TotalParking/TotalParking.csproj` does not list the two new `.sql` files. The
  repository lists every other `Database\*.sql` as `<None Include>`, but the
  project file is not in this task's ownership table. For a `.sql` file the entry
  only affects Solution Explorer visibility, not build or publish output.

<!-- The table above is the only task anchor/ownership table. Access read requires Action read. Access write requires create, modify, or delete. Targets are exact; globs and parent-directory claims are invalid. A verifier must not repeat the subject's Acceptance criterion. Specs-only never creates or updates docs and never fabricates execution proof; record doc impact as a brief recommendation only. -->
