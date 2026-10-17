# Task 05 — Thời gian hoạt động theo loại động cơ trên trang Bảo trì

Status: done

## Outcome
Khu "Động cơ" của trang Bảo trì hiện, cho từng động cơ và cho từng loại (trượt, nâng, hàng rào), số lần hoạt động và thời gian hoạt động = số lần × thời gian chuẩn của loại. Nam châm chỉ hiện số lần.

## Scope
- In:
  - Quyết định người dùng (16/10/2026): một lần hoạt động = bit động cơ 0→1 (khi có nguồn + thuận/nghịch) rồi 1→0 — đúng quy tắc `DongCoDetector` đang chạy, KHÔNG đổi việc nhận lượt chạy. Thời gian hoạt động KHÔNG lấy `duration_ms` đo được mà lấy số lần × thời gian chuẩn:

    | Loại | Mã động cơ | Thời gian chuẩn |
    |---|---|---|
    | Trượt (`TRUOT`) | 102, 103, 112, 113 | 20 giây |
    | Nâng (`NANG`) | 201, 202, 203, 211, 212, 213 | 24 giây |
    | Hàng rào (`HANG_RAO`) | RAO_CHAN | 12 giây |
    | Nam châm (`NAM_CHAM`) | NAM_CHAM | không có — chỉ đếm số lần |

  - `DongCoDetector`: thêm phân loại mã → loại và thời gian chuẩn (hằng số trong code, cạnh bảng bit; mã ngoài bảng trên = không có loại, không có thời gian).
  - `/BaoTri/DongCo`: mỗi phần tử `tong_hop` thêm `loai`, `thoi_gian_chuan_giay` (null với nam châm), `thoi_gian_hom_nay_giay`, `thoi_gian_tong_giay` (= số lần × chuẩn; null với nam châm). Thêm `theo_loai`: một dòng cho mỗi loại có mặt trong khối, theo thứ tự trượt, nâng, hàng rào, nam châm, với `so_lan_hom_nay`, `so_lan_tong`, `thoi_gian_hom_nay_giay`, `thoi_gian_tong_giay` (null với nam châm). Các trường cũ giữ nguyên.
  - `Maintenance.cshtml`: bảng động cơ có cột Loại, Hôm nay (lần · thời gian), Tổng (lần · thời gian), Lần cuối; thêm dòng tổng theo loại ở trên bảng. Thời gian hiện dạng "X giờ Y phút" / "Y phút Z giây". Danh sách 50 lượt gần nhất hiện mã, loại, giờ bắt đầu — bỏ cột thời lượng đo được để không lẫn với thời gian chuẩn.
- Out: đổi quy tắc nhận lượt chạy; cấu hình thời gian chuẩn qua Web.config; đổi `motor_run`.

## Coverage
- CP-03

## Ownership
- Modify: `TotalParking/Services/Plc/DongCoDetector.cs` (chỉ thêm phân loại), `TotalParking/Controllers/BaoTriController.cs`, `TotalParking/Views/Home/Maintenance.cshtml`, `tools/kiem_chung_bao_tri.ps1`

## Acceptance
- AC-11: với mọi khối cơ khí, `/BaoTri/DongCo` trả `thoi_gian_tong_giay = tong × 20/24/12` theo loại của từng động cơ, null với nam châm; `theo_loai` cộng đúng số lần và thời gian của các động cơ cùng loại; trang hiện cột Loại và thời gian hoạt động.

## Dependencies
- task-03-hien-thi-dong-co.md

## Verification Plan
- Command: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_bao_tri.ps1`
- Named probe: ca mới `thoi_gian` trong `kiem_chung_bao_tri.ps1` — với một khối mỗi loại 3/5/6/10: mọi động cơ có `loai` đúng bảng trên; `thoi_gian_hom_nay_giay`/`thoi_gian_tong_giay` = `hom_nay`/`tong` × chuẩn (null với nam châm); mỗi dòng `theo_loai` = tổng các động cơ cùng loại; trang `/Home/Maintenance` chứa `thoi_gian_tong_giay` và `theo_loai`. Năm ca cũ vẫn phải PASS.
- Reachability: bản deploy `http://localhost:8080` đã chép DLL + `Views/Home/Maintenance.cshtml` mới.
- Oracle: PASS/FAIL từng ca; thoát 0 khi mọi ca PASS. Giao diện do người dùng xác nhận tại C3.
- Counterexample: nhân sai hệ số (vd nâng × 20), tính thời gian cho nam châm, `theo_loai` không cộng đúng, hay trang không hiện thời gian đều làm script FAIL.
- Artifacts: ephemeral.

## Receipt

Verification: PASS
Command: powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_bao_tri.ps1
Exit: 0
Base: 84f7eacca4ce20725a4cfed8f3a73594e74ddd50
Head: c7dbdea7bc9adcd61e4074569a471e707a9716cd7fbbce7f94397a6229a4173f
```text
$ powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_bao_tri.ps1
PASS  loi_theo_khoi   http=200 khoi_co_loi=58 tong_dang_mo=58 lech=
PASS  loi_mot_khoi    khoi=1 dang_mo=1 lich_su=9 limit5=5
PASS  dong_co         nhanh=DA_CO_BANG loai3=khoi18/5 dc loai5=khoi1/7 dc loai6=khoi6/8 dc loai10=khoi5/12 dc
PASS  thoi_gian       khoi18:TRUOT=0l/0s,NANG=0l/0s,HANG_RAO=0l/0s,NAM_CHAM=0l/s khoi1:TRUOT=0l/0s,NANG=0l/0s,HANG_RAO=0l/0s,NAM_CHAM=0l/s khoi6:TRUOT=0l/0s,NANG=0l/0s,HANG_RAO=0l/0s,NAM_CHAM=0l/s khoi5:TRUOT=2l/40s,NANG=1l/24s,HANG_RAO=1l/12s,NAM_CHAM=1l/s khoi_that5:TRUOT=2l/40s,NANG=1l/24s,HANG_RAO=1l/12s,NAM_CHAM=1l/s
PASS  dau_vao_sai     Loi?abc=400 DongCo?abc=400 DongCo?901=400 DongCo?thieu=400
PASS  trang           http=200 thieu=[] con_bia=[]

TONG KET: 6/6 PASS
```
- Build: VS2019 MSBuild `-p:MvcBuildViews=true` EXIT=0. Deploy 16/10/2026 16:01: DLL + `Views/Home/Maintenance.cshtml` (bản cũ đuôi `.truoc-thoigian-20261016-160103`).
- Đối chứng âm: cùng lệnh trước deploy cho 5 PASS / 1 FAIL — `thoi_gian` báo `loai=` rỗng và `theo_loai=[]`.
- Ca `thoi_gian` kiểm hệ số trên dữ liệu thật: khối 5 trượt 2 lần = 40 s, nâng 1 lần = 24 s, hàng rào 1 lần = 12 s, nam châm 1 lần không thời gian.
- Review độc lập: PASS (2 Low ở probe đã sửa: thiếu thời gian với loại có chuẩn → FAIL; không có lượt chạy có chuẩn → FAIL; kiểm `thoi_gian_hom_nay_giay` của nam châm).
- Giới hạn: "quy tắc nhận lượt chạy không đổi" xác minh bằng đọc code — `DongCoDetector.cs` chưa commit nên không có baseline để diff. Giao diện chờ người dùng xác nhận tại C3.
