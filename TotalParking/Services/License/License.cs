using System;

namespace TotalParking.Services.License
{
    // Trang thai giay phep. Gate web CHI chan khi Locked; khong dung tien trinh
    // nen (PlcHost/LedHost/PgsHost) va khong dung lop an toan vat ly.
    public enum LicenseState
    {
        Valid,
        Locked
    }

    // Ket qua danh gia giay phep, du de hien thi tren man Activation.
    public class LicenseStatus
    {
        public LicenseState State { get; set; }
        public string CustomerId { get; set; }
        public DateTime? ExpiryUtc { get; set; }
        public string Reason { get; set; }

        public bool IsLocked { get { return State == LicenseState.Locked; } }
    }

    // Noi dung mot giay phep da parse + verify chu ky.
    internal class ParsedLicense
    {
        public string CustomerId;
        public DateTime IssuedUtc;
        public DateTime ExpiryUtc;
    }
}
