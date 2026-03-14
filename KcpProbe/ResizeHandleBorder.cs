using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KcpProbe
{
    public sealed class ResizeHandleBorder : Grid
    {
        public static readonly DependencyProperty CursorProperty =
            DependencyProperty.Register(nameof(Cursor), typeof(InputSystemCursorShape), typeof(ResizeHandleBorder), new PropertyMetadata(InputSystemCursorShape.Arrow, OnCursorChanged));

        public InputSystemCursorShape Cursor
        {
            get => (InputSystemCursorShape)GetValue(CursorProperty);
            set => SetValue(CursorProperty, value);
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
            // Default if not set
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
        }
    }
}
