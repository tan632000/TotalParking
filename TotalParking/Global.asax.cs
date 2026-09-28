using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using TotalParking.Services.Led;
using TotalParking.Services.Pgs;
using TotalParking.Services.Plc;

namespace TotalParking
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Nạp cấu hình PLC. Vòng poll chỉ chạy khi plc:enabled = true, còn
            // công cụ nghiệm thu /PlcStatus/Read thì dùng được ngay cả khi tắt.
            // Xem Services/Plc/PlcHost.cs.
            PlcHost.Initialize();

            // Tầng LED. Cùng khuôn: luôn nạp danh mục bảng, chỉ chạy vòng đẩy
            // khi led:enabled. Khi tắt có trật tự, LedHost xoá bảng về trạng
            // thái trống — board giữ nội dung cũ vĩnh viễn và không có watchdog.
            LedHost.Initialize();

            // Tầng cảm biến đỗ thường (PGS/ZCU). CHỈ ĐỌC — không gửi byte nào
            // xuống thiết bị, nên không có công tắc "cho phép ghi tay".
            //
            // Chưa nối vào số trên bảng LED: còn thiếu bảng ánh xạ bit -> ô đỗ.
            // Giai đoạn này đọc, lọc nhiễu và phơi ra /PgsStatus.
            PgsHost.Initialize();

            // Ghi trang thai tung cam bien do thuong xuong pgs_sensor_state.
            // Chay SAU PgsHost vi no chup anh tu ket noi CCU o do. Truoc day
            // khong co buoc nay, nen so lieu do thuong chi ton tai trong bo nho
            // va mat sach moi lan khoi dong lai.
            PgsSensorStateHost.Initialize();

            // Canh bao khi mot cam bien do thuong bao loi. Chay SAU vong ghi o
            // tren vi no doc chinh pgs_sensor_state.
            //
            // Vi sao can: ngay 28/09/2026 bon cam bien cua ZCU 0 lo 1 bao loi
            // 12 va 22 tieng ma khong ai duoc bao — truoc do khong co ma loi nao
            // cho cam bien. O loi khong duoc tinh la trong nen bang LED bao
            // THIEU cho trong, tuc sai theo chieu an toan va khong tu lo ra.
            CanhBaoCamBienService.Start();

            // Vòng quét ô đỗ: đọc D400/D202…D308 để biết ô nào đang giữ thẻ nào.
            // Công tắc riêng (plc:slotScanEnabled) vì vòng này CHỈ ĐỌC, trong khi
            // vòng poll của PlcHost có ghi D1000. Gộp chung sẽ buộc phải bật quyền
            // ghi mới có dữ liệu chiếm chỗ — thứ mà bảng LED và chức năng tìm xe
            // cần trước tiên. Chạy SAU PlcHost vì nó dùng danh sách kết nối ở đó.
            SlotScanHost.Initialize();
        }
    }
}
