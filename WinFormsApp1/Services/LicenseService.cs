using System.Net.NetworkInformation;

namespace WinFormsApp1.Services
{
    internal class LicenseService
    {
        private static readonly HashSet<string> AllowedMacAddresses = new(StringComparer.OrdinalIgnoreCase)
        {
            "2C-F0-5D-B5-7C-EE", // IFEZ PC 1
            "2C-F0-5D-B5-7C-71", // IFEZ PC 2
            "F0-2F-74-32-33-77", // 스타트업파크 PC1
            "D8-5E-D3-94-B4-F5", // AIT PC1
            "10-FF-E0-6F-51-45", // AIT PC2
            "E8-84-A5-72-9F-7A" // 신규 1
        };

        public static bool IsAuthorized(out string denyReason)
        {
            var macAddresses = GetMacAddresses();
            if (!macAddresses.Any(mac => AllowedMacAddresses.Contains(mac)))
            {
                denyReason = "등록되지 않은 PC입니다.\n관리자에게 문의해 주세요.\n\n";
                return false;
            }

            denyReason = string.Empty;
            return true;
        }

        private static List<string> GetMacAddresses()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                           && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(nic => nic.GetPhysicalAddress().ToString())
                .Where(mac => !string.IsNullOrEmpty(mac))
                .Select(mac => string.Join("-", Enumerable.Range(0, mac.Length / 2)
                    .Select(i => mac.Substring(i * 2, 2))))
                .ToList();
        }
    }
}
