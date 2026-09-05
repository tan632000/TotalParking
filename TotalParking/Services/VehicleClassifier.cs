using System.Collections.Generic;
using TotalParking.Models;

namespace TotalParking.Services
{
    // Mức độ khớp giữa hạng tải thẻ đã đăng ký và hạng tải chiếc xe thật sự cần.
    public enum CardCheckResult
    {
        Undetermined,        // chưa phân loại được xe, không kết luận gì
        Ok,                  // thẻ đủ cho chiếc xe này
        CardHigherThanNeeded,// thẻ đăng ký hạng cao hơn mức cần — chỉ ghi nhận
        VehicleExceedsCard   // xe cần hạng cao hơn thẻ — phải chặn
    }

    // Chuyển bốn số đo thô của Camera AI thành hồ sơ năng lực đỗ.
    //
    // Nguyên tắc xuyên suốt (docs/camera-led-routing-design.md §4.4): quyết định
    // bằng số mm/kg thô, không bằng trường category. Category nén bốn số thành
    // một trong sáu chuỗi và chỉ phân biệt được theo chiều dài, nên với khoang
    // giới hạn 1850 mm thì nhãn "Nhỏ" là vô dụng.
    public class VehicleClassifier
    {
        private readonly ClassifierOptions _o;

        public VehicleClassifier() : this(ClassifierOptions.FromConfig()) { }

        public VehicleClassifier(ClassifierOptions options)
        {
            _o = options;
        }

        public VehicleProfile Classify(VehicleEvent e)
        {
            var p = new VehicleProfile { EventId = e.EventId };

            // 1. Từ chối trước, dựa trên những gì đã biết. Một chiếc xe dài 12 m
            //    thì kết luận được ngay kể cả khi chưa biết khối lượng.
            var reasons = new List<string>();
            if (Over(e.LengthMm, _o.GarageMaxLengthMm)) reasons.Add("dài " + e.LengthMm + " mm > " + _o.GarageMaxLengthMm);
            if (Over(e.HeightMm, _o.GarageMaxHeightMm)) reasons.Add("cao " + e.HeightMm + " mm > " + _o.GarageMaxHeightMm);
            if (Over(e.WeightKg, _o.GarageMaxWeightKg)) reasons.Add("nặng " + e.WeightKg + " kg > " + _o.GarageMaxWeightKg);

            int? effectiveWidth = e.WidthMm.HasValue
                                      ? e.WidthMm.Value + _o.MirrorMarginMm
                                      : (int?)null;
            if (Over(effectiveWidth, _o.GarageMaxWidthMm))
                reasons.Add("rộng " + effectiveWidth + " mm (đã tính gương) > " + _o.GarageMaxWidthMm);

            p.EffectiveWidthMm = effectiveWidth;

            if (reasons.Count > 0)
            {
                p.Rejected = true;
                p.RejectReason = "Vượt giới hạn bãi: " + string.Join("; ", reasons);
                p.Lane = LedLane.Normal;
                p.WeightClass = WeightClassCode.Overweight;
                return p;
            }

            // 2. Thiếu bất kỳ số nào trong bốn số thì không được đoán khoang.
            //    Camera gửi 0 và "Unknown" khi không xác định, cả hai đã thành
            //    null lúc tiếp nhận.
            if (!e.LengthMm.HasValue || !e.WidthMm.HasValue
                || !e.HeightMm.HasValue || !e.WeightKg.HasValue)
            {
                p.RequiresManual = true;
                p.ManualReason = "Thiếu số đo: " + string.Join(", ", Missing(e));
                p.Lane = LedLane.Normal;
                p.WeightClass = WeightClassCode.Overweight;
                return p;
            }

            // 3. Khối lượng khi có tải, không dùng thẳng khối lượng bản thân.
            //    Đây là điểm an toàn quan trọng nhất: MPV 7 chỗ kerb 1.900 kg chở
            //    đủ người và hành lý có thể lên 2.400 kg.
            var loaded = e.WeightKg.Value + _o.LoadMarginKg;
            p.EstimatedLoadedWeightKg = loaded;

            // 4. Pallet cơ khí có nhận được chiếc xe này không? Đủ cả bốn chiều
            //    mới được, thiếu một chiều là phải xuống đỗ nền.
            var fitsPallet =
                e.LengthMm.Value      <= _o.MechanicalMaxLengthLongMm &&
                effectiveWidth.Value  <= _o.PalletMaxWidthMm &&
                e.HeightMm.Value      <= _o.PalletMaxHeightMm &&
                loaded                <= _o.PalletRatingHighKg;

            if (!fitsPallet)
            {
                p.Lane = LedLane.Normal;
                p.WeightClass = WeightClassCode.Overweight;
                p.RequiredPalletKg = null;
                return p;
            }

            // 5. Chọn làn theo chiều dài. Dải 4800–5000 mm đi vào làn L>=5M:
            //    khoang dài hơn luôn chứa được xe ngắn hơn, nên đó là phía an toàn.
            //    Vẫn cần xác nhận với các khoang đã thi công — xem §12.8.
            p.Lane = e.LengthMm.Value <= _o.MechanicalMaxLengthShortMm
                         ? LedLane.MechanicalL48M
                         : LedLane.MechanicalL5M;

            if (loaded <= _o.PalletRatingLowKg)
            {
                p.RequiredPalletKg = _o.PalletRatingLowKg;
                p.WeightClass = WeightClassCode.Max2200;
            }
            else
            {
                p.RequiredPalletKg = _o.PalletRatingHighKg;
                p.WeightClass = WeightClassCode.Max2600;
            }

            return p;
        }

        // Đối chiếu chiếc xe camera vừa đo với hạng tải thẻ đã đăng ký.
        // Thẻ 2200KG mà xe cần 2600KG nghĩa là tài xế mang xe khác xe đã đăng ký —
        // thứ phải chặn trước khi pallet nâng lên.
        public static CardCheckResult CheckAgainstCard(VehicleProfile profile, ParkingCard card)
        {
            if (profile == null || card == null) return CardCheckResult.Undetermined;
            if (profile.Rejected || profile.RequiresManual) return CardCheckResult.Undetermined;

            var needed = Rank(profile.WeightClass);
            var granted = Rank(card.WeightClass);
            if (needed == 0 || granted == 0) return CardCheckResult.Undetermined;

            if (granted == needed) return CardCheckResult.Ok;
            return granted > needed
                       ? CardCheckResult.CardHigherThanNeeded
                       : CardCheckResult.VehicleExceedsCard;
        }

        // Thang năng lực: 2200 < 2600 < đỗ nền. Chỗ đỗ nền nhận được mọi xe nằm
        // trong giới hạn bãi nên nó là hạng rộng nhất, không phải hạng thấp nhất.
        private static int Rank(string weightClass)
        {
            if (weightClass == WeightClassCode.Max2200)    return 1;
            if (weightClass == WeightClassCode.Max2600)    return 2;
            if (weightClass == WeightClassCode.Overweight) return 3;
            return 0;
        }

        private static bool Over(int? value, int limit)
        {
            return value.HasValue && value.Value > limit;
        }

        private static IEnumerable<string> Missing(VehicleEvent e)
        {
            if (!e.LengthMm.HasValue) yield return "dài";
            if (!e.WidthMm.HasValue)  yield return "rộng";
            if (!e.HeightMm.HasValue) yield return "cao";
            if (!e.WeightKg.HasValue) yield return "khối lượng";
        }
    }
}
