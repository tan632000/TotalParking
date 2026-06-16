# @ui_functional_upgrade
Feature: SCADA UI Functional Upgrade Validation
  As a SCADA Operator,
  I want the parking lot monitoring interface to accurately reflect hardware status, AI-VDS classification, alerts, and maintenance logs,
  So that I can effectively oversee and manage the puzzle parking system.

  Background:
    Given the SCADA system is online and connected to PLC, HMI, and AI-VDS modules
    And the user is logged in as "supervisor01"

  @happy_path @ai_vds_routing
  Scenario: Real-time vehicle classification and optimal routing
    When a vehicle with license plate "51G-12345" passes the entry camera
    And the AI-VDS measures its dimensions as "Height: 1.65m, Length: 4.85m"
    Then the "Routing" screen should display the new inbound vehicle row
    And the vehicle class should be identified as "SUV"
    And the system should propose "Block A-02" based on zone load-balancing logic
    And the suggested card type should be highlighted as "SUV-Card (Yellow)"
    And the transmission status for LED Display and PGS should show a green "Active" indicator

  @edge_case @oversized_vehicle
  Scenario: Handling oversized vehicle entry rejection
    When a vehicle with license plate "43A-56789" passes the entry camera
    And the AI-VDS measures its dimensions as "Height: 1.85m, Length: 5.20m"
    Then the "Routing" screen should flag the vehicle as "Oversized"
    And the status column should display "Rejected" in red
    And the operator notice banner should show "Oversized vehicle – Rejected entry"
    And the automatic card dispenser should be instructed to hold card release

  @error_state @plc_motor_failure
  Scenario: Device failure alarm trigger and UI propagation
    Given "Block A-04" is operating normally with green status on the "Floor Plan" screen
    When "Motor-Lift-01" of "Block A-04" encounters a critical current overload error (Code E-0401)
    Then the "Dashboard" alarm banner should blink red and display "3 Active Alerts: Motor-Lift-01 Overload - Block A04"
    And the "Floor Plan" block icon for "Block A-04" should turn red
    And the "Alarms" list should append a new row with timestamp, Severity "CRITICAL", Code "E-0401", and Status "Active"
    And a notification should be logged into the database

  @maintenance @predictive_maintenance
  Scenario: Wear limit threshold reached triggers auto-maintenance proposal
    Given the cycle limit for Lift Motors is configured as "6,000" cycles
    When "Block A-04" lift motor reaches "5,640" cycles (94% wear rate)
    Then the "Maintenance" screen should update the block's progress bar to orange (94%)
    And a new Work Order should be appended to the "Proposed Work Orders" table
    And its Priority should be set to "Urgent"
    And the expected maintenance date should be projected on the screen
