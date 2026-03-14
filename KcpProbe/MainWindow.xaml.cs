using Microsoft.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using Windows.ApplicationModel.DataTransfer;
using KcpProbe.ViewModels;
using System.IO;
using System;
using System.Linq;
using Windows.ApplicationModel;
using System.Reflection;

using KcpProbe.Models;

namespace KcpProbe
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }
        private AppWindow? _appWindow;
        private OverlappedPresenter? _presenter;

        public MainWindow(MainViewModel viewModel)
        {
            ViewModel = viewModel;
            InitializeComponent();
            ((FrameworkElement)this.Content).DataContext = ViewModel;

            var windowId = Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
            _appWindow = AppWindow.GetFromWindowId(windowId);
            if (_appWindow != null)
            {
                _appWindow.Resize(new SizeInt32(1256, 999));
                var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
                if (File.Exists(iconPath))
                {
                    _appWindow.SetIcon(iconPath);
                }
                _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                SetTitleBar(AppTitleBar);
                UpdateTitleBarInsets();
                _presenter = _appWindow.Presenter as OverlappedPresenter;
            }

            ViewModel.RttUpdated += OnRttUpdated;

            if (Content is FrameworkElement root)
            {
                ThemeIcon.Symbol = root.RequestedTheme == ElementTheme.Dark ? Symbol.Target : Symbol.Favorite;
            }

            AppVersionText.Text = $"v{GetAppVersion()}";
        }

        private string GetAppVersion()
        {
            try
            {
                var version = Package.Current.Id.Version;
                return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
            catch
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return version?.ToString() ?? "1.0.0";
            }
        }

        private void OnRttUpdated(double rtt)
        {
            RttChart.AddSample(rtt);
        }

        private void UpdateTitleBarInsets()
        {
            if (_appWindow == null)
            {
                return;
            }

            LeftPaddingColumn.Width = new GridLength(_appWindow.TitleBar.LeftInset);
            RightPaddingColumn.Width = new GridLength(_appWindow.TitleBar.RightInset);
        }

        private void PinTopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_presenter == null)
            {
                return;
            }

            _presenter.IsAlwaysOnTop = !_presenter.IsAlwaysOnTop;
            PinTopIcon.Symbol = _presenter.IsAlwaysOnTop ? Symbol.UnPin : Symbol.Pin;
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            if (Content is FrameworkElement rootElement)
            {
                if (rootElement.RequestedTheme == ElementTheme.Default)
                {
                    rootElement.RequestedTheme = ElementTheme.Light;
                }
                else
                {
                    rootElement.RequestedTheme = rootElement.RequestedTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
                }

                if (ThemeButton.Content is SymbolIcon icon)
                {
                    icon.Symbol = rootElement.RequestedTheme == ElementTheme.Dark ? Symbol.Target : Symbol.Favorite;
                }
                else if (ThemeIcon != null)
                {
                    ThemeIcon.Symbol = rootElement.RequestedTheme == ElementTheme.Dark ? Symbol.Target : Symbol.Favorite;
                }
            }
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Settings",
                Content = new TextBlock { Text = "Global settings configuration will be available in future updates." },
                CloseButtonText = "Close",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var versionStr = GetAppVersion();
            var dialog = new ContentDialog
            {
                Title = "Help",
                Content = new TextBlock { Text = $"KCP Tester v{versionStr}\n\n1. Configure IP/Port/Conv ID.\n2. Click Connect.\n3. Use Interface Test for single packets.\n4. Use Stress Test for load testing.\n5. Use Bot Swarm for multi-client simulation." },
                CloseButtonText = "Got it",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private void CopySelectedLogs_Click(object sender, RoutedEventArgs e)
        {
            var selected = LogsListView.SelectedItems.OfType<LogEntry>().Select(l => l.Message).ToList();
            if (selected.Count == 0)
            {
                return;
            }

            var package = new DataPackage();
            package.SetText(string.Join(Environment.NewLine, selected));
            Clipboard.SetContent(package);
        }

        private void CopyAllLogs_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.LogsView.Count == 0)
            {
                return;
            }

            var logs = ViewModel.LogsView.OfType<LogEntry>().Select(l => l.Message);
            var package = new DataPackage();
            package.SetText(string.Join(Environment.NewLine, logs));
            Clipboard.SetContent(package);
        }
    }
}
