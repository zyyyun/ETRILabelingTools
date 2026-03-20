using System.Net.NetworkInformation;

namespace WinFormsApp1.Services
{
    internal class LicenseService
    {
        private static readonly DateTime ExpirationDate = new DateTime(2026, 5, 31);

        private static readonly HashSet<string> AllowedMacAddresses = new(StringComparer.OrdinalIgnoreCase)
        {
            "2C-F0-5D-B5-7C-EE", // IFEZ PC 1
            "2C-F0-5D-B5-7C-71", // IFEZ PC 2
            "F0-2F-74-32-33-77", // 본인 PC
            "D8-5E-D3-94-B4-F5", // 박연구원 PC
            "10-FF-E0-6F-51-45",
        };

        private bool _isExpired;
        private bool _isMacAllowed;

        public LicenseService()
        {
            _isExpired = DateTime.Now.Date > ExpirationDate;
            _isMacAllowed = GetLocalMacAddresses().Any(mac => AllowedMacAddresses.Contains(mac));
        }

        public bool IsAuthorized() => !_isExpired && _isMacAllowed;

        public string GetDenyReason()
        {
            if (_isExpired)
                return "데모 사용 기간이 만료되었습니다. (만료일: 2026-05-31)";
            if (!_isMacAllowed)
                return "등록되지 않은 PC입니다. 관리자에게 문의해 주세요.";
            return string.Empty;
        }

        private static IEnumerable<string> GetLocalMacAddresses()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                           && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(nic => nic.GetPhysicalAddress().ToString())
                .Where(mac => !string.IsNullOrEmpty(mac))
                .Select(mac => string.Join("-", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2))));
        }
    }
}
