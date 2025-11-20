using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Graf.Views
{
    /// <summary>
    /// Logika interakcji dla klasy PPM.xaml
    /// </summary>
    public partial class PPM : Window
    {
        private TranslateTransform _imageTranslate;
        private ScaleTransform _imageScale;
        private System.Windows.Point _lastMousePosition;

        public PPM()
        {
            InitializeComponent();
            var transformGroup = new TransformGroup();
            _imageScale = new ScaleTransform(1.0, 1.0);
            _imageTranslate = new TranslateTransform(0, 0);
            transformGroup.Children.Add(_imageScale);
            transformGroup.Children.Add(_imageTranslate);
            displayedImage.RenderTransform = transformGroup;

            displayedImage.PreviewMouseWheel += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    double scaleChange = e.Delta > 0 ? 1.1 : 0.9;
                    _imageScale.ScaleX *= scaleChange;
                    _imageScale.ScaleY *= scaleChange;
                    e.Handled = true;
                }
            };

            displayedImage.PreviewMouseLeftButtonDown += (sender, e) =>
            {
                _lastMousePosition = e.GetPosition(displayedImage);
                displayedImage.CaptureMouse();
            };

            displayedImage.PreviewMouseLeftButtonUp += (sender, e) =>
            {
                displayedImage.ReleaseMouseCapture();
            };

            displayedImage.PreviewMouseMove += (sender, e) =>
            {
                if (!displayedImage.IsMouseCaptured) return;

                var newPos = e.GetPosition(displayedImage);

                if (_imageScale.ScaleX > 1.0 || _imageScale.ScaleY > 1.0)
                {
                    double dx = newPos.X - _lastMousePosition.X;
                    double dy = newPos.Y - _lastMousePosition.Y;
                    _lastMousePosition = newPos;
                    _imageTranslate.X += dx;
                    _imageTranslate.Y += dy;
                }
            };

            displayedImage.MouseMove += OnImageMouseMove;
        }

        private void OnImageMouseMove(object sender, MouseEventArgs e)
        {
            if (displayedImage.Source is not BitmapSource src) return;
            if (displayedImage.ActualWidth <= 0 || displayedImage.ActualHeight <= 0) return;

            var pos = e.GetPosition(displayedImage);

            if (displayedImage.RenderTransform is Transform t && !t.Value.IsIdentity)
            {
                var m = t.Value;
                if (m.HasInverse)
                {
                    m.Invert();
                    pos = m.Transform(pos);
                }
            }

            double sx = src.PixelWidth / displayedImage.ActualWidth;
            double sy = src.PixelHeight / displayedImage.ActualHeight;

            int x = (int)Math.Floor(pos.X * sx);
            int y = (int)Math.Floor(pos.Y * sy);

            if (x < 0 || y < 0 || x >= src.PixelWidth || y >= src.PixelHeight) return;

            var fmt = PixelFormats.Bgra32;
            BitmapSource converted = src.Format == fmt ? src : new FormatConvertedBitmap(src, fmt, null, 0);

            byte[] pixel = new byte[4];
            converted.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);

            var color = Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
            pixelInfoTextBlock.Text = $"R: {color.R}, G: {color.G}, B: {color.B}\nX: {x} Y: {y}";
        }

        // Wczytywanie obrazów
        private async void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Pliki PPM|*.ppm|Pliki JPEG|*.jpg;*.jpeg"
            };

            if (ofd.ShowDialog() != true) return;

            string path = ofd.FileName;

            try
            {
                if (path.EndsWith(".ppm", StringComparison.OrdinalIgnoreCase))
                {
                    string magic = ReadPPMFormat(path);
                    if (magic == "P3")
                    {
                        displayedImage.Source = LoadPPM_P3(path);
                    }
                    else if (magic == "P6")
                    {
                        displayedImage.Source = LoadPPM_P6(path);
                    }
                    else
                    {
                        MessageBox.Show("Nieobsługiwany format PPM. Wspierane: P3, P6.");
                    }
                }
                else if (path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                         path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                {
                    displayedImage.Source = LoadJpeg(path);
                }
                else
                {
                    MessageBox.Show("Nieobsługiwany format pliku.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas wczytywania: " + ex.Message);
            }
        }

        private BitmapSource LoadJpeg(string filePath)
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.UriSource = new Uri(filePath);
            img.EndInit();
            img.Freeze();
            return img;
        }

        // Zapis JPEG z kontrolą jakości i konwersją formatu
        private void SaveToJPEG_Click(object sender, RoutedEventArgs e)
        {
            if (displayedImage.Source is not BitmapSource src)
            {
                MessageBox.Show("Brak obrazu do zapisania.");
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "Pliki JPEG|*.jpg;*.jpeg",
                DefaultExt = ".jpg"
            };

            if (sfd.ShowDialog() != true) return;

            try
            {
                int quality = 95;
                if (!string.IsNullOrWhiteSpace(qualityTextBox?.Text) &&
                    int.TryParse(qualityTextBox.Text, out int q))
                {
                    quality = Math.Clamp(q, 1, 100);
                }

                // Upewnij się, że mamy format nadający się do zapisu
                BitmapSource toSave = src.Format == PixelFormats.Bgra32
                    ? src
                    : new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);

                var encoder = new JpegBitmapEncoder
                {
                    QualityLevel = quality
                };
                encoder.Frames.Add(BitmapFrame.Create(toSave));

                using var fs = new FileStream(sfd.FileName, FileMode.Create, FileAccess.Write);
                encoder.Save(fs);

                MessageBox.Show("Zapisano obraz jako JPEG.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas zapisu JPEG: " + ex.Message);
            }
        }

        // =====================  PPM P3 (ASCII) – WERSJA BLOKOWA  =====================

        private BitmapSource LoadPPM_P3(string filePath)
        {
            // BLOKOWE wczytanie całego pliku
            byte[] data = File.ReadAllBytes(filePath);
            int len = data.Length;
            int index = 0;

            static bool IsAsciiWhitespace(byte c) =>
                c == (byte)' ' || c == (byte)'\t' ||
                c == (byte)'\r' || c == (byte)'\n' ||
                c == (byte)'\f' || c == (byte)'\v';

            void SkipWhitespaceAndComments()
            {
                while (index < len)
                {
                    byte b = data[index];

                    if (b == (byte)'#')
                    {
                        while (index < len && data[index] != (byte)'\n' && data[index] != (byte)'\r')
                            index++;
                        continue;
                    }

                    if (IsAsciiWhitespace(b))
                    {
                        index++;
                        continue;
                    }

                    break;
                }
            }

            string ReadStringToken()
            {
                SkipWhitespaceAndComments();
                if (index >= len)
                    throw new EndOfStreamException("Nieoczekiwany koniec danych przy odczycie tokenu.");

                int start = index;
                while (index < len)
                {
                    byte b = data[index];
                    if (IsAsciiWhitespace(b) || b == (byte)'#')
                        break;
                    index++;
                }

                int count = index - start;
                if (count <= 0)
                    throw new InvalidDataException("Pusty token w nagłówku PPM.");

                return Encoding.ASCII.GetString(data, start, count);
            }

            int ReadIntToken()
            {
                SkipWhitespaceAndComments();
                if (index >= len)
                    throw new EndOfStreamException("Nieoczekiwany koniec danych przy odczycie liczby.");

                int value = 0;
                bool hasDigit = false;

                while (index < len)
                {
                    byte b = data[index];

                    if (b >= (byte)'0' && b <= (byte)'9')
                    {
                        hasDigit = true;
                        value = value * 10 + (b - (byte)'0');
                        index++;
                    }
                    else if (IsAsciiWhitespace(b) || b == (byte)'#')
                    {
                        break;
                    }
                    else
                    {
                        throw new InvalidDataException($"Nieoczekiwany znak w danych P3: {(char)b}");
                    }
                }

                if (!hasDigit)
                    throw new InvalidDataException("Nie znaleziono cyfr w miejscu oczekiwanej liczby.");

                return value;
            }

            // --- nagłówek ---
            string magic = ReadStringToken();
            if (magic != "P3")
                throw new InvalidDataException("Niepoprawny nagłówek P3.");

            int width = ReadIntToken();
            int height = ReadIntToken();
            int maxVal = ReadIntToken();

            if (width <= 0 || height <= 0 || maxVal <= 0)
                throw new InvalidDataException("Błędne wymiary obrazu lub maxVal.");

            int expectedSamples = width * height * 3;
            byte[] rgb = new byte[expectedSamples];

            // skalowanie liniowe kolorów
            double scale = maxVal == 255 ? 1.0 : 255.0 / maxVal;

            for (int i = 0; i < expectedSamples; i++)
            {
                int val = ReadIntToken();

                if (scale != 1.0)
                    val = (int)Math.Round(val * scale);

                rgb[i] = (byte)Math.Clamp(val, 0, 255);
            }

            return BuildBitmapFromRGB24(width, height, rgb);
        }


        private string ReadStringToken(byte[] data, ref int index)
        {
            int len = data.Length;

            static bool IsAsciiWhitespace(byte c) =>
                c == (byte)' ' || c == (byte)'\t' ||
                c == (byte)'\r' || c == (byte)'\n' ||
                c == (byte)'\f' || c == (byte)'\v';

            // Pomijamy komentarze i białe znaki
            while (index < len)
            {
                byte b = data[index];
                if (b == '#')
                {
                    while (index < len && data[index] != '\n' && data[index] != '\r')
                        index++;
                    continue;
                }

                if (IsAsciiWhitespace(b))
                {
                    index++;
                    continue;
                }

                break;
            }

            // Token znaków
            int start = index;
            while (index < len)
            {
                byte b = data[index];
                if (IsAsciiWhitespace(b) || b == (byte)'#')
                    break;
                index++;
            }

            return Encoding.ASCII.GetString(data, start, index - start);
        }


        // =====================  PPM P6 (BINARNY) – jak u Ciebie  =====================

        private BitmapSource LoadPPM_P6(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: false);

            string magic = ReadAsciiToken(br);
            if (magic != "P6") throw new InvalidDataException("Niepoprawny nagłówek P6.");

            int width = int.Parse(ReadAsciiToken(br));
            int height = int.Parse(ReadAsciiToken(br));
            int maxVal = int.Parse(ReadAsciiToken(br));

            if (width <= 0 || height <= 0 || maxVal <= 0) throw new InvalidDataException("Błędne wymiary lub maxVal.");

            int next = br.PeekChar();
            if (next == -1) throw new EndOfStreamException();
            if (!IsAsciiWhitespace((byte)next)) throw new InvalidDataException("Brak separatora po nagłówku P6.");
            _ = br.ReadByte();

            int pixelCount = width * height;
            byte[] rgb = new byte[pixelCount * 3];

            if (maxVal <= 255)
            {
                int toRead = pixelCount * 3;
                int read = br.Read(rgb, 0, toRead);
                if (read != toRead) throw new EndOfStreamException("Niepełne dane P6.");
            }
            else
            {
                // CZYTANIE BLOKOWE 16-bit
                int totalSamples = pixelCount * 3;
                int bytesToRead = totalSamples * 2;

                byte[] buf = br.ReadBytes(bytesToRead);
                if (buf.Length != bytesToRead)
                    throw new EndOfStreamException("Niepełne dane P6 (16-bit).");

                double scale = 255.0 / maxVal;

                int bi = 0;
                for (int i = 0; i < totalSamples; i++, bi += 2)
                {
                    int value = (buf[bi] << 8) | buf[bi + 1];
                    int scaled = (int)Math.Round(value * scale);
                    rgb[i] = (byte)Math.Clamp(scaled, 0, 255);
                }
            }

            return BuildBitmapFromRGB24(width, height, rgb);
        }

        // =====================  Helpery PPM  =====================

        private static BitmapSource BuildBitmapFromRGB24(int width, int height, byte[] rgb)
        {
            int stride = width * 3;
            var wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Rgb24, null);
            wb.WritePixels(new Int32Rect(0, 0, width, height), rgb, stride, 0);
            wb.Freeze();
            return wb;
        }

        private static string ReadPPMFormat(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sr = new StreamReader(fs, Encoding.ASCII, detectEncodingFromByteOrderMarks: false);
            string? first = sr.ReadLine();
            if (first == null) throw new InvalidDataException("Plik pusty.");
            return first.Trim();
        }

        private static string ReadAsciiToken(BinaryReader br)
        {
            int b;
            // Skip whitespace
            do
            {
                b = br.ReadByte();
                if (b == '#')
                {
                    while (true)
                    {
                        int c = br.ReadByte();
                        if (c == '\n' || c == '\r') break;
                    }
                    b = ' ';
                }
            } while (IsAsciiWhitespace((byte)b));

            var bytes = new List<byte> { (byte)b };
            while (br.PeekChar() != -1)
            {
                byte c = (byte)br.PeekChar();
                if (IsAsciiWhitespace(c) || c == '#') break;
                bytes.Add(br.ReadByte());
            }

            return Encoding.ASCII.GetString(bytes.ToArray());
        }

        private static bool IsAsciiWhitespace(byte c) =>
            c == (byte)' ' || c == (byte)'\t' ||
            c == (byte)'\r' || c == (byte)'\n' ||
            c == (byte)'\f' || c == (byte)'\v';

        private static string StripComment(string line)
        {
            int idx = line.IndexOf('#');
            return idx < 0 ? line : line[..idx];
        }
    }
}
