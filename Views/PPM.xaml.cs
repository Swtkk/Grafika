using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using MediaColor = System.Windows.Media.Color; // alias, żeby jasno używać koloru WPF

namespace Graf.Views
{
    public partial class PPM : Window
    {
        private TranslateTransform imageTranslate;
        private ScaleTransform imageScale;
        private System.Windows.Point lastMousePosition;

        // --- KONWERSJA KOLORÓW ---
        private bool _isUpdatingColorUi = false;
        private byte _r = 0, _g = 0, _b = 0;

        public PPM()
        {
            InitializeComponent();

            // Transformacje obrazu
            var transformGroup = new TransformGroup();
            imageScale = new ScaleTransform();
            imageTranslate = new TranslateTransform();
            transformGroup.Children.Add(imageScale);
            transformGroup.Children.Add(imageTranslate);
            displayedImage.RenderTransform = transformGroup;

            displayedImage.PreviewMouseWheel += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    double scaleChange = e.Delta > 0 ? 1.1 : 0.9;
                    imageScale.ScaleX *= scaleChange;
                    imageScale.ScaleY *= scaleChange;
                    e.Handled = true;
                }
            };

            displayedImage.PreviewMouseLeftButtonDown += (sender, e) =>
            {
                lastMousePosition = e.GetPosition(displayedImage);
                displayedImage.CaptureMouse();
            };

            displayedImage.PreviewMouseLeftButtonUp += (sender, e) =>
            {
                displayedImage.ReleaseMouseCapture();
            };

            displayedImage.PreviewMouseMove += (sender, e) =>
            {
                if (displayedImage.IsMouseCaptured)
                {
                    var newPosition = e.GetPosition(displayedImage);
                    if (imageScale.ScaleX > 1 && imageScale.ScaleY > 1)
                    {
                        double dx = newPosition.X - lastMousePosition.X;
                        double dy = newPosition.Y - lastMousePosition.Y;
                        lastMousePosition = newPosition;
                        imageTranslate.X += dx;
                        imageTranslate.Y += dy;
                    }
                }
            };

            displayedImage.MouseMove += OnImageMouseMove;

            // startowy kolor
            SetRgb(0, 0, 0);
        }

        // --- ODCZYT PIKSELA Z OBRAZU ---
        private void OnImageMouseMove(object sender, MouseEventArgs e)
        {
            var mousePosition = e.GetPosition(displayedImage);
            var bitmapSource = displayedImage.Source as BitmapSource;

            if (bitmapSource != null && displayedImage.ActualWidth > 0 && displayedImage.ActualHeight > 0)
            {
                int x = (int)(mousePosition.X * (bitmapSource.PixelWidth / displayedImage.ActualWidth));
                int y = (int)(mousePosition.Y * (bitmapSource.PixelHeight / displayedImage.ActualHeight));

                if (x >= 0 && x < bitmapSource.PixelWidth && y >= 0 && y < bitmapSource.PixelHeight)
                {
                    byte[] pixel = new byte[4];
                    var crop = new CroppedBitmap(bitmapSource, new Int32Rect(x, y, 1, 1));
                    crop.CopyPixels(pixel, 4, 0);
                    // BitmapSource najczęściej BGRA
                    byte b = pixel[0];
                    byte g = pixel[1];
                    byte r = pixel[2];
                    byte a = pixel[3];

                    pixelInfoTextBlock.Text = $"R: {r}, G: {g}, B: {b}   |   X: {x}  Y: {y}";
                    if (PickFromImageCheck.IsChecked == true)
                        SetRgb(r, g, b);
                }
            }
        }

        // --- PLIK: Wczytanie/Zapis ---
        private void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Pliki PPM|*.ppm|Pliki JPEG|*.jpg;*.jpeg" };
            if (dlg.ShowDialog() == true)
            {
                string path = dlg.FileName;
                if (path.EndsWith(".ppm", StringComparison.OrdinalIgnoreCase))
                {
                    string ppmFormat = ReadPPMFormat(path);
                    if (ppmFormat == "P3") LoadAndDisplayPPMP3(path);
                    else if (ppmFormat == "P6") LoadAndDisplayPPMP6(path);
                    else MessageBox.Show("Nieobsługiwany format PPM.");
                }
                else if (path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                {
                    LoadAndDisplayJPEG(path);
                }
                else MessageBox.Show("Nieobsługiwany format pliku.");
            }
        }

        private void LoadAndDisplayJPEG(string filePath)
        {
            try
            {
                var image = new BitmapImage(new Uri(filePath));
                displayedImage.Source = image;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas wczytywania pliku JPEG: " + ex.Message);
            }
        }

        private void SaveToJPEG_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "Pliki JPEG|*.jpg" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var bitmapSource = (BitmapSource)displayedImage.Source;
                    var encoder = new JpegBitmapEncoder();
                    int.TryParse(qualityTextBox.Text, out int q);
                    encoder.QualityLevel = q != 0 ? q : 95;
                    encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                    using var fs = new FileStream(dlg.FileName, FileMode.Create);
                    encoder.Save(fs);
                    MessageBox.Show("Obraz zapisany do JPEG.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd podczas zapisu JPEG: " + ex.Message);
                }
            }
        }

        // --- Wczytywanie PPM (P3/P6) - bez zmian istotnych ---
        private void LoadAndDisplayPPMP3(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var reader = new StreamReader(fs);
                _ = reader.ReadLine(); // "P3"

                int width = 0, height = 0, maxValue = 0;
                string dimensionsLine;
                string tmp = string.Empty;

                while ((dimensionsLine = reader.ReadLine()) != null)
                {
                    int commentIndex = dimensionsLine.IndexOf('#');
                    if (commentIndex >= 0) dimensionsLine = dimensionsLine[..commentIndex];

                    string[] tokens = dimensionsLine.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string token in tokens)
                    {
                        if (int.TryParse(token, out int v))
                        {
                            if (width == 0) width = v;
                            else if (height == 0) height = v;
                            else if (maxValue == 0) { maxValue = v; }
                            else { tmp += token + '\n'; }
                        }
                    }
                    if (width > 0 && height > 0 && maxValue > 0) break;
                }

                var image = new WriteableBitmap(width, height, 96, 96, PixelFormats.Rgb24, null);
                var allPixels = new List<byte>();

                while (true)
                {
                    char[] buffer = new char[4096];
                    int read = reader.ReadBlock(buffer, 0, buffer.Length);
                    if (read == 0) break;

                    int lastNewlineIndex = Array.LastIndexOf(buffer, '\n');
                    string dataBlock = tmp + new string(buffer, 0, read);
                    if (lastNewlineIndex >= 0 && lastNewlineIndex < read - 1)
                    {
                        dataBlock = tmp + new string(buffer, 0, lastNewlineIndex + 1);
                        tmp = new string(buffer, lastNewlineIndex + 1, read - lastNewlineIndex - 1);
                    }
                    else tmp = string.Empty;

                    while (dataBlock.Contains('#')) dataBlock = removeComments(dataBlock);

                    string[] lines = dataBlock.Split(new[] { "\n" }, StringSplitOptions.None);
                    foreach (var l in lines)
                    {
                        string[] tks = l.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var tk in tks)
                        {
                            string val;
                            if (maxValue > 255)
                            {
                                double scale = 255.0 / maxValue;
                                val = ((int)(int.Parse(tk) * scale)).ToString();
                            }
                            else val = tk;
                            allPixels.Add(byte.Parse(val));
                        }
                    }
                }

                var pixels = allPixels.ToArray();
                image.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 3, 0);
                displayedImage.Source = image;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas otwierania pliku: " + ex.Message);
            }
        }

        private void LoadAndDisplayPPMP6(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(fs);
                _ = Encoding.ASCII.GetString(reader.ReadBytes(2)); // "P6"

                int width = 0, height = 0, maxValue = 0;
                string dimensionsLine;
                while ((dimensionsLine = ReadLine(reader)) != null)
                {
                    int commentIndex = dimensionsLine.IndexOf('#');
                    if (commentIndex >= 0) dimensionsLine = dimensionsLine[..commentIndex];

                    string[] tokens = dimensionsLine.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string token in tokens)
                    {
                        if (int.TryParse(token, out int v))
                        {
                            if (width == 0) width = v;
                            else if (height == 0) height = v;
                            else if (maxValue == 0) { maxValue = v; break; }
                        }
                    }
                    if (width > 0 && height > 0 && maxValue > 0) break;
                }

                var image = new WriteableBitmap(width, height, 96, 96, PixelFormats.Rgb24, null);
                int dataSize = width * height * 3;
                byte[] allPixels = new byte[dataSize];
                int bytesRead = 0;
                while (bytesRead < dataSize)
                {
                    int toRead = Math.Min(dataSize - bytesRead, 4096);
                    int got = reader.Read(allPixels, bytesRead, toRead);
                    if (got == 0) break;
                    bytesRead += got;
                }
                image.WritePixels(new Int32Rect(0, 0, width, height), allPixels, width * 3, 0);
                displayedImage.Source = image;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas otwierania pliku: " + ex.Message);
            }
        }

        private string ReadLine(BinaryReader reader)
        {
            var buffer = new List<byte>();
            byte b;
            while ((b = reader.ReadByte()) != 10) buffer.Add(b); // '\n'
            return Encoding.ASCII.GetString(buffer.ToArray());
        }

        private string ReadPPMFormat(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var reader = new StreamReader(fs);
            return reader.ReadLine()?.Trim() ?? "";
        }

        private string removeComments(string line)
        {
            int idx = line.IndexOf('#');
            if (idx == 0) return null;
            if (idx >= 0)
            {
                var left = line[..idx];
                var right = line[(line.IndexOf('\n', idx) + 1)..];
                return left + right;
            }
            return line;
        }

        // ====== KONWERSJE RGB ↔ CMYK ======
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

        private void SetRgb(byte r, byte g, byte b, bool pushToUi = true)
        {
            _r = r; _g = g; _b = b;
            if (!pushToUi) return;

            _isUpdatingColorUi = true;
            try
            {
                // RGB -> UI
                RSlider.Value = r; GSlider.Value = g; BSlider.Value = b;
                RBox.Text = r.ToString(); GBox.Text = g.ToString(); BBox.Text = b.ToString();

                // RGB -> CMYK
                var (C, M, Y, K) = RgbToCmyk(r, g, b);
                CSlider.Value = C; CBox.Text = Math.Round(C, 1).ToString("0.#");
                MSlider.Value = M; MBox.Text = Math.Round(M, 1).ToString("0.#");
                YSlider.Value = Y; YBox.Text = Math.Round(Y, 1).ToString("0.#");
                KSlider.Value = K; KBox.Text = Math.Round(K, 1).ToString("0.#");

                // Podgląd + etykiety
                ColorPreview.Background = new SolidColorBrush(MediaColor.FromRgb(r, g, b));
                RgbLabel.Text = $"RGB: {r}, {g}, {b}";
                HexLabel.Text = $"#{r:X2}{g:X2}{b:X2}";
                CmykLabel.Text = $"CMYK: {Math.Round(C, 1)}, {Math.Round(M, 1)}, {Math.Round(Y, 1)}, {Math.Round(K, 1)}";
            }
            finally { _isUpdatingColorUi = false; }
        }

        // Tryb
        private void RgbModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (RgbPanel == null || CmykPanel == null) return;
            RgbPanel.Visibility = Visibility.Visible;
            CmykPanel.Visibility = Visibility.Collapsed;
        }
        private void CmykModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (RgbPanel == null || CmykPanel == null) return;
            RgbPanel.Visibility = Visibility.Collapsed;
            CmykPanel.Visibility = Visibility.Visible;
        }

        // Handlery RGB (suwaki + pola)
        private void RSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        { if (_isUpdatingColorUi) return; SetRgb((byte)Math.Round(RSlider.Value), _g, _b); }
        private void GSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        { if (_isUpdatingColorUi) return; SetRgb(_r, (byte)Math.Round(GSlider.Value), _b); }
        private void BSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        { if (_isUpdatingColorUi) return; SetRgb(_r, _g, (byte)Math.Round(BSlider.Value)); }

        private static byte ClampByte(string t) { return byte.TryParse(t, out var v) ? v : (byte)0; }
        private void RBox_TextChanged(object sender, TextChangedEventArgs e)
        { if (_isUpdatingColorUi) return; SetRgb(ClampByte(RBox.Text), _g, _b); }
        private void GBox_TextChanged(object sender, TextChangedEventArgs e)
        { if (_isUpdatingColorUi) return; SetRgb(_r, ClampByte(GBox.Text), _b); }
        private void BBox_TextChanged(object sender, TextChangedEventArgs e)
        { if (_isUpdatingColorUi) return; SetRgb(_r, _g, ClampByte(BBox.Text)); }

        // Handlery CMYK (suwaki + pola)
        private void CSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { CMYKSlidersChanged(); }
        private void MSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { CMYKSlidersChanged(); }
        private void YSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { CMYKSlidersChanged(); }
        private void KSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { CMYKSlidersChanged(); }
        private void CMYKSlidersChanged()
        {
            if (_isUpdatingColorUi) return;
            var (R, G, B) = CmykToRgb(CSlider.Value, MSlider.Value, YSlider.Value, KSlider.Value);
            SetRgb(R, G, B);
        }

        private static double ClampPercent(string t)
        {
            if (!double.TryParse(t, out var v)) v = 0;
            if (v < 0) v = 0; if (v > 100) v = 100; return v;
        }
        private void CBox_TextChanged(object sender, TextChangedEventArgs e) { CMYKBoxesChanged(); }
        private void MBox_TextChanged(object sender, TextChangedEventArgs e) { CMYKBoxesChanged(); }
        private void YBox_TextChanged(object sender, TextChangedEventArgs e) { CMYKBoxesChanged(); }
        private void KBox_TextChanged(object sender, TextChangedEventArgs e) { CMYKBoxesChanged(); }
        private void CMYKBoxesChanged()
        {
            if (_isUpdatingColorUi) return;
            var (R, G, B) = CmykToRgb(
                ClampPercent(CBox.Text),
                ClampPercent(MBox.Text),
                ClampPercent(YBox.Text),
                ClampPercent(KBox.Text));
            SetRgb(R, G, B);
        }
    }
}
