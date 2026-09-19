# Task R2-01: Block allocation
**Status:** pending

## Outcome

A Camera AI event that `ZoneRouter` routes to a zone now also carries a destination block. `vehicle_routing` gains the `block_no` column of C2, a new `BlockAllocator` picks the block on observed occupancy per D5 and D6, and `IngestController` writes block and outcome in the same save so no reader ever sees `ROUTED` without a destination.

## Scope

- **In scope:** the `vehicle_routing.block_no` column, its foreign key and check constraint; the `BlockAllocator` selection rule of D5, D6, and D9; the routing model and repository changes needed to carry and persist the block; the ingest call site; the companion verification script.
- **Out of scope:** the zone-selection rule in `ZoneRouter`, which stays byte-for-byte as it is; block-level weight or dimension fitness; the lane network; the path algorithm; the route endpoint; any view.

## Anchors and Ownership

| ID | Type | Target | Role | Access | Action |
|---|---|---|---|---|---|
| A-R2-01-01 | file | `TotalParking/Services/BlockAllocator.cs` | owner | write | create |
| A-R2-01-02 | file | `TotalParking/Database/36_routing_block.sql` | owner | write | create |
| A-R2-01-03 | file | `TotalParking/Database/verify_routing_block.sql` | proof | write | create |
| A-R2-01-04 | file | `TotalParking/Models/VehicleRouting.cs` | owner | write | modify |
| A-R2-01-05 | file | `TotalParking/Services/VehicleRoutingRepository.cs` | owner | write | modify |
| A-R2-01-06 | file | `TotalParking/Controllers/IngestController.cs` | owner | write | modify |

## Changes

- [ ] Add `vehicle_routing.block_no` with its foreign key and the check constraint of C2 behind an `information_schema` guard, add `BlockNo` and `OccupancyVerified` to the routing model and repository paths, and implement `BlockAllocator` selecting the candidate in the routed zone with the greatest free capacity, breaking ties on the lower `block_no`, per D5. _Requirements: 2.1_
- [ ] Rank candidates with a `plc_slot_state` row read inside 5 minutes ahead of candidates without one, and set the unverified-occupancy marker when only stale candidates remain, per D6. _Requirements: 2.2_
- [ ] Call the allocator from `IngestController` only on outcome `ROUTED`, write the block in the same save as the outcome, and read the stored value back on every later read rather than recomputing it, per D7. _Requirements: 2.3_
- [ ] Return outcome `NO_CAPACITY` with a null block when no candidate has free capacity above zero, and never emit `ROUTED` without a block. _Requirements: 2.4_
- [ ] Debit each candidate's free capacity by the destination blocks already persisted for other events inside the D8 window per D9, and write `verify_routing_block.sql` checking zone agreement, null discipline per outcome, over-capacity destinations, freshness ranking, and stability of a re-read event. _Requirements: 2.5_

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

<!-- The table above is the only task anchor/ownership table. Access read requires Action read. Access write requires create, modify, or delete. Targets are exact; globs and parent-directory claims are invalid. A verifier must not repeat the subject's Acceptance criterion. Specs-only never creates or updates docs and never fabricates execution proof; record doc impact as a brief recommendation only. -->
