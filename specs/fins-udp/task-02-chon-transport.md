# Task 02 — Chọn TCP/UDP theo block và hiện trên `/PlcStatus`

Status: done

## Outcome
`PlcConnection` dùng FINS/UDP cho các block liệt kê trong `plc:udpBlocks` và
FINS/TCP cho mọi block khác; thiếu khoá hoặc gõ sai thì vẫn là TCP và poll
không hỏng; `/PlcStatus` cho biết block nào đang dùng transport nào. Sau task
này, deploy mà KHÔNG thêm khoá thì hành vi y như trước.

## Scope
- In:
  - Hàm tĩnh công khai `PlcConnection.ParseUdpBlocks(string raw, out bool all)`
    trả `HashSet<int>` số block: tách theo `,`, `Trim` từng phần, `int.TryParse`,
    bỏ qua phần rỗng hoặc không hợp lệ (không ném lỗi); một phần đúng bằng `*` →
    `all = true` (mọi block dùng UDP). `null` hoặc rỗng → tập rỗng, `all = false`.
  - Đọc `plc:udpBlocks` MỘT lần vào biến static (lười, an toàn luồng). Đổi
    Web.config đã tự restart app nên không cần đọc lại.
  - Trường `_client` đổi kiểu sang `IFinsClient`; `PlcConnection.cs:339` dựng
    `OmronFinsUdpClient` khi block thuộc tập, ngược lại `OmronFinsClient`.
  - Thuộc tính công khai `PlcConnection.Transport` (`"udp"`/`"tcp"`), quyết định
    từ cấu hình lúc dựng, không phụ thuộc đã kết nối hay chưa.
  - `PlcStatusController.Index` thêm `transport` vào mỗi phần tử `blocks`
    (`PlcStatusController.cs:64-85`).
  - Script kiểm chứng `tools/kiem_chung_transport.ps1` (task 03 dùng lại).
- Out: sửa Web.config bản deploy; đổi chính sách giãn cách thử lại; nhánh
  `hetKhe` giữ nguyên (chỉ TCP mới phát sinh).

## Coverage
- CP-02

## Ownership
- Modify: `TotalParking/Services/Plc/PlcConnection.cs`
- Modify: `TotalParking/Controllers/PlcStatusController.cs`
- Create: `tools/kiem_chung_transport.ps1`
- Read: `TotalParking/Services/Plc/IFinsClient.cs`, `TotalParking/Services/Plc/OmronFinsUdpClient.cs`, `TotalParking/Services/Plc/PlcConnectionManager.cs`

## Acceptance
- AC-10 (offline, qua DLL): `ParseUdpBlocks` cho
  - `null` → rỗng; `""` → rỗng
  - `"1,2"` → {1,2}; `" 1 , 2 "` → {1,2}
  - `"1;2"` → rỗng; `"b1,3"` → {3}
  - `"*"` → `all = true`; `"1,2"` → `all = false`
  và không ca nào ném lỗi.
- AC-04 (live, bản deploy KHÔNG có `plc:udpBlocks`): `/PlcStatus` báo
  `transport = "tcp"` cho cả 112 block.

## Dependencies
- task-01-client-udp.md

## Verification Plan
- Command: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_transport.ps1; exit $LASTEXITCODE`
- Named probe: `tools/kiem_chung_transport.ps1` — tham số `-UdpBlocks` kiểu `[string]`, mặc định `""` (bỏ tham số = kỳ vọng mọi block TCP; PowerShell 5.1 bỏ đối số rỗng khi gọi qua `-File`, nên KHÔNG truyền `-UdpBlocks ""`), tự tách bằng `ParseUdpBlocks`.
  - Bước `parse` (offline): nạp `TotalParking\bin\TotalParking.dll` bằng `[Reflection.Assembly]::LoadFrom`, chạy các ca AC-10.
  - Bước `transport` (live): đọc `http://localhost:8080/PlcStatus`, so `transport` từng block với tập kỳ vọng.
  - In `PASS`/`FAIL` từng kiểm tra; `exit 0` chỉ khi mọi kiểm tra PASS.
  - Khi `-UdpBlocks` khác rỗng, chạy thêm các kiểm tra của task 03 (định nghĩa ở task 03).
- Reachability: người dùng deploy bản build từ task 01+02 lên `C:\Users\Admin\Documents\Web\totalParking\`, Web.config bản deploy KHÔNG có khoá `plc:udpBlocks`; app trả lời ở `http://localhost:8080`.
- Oracle: mọi dòng `PASS`, exit 0.
- Counterexample: `int.Parse` thay cho `TryParse` → ca `"1;2"` ném lỗi, FAIL; khoá vắng mà mặc định UDP → block báo `"udp"`, FAIL; quên thêm `transport` vào JSON → FAIL.
- Artifacts: ephemeral — chỉ output console.

## Receipt

Verification: PASS
Command: powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_transport.ps1; exit $LASTEXITCODE
Exit: 0
Base: 79f491a7c8aace00e094132e3b61e0bfe84d49d9
Head: 3eada9e7934c4b358d3e1b850d54c1fbbbcec6fc476423dfa37eb7b717392915
```text
$ powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_transport.ps1; exit $LASTEXITCODE
PASS  parse <null>                 tap={} all=False
PASS  parse ""                     tap={} all=False
PASS  parse "1,2"                  tap={1,2} all=False
PASS  parse " 1 , 2 "              tap={1,2} all=False
PASS  parse "1;2"                  tap={} all=False
PASS  parse "b1,3"                 tap={3} all=False
PASS  parse "*"                    tap={} all=True
PASS  transport co trong JSON      112/112 block, thieu truong transport: 0
PASS  transport dung cau hinh      ky_vong udpBlocks='' -> udp=0 tcp=112; sai: khong

TONG KET: 9/9 PASS
```

Chạy lại lúc 23:50:24 (final-Head: tạm gỡ `plc:udpBlocks`, Web.config trùng SHA-256 bản sao lưu trước thí điểm; 112/112 online, udp=0) — output y hệt khối trên.
Điều kiện live lần đầu: bản deploy `bin\TotalParking.dll` trùng SHA-256 với bản build nguồn (`321ACC06811D…`, 05/10 23:29:37); Web.config bản deploy KHÔNG có `plc:udpBlocks`.

Negative controls (bản copy dùng một lần trong scratchpad):
- Bỏ kiểm null trong `ParseUdpBlocks` → `parse <null>` FAIL (NullReference).
- Bản deploy cũ chưa có trường `transport` → 2 kiểm tra live FAIL, exit 1 (chạy trước khi deploy).

Review: code-auditor PASS_WITH_WARNINGS (ca null không truyền null thật; không kiểm đủ 112) → sửa script → re-review PASS.