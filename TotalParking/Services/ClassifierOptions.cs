using System.Configuration;
using System.Globalization;

namespace TotalParking.Services
{
    // Ngưỡng phân loại xe. Để trong Web.config vì phần lớn là số đo hiện trường
    // chưa được xác nhận: sửa được mà không phải build lại.
    //
    // Xuất xứ từng nhóm giá trị được ghi rõ trong Web.config.
    public class ClassifierOptions
    {
        // Ranh giới hai làn pallet cơ khí.
        public int MechanicalMaxLengthShortMm = 4800;
        public int MechanicalMaxLengthLongMm  = 5000;

        // Giới hạn hình học của khoang pallet.
        public int PalletMaxWidthMm  = 2000;
        public int PalletMaxHeightMm = 1900;

        // Hai hạng tải pallet thật của bãi này, lấy từ danh sách thẻ
        // (weight_class 2200KG / 2600KG) chứ không phải từ bảng của camera.
        public int PalletRatingLowKg  = 2200;
        public int PalletRatingHighKg = 2600;

        // Biên an toàn. width_mm camera gửi không tính gương; weight_kg là khối
        // lượng bản thân, không phải khối lượng khi chở người và hành lý.
        public int MirrorMarginMm = 200;
        public int LoadMarginKg   = 400;

        // Vượt bất kỳ ngưỡng nào trong nhóm này thì bãi không tiếp nhận được,
        // kể cả chỗ đỗ nền.
        public int GarageMaxLengthMm = 6000;
        public int GarageMaxWidthMm  = 2200;
        public int GarageMaxHeightMm = 2200;
        public int GarageMaxWeightKg = 3500;

        public static ClassifierOptions FromConfig()
        {
            var o = new ClassifierOptions();
            o.MechanicalMaxLengthShortMm = Int("classifier:mechanicalMaxLengthShortMm", o.MechanicalMaxLengthShortMm);
            o.MechanicalMaxLengthLongMm  = Int("classifier:mechanicalMaxLengthLongMm",  o.MechanicalMaxLengthLongMm);
            o.PalletMaxWidthMm           = Int("classifier:palletMaxWidthMm",           o.PalletMaxWidthMm);
            o.PalletMaxHeightMm          = Int("classifier:palletMaxHeightMm",          o.PalletMaxHeightMm);
            o.PalletRatingLowKg          = Int("classifier:palletRatingLowKg",          o.PalletRatingLowKg);
            o.PalletRatingHighKg         = Int("classifier:palletRatingHighKg",         o.PalletRatingHighKg);
            o.MirrorMarginMm             = Int("classifier:mirrorMarginMm",             o.MirrorMarginMm);
            o.LoadMarginKg               = Int("classifier:loadMarginKg",               o.LoadMarginKg);
            o.GarageMaxLengthMm          = Int("classifier:garageMaxLengthMm",          o.GarageMaxLengthMm);
            o.GarageMaxWidthMm           = Int("classifier:garageMaxWidthMm",           o.GarageMaxWidthMm);
            o.GarageMaxHeightMm          = Int("classifier:garageMaxHeightMm",          o.GarageMaxHeightMm);
            o.GarageMaxWeightKg          = Int("classifier:garageMaxWeightKg",          o.GarageMaxWeightKg);
            return o;
        }

        private static int Int(string key, int fallback)
        {
            var raw = ConfigurationManager.AppSettings[key];
            if (raw == null) return fallback;

            int value;
            if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                && value > 0)
                return value;

            return fallback;
        }
    }
}
