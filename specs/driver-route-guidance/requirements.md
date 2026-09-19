# Requirements

## Outcomes

- A driver stopped at the barrier sees, on a fixed TV screen, the parking floor plan with a drawn path running from the ramp head to the one block they should drive to.
- The block drawn on the screen is the block the server decided and stored against the Camera AI event, so the screen, the operator page, and the database never disagree about the destination.
- The drawn path follows the digitised drivable lane network instead of a straight or axis-aligned line, so the path never crosses through parked blocks.
- When the system cannot produce a destination, the screen states that plainly and draws no path at all.

## Scope

### In scope

- A lane network (nodes and edges) persisted in the same map frame as `block.map_x`/`block.map_y`, with the ramp head as a real node.
- Server-side shortest-path computation from the ramp head node to a block's arrival node.
- Server-side selection of one destination block inside the zone `ZoneRouter` already chose, persisted on `vehicle_routing`.
- A new driver-facing full-screen view plus the read-only endpoint that feeds it.
- Removing the client-side block-selection logic in `TotalParking/Views/Home/Routing.cshtml` so it renders the server's decision.
- Registering the new view in `TotalParking/TotalParking.csproj` and in the sidebar active-state expression in `TotalParking/Views/Shared/_ScadaLayout.cshtml`.

### Non-goals

- Changing the zone-selection rule in `TotalParking/Services/ZoneRouter.cs`; zone choice and weight-class filtering stay exactly as they are.
- Block-level weight or dimension fitness. `block.column_count` is `NULL` for all 112 rows and `docs/block-capability-README.md` states the field survey behind it has not been collected, so block choice is made on occupancy only.
- Destination granularity below a block. `parking_slot` has no generated rows and `VehicleLocation.SlotLabel` is documented as always `NULL`.
- LED panel frames, PLC register contracts, HMI flow, and vehicle classification rules.
- Server push. The existing browser polling pattern in `Routing.cshtml` is kept; no SignalR or SSE is introduced.
- Authentication or authorisation for the driver view. The project has no authentication of any kind.
- Regenerating `TotalParking/Images/plan_map.jpg` or recomputing `block.map_x`/`block.map_y`.

## Requirements

### Requirement 1: Lane network and ramp origin

**Objective:** As the routing service, I want a persisted drivable lane network anchored to the routing map frame, so that a path can be computed instead of approximated.

#### Acceptance Criteria

- **R1.1** When the lane-network migration runs, the system shall persist every lane node with coordinates inside the routing map frame, where the frame bounds are `0 <= x <= 1200` and `0 <= y <= 1139` as declared by `BlockMapRepository.ViewW` and `BlockMapRepository.ViewH`, and shall reject the migration when any node falls outside those bounds.
- **R1.2** The system shall persist exactly one lane node marked as the ramp entry origin, and shall fail the migration when zero or more than one node carries that mark.
- **R1.3** When the migration runs, the system shall attach every `block` row having `map_x IS NOT NULL` to exactly one lane node as that block's arrival node, and shall list the offending `block_no` values and fail when any such block has zero or more than one arrival node.
- **R1.4** When the migration is executed a second time against an already-migrated database, the system shall produce the identical node, edge, and arrival-node rows without raising a duplicate-key error.

### Requirement 2: Server-owned destination block

**Objective:** As the system of record, I want the destination block chosen and stored server-side, so that every surface shows the same destination and it does not change while a driver is reading it.

#### Acceptance Criteria

- **R2.1** When `ZoneRouter` returns outcome `ROUTED` for a Camera AI event, the system shall select exactly one block whose `zone_id` equals the routed zone and whose observed occupancy is below its `slot_count`.
- **R2.2** When at least one candidate block has a `plc_slot_state` row read within the last 5 minutes, the system shall select only from those blocks; when no candidate has such a row, the system shall still select a block and shall mark the returned decision as unverified occupancy.
- **R2.3** When a destination block has been persisted for an event, the system shall return that stored block on every later read of that event and shall not recompute the selection.
- **R2.4** If no block in the routed zone has occupancy below its `slot_count`, then the system shall persist outcome `NO_CAPACITY` with a null block and shall not persist outcome `ROUTED`.
- **R2.5** When selecting a block, the system shall subtract from each candidate's free count the number of destination blocks already persisted for other events inside the display freshness window, so that two vehicles arriving within that window are not both sent to a block that only has room for one.

### Requirement 3: Route computation and route contract

**Objective:** As the driver view, I want an ordered list of map points from the ramp to a block, so that I can draw a path without re-deriving any routing rule in the browser.

#### Acceptance Criteria

- **R3.1** When a route to a block is requested, the system shall return a minimum-total-cost node sequence from the ramp origin node to that block's arrival node, where an edge's cost is the Euclidean distance between its endpoints in the routing map frame, and shall return the same sequence on every repeated request while the lane network is unchanged.
- **R3.2** The system shall return the route as an ordered array of point objects, each carrying an `x` and a `y` field in the routing map frame, whose first element holds the ramp origin node coordinates and whose last element holds the requested block's arrival node coordinates.
- **R3.3** If no sequence of edges connects the ramp origin node to the requested block's arrival node, then the system shall return an explicit no-route result naming the unreachable `block_no`, and shall not return a straight-line, axis-aligned, or partial point array.

### Requirement 4: Driver guidance screen

**Objective:** As a driver stopped at the barrier, I want the screen to show me where to drive, so that I do not have to ask an attendant.

#### Acceptance Criteria

- **R4.1** When a routing decision with a destination block is current, the driver view shall render the floor-plan image at the routing map frame, the returned route as a single drawn path, and the destination block number as a text label.
- **R4.2** When a new routing decision is persisted, the driver view shall display that decision's route within 3 seconds of it becoming readable, and shall display the most recently decided `ROUTED` event when several are inside the freshness window.
- **R4.3** When no routing decision falls inside the freshness window, the driver view shall display a waiting state and shall remove any previously drawn path rather than leaving the last route on screen.
- **R4.4** When the current decision's outcome is `REJECTED`, `NO_CAPACITY`, `NO_DATA`, or `MANUAL`, the driver view shall display that outcome's reason text and shall draw no path and no destination label.
- **R4.5** The driver view file shall be listed as a `<Content Include>` item in `TotalParking/TotalParking.csproj`, shall set `Layout = "~/Views/Shared/_ScadaLayout.cshtml"`, and its action name shall be present in the sidebar active-state expression in `TotalParking/Views/Shared/_ScadaLayout.cshtml`.

### Requirement 5: Operator page consistency

**Objective:** As an operator, I want the Điều hướng xe page to show the same destination the system recorded, so that the page never teaches me a behaviour the system does not have.

#### Acceptance Criteria

- **R5.1** The map on `TotalParking/Views/Home/Routing.cshtml` shall render the destination block supplied by the server, and the view shall contain no client-side candidate filtering, sorting, or selection of blocks.
- **R5.2** When the operator runs a weight-class simulation, the system shall choose the previewed destination block through the same server-side selection used for real Camera AI events, and shall write no `vehicle_event`, `vehicle_profile`, or `vehicle_routing` row while doing so.

## Unresolved Questions

- None.
