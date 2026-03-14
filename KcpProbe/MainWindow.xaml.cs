using Microsoft.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.ApplicationModel.DataTransfer;
using KcpProbe.ViewModels;
using System.IO;
using System;
using System.Linq;

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

        public MainWindow()
        {
            this.InitializeComponent();
            ((FrameworkElement)this.Content).DataContext = ViewModel;

            var windowId = Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
            _appWindow = AppWindow.GetFromWindowId(windowId);
            if (_appWindow != null)
            {
                _appWindow.Resize(new SizeInt32(1148, 868));
                var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
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

        private void CallApi_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SendRpc();
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

        private void RunRegression_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RunRegression();
        }

        private void StopRegression_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.StopRegression();
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

            var newContentHeight = _contentStartHeight + delta;
            var newLogHeight = _logStartHeight - delta;

            if (newContentHeight < minContentHeight)
            {
                newContentHeight = minContentHeight;
                newLogHeight = _contentStartHeight + _logStartHeight - newContentHeight;
            }

            if (newLogHeight < minLogHeight)
            {
                newLogHeight = minLogHeight;
                newContentHeight = _contentStartHeight + _logStartHeight - newLogHeight;
            }

            MainContentRow.Height = new GridLength(newContentHeight, GridUnitType.Pixel);
            LogPanelRow.Height = new GridLength(newLogHeight, GridUnitType.Pixel);
        }

        private void LogResizeHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingLogPanel = false;
            LogResizeHandle.ReleasePointerCaptures();
        }

        private void LogResizeHandle_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingLogPanel = false;
        }

        private void CopySelectedLogs_Click(object sender, RoutedEventArgs e)
        {
            var selected = LogsListView.SelectedItems.OfType<string>().ToList();
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
            if (ViewModel.Logs.Count == 0)
            {
                return;
            }

            var package = new DataPackage();
            package.SetText(string.Join(Environment.NewLine, ViewModel.Logs));
            Clipboard.SetContent(package);
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearLogs();
        }
    }
}
