# Tài liệu Đặc tả Yêu cầu - Operation Control Center (OCC)

## Giới thiệu
Tài liệu này xác định các yêu cầu kỹ thuật và nghiệp vụ cho trang Trung tâm Điều khiển Vận hành (OCC - Operation Control Center) mới của hệ thống TotalParking SCADA. Trang này cung cấp cho nhân viên vận hành (operator) một giao diện điều khiển tập trung để quản lý toàn bộ các khối block đỗ xe cơ khí, gửi lệnh trực tiếp xuống PLC, giám sát quy trình chạy và phục hồi lỗi có hướng dẫn chi tiết.

## Requirements

### Requirement 1: Giao diện Trung tâm Điều khiển Vận hành (OCC UI Layout)
**Objective:** As an operator, I want a dedicated centralized SCADA layout page for OCC, so that I can monitor and control all blocks in one place.

#### Acceptance Criteria
1. The OCC UI shall display a sidebar navigation link titled "Điều khiển vận hành" in the master SCADA layout.
2. When operator visits the OCC page, the OCC UI shall render a summary dashboard of all 6 zones and their active blocks.
3. The OCC UI shall provide a block selection dropdown containing blocks mapped dynamically from the selected Zone (Zone 1 to Zone 6).
4. While a block is selected, the OCC UI shall render its control panel containing block status indicators, mode selector, action buttons, command logs, and dispatch forms.

### Requirement 2: Bảng Điều khiển Block & Chuyển chế độ Vận hành (Block Controls & Mode Selector)
**Objective:** As an operator, I want to send operational state commands (Start, Stop, Pause, Resume, Lock, Unlock, Disable) and switch modes, so that I can manage block operations dynamically.

#### Acceptance Criteria
1. When operator clicks Start, Stop, Pause, or Resume button, the OCC UI shall send the corresponding operational state command to the simulated PLC.
2. When operator clicks Lock, Unlock, or Disable block button, the OCC UI shall send the corresponding block maintenance state command.
3. When operator selects a mode from Auto, Manual, Maintenance, or Emergency, the OCC UI shall send the mode change command to the simulated PLC.
4. While the block state is set to "Disabled", the OCC UI shall block all Start, Pause, Resume, and vehicle dispatch commands for that block.

### Requirement 3: Phục hồi sau lỗi & Checklist hướng dẫn động (Fault Reset & Guided Recovery Checklist)
**Objective:** As an operator, I want to perform fault resets, home mechanism synchronization, and follow an interactive step-by-step checklist, so that I can recover blocks after fault states safely.

#### Acceptance Criteria
1. When a block status is "Error", the OCC UI shall display a red warning panel showing the active error code and a "Bắt đầu phục hồi sau lỗi (Start Recovery)" button.
2. When operator clicks "Bắt đầu phục hồi sau lỗi", the OCC UI shall render an interactive checklist showing guided recovery steps tailored to the active error code.
3. While the guided recovery checklist is active, the OCC UI shall require the operator to check each step sequentially before enabling the "Reset PLC Fault" and "Home Mechanism" buttons.
4. When operator completes the checklist and clicks "Reset PLC Fault" and "Home Mechanism", the OCC UI shall send the reset and homing commands, restore the block state to "Normal", and hide the recovery panel.

### Requirement 4: Bộ gửi lệnh PLC trực tiếp & Pipeline giám sát (Direct PLC Command Generator & Pipeline)
**Objective:** As an operator, I want to send raw PLC commands directly with confirmation gates and monitor the execution pipeline, so that I can debug and verify PLC responses.

#### Acceptance Criteria
1. When operator inputs a raw PLC command and clicks "Send", the OCC UI shall display a security confirmation modal asking the operator to confirm the execution.
2. When operator confirms the execution, the OCC UI shall initiate the command pipeline and render a step-by-step progress chain: `Command Sent` -> `PLC Accepted` -> `Running` -> `Done/Failed` using visual indicator LEDs.
3. While the command pipeline is in `Running` state, the OCC UI shall disable the command generator input to prevent overlapping commands.
4. If the PLC rejects the command (simulated), then the OCC UI shall set the pipeline status to `Failed` and display the specific PLC rejection error code (e.g. `ERR_PLC_REJECT_0x0A`).

### Requirement 5: Điều phối xe - Gọi xe và Trả xe (Operator Vehicle Dispatch)
**Objective:** As an operator, I want to manually call a vehicle into a block or dispatch a vehicle out, so that I can override automatic gate operations in manual mode.

#### Acceptance Criteria
1. When operator selects manual mode, the OCC UI shall enable the vehicle dispatch form containing "Gọi xe vào" (Store) and "Trả xe ra" (Retrieve) panels.
2. When operator inputs a plate number or swipe card and clicks "Gửi lệnh Gọi xe vào", the OCC UI shall dispatch the vehicle to the first available empty pallet of the selected block.
3. When operator selects an occupied pallet and clicks "Gửi lệnh Trả xe ra", the OCC UI shall dispatch the vehicle and release the pallet.
4. While a vehicle dispatch command is running, the OCC UI shall render the command status pipeline and update the block layout occupancy dynamically upon completion.

### Requirement 9: Sequence View – Luồng xử lý lệnh từng bước (Step-by-step Command Sequence View)
**Objective:** As an operator, I want to monitor the command execution process step-by-step in a sequential view, so that I know exactly which step the command is currently executing or where it gets stuck.

#### Acceptance Criteria
9.1 The OCC UI shall implement a Sequence View panel under a new Tab in the left container (Tab A: Block Layout Grid, Tab B: Sequence View).
9.2 The Sequence View shall display step indicators for the active command.
9.3 The steps shall change colors dynamically based on execution state:
    - Completed steps: green.
    - Active running step: flashing yellow/amber.
    - Blocked/Failed step: red with specific failure reason.
    - Pending steps: gray.
9.4 The Sequence View shall display elapsed time and estimated time to complete.
9.5 The OCC UI shall support two main sequences: Storing (Gọi xe vào) and Retrieving (Lấy xe ra) with the corresponding steps:
    - Storing steps: `Xác nhận thẻ / biển số xe` -> `Kiểm tra cảm biến an toàn & chiều cao` -> `Xác định vị trí khay đỗ trống` -> `Kiểm tra Safety Interlock` -> `Gửi lệnh di chuyển đến PLC` -> `Bàn nâng di chuyển khay đến vị trí nhận` -> `Khách hàng đưa xe vào khay` -> `Đưa xe vào vị trí đỗ an toàn` -> `Hoàn tất`.
    - Retrieving steps: `Xác nhận thẻ / biển số xe` -> `Xác định vị trí xe trong hệ thống` -> `Chọn zone và block phù hợp` -> `Kiểm tra Safety Interlock tổng thể` -> `Gửi lệnh đến PLC` -> `Pallet di chuyển đến vị trí xe` -> `Xe được đưa về vị trí xuất` -> `Hoàn tất – Thông báo khách hàng`.
9.6 The OCC UI shall provide a "Kích hoạt Kẹt lệnh" (Simulate Stuck Command) checkbox in the Dispatch Panel. When checked, sending a Store or Retrieve command shall cause the Sequence View to fail at a specific intermediate step (e.g. `Pallet di chuyển` or `Bàn nâng di chuyển`), turning that step red and displaying a specific mechanical failure reason.

---

## Non-Functional Requirements

### Requirement 6: Performance & Scalability
**Objective:** As a system owner, I want the OCC dashboard to perform efficiently, so that operators can react to SCADA alarms without delay.

#### Acceptance Criteria
6.1 The OCC UI shall render block layout updates and command status pipeline updates within 100ms on the client side.
6.2 The OCC UI shall support continuous rendering of simulated PLC data updates without blocking browser thread execution.

### Requirement 7: Security & Privacy
**Objective:** As a security stakeholder, I want to restrict raw PLC commands, so that unauthorized personnel cannot trigger mechanical movements.

#### Acceptance Criteria
7.1 If the current user session role is not "Operator" or "Administrator", then the OCC UI shall hide all direct PLC command inputs and block action buttons.
7.2 If a raw PLC command is sent, the OCC UI shall log the action, timestamp, and user identifier to the local SCADA Audit Log.

### Requirement 8: Reliability & Availability
**Objective:** As an operator, I want fault recovery checklists to persist across browser reloads, so that I do not lose progress during a recovery session.

#### Acceptance Criteria
8.1 The OCC UI shall save the active guided recovery checklist state to `localStorage` on every checklist update.
8.2 When the OCC UI initializes, the OCC UI shall restore any in-progress guided recovery checklist from `localStorage` if the block remains in an error state.
