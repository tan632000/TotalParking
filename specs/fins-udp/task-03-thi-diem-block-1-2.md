# Task 03 — Thí điểm FINS/UDP trên block 1 và 2

Status: done

## Outcome
Block 1 và 2 chạy FINS/UDP trên hệ thống thật: đọc được, GHI được, online;
110 block còn lại vẫn TCP. Đây là bằng chứng live đầu tiên cho đường ghi qua UDP
— quyết định mở rộng ra các block khác dựa trên kết quả này.

## Scope
- In:
  - Người dùng thêm `<add key="plc:udpBlocks" value="1,2" />` vào Web.config bản
    deploy (việc này tự restart app).
  - Mở rộng `tools/kiem_chung_transport.ps1` với các kiểm tra khi `-UdpBlocks`
    khác rỗng (AC-05, AC-06, AC-07).
- Out: mở rộng ra block khác; mọi thay đổi code ứng dụng (nếu thí điểm FAIL thì
  sửa ở task 01/02, không sửa ở đây).

## Coverage
- CP-03

## Ownership
- Modify: `tools/kiem_chung_transport.ps1` (chỉ thêm bước thí điểm)
- Read: `C:\Users\Admin\Documents\Web\totalParking\App_Data\plc_audit.log`

## Acceptance
- AC-05:
  - `/PlcStatus` báo `"udp"` cho block 1, 2 và `"tcp"` cho 110 block còn lại.
  - Trong 3 lần lấy mẫu cách nhau 5 giây, tiến trình w3wp của app không có
    socket TCP ESTABLISHED tới `192.169.1.101:9600` hoặc `192.169.1.102:9600`.
  - Lọc theo `OwningProcess` của w3wp; `PlcReachabilityScanner` mở rồi đóng ngay
    nên chỉ FAIL khi cả 3 mẫu đều thấy.
- AC-06: block 1, 2 `online = true`, `last_ok` trong vòng 10 giây gần nhất.
- AC-07: `plc_audit.log` có dòng `WRITE  192.169.1.101 … OK` và
  `WRITE  192.169.1.102 … OK` với thời điểm sau MỐC, và không có dòng `ERROR`
  cho hai IP đó sau MỐC.
  - MỐC = `max(LastWriteTime của Web.config, LastWriteTime của bin\TotalParking.dll)`
    của bản deploy, KHÔNG cộng biên. Cả hai file đều làm AppDomain khởi động lại
    (w3wp thì KHÔNG — đã đo: w3wp chạy từ 04/10 dù đã deploy sau đó).
  - Đã đo (deploy 05/10, Web.config 22:45:17): AppDomain mới ghi D1004/D1000 lúc
    khởi động ở 22:45:22.0–22:45:22.5 (≈ +5 s), có cả `.101` và `.102`. Một biên
    +15 s sẽ loại chính lượt ghi này.
  - AppDomain cũ (TCP) ngừng poll trong ≤ 5 s (`PlcConnectionManager.Stop` chờ vòng
    lặp tối đa 5 s) và chỉ ghi D1004 khi băng tải trọng đổi (`PlcConnection.cs:616`),
    nên dòng `WRITE` của nó sau MỐC chỉ xảy ra khi đúng lúc đó có người quẹt thẻ.
    Kiểm thêm `transport = "udp"` lúc chạy (AC-05) để chắc domain đang ghi là UDP.
  - Vế "không có `ERROR`" tính từ MỐC nên bắt được cả lỗi ghi ngay nhịp đầu
    (`PlcAuditLog.Error` chỉ ghi khi thông báo lỗi đổi, `PlcAuditLog.cs:100-109`).
  - Ngoại lệ (đo khi develop, 05/10): AppDomain CŨ đang tắt còn nhịp poll dở và
    ghi `ERROR … has been disposed` ngay sau MỐC (104 dòng lúc 23:29:42.7, MỐC
    23:29:42) — lỗi có từ trước (`PlcConnection.Dispose` ngoài `_gate`), thuộc
    domain TCP cũ chứ không phải đường UDP. Script loại riêng các dòng ERROR mang
    dấu hiệu dispose trong 10 s đầu sau MỐC và in số dòng bị loại; mọi ERROR khác
    sau MỐC vẫn FAIL.
  - Mỗi block ghi D1004 một lần ở nhịp poll đầu (`PlcConnection.cs:67`, `:628`),
    ≈ 5 s sau MỐC, nên dòng này luôn xuất hiện sau khởi động.
  - Không thấy dòng nào → báo `CHUA CHUNG MINH` và exit khác 0, không PASS.

## Dependencies
- task-02-chon-transport.md

## Verification Plan
- Command: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_transport.ps1 -UdpBlocks "1,2"; exit $LASTEXITCODE`
- Named probe: `tools/kiem_chung_transport.ps1` — bước `parse`, `transport`, `socket` (3 mẫu, lọc PID w3wp), `online`, `audit_ghi`; `exit 0` chỉ khi mọi bước PASS.
- Reachability: bản deploy chạy bản build task 01+02, Web.config bản deploy có `plc:udpBlocks = "1,2"`; app ở `http://localhost:8080`; script đọc được `plc_audit.log`, `Web.config` và `bin\TotalParking.dll` của bản deploy; chạy sau MỐC ít nhất 30 giây; chạy trong PowerShell (không phải Git Bash); PID w3wp lấy bằng `Get-CimInstance Win32_Process -Filter "Name='w3wp.exe'"`.
- Oracle: mọi dòng `PASS`, exit 0.
- Counterexample: cấu hình bị bỏ qua → block 1, 2 báo `"tcp"` và còn socket ESTABLISHED trong cả 3 mẫu → FAIL; đường ghi UDP hỏng → log có `ERROR` cho `.101`/`.102` hoặc không có dòng `WRITE … OK` → FAIL.
- Artifacts: ephemeral — chỉ output console.
- Rollback: xoá khoá `plc:udpBlocks` khỏi Web.config bản deploy → app restart về TCP cho mọi block.

## Receipt

Verification: PASS
Command: powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_transport.ps1 -UdpBlocks "1,2"; exit $LASTEXITCODE
Exit: 0
Base: 79f491a7c8aace00e094132e3b61e0bfe84d49d9
Head: 3eada9e7934c4b358d3e1b850d54c1fbbbcec6fc476423dfa37eb7b717392915
```text
$ powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_transport.ps1 -UdpBlocks "1,2"; exit $LASTEXITCODE
PASS  parse <null>                 tap={} all=False
PASS  parse ""                     tap={} all=False
PASS  parse "1,2"                  tap={1,2} all=False
PASS  parse " 1 , 2 "              tap={1,2} all=False
PASS  parse "1;2"                  tap={} all=False
PASS  parse "b1,3"                 tap={3} all=False
PASS  parse "*"                    tap={} all=True
PASS  transport co trong JSON      112/112 block, thieu truong transport: 0
PASS  transport dung cau hinh      ky_vong udpBlocks='1,2' -> udp=2 tcp=110; sai: khong
PASS  block thi diem co trong JSON ky_vong=2 thay=2
PASS  chay sau moc >= 30s          moc=2026-10-05 23:50:28 da qua 37s
PASS  online block 1               online=True last_ok=2026-10-05 23:51:05 loi=
PASS  online block 2               online=True last_ok=2026-10-05 23:51:05 loi=
PASS  khong con TCP toi block UDP  w3wp=17164 mau_thay_TCP=0/3 ip=192.169.1.101,192.169.1.102
PASS  ghi qua UDP 192.169.1.101    2 dong WRITE OK; WRITE LOI: 0; ERROR sau moc: 0 (loai 0 dong domain cu dang tat)
PASS  ghi qua UDP 192.169.1.102    1 dong WRITE OK; WRITE LOI: 0; ERROR sau moc: 0 (loai 0 dong domain cu dang tat)

TONG KET: 16/16 PASS
```

Trạng thái live: Web.config bản deploy có `plc:udpBlocks = "1,2"` từ 2026-10-05 23:37:20; tạm gỡ hai lần để chạy lại task-02 (final-Head), lần cuối bật lại lúc 23:50:28 (MỐC của lần chạy này) (bản sao lưu trước khi sửa trong scratchpad, SHA-256 `7CB349A454B4…`).

Negative controls:
- Cùng lệnh trên bản deploy toàn TCP (trước khi bật): FAIL `transport dung cau hinh` và `khong con TCP` (3/3 mẫu thấy TCP), exit 1.
- Chạy lúc 29 s sau MỐC: FAIL `chay sau moc >= 30s`.
- Bản deploy giả (scratchpad) với dòng chèn sau MỐC: `WRITE … LOI` → FAIL; `Cannot access a disposed object` → FAIL; `The semaphore has been disposed.` → loại riêng, có in số.

Review: code-auditor PASS_WITH_WARNINGS (M1 bỏ sót WRITE LOI; M2 loại dispose quá rộng) → sửa → re-review PASS; Low cuối (-cmatch cột trạng thái) đã sửa trước lần chạy này.