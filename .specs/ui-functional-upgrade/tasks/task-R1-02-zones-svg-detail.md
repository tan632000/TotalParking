# Task R1-02: Cấu hình bản đồ tương tác SVG 4 Zone lớn và Modal Pallet Grid

**Requirement:** R2 & R3 — Bản đồ chi tiết các Zone và hiển thị trạng thái khay Pallet
**Status:** pending
**Priority:** High
**Estimated Effort:** 2h
**Dependencies:** tasks/task-R1-01-floorplan-four-zones.md
**Spec:** .specs/ui-functional-upgrade/

## Objective
Đồng bộ hóa bản đồ SVG đa giác trong [Zones.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Zones.cshtml) tương thích với 4 phân khu chính (Zone A, B, C, D) và cập nhật lưới Pallet trong Modal ở [ZoneDetail.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/ZoneDetail.cshtml).

## Constraints
- **MUST**: Đảm bảo lớp phủ SVG polygons trong Zones.cshtml bao phủ chính xác các vùng phân khu trên ảnh nền [zones_map.jpeg](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Images/zones_map.jpeg).

## Implementation Steps
- [ ] 1. Sửa đổi [Zones.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Zones.cshtml)
  - [ ] 1.1 Điều chỉnh lại các liên kết đa giác SVG để trỏ tới 4 Phân khu chính (Zone A, B, C, D) tương ứng ID 1, 2, 3, 4. _Requirements: 2.1_
- [ ] 2. Sửa đổi [ZoneDetail.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/ZoneDetail.cshtml)
  - [ ] 2.1 Cập nhật case switch-case cho `zoneId` để xử lý chính xác 4 Phân khu chính. _Requirements: 3.1_
  - [ ] 2.2 Sửa hàm `openBlockModal` để khởi tạo lưới pallet mô phỏng đầy đủ trạng thái thực tế: Có xe (với biển số, loại xe, giờ vào), khay trống, khay lỗi, khay khóa. _Requirements: 3.2_

## Related Files
| Path | Action | Description |
|---|---|---|
| `TotalParking/Views/Home/Zones.cshtml` | Modify | Điều chỉnh SVG đa giác |
| `TotalParking/Views/Home/ZoneDetail.cshtml` | Modify | Cập nhật logic switch zone và modal hiển thị Pallet Grid |

## Completion Criteria
- [ ] Rê chuột trên bản đồ phân khu Zones hiển thị nổi bật 4 Zone đỗ xe.
- [ ] Click mở modal chi tiết block hiển thị sơ đồ khay Pallet với đầy đủ thông tin phương tiện.

## Verification & Evidence
- [ ] Artifact / runtime verification
  - Inspect: Truy cập `/Home/Zones` và kiểm tra hover, click mở block modal tại `/Home/Zones?id=1`.
  - Expect: Hiển thị đúng grid pallet và thông tin xe đỗ.
