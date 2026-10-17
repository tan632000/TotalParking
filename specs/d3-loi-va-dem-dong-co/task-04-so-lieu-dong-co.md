# Task 04 — Số lượt chạy trên trang Bảo trì khớp motor_run

Status: pending

## Outcome
Khi đã có lượt chạy thật trong `motor_run`, số lượt chạy và lịch sử hiện trên trang Bảo trì đúng bằng dữ liệu trong DB.

## Scope
- In: thêm ca `api` vào `tools/kiem_chung_bao_tri.ps1` (tham số `-ChiDongCo`) so `/BaoTri/DongCo` với `motor_run`; sửa `BaoTriController`/`MotorRunRepository` nếu ca đó lộ ra sai.
- Out: thay đổi bố cục trang (task 03).

## Coverage
- CP-03

## Ownership
- Modify: `tools/kiem_chung_bao_tri.ps1`
- Modify (chỉ khi probe lộ sai): `TotalParking/Controllers/BaoTriController.cs`, `TotalParking/Services/MotorRunRepository.cs`

## Acceptance
- AC-10: với một khối có `motor_run`, `tong` và `hom_nay` của từng động cơ trên `/BaoTri/DongCo` bằng `COUNT(*)` tương ứng trong DB (giới hạn `ended_at <= T`, T chụp trước khi gọi API); `lich_su` ≤ 50 dòng, mới nhất trước.

## Dependencies
- task-02-dem-dong-co.md
- task-03-hien-thi-dong-co.md

## Verification Plan
- Command: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_bao_tri.ps1 -ChiDongCo`
- Named probe: ca `api` — chọn khối có `motor_run` gần nhất (không có → thoát ≠ 0 với `NO_DATA`); chụp `T = MAX(ended_at)` của khối; so `tong`/`hom_nay` từng động cơ với DB giới hạn `ended_at <= T`, chấp nhận API lớn hơn đúng bằng số dòng `ended_at > T`; `lich_su` ≤ 50, mới nhất trước.
- Reachability: bản deploy `http://localhost:8080`; ≥ 1 dòng `motor_run` thật (task 02).
- Oracle: PASS/FAIL; thoát 0 khi PASS.
- Counterexample: đếm lệch DB, sai `hom_nay`, hay quá 50 dòng đều FAIL.
- Artifacts: ephemeral.

## Receipt
