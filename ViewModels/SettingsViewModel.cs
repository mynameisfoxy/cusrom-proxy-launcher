using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Win32;
using launcherProxy.Commands;
using launcherProxy.Models;

namespace launcherProxy.ViewModels
{
    internal class SettingsViewModel : BaseInpc
    {
        public readonly string ProxyResultPath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\CustomProxyLauncher";
        private readonly Settings _settings;

        private RelayCommand _saveSettingsCommand;
        private RelayCommand _loadSettingsCommand;
        private RelayCommand _openProxyMainCommand;

        public SettingsViewModel()
        {
            _settings = new Settings();
        }

        public string Password
        {
            get => _settings.Password;
            set
            {
                _settings.Password = value;
                OnPropertyChanged();
            }
        }

        private string ProxySourcesPath
        {
            get => _settings.ProxySourcesPath;
            set
            {
                _settings.ProxySourcesPath = value;
                OnPropertyChanged();
            }
        }

        public string ProxySourcesFileName
        {
            get => _settings.ProxySourcesFileName;
            private set
            {
                _settings.ProxySourcesFileName = value;
                OnPropertyChanged();
            }
        }

        public string VaultSecretKey
        {
            get => _settings.VaultSecretKey;
            set
            {
                _settings.VaultSecretKey = value;
                OnPropertyChanged();
            }
        }

        public string ProxyAppPath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ProxySourcesPath) || string.IsNullOrWhiteSpace(ProxySourcesFileName))
                {
                    return string.Empty;
                }

                return $"{ProxySourcesPath}/{ProxySourcesFileName}";
            }
        }

        public RelayCommand SaveSettingsCommand => _saveSettingsCommand ?? (_saveSettingsCommand = new RelayCommand(Save));
        public RelayCommand LoadSettingsCommand => _loadSettingsCommand ?? (_loadSettingsCommand = new RelayCommand(Load));
        public RelayCommand OpenProxyMainCommand => _openProxyMainCommand ?? (_openProxyMainCommand = new RelayCommand(OpenProxyMain));

        private void Save(object options = null)
        {
            var formatter = new BinaryFormatter();

            using (var fs = new FileStream($@"{ProxyResultPath}\settings.dat", FileMode.OpenOrCreate))
            {
                formatter.Serialize(fs, _settings);
            }
        }

        private void Load(object options = null)
        {
            var formatter = new BinaryFormatter();
            if (!File.Exists($@"{ProxyResultPath}\settings.dat"))
            {
                using (var fs = new FileStream($@"{ProxyResultPath}\settings.dat", FileMode.OpenOrCreate))
                {
                    formatter.Serialize(fs, _settings);
                }
            }
            try
            {
                using (var fs = new FileStream($@"{ProxyResultPath}\settings.dat", FileMode.OpenOrCreate))
                {
                    if (!(formatter.Deserialize(fs) is Settings settings)) return;
                    Password = settings.Password;
                    ProxySourcesPath = settings.ProxySourcesPath;
                    ProxySourcesFileName = settings.ProxySourcesFileName;
                    VaultSecretKey = settings.VaultSecretKey;
                }
            }
            catch (Exception)
            {
                File.Delete($@"{ProxyResultPath}\settings.dat");
            }
        }

        private void OpenProxyMain(object options = null)
        {
            var fileDialog = new OpenFileDialog
            {
                Filter = "Go files (*.go)|*.go",
                RestoreDirectory = true
            };

            if (!(!fileDialog.ShowDialog() is bool _)) return;
            if (string.IsNullOrEmpty(fileDialog.FileName)) return;
            ProxySourcesFileName = fileDialog.SafeFileName;
            ProxySourcesPath = fileDialog.FileName.Replace(fileDialog.SafeFileName, "");
        }
    }
}
