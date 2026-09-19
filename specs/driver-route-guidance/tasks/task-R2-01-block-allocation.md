# Task R2-01: Block allocation
**Status:** done

## Outcome

A Camera AI event that `ZoneRouter` routes to a zone now also carries a destination block. `vehicle_routing` gains the `block_no` column of C2, a new `BlockAllocator` picks the block on observed occupancy per D5 and D6, and `IngestController` writes block and outcome in the same save so no reader ever sees `ROUTED` without a destination.

## Scope

- **In scope:** the `vehicle_routing.block_no` column, its foreign key and check constraint; the `BlockAllocator` selection rule of D5, D6, and D9; the routing model and repository changes needed to carry and persist the block; the ingest call site; the companion verification script.
- **Out of scope:** the zone-selection rule in `ZoneRouter`, which stays byte-for-byte as it is; block-level weight or dimension fitness; the lane network; the path algorithm; the route endpoint; any view.

## Anchors and Ownership

| ID | Type | Target | Role | Access | Action |
|---|---|---|---|---|---|
| A-R2-01-01 | file | `TotalParking/Services/BlockAllocator.cs` | owner | write | create |
| A-R2-01-02 | file | `TotalParking/Database/38_routing_block.sql` | owner | write | create |
| A-R2-01-03 | file | `TotalParking/Database/verify_routing_block.sql` | proof | write | create |
| A-R2-01-04 | file | `TotalParking/Models/VehicleRouting.cs` | owner | write | modify |
| A-R2-01-05 | file | `TotalParking/Services/VehicleRoutingRepository.cs` | owner | write | modify |
| A-R2-01-06 | file | `TotalParking/Controllers/IngestController.cs` | owner | write | modify |

## Changes

- [x] Add `vehicle_routing.block_no` with its foreign key and the check constraint of C2 behind an `information_schema` guard, add `BlockNo` and `OccupancyVerified` to the routing model and repository paths, and implement `BlockAllocator` selecting the candidate in the routed zone with the greatest free capacity, breaking ties on the lower `block_no`, per D5. _Requirements: 2.1_
- [x] Rank candidates with a `plc_slot_state` row read inside 5 minutes ahead of candidates without one, and set the unverified-occupancy marker when only stale candidates remain, per D6. _Requirements: 2.2_
- [x] Call the allocator from `IngestController` only on outcome `ROUTED`, write the block in the same save as the outcome, and read the stored value back on every later read rather than recomputing it, per D7. _Requirements: 2.3_
- [x] Return outcome `NO_CAPACITY` with a null block when no candidate has free capacity above zero, and never emit `ROUTED` without a block. _Requirements: 2.4_
- [x] Debit each candidate's free capacity by the destination blocks already persisted for other events inside the D8 window per D9, and write `verify_routing_block.sql` checking zone agreement, null discipline per outcome, over-capacity destinations, freshness ranking, and stability of a re-read event. _Requirements: 2.5_

## Acceptance

- **R2.1:** every `vehicle_routing` row with `outcome = 'ROUTED'` has a non-null `block_no` whose `block.zone_id` equals the row's `zone_id`; the verification script reports a zero count of rows breaking either condition.
- **R2.2:** when a zone holds both a fresh and a stale candidate of equal free capacity, the persisted `block_no` is the fresh one; when every candidate is stale the row is still written and `occupancy_verified` reads false.
- **R2.3:** reading the same `event_id` twice returns the `block_no` written on the first save, and no code path recomputes the selection on read.
- **R2.4:** a routed zone whose every block is at `slot_count` yields a persisted row with `outcome = 'NO_CAPACITY'` and a null `block_no`; inserting `ROUTED` with a null `block_no` is refused by the check constraint.
- **R2.5:** with the best candidate holding exactly one free space and a destination for another event already persisted inside the D8 window, the next event is assigned a different `block_no`.

## Dependencies

- none

## Verification Plan

- **Verification ref:** V2
- **Task role:** subject
- **Command:** `mysql -h 127.0.0.1 -P 3306 -u root total_parking < TotalParking/Database/verify_routing_block.sql`
- **Expected:** zero `ROUTED` rows with a null or cross-zone `block_no`, zero non-`ROUTED` rows with a non-null `block_no`, zero `ROUTED` rows pointing at a block whose `v_slot_taken` count already equals its `slot_count`, and an unchanged `block_no` when an event is read a second time.
- **Negative path:** inserting a `ROUTED` row with a null `block_no`, or a `NO_CAPACITY` row with a non-null `block_no`, is refused by the check constraint and the script records the refusal as the expected result.
- **Reachability:** `TotalParking/Controllers/IngestController.cs` calls the allocator on the live `POST /vehicle` path.

## Receipt

- **Verification:** PASS
- **Command:** `mysql -h 127.0.0.1 -P 3306 -u root total_parking < TotalParking/Database/verify_routing_block.sql`
- **Exit:** 0
- **Base:** 2a57347b0bff0bb6c71e4001937988067cb5e7f1
- **Head:** 2a57347b0bff0bb6c71e4001937988067cb5e7f1
- **Run at:** 2026-09-19, MySQL 8.4.11, database `total_parking` on 127.0.0.1:3306. The
  root password was supplied through `MYSQL_PWD` so it stays out of this file.

Current output:

```text
kiem_tra	so_trigger	ket_qua	chi_tiet
Trigger con day du	2	PASS	tg_vehicle_routing_block_ins tg_vehicle_routing_block_upd
kiem_tra	so_dong_sai	ket_qua	chi_tiet
R2.1 ROUTED moi deu co block_no	0	PASS	-
kiem_tra	so_dong_lech_zone	ket_qua	chi_tiet
R2.1 block dich dung zone	0	PASS	-
kiem_tra	so_dong_sai	ket_qua	chi_tiet
R2.4 outcome khac ROUTED khong co block	0	PASS	-
kiem_tra	so_dong_tro_toi_block_day	ket_qua	chi_tiet
R2.1 block dich chua day	0	PASS	-
kiem_tra	so_zone_xep_sai	ket_qua	chi_tiet
D6 khong chon block cu khi con block moi	0	PASS	-
kiem_tra	su_kien	ket_qua	chi_tiet
R2.3 doc lai ra cung block	verify-r201-1789825903	PASS	lan 1 = 112, lan 2 = 112
kiem_tra	moc_trigger	lich_su_giu_nguyen	dong_co_block	tong_dong
Doi chieu	2026-09-19 20:48:55.58	533	1	573
```

### Build

`MSBuild.exe TotalParking/TotalParking.csproj -t:Build -p:Configuration=Debug` exited 0:

```text
TotalParking -> C:\Users\Admin\source\repos\TotalParking\TotalParking\bin\TotalParking.dll
```

### Reachability — the live ingest path, not a simulation

The site was started on IIS Express at `http://localhost:58349`, a real event was
posted to the loopback-only `POST /vehicle`, and the row it produced was read back
from the database. `IngestController` therefore reached `BlockAllocator`, and block
and outcome were written in one save:

```text
POST /vehicle {"event_id":"verify-r201-1789825903", ... ,"weight_kg":1100}
-> 200 {"status":"ok"}

          event_id: verify-r201-1789825903
        decided_at: 2026-09-19 20:51:43.871
           zone_id: 1
           outcome: ROUTED
          block_no: 112
occupancy_verified: 1
        block_zone: 1          <- matches the zone_id on the decision row
              kind: Mechanical
        slot_count: 5
      weight_class: 2200KG
```

The IIS Express process was stopped afterwards and port 58349 released.

### R2.5 — the pending-assignment debit

Run inside a transaction and rolled back, against the allocator's own query. In zone 1,
block 112 has `slot_count` 5. With no assignment inside the 90-second window it is the
top candidate; once five assignments land in that window it leaves the candidate list
entirely and the next event goes elsewhere:

```text
A. no assignment inside the window
   block_no  free_capacity  fresh_reads
   112       5              5
   103       3              3
   901       13             0

B. five assignments to block 112 inside the window
   block_no  free_capacity  fresh_reads
   103       3              3
   901       13             0
   98        10             0

C. after ROLLBACK: dong_co_block = 1   (only the live test event above)
```

Listings A and B also show D6 working on real data: block 901 has 13 free spaces
against block 103's 3, yet 103 ranks first because the PLC has reported it inside five
minutes and has not reported 901.

### R2.2 — a zone where every candidate is stale

Zone 5 currently has 0 of 17 blocks reported by the PLC inside five minutes. The
allocator still returns a block, with the marker off rather than suppressing it:

```text
block_no  free_capacity  fresh_reads  occupancy_verified_se_ghi
905       11             0            false
```

### Negative path — the trigger refuses both directions

```text
INSERT ... outcome='ROUTED', block_no = NULL
ERROR 1644 (45000): vehicle_routing: outcome ROUTED bat buoc phai co block_no

UPDATE ... SET outcome='NO_CAPACITY'   (row still holds block_no 112)
ERROR 1644 (45000): vehicle_routing: outcome khac ROUTED bat buoc block_no phai NULL

SELECT COUNT(*) FROM vehicle_routing;  -> 573   (unchanged; neither write landed)
```

Reproducing the constraint failure that forced this mechanism:

```text
ALTER TABLE vr ADD CONSTRAINT ck_vr CHECK (...);
ERROR 3819 (HY000) at line 5: Check constraint 'ck_vr' is violated.
```

### Idempotence

`38_routing_block.sql` was applied twice. Both runs exited 0 and printed the same
totals, and the 533 historical rows were left alone:

```text
run 1: so_cot_moi 2 | so_trigger 2 | so_dong_lich_su_giu_nguyen 533   (exit 0)
run 2: so_cot_moi 2 | so_trigger 2 | so_dong_lich_su_giu_nguyen 533   (exit 0)
```

### Deviations from the written contracts

- **C2 names a check constraint; this uses a trigger.** The table already held 533
  `ROUTED` rows with a null `block_no`, written before the column existed, and MySQL
  validates a new `CHECK` against every existing row, so the `ALTER` fails outright
  (reproduced above). C2 also requires that existing rows keep their null `block_no`.
  Both cannot hold. The user chose the trigger, which enforces the behaviour R2.4 asks
  for on every new write while leaving history untouched.
- **`occupancy_verified` is a second new column.** C2's shape names only `block_no`,
  but R2.2 requires the marker to be readable from the row and C1 carries it in the
  response. It is stored with the decision rather than recomputed on read, so the label
  cannot flip while a driver is looking at the screen.
- **`36_routing_block.sql` was renumbered to `38_routing_block.sql`**, because
  `36_clear_stale_slot_cache.sql` already holds that number. Same decision the user made
  for the lane-network migration.

### Limitations

- **D9's "not yet observed parked" is not evaluated.** No column links a camera event to
  a parked slot, the same gap `08_vehicle_routing.sql` records for sessions. The debit
  counts every `ROUTED` row for that block inside the 90-second window, so a vehicle that
  both parked and is still inside the window is counted twice, once in `v_slot_taken` and
  once in the debit. The error is always in the safe direction: it over-debits, so it
  never sends two cars to one space.
- **The `occupancy_verified = false` write was proven at query level, not live.** The
  zone-5 listing above is the allocator's own SQL, but no posted event reached zone 5:
  `ZoneRouter` picks by `gate_rank`, and steering it to zone 5 would mean changing zone
  data. The C# that maps `fresh_reads > 0` onto the flag is one expression, and the live
  run exercised its true branch.
- **Blocks 901 to 906 have no map position.** They are `kind = 'Ground'`, one per zone,
  80 slots in total, and carry neither `map_x` nor `lane_node_id`. D5 selects on occupancy
  only, so a `THUONG` vehicle can be sent to one, and the driver view will then have no
  path to draw. That is the case the design's error table already assigns to the route
  service, so it belongs to R3-01 rather than being a defect here.
- **`TotalParking.csproj` gained one line**, `<Compile Include="Services\BlockAllocator.cs" />`.
  The project file is not in this task's ownership table, but a `.cs` file absent from it
  is not compiled at all, so the owned artifact would not exist without it. The four new
  `.sql` files are still not listed as `<None Include>`.

<!-- The table above is the only task anchor/ownership table. Access read requires Action read. Access write requires create, modify, or delete. Targets are exact; globs and parent-directory claims are invalid. A verifier must not repeat the subject's Acceptance criterion. Specs-only never creates or updates docs and never fabricates execution proof; record doc impact as a brief recommendation only. -->
