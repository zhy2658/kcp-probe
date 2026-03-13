using Microsoft.UI.Xaml;
using KcpProbe.ViewModels;

namespace KcpProbe
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; } = new MainViewModel();

        public MainWindow()
        {
            this.InitializeComponent();
            ((FrameworkElement)this.Content).DataContext = ViewModel;
        }

        private void SendPing_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SendPing();
        }

        private void Stress_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ToggleStress();
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ToggleConnect();
        }
    }
}
