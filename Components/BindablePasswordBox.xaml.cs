using System.Windows;
using System.Windows.Data;

namespace launcherProxy.Components
{
    public partial class BindablePasswordBox
    {
        private bool _isPasswordChanging;
        public static readonly DependencyProperty PasswordProperty =
            DependencyProperty.Register("Password", typeof(string), typeof(BindablePasswordBox),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    PasswordPropertyChanged, null, false, UpdateSourceTrigger.PropertyChanged));

        public string Password
        {
            get => (string) GetValue(PasswordProperty);
            set => SetValue(PasswordProperty, value);
        }

        public BindablePasswordBox()
        {
            InitializeComponent();
        }

        private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            _isPasswordChanging = true;
            Password = PasswordBox.Password;
            _isPasswordChanging = false;
        }

        private static void PasswordPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BindablePasswordBox passwordBox)
            {
                passwordBox.UpdatePassword();
            }
        }

        private void UpdatePassword()
        {
            if (_isPasswordChanging) return;
            PasswordBox.Password = Password;
        }
    }
}
