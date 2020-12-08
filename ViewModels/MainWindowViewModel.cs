using launcherProxy.Commands;
using launcherProxy.Models;

namespace launcherProxy.ViewModels
{
    internal class MainWindowViewModel : BaseInpc
    {
        private RelayCommand _closeWindowCommand;
        public ProxyViewModel Proxy { get; set; } = new ProxyViewModel();

        public RelayCommand CloseWindowCommand => _closeWindowCommand ?? (_closeWindowCommand = new RelayCommand(CloseWindow));

        private void CloseWindow(object options = null)
        {
            Proxy.StopAllCommand.Execute();
        }
    }
}
