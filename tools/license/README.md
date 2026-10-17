# Công cụ license (offline)

`issue-license.ps1` phát hành và kiểm tra license cho gate kích hoạt của SCADA.
Chạy **offline trên máy của nhà cung cấp** — private key không bao giờ rời máy.

> Gate chỉ khóa **tầng web MVC**. Không đụng PlcHost/LedHost/PgsHost hay lớp an
> toàn vật lý. Xem `specs/license-activation/`.

## Hợp đồng ký (phải khớp C# verifier)

- RSA-2048, chữ ký `RSA.VerifyData(data, SHA256, Pkcs1)`.
- Payload canonical (đúng các byte được ký) = chuỗi JSON UTF-8, **đúng thứ tự
  khóa, không khoảng trắng, không xuống dòng**:
  `{"customerId":"<id>","issuedUtc":"<iso>","expiryUtc":"<iso>"}`
  ngày dạng `yyyy-MM-ddTHH:mm:ssZ` (UTC, InvariantCulture).
- File `.lic` = 2 dòng: dòng 1 payload canonical, dòng 2 Base64 chữ ký.
- Public key lưu Base64(XML) trong `Web.config` key `license:publicKey`.

## Lệnh

```powershell
# 1) Sinh keypair + ghi public key vào Web.config (chạy MỘT lần)
powershell -NoProfile -File tools/license/issue-license.ps1 -GenerateKeys

# 2) Phát hành license dùng thử (đã làm cho bản bàn giao)
powershell -NoProfile -File tools/license/issue-license.ps1 -Issue `
    -Expiry 2026-11-07 -Out TotalParking/App_Data/license/license.lic

# 3) Kiểm tra license committed khớp public key committed
powershell -NoProfile -File tools/license/issue-license.ps1 -VerifyCommitted `
    -License TotalParking/App_Data/license/license.lic
```

## Gia hạn (khi khách thanh toán)

Phát hành license mới với ngày xa hơn rồi gửi khách dán vào màn **Activation**:

```powershell
powershell -NoProfile -File tools/license/issue-license.ps1 -Issue `
    -Expiry 2027-06-30 -Out ban-giao-khach/license.lic
```

## License cho máy dev (tránh rebuild sau ngày khóa bị kẹt)

Đặt tên `*.dev.lic` (đã gitignore) với hạn dài, bỏ vào `App_Data/license/` **chỉ
trên máy dev** — không commit:

```powershell
powershell -NoProfile -File tools/license/issue-license.ps1 -Issue `
    -Expiry 2030-01-01 -Out TotalParking/App_Data/license/local.dev.lic
```

## ⚠️ Khóa bí mật

- `tools/license/keys/private-key.xml` (gitignore) là **private key**. Ai có nó
  đều phát hành được license. **Sao lưu an toàn & giữ offline.** Mất nó = không
  gia hạn được, phải `-GenerateKeys` lại và cập nhật `license:publicKey`.
- Keypair sinh ở bước phát triển nên coi là **key dev**. Trước khi bàn giao thật,
  nên `-GenerateKeys -Force` trên máy an toàn của nhà cung cấp, rồi phát hành lại
  `license.lic`.
