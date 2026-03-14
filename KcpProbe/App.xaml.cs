using KcpProbe.Infrastructure;
using Microsoft.UI.Xaml;

namespace KcpProbe
{
    public partial class App : Application
    {
        private Window? _window;
        private readonly AppServiceProvider _services;

        public App()
        {
            InitializeComponent();
            _services = new AppServiceProvider();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow(_services.CreateMainViewModel());
            _window.Activate();
        }
    }
}
