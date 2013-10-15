using System.Reflection;
using System.Configuration.Install;

namespace GacInstaller
{
    class SelfInstaller
    {
        private static readonly string _exePath = Assembly.GetExecutingAssembly().Location;
        public static bool Install()
        {
            try
            {
                ManagedInstallerClass.InstallHelper(new[] { _exePath });
            }
            catch
            {
                return false;
            }
            return true;
        }

        public static bool Uninstall()
        {
            try
            {
                ManagedInstallerClass.InstallHelper(new[] { "/u", _exePath });
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}
