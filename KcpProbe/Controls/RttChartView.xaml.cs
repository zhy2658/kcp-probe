using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace KcpProbe.Controls
{
    public sealed partial class RttChartView : UserControl
    {
        private readonly Polyline _line;
        private readonly List<double> _history = new List<double>();
        private const int MaxHistory = 100;

        public RttChartView()
        {
            InitializeComponent();
            _line = new Polyline
            {
                Stroke = new SolidColorBrush(Colors.LightGreen),
                StrokeThickness = 2
            };
            ChartCanvas.Children.Add(_line);
        }

        public void AddSample(double rtt)
        {
            _history.Add(rtt);
            if (_history.Count > MaxHistory)
            {
                _history.RemoveAt(0);
            }

            CurrentRttText.Text = $"{rtt:F0} ms";
            Render();
        }

        private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Render();
        }

        private void Render()
        {
            if (ChartCanvas.ActualWidth <= 0 || ChartCanvas.ActualHeight <= 0)
            {
                return;
            }

            var width = ChartCanvas.ActualWidth;
            var height = ChartCanvas.ActualHeight;
            var step = width / MaxHistory;
            var points = new PointCollection();
            double maxRtt = 100;

            foreach (var value in _history)
            {
                if (value > maxRtt)
                {
                    maxRtt = value;
                }
            }

            for (var i = 0; i < _history.Count; i++)
            {
                var x = i * step;
                var y = height - (_history[i] / maxRtt * height);
                if (y < 0)
                {
                    y = 0;
                }
                if (y > height)
                {
                    y = height;
                }
                points.Add(new Windows.Foundation.Point(x, y));
            }

            _line.Points = points;
        }
    }
}
