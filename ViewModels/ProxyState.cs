using launcherProxy.Models;

namespace launcherProxy.ViewModels
{
    internal class ProxyState : BaseInpc
    {
        private bool _proxyIsRunning;
        private bool _vaultIsRunning;
        private bool _isSecretStored;
        private bool _rebuildNeeded;
        private string _mainLog;
        private string _proxyLog;
        private string _vaultLog;

        public bool ProxyIsRunning
        {
            get => _proxyIsRunning;
            set
            {
                _proxyIsRunning = value;
                OnPropertyChanged("ProxyIsRunning");
            }
        }

        public bool VaultIsRunning
        {
            get => _vaultIsRunning;
            set
            {
                _vaultIsRunning = value;
                OnPropertyChanged("VaultIsRunning");
            }
        }

        public bool IsSecretStored
        {
            get => _isSecretStored;
            set
            {
                _isSecretStored = value;
                OnPropertyChanged("IsSecretStored");
            }
        }

        public bool RebuildNeeded
        {
            get => _rebuildNeeded;
            set
            {
                _rebuildNeeded = value;
                OnPropertyChanged("RebuildNeeded");
            }
        }

        public string MainLog
        {
            get => _mainLog;
            set
            {
                _mainLog = value;
                OnPropertyChanged("MainLog");
            }
        }

        public string ProxyLog
        {
            get => _proxyLog;
            set
            {
                _proxyLog = value;
                OnPropertyChanged("ProxyLog");
            }
        }

        public string VaultLog
        {
            get => _vaultLog;
            set
            {
                _vaultLog = value;
                OnPropertyChanged("VaultLog");
            }
        }
    }
}
