using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace KcpProbe
{
    public sealed class ResizeHandleBorder : Grid
    {
        public ResizeHandleBorder()
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
        }
    }
}
