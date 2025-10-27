using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Graf.Views
{
    public partial class Cmyk : Window
    {
        private bool _updating;
        private byte _r, _g, _b;

        public Cmyk()
        {
            InitializeComponent();
            SetRgb(0, 0, 0);
        }

        // ====== konwersje ======
        private (double C, double M, double Y, double K) RgbToCmyk(byte r, byte g, byte b)
        {
            double rd = r / 255.0, gd = g / 255.0, bd = b / 255.0;
            double k = 1.0 - Math.Max(rd, Math.Max(gd, bd));
            double c = (1 - rd - k) / (1 - k);
            double m = (1 - gd - k) / (1 - k);
            double y = (1 - bd - k) / (1 - k);
            if (double.IsNaN(c)) c = 0;
            if (double.IsNaN(m)) m = 0;
            if (double.IsNaN(y)) y = 0;
            return (c * 100.0, m * 100.0, y * 100.0, k * 100.0);
        }
        private (byte R, byte G, byte B) CmykToRgb(double c, double m, double y, double k)
        {
            double C = c / 100.0, M = m / 100.0, Y = y / 100.0, K = k / 100.0;
            double r = 255.0 * (1 - C) * (1 - K);
            double g = 255.0 * (1 - M) * (1 - K);
            double b = 255.0 * (1 - Y) * (1 - K);
            return ((byte)Math.Round(r), (byte)Math.Round(g), (byte)Math.Round(b));
        }

        private void SetRgb(byte r, byte g, byte b)
        {
            _r = r; _g = g; _b = b;
            _updating = true;
            try
            {
                RSlider.Value = r; GSlider.Value = g; BSlider.Value = b;
                RBox.Text = r.ToString(); GBox.Text = g.ToString(); BBox.Text = b.ToString();

                var (C, M, Y, K) = RgbToCmyk(r, g, b);
                CSlider.Value = C; MSlider.Value = M; YSlider.Value = Y; KSlider.Value = K;
                CBox.Text = Math.Round(C, 1).ToString("0.#");
                MBox.Text = Math.Round(M, 1).ToString("0.#");
                YBox.Text = Math.Round(Y, 1).ToString("0.#");
                KBox.Text = Math.Round(K, 1).ToString("0.#");

                ColorPreview.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
                RgbLabel.Text = $"RGB: {r}, {g}, {b}";
                HexLabel.Text = $"#{r:X2}{g:X2}{b:X2}";
                CmykLabel.Text = $"CMYK: {Math.Round(C, 1)}, {Math.Round(M, 1)}, {Math.Round(Y, 1)}, {Math.Round(K, 1)}";
            }
            finally { _updating = false; }
        }

        // ====== RGB handlers ======
        private void RSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e) { if (_updating) return; SetRgb((byte)e.NewValue, _g, _b); }
        private void GSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e) { if (_updating) return; SetRgb(_r, (byte)e.NewValue, _b); }
        private void BSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e) { if (_updating) return; SetRgb(_r, _g, (byte)e.NewValue); }

        private static byte ParseByte(string t) => byte.TryParse(t, out var v) ? v : (byte)0;
        private void RBox_TextChanged(object s, TextChangedEventArgs e) { if (_updating) return; SetRgb(ParseByte(RBox.Text), _g, _b); }
        private void GBox_TextChanged(object s, TextChangedEventArgs e) { if (_updating) return; SetRgb(_r, ParseByte(GBox.Text), _b); }
        private void BBox_TextChanged(object s, TextChangedEventArgs e) { if (_updating) return; SetRgb(_r, _g, ParseByte(BBox.Text)); }

        // ====== CMYK handlers ======
        private void CSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e) { if (_updating) return; ApplyCmyk(); }
        private void MSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e) { if (_updating) return; ApplyCmyk(); }
        private void YSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e) { if (_updating) return; ApplyCmyk(); }
        private void KSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e) { if (_updating) return; ApplyCmyk(); }

        private void CBox_TextChanged(object s, TextChangedEventArgs e) { if (_updating) return; ApplyCmyk(true); }
        private void MBox_TextChanged(object s, TextChangedEventArgs e) { if (_updating) return; ApplyCmyk(true); }
        private void YBox_TextChanged(object s, TextChangedEventArgs e) { if (_updating) return; ApplyCmyk(true); }
        private void KBox_TextChanged(object s, TextChangedEventArgs e) { if (_updating) return; ApplyCmyk(true); }

        private static double ClampPct(string t)
        {
            if (!double.TryParse(t, out var v)) v = 0;
            if (v < 0) v = 0; if (v > 100) v = 100;
            return v;
        }
        private void ApplyCmyk(bool fromText = false)
        {
            double c = fromText ? ClampPct(CBox.Text) : CSlider.Value;
            double m = fromText ? ClampPct(MBox.Text) : MSlider.Value;
            double y = fromText ? ClampPct(YBox.Text) : YSlider.Value;
            double k = fromText ? ClampPct(KBox.Text) : KSlider.Value;

            var (R, G, B) = CmykToRgb(c, m, y, k);
            SetRgb(R, G, B);
        }
    }
}
