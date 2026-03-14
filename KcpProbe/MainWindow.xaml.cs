using Microsoft.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;
using Windows.ApplicationModel.DataTransfer;
using KcpProbe.ViewModels;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;
using Windows.ApplicationModel;
using System.Reflection;

using KcpProbe.Models;

namespace KcpProbe
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; } = new MainViewModel();
        private AppWindow? _appWindow;
        private OverlappedPresenter? _presenter;
        private bool _isDraggingLogPanel;
        private double _dragStartY;
        private double _contentStartHeight;
        private double _logStartHeight;

        private Polyline _rttLine;
        private List<double> _rttHistory = new List<double>();
        private const int MaxHistory = 100;

        public MainWindow()
        {
            this.InitializeComponent();
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

            // Init Chart
            _rttLine = new Polyline
            {
                Stroke = new SolidColorBrush(Colors.LightGreen),
                StrokeThickness = 2
            };
            RttCanvas.Children.Add(_rttLine);
            
            ViewModel.RttUpdated += OnRttUpdated;
            
            // Set initial theme icon
            if (Content is FrameworkElement root)
            {
                // Use Target for Light, Favorite for Dark
                ThemeIcon.Symbol = root.RequestedTheme == ElementTheme.Dark ? Symbol.Target : Symbol.Favorite;
            }

            // Set Version (Safe for Unpackaged)
            AppVersionText.Text = $"v{GetAppVersion()}";
        }
        
        private string GetAppVersion()
        {
            try 
            {
                // Try to get from Package first (if packaged)
                var version = Package.Current.Id.Version;
                return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
            catch
            {
                // Fallback to Assembly version (for Unpackaged)
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return version?.ToString() ?? "1.0.0";
            }
        }

        private void OnRttUpdated(double rtt)
        {
            _rttHistory.Add(rtt);
            if (_rttHistory.Count > MaxHistory) _rttHistory.RemoveAt(0);
            
            CurrentRttText.Text = $"{rtt:F0} ms";
            UpdateChart();
        }

        private void RttCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateChart();
        }

        private void UpdateChart()
        {
            if (RttCanvas.ActualWidth == 0 || RttCanvas.ActualHeight == 0) return;
            
            var width = RttCanvas.ActualWidth;
            var height = RttCanvas.ActualHeight;
            var step = width / MaxHistory;
            
            var points = new PointCollection();
            double maxRtt = 100; // default min scale
            foreach (var val in _rttHistory) if (val > maxRtt) maxRtt = val;
            
            for (int i = 0; i < _rttHistory.Count; i++)
            {
                var x = i * step;
                var y = height - (_rttHistory[i] / maxRtt * height);
                // Clamp y
                if (y < 0) y = 0;
                if (y > height) y = height;
                
                points.Add(new Windows.Foundation.Point(x, y));
            }
            
            _rttLine.Points = points;
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

        private void LogResizeHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingLogPanel = true;
            _dragStartY = e.GetCurrentPoint(this.Content as UIElement).Position.Y;
            _contentStartHeight = MainContentRow.ActualHeight;
            _logStartHeight = LogPanelRow.ActualHeight;
            LogResizeHandle.CapturePointer(e.Pointer);
        }

        private void LogResizeHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDraggingLogPanel)
            {
                return;
            }

            var currentY = e.GetCurrentPoint(this.Content as UIElement).Position.Y;
            var delta = currentY - _dragStartY;
            var minContentHeight = MainContentRow.MinHeight;
            var minLogHeight = LogPanelRow.MinHeight;

            var totalHeight = _contentStartHeight + _logStartHeight;
            var newLogHeight = _logStartHeight - delta;
            
            // Limit log height based on min heights
            if (newLogHeight < minLogHeight)
            {
                newLogHeight = minLogHeight;
            }
            
            if ((totalHeight - newLogHeight) < minContentHeight)
            {
                newLogHeight = totalHeight - minContentHeight;
            }

            LogPanelRow.Height = new GridLength(newLogHeight, GridUnitType.Pixel);
            // Don't set MainContentRow height, let it be '*' to fill remaining space
        }

        private void LogResizeHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingLogPanel = false;
            LogResizeHandle.ReleasePointerCaptures();
        }

        private bool _isDraggingSplitter;
        private double _dragStartX;
        private double _leftColStartWidth;
        private double _rightColStartWidth;

        private void Splitter_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;
            
            _isDraggingSplitter = true;
            _dragStartX = e.GetCurrentPoint(this.Content as UIElement).Position.X;
            var grid = element.Parent as Grid;
            if (grid == null) return;
            
            _leftColStartWidth = grid.ColumnDefinitions[0].ActualWidth;
            _rightColStartWidth = grid.ColumnDefinitions[2].ActualWidth;
            element.CapturePointer(e.Pointer);
        }

        private void Splitter_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDraggingSplitter) return;

            var currentX = e.GetCurrentPoint(this.Content as UIElement).Position.X;
            var delta = currentX - _dragStartX;
            var element = sender as FrameworkElement;
            if (element == null) return;
            
            var grid = element.Parent as Grid;
            if (grid == null) return;

            var newLeftWidth = _leftColStartWidth + delta;
            var newRightWidth = _rightColStartWidth - delta;

            if (newLeftWidth < 300 || newRightWidth < 300) return;

            grid.ColumnDefinitions[0].Width = new GridLength(newLeftWidth, GridUnitType.Pixel);
            grid.ColumnDefinitions[2].Width = new GridLength(newRightWidth, GridUnitType.Pixel);
        }

        private void Splitter_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingSplitter = false;
            (sender as UIElement)?.ReleasePointerCaptures();
        }

        private void Splitter_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingSplitter = false;
        }

        private void LogResizeHandle_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingLogPanel = false;
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
                 
                 // Update icon
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
                 XamlRoot = this.Content.XamlRoot
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
                 XamlRoot = this.Content.XamlRoot
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
            // Access LogsView directly
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
