using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace KcpProbe
{
    public enum ResizeHandleMode
    {
        None,
        Columns,
        Rows
    }

    public sealed class ResizeHandleBorder : Grid
    {
        public static readonly DependencyProperty CursorProperty =
            DependencyProperty.Register(nameof(Cursor), typeof(InputSystemCursorShape), typeof(ResizeHandleBorder), new PropertyMetadata(InputSystemCursorShape.Arrow, OnCursorChanged));

        public static readonly DependencyProperty ModeProperty =
            DependencyProperty.Register(nameof(Mode), typeof(ResizeHandleMode), typeof(ResizeHandleBorder), new PropertyMetadata(ResizeHandleMode.None));

        public static readonly DependencyProperty PrimaryIndexProperty =
            DependencyProperty.Register(nameof(PrimaryIndex), typeof(int), typeof(ResizeHandleBorder), new PropertyMetadata(0));

        public static readonly DependencyProperty SecondaryIndexProperty =
            DependencyProperty.Register(nameof(SecondaryIndex), typeof(int), typeof(ResizeHandleBorder), new PropertyMetadata(1));

        public static readonly DependencyProperty MinPrimaryProperty =
            DependencyProperty.Register(nameof(MinPrimary), typeof(double), typeof(ResizeHandleBorder), new PropertyMetadata(80d));

        public static readonly DependencyProperty MinSecondaryProperty =
            DependencyProperty.Register(nameof(MinSecondary), typeof(double), typeof(ResizeHandleBorder), new PropertyMetadata(80d));

        private bool _isDragging;
        private double _startPrimary;
        private double _startSecondary;
        private double _startPosition;

        public InputSystemCursorShape Cursor
        {
            get => (InputSystemCursorShape)GetValue(CursorProperty);
            set => SetValue(CursorProperty, value);
        }

        public ResizeHandleMode Mode
        {
            get => (ResizeHandleMode)GetValue(ModeProperty);
            set => SetValue(ModeProperty, value);
        }

        public int PrimaryIndex
        {
            get => (int)GetValue(PrimaryIndexProperty);
            set => SetValue(PrimaryIndexProperty, value);
        }

        public int SecondaryIndex
        {
            get => (int)GetValue(SecondaryIndexProperty);
            set => SetValue(SecondaryIndexProperty, value);
        }

        public double MinPrimary
        {
            get => (double)GetValue(MinPrimaryProperty);
            set => SetValue(MinPrimaryProperty, value);
        }

        public double MinSecondary
        {
            get => (double)GetValue(MinSecondaryProperty);
            set => SetValue(MinSecondaryProperty, value);
        }

        private static void OnCursorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ResizeHandleBorder border && e.NewValue is InputSystemCursorShape shape)
            {
                border.ProtectedCursor = InputSystemCursor.Create(shape);
            }
        }

        public ResizeHandleBorder()
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            PointerCaptureLost += OnPointerCaptureLost;
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (Parent is not Grid grid || Mode == ResizeHandleMode.None)
            {
                return;
            }

            if (Mode == ResizeHandleMode.Columns)
            {
                if (PrimaryIndex >= grid.ColumnDefinitions.Count || SecondaryIndex >= grid.ColumnDefinitions.Count)
                {
                    return;
                }

                _startPrimary = grid.ColumnDefinitions[PrimaryIndex].ActualWidth;
                _startSecondary = grid.ColumnDefinitions[SecondaryIndex].ActualWidth;
                _startPosition = e.GetCurrentPoint(grid).Position.X;
            }
            else
            {
                if (PrimaryIndex >= grid.RowDefinitions.Count || SecondaryIndex >= grid.RowDefinitions.Count)
                {
                    return;
                }

                _startPrimary = grid.RowDefinitions[PrimaryIndex].ActualHeight;
                _startSecondary = grid.RowDefinitions[SecondaryIndex].ActualHeight;
                _startPosition = e.GetCurrentPoint(grid).Position.Y;
            }

            _isDragging = true;
            CapturePointer(e.Pointer);
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragging || Parent is not Grid grid || Mode == ResizeHandleMode.None)
            {
                return;
            }

            var position = Mode == ResizeHandleMode.Columns ? e.GetCurrentPoint(grid).Position.X : e.GetCurrentPoint(grid).Position.Y;
            var delta = position - _startPosition;
            var primary = _startPrimary + delta;
            var secondary = _startSecondary - delta;
            if (primary < MinPrimary || secondary < MinSecondary)
            {
                return;
            }

            if (Mode == ResizeHandleMode.Columns)
            {
                grid.ColumnDefinitions[PrimaryIndex].Width = new GridLength(primary, GridUnitType.Pixel);
                grid.ColumnDefinitions[SecondaryIndex].Width = new GridLength(secondary, GridUnitType.Pixel);
            }
            else
            {
                grid.RowDefinitions[PrimaryIndex].Height = new GridLength(primary, GridUnitType.Pixel);
                grid.RowDefinitions[SecondaryIndex].Height = new GridLength(secondary, GridUnitType.Pixel);
            }
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isDragging = false;
            ReleasePointerCaptures();
        }

        private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _isDragging = false;
        }
    }
}
