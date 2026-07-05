Feature: Operation Control Center (OCC) Live Command and Monitoring Dashboard

  As a SCADA Operator
  I want a centralized Operation Control Center page
  So that I can securely manage blocks, dispatch vehicles, send PLC commands, and perform fault recoveries.

  Background:
    Given the SCADA operator session is active
    And the database is loaded with mock block states and active alarm logs

  Scenario Outline: Dynamic Block Filtering by Zone
    When the operator selects Zone "<ZoneId>" from the zone selector
    Then the block selector dropdown should display the corresponding blocks "<BlockList>"
    And the OCC summary grid should update to show block cards for "<BlockList>" only

    Examples:
      | ZoneId | BlockList                                 |
      | 1      | Block A-01, Block A-02, Block A-03        |
      | 2      | Block A-04, Block B-01, Block B-02        |
      | 6      | Block E-01, Block E-02, Block E-03        |

  Scenario: Switching Block Operation Mode
    Given the operator selects "Block A-01"
    When the operator clicks the mode selector button "Manual"
    Then the system should send the mode transition command to the simulated PLC
    And the block mode indicator on the OCC panel should display "Manual"
    And the manual vehicle dispatch form should become active

  Scenario: Lock/Unlock or Disable Block for Maintenance
    Given the operator selects "Block A-01"
    And the block mode is set to "Maintenance"
    When the operator clicks the action button "Disable Block"
    Then the system should change the block operational status to "Disabled"
    And all vehicle dispatch commands and operational buttons for "Block A-01" should be deactivated

  Scenario: Direct PLC Command Execution with Confirmation
    Given the operator selects "Block A-01"
    And inputs the raw PLC command "CMD_ROTATE_P03"
    When the operator clicks the "Send Command" button
    Then the system shall display a security confirmation modal asking "Bạn có chắc chắn muốn gửi trực tiếp lệnh CMD_ROTATE_P03?"
    When the operator clicks "Confirm" in the modal
    Then the system shall close the modal and start the PLC command pipeline status loop

  Scenario: Direct PLC Command Pipeline Indicators Success Flow
    Given a PLC command pipeline is initialized for "Block A-01"
    When the command is sent to the PLC
    Then the status indicator LED "Command Sent" shall turn green
    And after 250ms, the status indicator LED "PLC Accepted" shall turn green
    And after 500ms, the status indicator LED "Running" shall turn green and display a progress bar
    And upon reaching 100% progress, the status indicator LED "Done" shall turn green
    And the command input shall be unlocked for further entry

  Scenario: Direct PLC Command Failure and Error Code Display
    Given the operator sends an invalid command "CMD_INVALID" to the PLC
    And confirms the execution modal
    When the simulated PLC rejects the command
    Then the status indicator LED "Failed" shall turn red
    And the system shall display the error code "ERR_PLC_CMD_REJECT_0x0A" on the OCC control panel

  Scenario: Guided Fault Recovery Checklist Sequence
    Given "Block B-04" status is set to "Error" with active alarm "ERR-MTR-04"
    When the operator selects "Block B-04" on the OCC panel
    Then the system shall display the red fault recovery warning panel
    And the operational controls (Start/Pause/Resume) shall be locked
    When the operator clicks "Bắt đầu phục hồi sau lỗi"
    Then the system shall render the guided recovery checklist with steps:
      | Step 1: Xác nhận không có vật cản cơ cấu nâng  |
      | Step 2: Đóng chốt khóa an toàn bàn nâng        |
      | Step 3: Thực hiện Reset PLC Fault & Home Block |
    And the "Reset PLC Fault & Home Block" button shall remain deactivated
    When the operator checks Step 1 and Step 2
    Then the "Reset PLC Fault & Home Block" button shall be enabled
    When the operator clicks the reset button
    Then the system shall send reset and homing commands to the PLC
    And the status of "Block B-04" shall be updated to "Normal"
    And the active alarm "ERR-MTR-04" shall be removed from the alarms list
    And the fault recovery panel shall be hidden

  Scenario: Manual Vehicle Dispatch - Store Command
    Given the operator selects "Block A-01" and sets mode to "Manual"
    And there is an available empty pallet P02 in the block
    When the operator inputs vehicle plate "30K-88888" and clicks "Gửi lệnh Gọi xe vào"
    Then the system shall send storing command to the PLC
    And the command status pipeline LEDs shall display the progress
    And upon completion, pallet P02 shall be updated to "Occupied" with vehicle "30K-88888"
