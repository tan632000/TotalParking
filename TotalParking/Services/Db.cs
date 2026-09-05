using System.Configuration;

namespace TotalParking.Services
{
    // Điểm duy nhất đọc connection string, để hai repository không lặp lại
    // đoạn tra config kèm xử lý thiếu cấu hình.
    internal static class Db
    {
        private const string ConnectionName = "TotalParkingDb";

        public static string ConnectionString
        {
            get
            {
                var cs = ConfigurationManager.ConnectionStrings[ConnectionName];
                if (cs == null)
                    throw new ConfigurationErrorsException(
                        "Thieu connection string '" + ConnectionName + "' trong Web.config.");
                return cs.ConnectionString;
            }
        }
    }
}
