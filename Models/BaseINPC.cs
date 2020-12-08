using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace launcherProxy.Models
{
    public abstract class BaseInpc : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
