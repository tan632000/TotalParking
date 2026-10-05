using System;
using System.Threading.Tasks;

namespace TotalParking.Services.Plc
{
    // Phần chung của client FINS/TCP và FINS/UDP mà PlcConnection dùng.
    //
    // Hai transport khác nhau ở cách giữ kết nối, KHÔNG khác ở khung lệnh: cùng
    // MRC/SRC, cùng mã vùng nhớ, cùng End Code. Nên PlcConnection chỉ cần biết
    // interface này, và việc chọn transport là một dòng lúc dựng client.
    //
    // Hợp đồng lỗi giống nhau ở cả hai:
    //   FinsFramingException  kết nối không còn tin được -> phía gọi đóng và dựng lại
    //   FinsException         PLC trả End Code lỗi -> kết nối vẫn tốt, lệnh sai
    public interface IFinsClient : IDisposable
    {
        bool IsConnected { get; }

        // "tcp" hoặc "udp" — để /PlcStatus cho người vận hành biết block nào
        // đang chạy đường nào trong lúc thí điểm.
        string Transport { get; }

        Task ConnectAsync(string ipAddress, int port, int timeoutMs);

        Task<ushort[]> ReadWordsAsync(PlcMemoryArea area, ushort startAddress, ushort count, int timeoutMs);

        Task WriteWordsAsync(PlcMemoryArea area, ushort startAddress, ushort[] data, int timeoutMs);

        Task<bool> GetBitStateAsync(PlcMemoryArea area, string bitAddress, int timeoutMs);

        Task SetBitStateAsync(PlcMemoryArea area, string bitAddress, BitState state, int timeoutMs);

        void Close();
    }
}
