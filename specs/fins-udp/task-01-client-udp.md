# Task 01 — Client FINS/UDP cùng API với client TCP

Status: done

## Outcome
Có `OmronFinsUdpClient` đọc/ghi PLC Omron qua FINS/UDP, cùng interface với
`OmronFinsClient` (TCP), để `PlcConnection` đổi transport mà không đổi lời gọi.
Chưa ai trong ứng dụng dùng nó — task 02 mới nối vào.

## Scope
- In:
  - Interface `IFinsClient`: `IsConnected`, `ConnectAsync(ip, port, timeoutMs)`,
    `ReadWordsAsync`, `WriteWordsAsync`, `GetBitStateAsync`, `SetBitStateAsync`,
    `Close`, `Dispose` — đúng chữ ký đang có ở `OmronFinsClient.cs:62`, `:111`,
    `:146`, `:173`, `:198`, `:410`, `:418`.
  - `OmronFinsClient` thực thi `IFinsClient`, không đổi hành vi.
  - `OmronFinsUdpClient`:
    - `ConnectAsync`: tạo socket UDP, `Connect(ip, port)` — KHÔNG bind 9600,
      cổng nguồn tạm (đã đo được, xem plan). DA1 = octet cuối IP PLC; SA1 =
      octet cuối `LocalEndPoint` sau `Connect`; SA1 ngoài 1..254 → ném
      `FinsFramingException`. Rồi đọc thử 1 word D0 (thay bước bắt tay TCP);
      mọi lỗi của bước này → đóng socket, ném `FinsFramingException`.
    - **Một lượt nhận duy nhất**: giữ tối đa MỘT tác vụ nhận đang chờ trong một
      field (`_pendingReceive`). Hết giờ thì KHÔNG bỏ tác vụ đó — lần thử sau,
      vòng lọc SID và lệnh sau dùng lại chính nó; chỉ tạo tác vụ nhận mới khi
      tác vụ trước đã hoàn tất. (`UdpClient.ReceiveAsync` trên .NET 4.5 không
      huỷ được; tạo lượt nhận mới mỗi lần sẽ để lượt cũ nuốt mất phản hồi.)
    - Lệnh ĐỌC (MRC 01 / SRC 01): tối đa 2 lần gửi, mỗi lần chờ
      `max(500, timeoutMs / 2)` ms, mỗi lần một SID mới. Gói có SID khác bị bỏ
      qua và tiếp tục chờ trong cùng thời hạn. Hết 2 lần → `FinsFramingException`.
    - Lệnh GHI (MRC 01 / SRC 02): gửi ĐÚNG MỘT lần, chờ `timeoutMs`; hết giờ →
      `FinsFramingException`. Không gửi lại: bit yêu cầu (`PlcConnection.cs:494`)
      và D1002 (`:246`) cũng do ladder ghi, gửi lại có thể xoá một lượt quẹt mới.
    - Phản hồi < 14 byte, hoặc thiếu byte dữ liệu → `FinsFramingException`.
    - End Code ≠ 0 → `FinsException`, giữ socket.
    - `SocketException` khi GỬI hoặc NHẬN (ví dụ 10054, 10065) →
      `FinsFramingException`.
    - Thuộc tính `Transport` trả `"udp"` (client TCP trả `"tcp"`), khai báo
      trong `IFinsClient`.
  - Thêm 2 file `.cs` mới vào `TotalParking.csproj`.
  - Script kiểm chứng `tools/kiem_chung_fins_udp.ps1`.
- Out: chọn transport trong `PlcConnection`; mọi lệnh ghi xuống PLC THẬT trong
  script (ghi chỉ thử với responder giả trên loopback).

## Coverage
- CP-01

## Ownership
- Create: `TotalParking/Services/Plc/IFinsClient.cs`
- Create: `TotalParking/Services/Plc/OmronFinsUdpClient.cs`
- Modify: `TotalParking/Services/Plc/OmronFinsClient.cs` (khai báo `: IFinsClient`, thêm `Transport`)
- Modify: `TotalParking/TotalParking.csproj` (2 dòng `Compile Include`)
- Create: `tools/kiem_chung_fins_udp.ps1`
- Read: `TotalParking/Services/Plc/PlcTypes.cs`, `TotalParking/Services/Plc/PlcConnection.cs`

## Acceptance
- AC-01: `live_doc_D1000_block1` — đọc D1000 của `192.169.1.101` qua
  `OmronFinsUdpClient` trả 1 word; `Transport == "udp"`.
- AC-02:
  - `live_ip_khong_ton_tai` — `timeoutMs = 3000` tới `192.169.1.250` ném
    `FinsFramingException` (kiểm `InnerException`) sau 2,5–4,5 s.
  - `loopback_bo_goi_dau` — responder bỏ datagram đọc đầu tiên, trả lời datagram
    thứ hai → lệnh đọc thành công; responder ghi nhận đúng 2 datagram có SID khác nhau.
  - `loopback_sid_cu` — responder gửi một gói SID cũ rồi mới gửi gói đúng →
    lệnh nhận đúng giá trị của gói đúng.
- AC-03: `live_dia_chi_ngoai_dai` — đọc D32767 ném `FinsException`, `IsConnected`
  vẫn `true`; đọc D1000 tiếp theo trên CÙNG đối tượng thành công.
- AC-08: `loopback_ghi_mot_lan` — responder không trả lời lệnh ghi → client ném
  `FinsFramingException`; responder ghi nhận đúng 1 datagram ghi.
- AC-09: `loopback_lenh_sau_khong_bi_nuot` — responder bỏ mọi datagram của lệnh
  1 (lệnh 1 hết giờ), trả lời lệnh 2 → lệnh 2 thành công ngay lần gửi đầu.
- Scope "`SocketException` khi NHẬN": `loopback_plc_dung` — responder tắt sau
  khi kết nối (cổng đóng, ICMP 10054) → lệnh đọc ném `FinsFramingException`, không
  phải `SocketException` thô. (Thêm khi review task: `ReceiveAsync` trên .NET 4.x
  ném lỗi này đồng bộ.)

## Dependencies
- none

## Verification Plan
- Command: `& "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" TotalParking\TotalParking.csproj /p:Configuration=Debug /v:minimal /nologo; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }; powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_fins_udp.ps1; exit $LASTEXITCODE`
- Named probe: `tools/kiem_chung_fins_udp.ps1` — nạp `TotalParking\bin\TotalParking.dll` bằng `[Reflection.Assembly]::LoadFrom`; responder UDP giả viết bằng C# qua `Add-Type` (không tham chiếu DLL ứng dụng), lắng nghe `127.0.0.1` cổng tạm, kịch bản theo từng ca; chạy 8 ca ở mục Acceptance, in `PASS`/`FAIL` từng ca, `exit 0` chỉ khi đếm đủ 8 PASS và 0 FAIL, ngược lại `exit 1`. Gọi async bằng `.GetAwaiter().GetResult()` (không dùng `.Result`/`.Wait()` — chúng bọc lỗi trong `AggregateException`). Kiểu lỗi kiểm bằng `$_.Exception.InnerException -is [TotalParking.Services.Plc.FinsFramingException]` (hoặc `FinsException`). Responder đếm datagram theo MRC/SRC và SID, tách riêng gói thăm dò D0 của `ConnectAsync` khỏi gói của lệnh đang thử.
- Reachability: chạy từ máy chủ SCADA (`192.169.1.4`), cùng mạng `192.169.1.0/24` với PLC; PowerShell 5.1 nạp được DLL .NET Framework 4.5. Ca `live_*` chỉ ĐỌC. Chạy được cả khi bản deploy đang cho block 1 dùng UDP — đã đo hai nguồn cùng SA1 không xung đột (plan.md).
- Oracle: 8 dòng `PASS`, dòng `TONG KET: 8/8 PASS`, exit 0.
- Counterexample: SA1 lấy từ `pc_node` DB → `live_doc_D1000_block1` nhận `2108`, FAIL; tạo lượt nhận mới mỗi lần thử → `loopback_bo_goi_dau` và `loopback_lenh_sau_khong_bi_nuot` FAIL; không lọc SID → `loopback_sid_cu` trả giá trị gói cũ, FAIL; gửi lại lệnh ghi → `loopback_ghi_mot_lan` đếm 2 datagram, FAIL; coi End Code là lỗi khung → `live_dia_chi_ngoai_dai` nhận `FinsFramingException` thay vì `FinsException`, FAIL; để `SocketException` lọt ra → `loopback_plc_dung` FAIL (đã chạy trên bản copy code trước khi sửa: FAIL, exit 1); build hỏng → exit khác 0.
- Artifacts: ephemeral — chỉ output console; script không ghi file.

## Receipt

Verification: PASS
Command: & "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" TotalParking\TotalParking.csproj /p:Configuration=Debug /v:minimal /nologo; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }; powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_fins_udp.ps1; exit $LASTEXITCODE
Exit: 0
Base: 79f491a7c8aace00e094132e3b61e0bfe84d49d9
Head: 3eada9e7934c4b358d3e1b850d54c1fbbbcec6fc476423dfa37eb7b717392915
```text
$ & "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" TotalParking\TotalParking.csproj /p:Configuration=Debug /v:minimal /nologo; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }; powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_fins_udp.ps1; exit $LASTEXITCODE
  TotalParking -> C:\Users\Admin\source\repos\TotalParking\TotalParking\bin\TotalParking.dll
PASS  live_doc_D1000_block1              D1000=0 transport=udp
PASS  live_ip_khong_ton_tai              3.04s FinsFramingException
PASS  live_dia_chi_ngoai_dai             loi=FinsException PLC tra ve End Code 0x1103. IsConnected=True doc_tiep=True
PASS  loopback_bo_goi_dau                gia_tri=4321 so_goi=2 sid_khac_nhau=2
PASS  loopback_sid_cu                    gia_tri=2222 (2222 = goi dung SID, 1111 = goi cu)
PASS  loopback_ghi_mot_lan               loi=FinsFramingException so_goi_ghi=1
PASS  loopback_lenh_sau_khong_bi_nuot    lenh1=FinsFramingException lenh2=3333 so_lan_gui_lenh2=1
PASS  loopback_plc_dung                  loi=FinsFramingException Loi nhan FINS/UDP: An existing connection was forcibly closed by the remote host

TONG KET: 8/8 PASS
```

Negative controls (bản copy dùng một lần trong scratchpad, không phải cây mã):
- Tạo lượt nhận mới mỗi lần chờ → `loopback_bo_goi_dau` và `loopback_lenh_sau_khong_bi_nuot` FAIL, exit 1.
- Gửi lại lệnh ghi → `loopback_ghi_mot_lan` FAIL (so_goi_ghi=2), exit 1.
- Code trước khi sửa H1 → `loopback_plc_dung` FAIL (SocketException thô), exit 1.

Review: code-auditor FAIL (H1: SocketException đồng bộ từ ReceiveAsync) → sửa → re-review PASS.
Hạn chế: `loopback_plc_dung` dựa vào ICMP về trước khi tạo lượt nhận (đúng trên loopback); máy chặn ICMP có thể PASS qua đường hết giờ.