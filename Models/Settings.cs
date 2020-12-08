using System;

namespace launcherProxy.Models
{
    [Serializable]
    class Settings
    {
        public string Password { get; set; }
        public string ProxySourcesPath { get; set; }
        public string ProxySourcesFileName { get; set; }
        public string VaultSecretKey { get; set; }
    }
}
