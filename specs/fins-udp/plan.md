# FINS/UDP cho kết nối PLC
Specs-Contract: process-first-ready-v1

## Scope decision (C1 — 2026-10-05)
- Vấn đề: FINS/TCP mỗi PLC chỉ có 3 khe; mỗi lần máy chủ restart có thể để lại
  phiên bỏ rơi chiếm khe hàng giờ → `0x00000020` (đo ngày 04/10: 86/112 block).
  Xem `docs/cau_hoi_plc_loi_0x20.md`.
- Bằng chứng khả thi (05/10, đo live, chỉ đọc, socket UDP KHÔNG bind — cổng
  nguồn tạm do hệ điều hành cấp):
  - Đọc D1000 qua UDP: 112/112 PLC trả lời, 2–3 ms.
  - DA1 phải = octet cuối IP PLC (hoặc 0); SA1 phải = octet cuối IP máy chủ (4).
    `plc_node = 1` của DB → End Code `9005`; `pc_node = 0` hoặc 251 → `2108`.
    Nên node UDP suy từ IP, KHÔNG đọc từ `plc_device`.
  - D0 đọc được; D32767 → End Code `1103`.
  - Hai socket cùng SA1 = 4, khác cổng (61456/61457), gửi xen kẽ 50 lượt tới
    block 1: 100/100 đúng SID, đúng socket. PLC trả về cổng nguồn của từng gói.
- Existing:
  - Khung FINS, mã vùng, đối chiếu SID: `TotalParking/Services/Plc/OmronFinsClient.cs:276-295`, `:258`, `:363-389`.
  - Tuần tự hoá qua `_gate`, giãn cách thử lại, online/offline: `TotalParking/Services/Plc/PlcConnection.cs:54`, `:153-187`, `:782-812`.
  - Điểm tạo client duy nhất: `TotalParking/Services/Plc/PlcConnection.cs:339-340`; `new PlcConnection(` ở `PlcConnectionManager.cs:69`, `:118`.
  - Điểm ghi: `PlcConnection.cs:246` (xoá D1002), `:308` (xoá D1000 cũ), `:494`
    (tắt bit yêu cầu), `:618` (D1004), `:663` (D1000). `:246` và `:494` chạm
    thanh ghi mà ladder cũng ghi → gửi lại lệnh ghi KHÔNG an toàn. Khi một lệnh
    ghi hỏng, luồng hiện có tự thử lại ở nhịp sau (`:353`, `:641`, `:275-277`).
- Minimum change: client UDP cùng API với client TCP qua một interface chung;
  chỉ gửi lại lệnh ĐỌC; `PlcConnection` chọn transport theo `plc:udpBlocks`
  trong Web.config (mặc định TCP); `/PlcStatus` hiện `transport` từng block.
- Expansion signals: không (≈8 file, 2 lớp mới, một hệ con).
- Risk: `critical` — ghi thanh ghi PLC thật đang vận hành. Bắt buộc cờ, thí
  điểm từng block, quay về TCP bằng cấu hình.
- User decision: KEEP — thí điểm 1–2 block rồi mở rộng, không đổi DB.

## Out of scope
- Tool Python (`tools/plc_register.py`, `tools/kiem_chung_doc_hut.py`, …) giữ TCP.
- `PlcReachabilityScanner` vẫn dò bằng TCP connect-rồi-đóng (`PlcReachabilityScanner.cs:174-192`), kể cả block chạy UDP.
- Tự động chuyển TCP → UDP khi gặp `0x20`.
- Sửa schema hoặc dữ liệu `plc_device`.
- Đổi nhịp poll, throttle, hay hợp đồng thanh ghi.
- Sửa Web.config của bản deploy — là thao tác vận hành của người dùng.
- Mở rộng UDP ra ngoài block 1, 2 — quyết định sau thí điểm.

## Coverage profile
| ID | Outcome | Change kinds | Material surfaces | Ambiguity/action | Risk/evidence | Required proof |
|---|---|---|---|---|---|---|
| CP-01 | Đọc/ghi PLC qua FINS/UDP với node suy từ IP; gửi lại chỉ lệnh đọc; một lượt nhận duy nhất; phân biệt lỗi khung và End Code | add, modify | Integration (PLC), Async/state (SID, timeout, retry, lượt nhận treo) | none | critical; đo live 05/10 | source (build), local (responder UDP giả trên loopback), live (đọc block 1) |
| CP-02 | Chọn TCP/UDP theo block bằng `plc:udpBlocks`, mặc định TCP, cấu hình sai không làm hỏng poll, quan sát được qua `/PlcStatus` | modify | Runtime/deploy (config, rollback), API (`/PlcStatus` JSON) | none | critical; `PlcConnection.cs:339`, `PlcConnectionManager.cs:69` | source (build), local (parse qua DLL), live (bản deploy không có khoá) |
| CP-03 | Thí điểm block 1, 2 chạy UDP: đọc, ghi, online | other:pilot | Integration (PLC), Runtime/deploy | none | critical; đường ghi UDP chưa từng chạy live | live (bản deploy với `plc:udpBlocks = "1,2"`) |

## Acceptance criteria
| ID | EARS criterion | Proof |
|---|---|---|
| AC-01 | The UDP client shall send FINS frames without the TCP header or handshake, with DA1 = last octet of the PLC IP and SA1 = last octet of the local IP bound to that PLC. | `tools/kiem_chung_fins_udp.ps1` |
| AC-02 | If no reply with the matching SID arrives within one attempt timeout for a READ command, the UDP client shall resend once with a new SID, ignore datagrams whose SID differs, and after two failed attempts raise `FinsFramingException`. | `tools/kiem_chung_fins_udp.ps1` |
| AC-03 | If the PLC replies with a non-zero End Code, the UDP client shall raise `FinsException` and keep its socket. | `tools/kiem_chung_fins_udp.ps1` |
| AC-08 | The UDP client shall send each WRITE command exactly once and raise `FinsFramingException` when its reply does not arrive in time. | `tools/kiem_chung_fins_udp.ps1` |
| AC-09 | When a command has failed by timeout, the next command on the same client shall receive its own reply (no earlier pending receive consumes it). | `tools/kiem_chung_fins_udp.ps1` |
| AC-04 | Where `plc:udpBlocks` is absent or empty, every block shall use FINS/TCP exactly as before. | `tools/kiem_chung_transport.ps1` (không tham số) |
| AC-10 | If `plc:udpBlocks` contains an invalid token, the system shall ignore that token, keep TCP for it, and keep polling every block. | `tools/kiem_chung_transport.ps1` (không tham số) |
| AC-05 | Where a block number is listed in `plc:udpBlocks` (or the value is `*`), that block shall use FINS/UDP and `/PlcStatus` shall report `transport: "udp"` for it and `"tcp"` for the rest. | `tools/kiem_chung_transport.ps1 -UdpBlocks "1,2"` |
| AC-06 | While a block uses UDP and its PLC answers, `/PlcStatus` shall report it online. | `tools/kiem_chung_transport.ps1 -UdpBlocks "1,2"` |
| AC-07 | While a block uses UDP, a write to its PLC shall succeed and be logged as OK in `plc_audit.log`. | `tools/kiem_chung_transport.ps1 -UdpBlocks "1,2"` |

## Tasks
| # | Task | Criteria | Primary ownership | Dependencies | Status |
|---|---|---|---|---|---|
| 01 | Client FINS/UDP | AC-01, AC-02, AC-03, AC-08, AC-09 | `TotalParking/Services/Plc/OmronFinsUdpClient.cs` | - | done |
| 02 | Chọn transport theo block + `/PlcStatus` | AC-04, AC-10 | `TotalParking/Services/Plc/PlcConnection.cs` | task-01-client-udp.md | done |
| 03 | Thí điểm UDP trên block 1, 2 | AC-05, AC-06, AC-07 | (không sửa code; chạy kiểm chứng live) | task-02-chon-transport.md | done |

## Review log
- Round 1 (2026-10-05): 2 reviewer độc lập (Fact/Contract/Security; Failure-mode/Flow/Assumption). 8 finding chấp nhận tại C2 (R1–R8); 1 bác bỏ bằng đo live (hai nguồn cùng SA1: 100/100 đúng).
  - R1 một lượt nhận duy nhất → task-01 Scope + AC-09.
  - R2 chỉ gửi lại lệnh đọc → plan Existing, AC-02, AC-08, task-01 Scope.
  - R3 responder giả trên loopback → task-01 Verification.
  - R4 bằng chứng ghi live → AC-07, task-03.
  - R5 exit code / kiểu exception / LoadFrom → task-01 Verification.
  - R6 parse cấu hình an toàn → AC-10, task-02.
  - R7 tham số `[string]`, tách lệnh → task-02, task-03 Verification.
  - R8 oracle socket lọc theo PID w3wp, lấy mẫu → task-03; AC-01 kiểm kiểu client; Out of scope thêm bộ dò TCP.
  - Sweep: 4 file đọc lại / 8 delta / 0 tham chiếu cũ / 0 mâu thuẫn còn lại.
- Closure round 1 (fresh reviewer): R1, R2, R3, R5, R6, R8 PASS. R4 FAIL (mốc w3wp sai — w3wp không restart khi deploy, log lẫn dòng thời TCP); R7 FAIL (đo: PS 5.1 bỏ `-UdpBlocks ""` qua `-File` → exit 1). Thêm: AC-04 so với mốc online không đo được.
- Round 2 repair: R4 → MỐC = max(LastWriteTime Web.config, bin\TotalParking.dll) + 15 s (task-03 AC-07); R7 → tham số mặc định rỗng, lệnh task-02 không truyền `-UdpBlocks`; AC-04 bỏ vế so số online.
- Closure round 2 (fresh reviewer, có chạy thật): R7 PASS (dummy script PS 5.1: không tham số → exit 0; `-UdpBlocks "1,2"` → chuỗi `1,2`, exit 0); AC-04 PASS (`/PlcStatus` trả đúng 112 block). R4 FAIL: đo log deploy 05/10 — lượt ghi khởi động ở ≈ +5 s, biên +15 s loại mất nó.
- Round 3 (bằng chứng runtime, theo B4): R4 → bỏ biên, MỐC = max(LastWriteTime); replay trên chính số đo closure round 2: MỐC 22:45:17 < lượt ghi `.101`/`.102` 22:45:22 → nằm trong cửa sổ → PASS. Replay do tác giả packet làm trên số đo của reviewer, không phải reviewer độc lập — nêu ở handoff.
- Sweep cuối: 4 file đọc lại / R4, R7, AC-04 / 0 tham chiếu cũ ("+ 15 s" chỉ còn trong review log) / 0 mâu thuẫn còn lại.

## Completion (C3 — 2026-10-07)
- User decision: ACCEPT — feature hoàn thành.
- Bằng chứng live sau receipt:
  - Block 1: gửi xe và lấy xe thật qua UDP (06/10 16:50–16:53).
  - Block 2: tìm xe thật, trả đúng `D1000 <- 1` (06/10 17:02).
  - Chuyển toàn bãi `"*"` (06/10 17:13): 112 UDP, 110/112 online.
- Sự cố 07/10: một lần publish (06/10 17:17) ghi đè Web.config bản deploy làm mất
  khoá, cả bãi về TCP; 3 lần restart máy chủ sáng 07/10 làm 104/112 block báo
  `0x20`. Bật lại `"*"` lúc 10:37:38 → 110/112 online trong ~11 s, 0 socket TCP.
  Đây là bằng chứng thực tế cho mục tiêu của feature.
- Khắc phục: khoá `plc:udpBlocks = "*"` thêm vào `TotalParking/Web.config` (source)
  để publish không xoá nữa.
- Amendment (2026-10-07, sau C3, theo quyết định user "không giao tiếp TCP với PLC"):
  bỏ hẳn FINS/TCP. Xoá `OmronFinsClient` và `IFinsClient`; `PlcConnection` luôn dùng
  `OmronFinsUdpClient`; khoá `plc:udpBlocks`, `ParseUdpBlocks` và nhánh xử lý `0x20`
  không còn. Vì vậy AC-04, AC-05 và AC-10 (chọn TCP/UDP theo block) không còn áp
  dụng với code hiện tại, và lệnh trong Verification Plan của task-02/03 (tham số
  `-UdpBlocks`) không chạy lại được. Receipt cũ giữ nguyên làm lịch sử của build lúc
  đó. Kiểm chứng hiện hành:
  - `tools/kiem_chung_fins_udp.ps1` (10 ca, thêm `ProbeAsync`);
  - `tools/kiem_chung_transport.ps1` (không tham số; kiểm DLL deploy không còn
    `OmronFinsClient`, cả 112 block UDP, không có TCP tới 9600).
  Bộ dò khả dụng và trang Settings dò PLC bằng một lệnh đọc FINS/UDP, nên "sống"
  giờ nghĩa là PLC trả lời FINS (chặt hơn "cổng 9600 mở"). Tool Python trong
  `tools/` vẫn dùng FINS/TCP (công cụ test) — xem quyết định ở commit.
- Còn mở (ngoài scope, không do UDP): block 97, 98 mất mạng vật lý từ 06/10 09:17;
  block 53 D106 kẹt nửa mã `0000 A0BB` nên không ghi D1004 lúc khởi động; ~104 dòng
  `semaphore has been disposed` mỗi lần recycle (`PlcConnection.Dispose` ngoài `_gate`).
