using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Management;
using Newtonsoft.Json;
using launcherProxy.Models;
using launcherProxy.Commands;

namespace launcherProxy.ViewModels
{
    internal class ProxyViewModel : BaseInpc
    {
        private bool _proxyIsRunning;
        private bool _vaultIsRunning;
        private bool _isSecretStored;
        private bool _rebuildNeeded;
        private string _startButtonTitle = "Start proxy";
        private string _vaultApiAddress;
        private string _vaultRootToken;
        private string _mainLog;
        private string _proxyLog;
        private string _vaultLog;

        private RelayCommand _startProxyCommand;
        private RelayCommand _closeAllCommand;

        private bool IsAllSettingsSet =>
            !string.IsNullOrEmpty(VaultRootToken) && !string.IsNullOrEmpty(VaultApiAddress);

        public SettingsViewModel Settings { get; set; } = new SettingsViewModel();

        public RelayCommand StartProxyCommand
        {
            get => _startProxyCommand ?? (_startProxyCommand = new RelayCommand(StartVault));
            set
            {
                _startProxyCommand = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand StopAllCommand => _closeAllCommand ?? (_closeAllCommand = new RelayCommand(StopAll));

        public ProxyViewModel()
        {
            KillVault();
            KillProxy();
            Settings.LoadSettingsCommand.Execute();
            StartWatchers();
        }

        public string StartButtonTitle
        {
            get => _startButtonTitle;
            set
            {
                _startButtonTitle = value;
                OnPropertyChanged();
            }
        }

        public bool ProxyIsRunning
        {
            get => _proxyIsRunning;
            set
            {
                _proxyIsRunning = value;
                OnPropertyChanged();
            }
        }

        public bool VaultIsRunning
        {
            get => _vaultIsRunning;
            set
            {
                _vaultIsRunning = value;
                OnPropertyChanged();
            }
        }

        private bool IsSecretStored
        {
            get => _isSecretStored;
            set
            {
                _isSecretStored = value;
                OnPropertyChanged();
            }
        }

        private string VaultRootToken
        {
            get => _vaultRootToken;
            set
            {
                _vaultRootToken = value;
                OnPropertyChanged();
            }
        }

        private string VaultApiAddress
        {
            get => _vaultApiAddress;
            set
            {
                _vaultApiAddress = value;
                OnPropertyChanged();
            }
        }

        public bool RebuildNeeded
        {
            get => _rebuildNeeded;
            set
            {
                _rebuildNeeded = value;
                OnPropertyChanged();
            }
        }

        public string MainLog
        {
            get => _mainLog;
            set
            {
                _mainLog = value;
                if (MainLog.Length >= 500)
                {
                    _mainLog = _mainLog.Substring(25);
                }

                OnPropertyChanged();
            }
        }

        public string ProxyLog
        {
            get => _proxyLog;
            set
            {
                _proxyLog = value;
                if (ProxyLog.Length >= 500)
                {
                    _proxyLog = _proxyLog.Substring(25);
                }

                OnPropertyChanged();
            }
        }

        public string VaultLog
        {
            get => _vaultLog;
            set
            {
                _vaultLog = value;
                OnPropertyChanged();
                ParseVaultRootToken();
            }
        }

        private void KillVault()
        {
            ClearVaultCredentials();
            ClearAllLogs();
            var processIsRunning = Process.GetProcessesByName("vault");
            VaultIsRunning = false;
            foreach (var process in processIsRunning)
            {
                process.Kill();
            }
        }

        private void KillProxy()
        {
            var processIsRunning = Process.GetProcessesByName("proxy");
            ProxyIsRunning = false;
            foreach (var process in processIsRunning)
            {
                process.Kill();
            }
        }

        private void ClearVaultCredentials()
        {
            VaultRootToken = null;
            VaultApiAddress = null;
            IsSecretStored = false;
        }

        private void ClearAllLogs()
        {
            MainLog = string.Empty;
            VaultLog = string.Empty;
            ProxyLog = string.Empty;
        }

        private void StartVault(object options = null)
        {
            using (var process = new Process())
            {
                try
                {
                    KillVault();
                    VaultLog = "";

                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = "vault",
                        Arguments = "server -dev",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    };

                    process.OutputDataReceived += (se, ee) => { VaultLog += $"\n{ee.Data}"; };

                    process.Start();
                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();
                    StartProxyCommand = new RelayCommand(StopAll);
                    StartButtonTitle = "Stop proxy";
                }
                catch
                {
                    ClearVaultCredentials();
                    if (MessageBox.Show("Seems you've no vault installed. " +
                                        "Please visit vault website to download latest version.",
                        "Vault", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes)
                    {
                        Process.Start("https://www.vaultproject.io/downloads");
                    }
                }
            }

            ;
        }

        private void StartWatchers()
        {
            var startWatcher = new ManagementEventWatcher(
                new WqlEventQuery(
                    "SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName='proxy.exe' OR ProcessName='vault.exe'"));
            startWatcher.EventArrived += (s, e) =>
            {
                Thread.Sleep(500); //for a clearer reaction when starting the process
                switch ((string) e.NewEvent.Properties["ProcessName"].Value)
                {
                    case "proxy.exe":
                        ProxyIsRunning = true;
                        MainLog += $"\n{DateTime.Now.ToLongTimeString()}: Proxy started.";
                        ProxyIsRunning = true;
                        break;
                    case "vault.exe":
                        VaultIsRunning = true;
                        MainLog += $"\n{DateTime.Now.ToLongTimeString()}: Vault started.";
                        VaultIsRunning = true;
                        break;
                }
            };
            startWatcher.Start();

            var stopWatcher = new ManagementEventWatcher(
                new WqlEventQuery(
                    "SELECT * FROM Win32_ProcessStopTrace WHERE ProcessName='proxy.exe' OR ProcessName='vault.exe'"));
            stopWatcher.EventArrived += (s, e) =>
            {
                switch ((string) e.NewEvent.Properties["ProcessName"].Value)
                {
                    case "proxy.exe":
                    {
                        ProxyIsRunning = false;
                        if (ProxyIsRunning)
                        {
                            MainLog += $"\n{DateTime.Now.ToLongTimeString()}: Proxy crashed. Restarting...";
                            KillProxy();
                            StartProxyServer();
                        }
                        else
                        {
                            MainLog += $"\n{DateTime.Now.ToLongTimeString()}: Proxy stoped.";
                        }

                        break;
                    }
                    case "vault.exe":
                    {
                        VaultIsRunning = false;
                        if (VaultIsRunning)
                        {
                            KillVault();
                            KillProxy();
                            MainLog += $"\n{DateTime.Now.ToLongTimeString()}: Vault crashed. Restarting...";
                            StartVault();
                        }
                        else
                        {
                            MainLog += $"\n{DateTime.Now.ToLongTimeString()}: Vault stoped.";
                        }

                        break;
                    }
                }
            };
            stopWatcher.Start();
        }

        private async void ParseVaultRootToken()
        {
            if (VaultLog.Contains("Root Token:") && string.IsNullOrEmpty(VaultRootToken))
            {
                VaultRootToken = VaultLog.Substring(VaultLog.IndexOf("Root Token:", StringComparison.Ordinal) + 12, 26);
            }

            if (VaultLog.Contains("Api Address:") && string.IsNullOrEmpty(VaultApiAddress))
            {
                VaultApiAddress =
                    VaultLog.Substring(VaultLog.IndexOf("Api Address:", StringComparison.Ordinal) + 13, 21);
            }

            if (IsAllSettingsSet && !IsSecretStored)
            {
                await PutSecretToVault();
            }
        }

        private async Task PutSecretToVault(bool next = true)
        {
            var payload = JsonConvert.SerializeObject(new
            {
                data = new {encode_key = $"{Settings.VaultSecretKey}"},
                options = new {cas = 0},
            });

            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri(VaultApiAddress);
                httpClient.DefaultRequestHeaders.Add("X-Vault-Token", VaultRootToken);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var request = new HttpRequestMessage(HttpMethod.Post, "/v1/secret/data/secret_key")
                {
                    Method = HttpMethod.Post,
                    Content = new StringContent(payload, Encoding.UTF8, "application/json"),
                };

                var response = await httpClient.SendAsync(request);

                if (response != null && response.StatusCode == HttpStatusCode.OK)
                {
                    IsSecretStored = true;
                    if (RebuildNeeded)
                    {
                        if (next) ProxyBuild();
                    }
                    else
                    {
                        if (next) KeystoreGeneration();
                    }
                }
            }
        }

        private void ProxyBuild(bool next = true)
        {
            try
            {
                if (File.Exists($@"{Settings.ProxyAppPath}proxy.exe"))
                {
                    File.Delete($@"{Settings.ProxyAppPath}proxy.exe");
                }

                using (var process = new Process())
                {
                    var path = Settings.ProxyAppPath.Replace(Settings.ProxySourcesFileName, "");
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = "go",
                        WorkingDirectory = path,
                        Arguments = $"build -o {Settings.ProxyAppPath}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };

                    process.Start();
                    process.WaitForExit();
                    if (next) ProxyMoving();
                }
            }
            catch (Exception exp)
            {
                MessageBox.Show(exp.Message);
            }
        }

        private void ProxyMoving(bool next = true)
        {
            try
            {
                if (File.Exists($@"{Settings.ProxyResultPath}\proxy.exe"))
                {
                    File.Delete($@"{Settings.ProxyResultPath}\proxy.exe");
                }

                if (!File.Exists($"{Settings.ProxyAppPath}proxy.exe")) return;
                File.Move($@"{Settings.ProxyAppPath}proxy.exe", $@"{Settings.ProxyResultPath}\proxy.exe");
                if (next) KeystoreGeneration();
            }
            catch (Exception exp)
            {
                MessageBox.Show(exp.Message);
            }
        }

        private void KeystoreGeneration(bool next = true)
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = $@"{Settings.ProxyResultPath}\proxy.exe",
                        WorkingDirectory = Settings.ProxyResultPath,
                        Arguments =
                            $"-password=\"{Settings.Password}\" -mode=\"file\" -vault=\"{VaultApiAddress}\" -token=\"{VaultRootToken}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };

                    process.Start();
                    process.WaitForExit();
                    if (next) StartProxyServer();
                }
            }
            catch (Exception exp)
            {
                MessageBox.Show(exp.Message);
            }
        }

        private void StartProxyServer(bool next = true)
        {
            try
            {
                ProxyLog = string.Empty;

                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = $@"{Settings.ProxyResultPath}\proxy.exe",
                        WorkingDirectory = Settings.ProxyResultPath,
                        Arguments = $"-password=\"{Settings.Password}\" -mode=\"server\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    };

                    process.OutputDataReceived += (se, ee) => { ProxyLog += $"\n{ee.Data}"; };

                    process.Exited += (s, e) => { MessageBox.Show("Proxy crashed"); };

                    process.Start();
                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();
                }
            }
            catch (Exception exp)
            {
                MessageBox.Show(exp.Message);
            }
        }

        private void StopAll(object options = null)
        {
            KillVault();
            KillProxy();
            StartButtonTitle = "Start proxy";
            StartProxyCommand = new RelayCommand(StartVault);
        }
    }
}