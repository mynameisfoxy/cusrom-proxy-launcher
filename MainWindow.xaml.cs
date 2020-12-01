using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace launcherProxy
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //const
        private static readonly string proxyResultPath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\CustomProxyLauncher";
        public const string STARTED = "Started";
        public const string STOPPED = "Not started";

        //events
        private delegate void StatusDelegate(bool next = true);
        private event StatusDelegate SecretAdded;
        private event StatusDelegate ProxyBuilded;
        private event StatusDelegate ProxyMoved;
        private event StatusDelegate KeystoreReady;
        private event StatusDelegate ProxyServerStarted;

        //state
        public bool ProxyIsRunning = false;
        public bool VaultIsRunning = false;
        private string proxyPath = "";
        private string proxyFileName = "";
        private string VaultRootToken = null;
        private string VaultApiAddress = null;
        private bool IsSecretStored = false;
        private bool IsAllSettingsSet => !string.IsNullOrEmpty(VaultRootToken) && !string.IsNullOrEmpty(VaultApiAddress);

        private string VaultSecretKey
        {
            get => string.IsNullOrEmpty(keyValue.Password) ? "" : keyValue.Password;
            set => keyValue.Password = value;
        }

        private string ProxyAppPath => proxyPath ?? $"{proxyPath}/{proxyFileName}";

        public MainWindow()
        {
            InitializeComponent();
            InitializeEventHandlers();
            KillVault();
            KillProxy();
            LoadSettings();
        }

        private void InitializeEventHandlers()
        {
            LogOutput.TextChanged += CleanupLoggingOutput;
            vaultOutput.TextChanged += ParseVaultRootToken;
            proxyOutput.TextChanged += CleanupProxyOutput;
            SecretAdded += ProxyBuild;
            ProxyBuilded += ProxyMoving;
            ProxyMoved += KeystoreGeneration;
            KeystoreReady += StartProxyServer;

            Closing += (s, e) =>
            {
                KillVault();
                KillProxy();
            };

            ManagementEventWatcher startWatcher = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName='proxy.exe' OR ProcessName='vault.exe'"));
            startWatcher.EventArrived += new EventArrivedEventHandler((s, e) =>
            {
                Thread.Sleep(500); //for a clearer reaction when starting the process
                Dispatcher.BeginInvoke(new ThreadStart(delegate
                {
                    if ((string)e.NewEvent.Properties["ProcessName"].Value == "proxy.exe")
                    {
                        statusValue.Content = STARTED;
                        LogOutput.Text += $"\n{DateTime.Now.ToLongTimeString()}: Proxy started.";
                        ProxyIsRunning = true;
                        restartProxyButton.IsEnabled = true;
                    }
                    else if ((string)e.NewEvent.Properties["ProcessName"].Value == "vault.exe")
                    {
                        vaultStatusValue.Content = STARTED;
                        LogOutput.Text += $"\n{DateTime.Now.ToLongTimeString()}: Vault started.";
                        VaultIsRunning = true;
                    }
                }));
            });
            startWatcher.Start();

            ManagementEventWatcher stopWatcher = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace WHERE ProcessName='proxy.exe' OR ProcessName='vault.exe'"));
            stopWatcher.EventArrived += new EventArrivedEventHandler((s, e) =>
            {
                Dispatcher.BeginInvoke(new ThreadStart(delegate
                {
                    if ((string)e.NewEvent.Properties["ProcessName"].Value == "proxy.exe")
                    {
                        statusValue.Content = STOPPED;
                        restartProxyButton.IsEnabled = false;
                        if (ProxyIsRunning)
                        {
                            LogOutput.Text += $"\n{DateTime.Now.ToLongTimeString()}: Proxy crashed. Restarting...";
                            KillProxy();
                            StartProxyServer();
                        }
                        else
                        {
                            LogOutput.Text += $"\n{DateTime.Now.ToLongTimeString()}: Proxy stoped.";
                        }
                    }
                    else if ((string)e.NewEvent.Properties["ProcessName"].Value == "vault.exe")
                    {
                        vaultStatusValue.Content = STOPPED;
                        if (VaultIsRunning)
                        {
                            KillVault();
                            KillProxy();
                            LogOutput.Text += $"\n{DateTime.Now.ToLongTimeString()}: Vault crashed. Restarting...";
                            StartVault();
                        }
                        else
                        {
                            LogOutput.Text += $"\n{DateTime.Now.ToLongTimeString()}: Vault stoped.";
                        }
                    }
                }));
            });
            stopWatcher.Start();
        }

        private void ClearVaultCredentials()
        {
            VaultRootToken = null;
            VaultApiAddress = null;
            IsSecretStored = false;
        }

        private void ClearAllLogs()
        {
            LogOutput.Text = "";
            vaultOutput.Text = "";
            proxyOutput.Text = "";
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
            restartProxyButton.IsEnabled = false;
        }

        private void StartVault()
        {
            using (var process = new Process())
            {
                try
                {
                    KillVault();
                    vaultOutput.Text = "";

                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = "vault",
                        Arguments = "server -dev",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    };

                    process.OutputDataReceived += new DataReceivedEventHandler((se, ee) =>
                    {
                        Dispatcher.BeginInvoke(new ThreadStart(delegate
                        {
                            vaultOutput.Text += $"\n{ee.Data}";
                        }));
                    });

                    process.Start();
                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();
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
            };
        }

        private async void ParseVaultRootToken(object sender, TextChangedEventArgs e)
        {
            if (vaultOutput.Text.Contains("Root Token:") && string.IsNullOrEmpty(VaultRootToken))
            {
                VaultRootToken = vaultOutput.Text.Substring(vaultOutput.Text.IndexOf("Root Token:") + 12, 26);
            }

            if (vaultOutput.Text.Contains("Api Address:") && string.IsNullOrEmpty(VaultApiAddress))
            {
                VaultApiAddress = vaultOutput.Text.Substring(vaultOutput.Text.IndexOf("Api Address:") + 13, 21);
            }

            if (IsAllSettingsSet && !IsSecretStored)
            {
                await PutSecretToVault();
            }
        }

        private void CleanupProxyOutput(object sender, TextChangedEventArgs e)
        {
            if (proxyOutput.Text.Length > 500)
            {
                proxyOutput.Text = proxyOutput.Text.Substring(25);
            }
        }

        private void CleanupLoggingOutput(object sender, TextChangedEventArgs e)
        {
            if (LogOutput.Text.Length > 500)
            {
                LogOutput.Text = LogOutput.Text.Substring(25);
            }
        }

        private async Task PutSecretToVault(bool next = true)
        {
            var payload = JsonConvert.SerializeObject(new
            {
                data = new { encode_key = $"{VaultSecretKey}" },
                options = new { cas = 0 },
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
                    if (rebuildNeeded.IsChecked != null && (bool)rebuildNeeded.IsChecked)
                    {
                        if (next) SecretAdded?.Invoke();
                    }
                    else
                    {
                        if (next) ProxyMoved?.Invoke();
                    }
                }
            }
        }

        private void ProxyBuild(bool next = true)
        {
            try
            {
                if (File.Exists($@"{proxyPath}proxy.exe"))
                {
                    File.Delete($@"{proxyPath}proxy.exe");
                }

                using (var process = new Process())
                {
                    var path = proxyPath.Replace(proxyFileName, "");
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = "go",
                        WorkingDirectory = path,
                        Arguments = $"build -o {ProxyAppPath}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };

                    process.Start();
                    process.WaitForExit();
                    if (next) ProxyBuilded?.Invoke();
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
                if (File.Exists($@"{proxyResultPath}\proxy.exe"))
                {
                    File.Delete($@"{proxyResultPath}\proxy.exe");
                }

                if (File.Exists($"{proxyPath}proxy.exe"))
                {
                    File.Move($@"{proxyPath}proxy.exe", $@"{proxyResultPath}\proxy.exe");
                    if (next) ProxyMoved?.Invoke();
                }
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
                    var path = proxyPath.Replace(proxyFileName, "");
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = $@"{proxyResultPath}\proxy.exe",
                        WorkingDirectory = proxyResultPath,
                        Arguments = $"-password=\"{userPasswordValue.Password}\" -mode=\"file\" -vault=\"{VaultApiAddress}\" -token=\"{VaultRootToken}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };

                    process.Start();
                    process.WaitForExit();
                    if (next) KeystoreReady?.Invoke();
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
                proxyOutput.Text = "";

                using (var process = new Process())
                {
                    var path = proxyPath.Replace(proxyFileName, "");
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = $@"{proxyResultPath}\proxy.exe",
                        WorkingDirectory = proxyResultPath,
                        Arguments = $"-password=\"{userPasswordValue.Password}\" -mode=\"server\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    };

                    process.OutputDataReceived += (se, ee) =>
                    {
                        Dispatcher.BeginInvoke(new ThreadStart(delegate
                        {
                            proxyOutput.Text += $"\n{ee.Data}";
                        }));
                    };

                    process.Exited += (s, e) =>
                    {
                        MessageBox.Show("Proxy crashed");
                    };

                    process.Start();
                    if (next) ProxyServerStarted?.Invoke();
                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();
                }
            }
            catch (Exception exp)
            {
                MessageBox.Show(exp.Message);
            }
        }

        /* method, which creates file selector */
        private void OpenProxyMain(object sender, RoutedEventArgs e)
        {
            var fileDialog = new OpenFileDialog
            {
                Filter = "go files (*.go)|*.go",
                RestoreDirectory = true
            };

            if (!(bool) fileDialog.ShowDialog()) return;
            if (string.IsNullOrEmpty(fileDialog.FileName)) return;
            proxyFileName = fileDialog.SafeFileName;
            proxyPath = fileDialog.FileName.Replace(proxyFileName, "");
            proxyPathLabel.Content = fileDialog.FileName;
        }

        private void SaveSettings(object sender, RoutedEventArgs e)
        {
            var formatter = new BinaryFormatter();
            var settings = new ProxySettings
            {
                Password = userPasswordValue.Password,
                ProxySourcesPath = proxyPath,
                ProxySourcesFileName = proxyFileName,
                VaultSecretKey = keyValue.Password,
            };
            
            using (var fs = new FileStream($@"{proxyResultPath}\settings.dat", FileMode.OpenOrCreate))
            {
                formatter.Serialize(fs, settings);
            }
        }

        private void LoadSettings()
        {
            var formatter = new BinaryFormatter();
            if (!File.Exists($@"{proxyResultPath}\settings.dat")) return;
            using (var fs = new FileStream($@"{proxyResultPath}\settings.dat", FileMode.OpenOrCreate))
            {
                var settings = (ProxySettings)formatter.Deserialize(fs);

                userPasswordValue.Password = settings.Password;
                proxyPath = settings.ProxySourcesPath;
                proxyFileName = settings.ProxySourcesFileName;
                proxyPathLabel.Content = ProxyAppPath;
                VaultSecretKey = settings.VaultSecretKey;
            }
        }

        private void StartProxyButtonClick(object sender, RoutedEventArgs e)
        {
            // starts event chain from vault starting process
            if (!VaultIsRunning)
            {
                StartVault();
                startProxy.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                startProxy.Content = "Stop proxy";
            }
            else
            {
                KillVault();
                KillProxy();
                LogOutput.Text += $"\n{DateTime.Now.ToLongTimeString()}: Vault and proxy stoped by user.";
                startProxy.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                startProxy.Content = "Start proxy";
            }
        }

        private void RestartProxy(object sender, RoutedEventArgs e)
        {
            KillProxy();
            if (rebuildNeeded.IsChecked == true)
            {
                ProxyBuild();
            }
            else
            {
                StartProxyServer();
            }
        }
    }
}
