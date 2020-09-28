using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace launcherProxy
{
    [Serializable]
    class ProxySettings
    {
        public string Password { get; set; }
        public string ProxySourcesPath { get; set; }
        public string ProxySourcesFileName { get; set; }
        public string VaultSecretKey { get; set; }
    }
}
