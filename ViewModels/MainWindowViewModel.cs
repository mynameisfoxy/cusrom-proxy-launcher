using launcherProxy.Models;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace launcherProxy.ViewModels
{
    internal class MainWindowViewModel : BaseInpc
    {
        private static readonly string ProxyResultPath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\CustomProxyLauncher";
        
        private readonly Settings _proxySettings = new Settings();

        public MainWindowViewModel()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            var formatter = new BinaryFormatter();
            if (!File.Exists($@"{ProxyResultPath}\settings.dat")) return;
            try
            {
                using (var fs = new FileStream($@"{ProxyResultPath}\settings.dat", FileMode.OpenOrCreate))
                {
                    if (!(formatter.Deserialize(fs) is Settings settings)) return;
                    _proxySettings.Password = settings.Password;
                    _proxySettings.ProxySourcesPath = settings.ProxySourcesPath;
                    _proxySettings.ProxySourcesFileName = settings.ProxySourcesFileName;
                    _proxySettings.VaultSecretKey = settings.VaultSecretKey;

                    //$"{settings.ProxySourcesPath}/{settings.ProxySourcesFileName}";
                }
            }
            catch (Exception)
            {
                File.Delete($@"{ProxyResultPath}\settings.dat");
            }
        }
    }
}
