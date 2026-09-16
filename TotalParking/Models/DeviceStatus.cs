using System;
using System.Collections.Generic;

namespace TotalParking.Models
{
    // Trạng thái sống của một nhóm thiết bị ngoại vi, cho khối "Trạng thái truyền
    // thông & Thiết bị ngoại vi" ở trang Điều hướng.
    //
    // Monitored là trường quan trọng nhất ở đây. Bản cũ của khối này ghi cứng
    // "Hoạt động" cho cả máy phát thẻ — thứ hệ thống KHÔNG có đường nào đọc được.
    // Trong buổi nghiệm thu nó trông y như giám sát trực tiếp.
    //
    // "Không giám sát được" và "giám sát được, đang hỏng" là hai sự thật khác
    // nhau, và chỉ một trong hai đáng để kỹ thuật viên chạy xuống hầm kiểm tra.
    public class DeviceGroupStatus
    {
        public string Key  { get; set; }
        public string Name { get; set; }

        // false = chưa có nguồn dữ liệu nào để biết thiết bị này sống hay chết.
        // Giao diện phải hiện màu xám "chưa giám sát", KHÔNG được hiện xanh.
        public bool Monitored { get; set; }

        public int Total  { get; set; }
        public int Online { get; set; }

        // Thời điểm có tín hiệu gần nhất. Với camera là lúc nhận sự kiện cuối,
        // với thiết bị mạng là lúc thăm dò xong.
        public DateTime? LastSeen { get; set; }

        // Giám sát được, nhưng tín hiệu KHÔNG ĐỦ để kết luận sống hay chết.
        //
        // Camera AI là ca điển hình: nó không có heartbeat, chỉ gửi khi có xe.
        // Im lặng lúc 11 giờ đêm có thể là camera chết, cũng có thể chỉ là không
        // có xe nào vào — và hai khả năng đó không phân biệt được từ dữ liệu.
        // Báo đỏ "hỏng" trong trường hợp này là báo động giả; đỏ phải để dành cho
        // thứ ta biết CHẮC là chết.
        public bool Inconclusive { get; set; }

        public string Detail { get; set; }
        // Địa chỉ không phản hồi, để kỹ thuật viên biết đi đâu mà tìm.
        public IList<string> Offline { get; set; }

        public DeviceGroupStatus()
        {
            Offline = new List<string>();
        }

        // Ba mức, không phải hai: chưa giám sát / đủ / thiếu một phần / hỏng hết.
        // Gộp "thiếu một phần" vào "hỏng" sẽ báo động giả khi bãi đang lắp dở,
        // gộp vào "đủ" thì giấu mất thiết bị chết.
        public string Health
        {
            get
            {
                if (!Monitored)      return "unmonitored";
                if (Total == 0)      return "empty";
                if (Inconclusive)    return "unknown";
                if (Online == 0)     return "down";
                if (Online < Total)  return "partial";
                return "ok";
            }
        }
    }
}
