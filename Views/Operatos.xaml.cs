using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace Graf.Views
{
  
    public partial class Operators : Window
    {
        private WriteableBitmap currentBitmap;
        private WriteableBitmap snapshot;
        private BitmapImage originalImage;
        private bool IsChangeOriginal = false;
        int width;
        int height;

        public Operators()
        {
            InitializeComponent();
            TransformGroup group = new TransformGroup();

            ScaleTransform xform = new ScaleTransform();
            group.Children.Add(xform);

            TranslateTransform tt = new TranslateTransform();
            group.Children.Add(tt);

            displayedImage.RenderTransform = group;
        }

        private void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Pliki JPEG|*.jpg;*.jpeg";

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;

                if (filePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                {
                    LoadAndDisplayJPEG(filePath);
                }
                else
                {
                    MessageBox.Show("Nieobsługiwany format pliku.");
                }
            }
        }

        private void LoadAndDisplayJPEG(string filePath)
        {
            try
            {
                BitmapImage image = new BitmapImage(new Uri(filePath));

                originalImage = image;

                var Width = originalImage.PixelWidth;
                var Height = originalImage.PixelHeight;
                var bytesPerPixel = (originalImage.Format.BitsPerPixel + 7) / 8;
                var stride = Width * bytesPerPixel;

                byte[] pixelData = new byte[Height * stride];

                originalImage.CopyPixels(pixelData, stride, 0);
                var Pixels = pixelData;

                currentBitmap = new WriteableBitmap(Width, Height, 96, 96, PixelFormats.Rgb24, null);
                currentBitmap.WritePixels(new Int32Rect(0, 0, Width, Height), pixelData, 3 * Width, 0);

                width = currentBitmap.PixelWidth;
                height = currentBitmap.PixelHeight;

                displayedImage.Source = originalImage;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas wczytywania pliku JPEG: " + ex.Message);
            }
        }

        private void Dilatation_Click(object sender, RoutedEventArgs e)
        {
            if (currentBitmap != null)
            {
                var newBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Rgb24, null);
                var pixels = GetPixels();

                var newPixels = PerformDilatation(pixels, width, height);
                if (newPixels != null)
                {
                    newBitmap.WritePixels(new Int32Rect(0, 0, width, height), newPixels, width * 3, 0);
                    if (IsChangeOriginal)
                        currentBitmap = newBitmap;
                    displayedImage.Source = newBitmap;
                }
            }
            else
            {
                MessageBox.Show("Nie załadowano obrazu");
                return;
            }
        }

        private void Erosion_Click(object sender, RoutedEventArgs e)
        {
            if (currentBitmap != null)
            {
                var newBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Rgb24, null);
                var pixels = GetPixels();

                var newPixels = PerformErosion(pixels, width, height); ;
                if (newPixels != null)
                {
                    newBitmap.WritePixels(new Int32Rect(0, 0, width, height), newPixels, width * 3, 0);
                    if (IsChangeOriginal)
                        currentBitmap = newBitmap;
                    displayedImage.Source = newBitmap;
                }
            }
            else
            {
                MessageBox.Show("Nie załadowano obrazu");
                return;
            }
        }

        private void HitOrMiss_Click(object sender, RoutedEventArgs e)
        {
            if (currentBitmap != null)
            {
                var newBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Rgb24, null);
                var pixels = GetPixels();

                var newPixels = PerformHitOrMiss(pixels, width, height);
                if (newPixels != null)
                {
                    newBitmap.WritePixels(new Int32Rect(0, 0, width, height), newPixels, width * 3, 0);
                    if (IsChangeOriginal)
                        currentBitmap = newBitmap;
                    displayedImage.Source = newBitmap;
                }
            }
            else
            {
                MessageBox.Show("Nie załadowano obrazu");
                return;
            }

        }

        private void Thinning_Click(object sender, RoutedEventArgs e)
        {
            if (currentBitmap != null)
            {
                var newBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Rgb24, null);
                var pixels = GetPixels();

                var hitOrMiss = PerformHitOrMiss(pixels, width, height);
                if (hitOrMiss != null)
                {
                    var newPixels = new byte[width * height * 3];
                    for (int i = 0; i < 3 * width * height;)
                    {
                        var hitOrMissPixel = (int)hitOrMiss[i];
                        var basePixel = (int)pixels[i];

                        newPixels[i++] = basePixel - hitOrMissPixel > 255 ? (byte)255 : basePixel - hitOrMissPixel < 0 ? (byte)0 : (byte)(basePixel - hitOrMissPixel);
                        newPixels[i++] = basePixel - hitOrMissPixel > 255 ? (byte)255 : basePixel - hitOrMissPixel < 0 ? (byte)0 : (byte)(basePixel - hitOrMissPixel);
                        newPixels[i++] = basePixel - hitOrMissPixel > 255 ? (byte)255 : basePixel - hitOrMissPixel < 0 ? (byte)0 : (byte)(basePixel - hitOrMissPixel);

                    }

                    newBitmap.WritePixels(new Int32Rect(0, 0, width, height), newPixels, width * 3, 0);
                    if (IsChangeOriginal)
                        currentBitmap = newBitmap;
                    displayedImage.Source = newBitmap;
                }
                else
                {
                    MessageBox.Show("Nie podano maski");
                    return;
                }
            }
            else
            {
                MessageBox.Show("Nie załadowano obrazu");
                return;
            }
        }

        private void Thickening_Click(object sender, RoutedEventArgs e)
        {
            if (currentBitmap != null)
            {
                var newBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Rgb24, null);
                var pixels = GetPixels();

                var hitOrMiss = PerformHitOrMiss(pixels, width, height);
                if (hitOrMiss != null)
                {
                    var newPixels = new byte[width * height * 3];
                    for (int i = 0; i < 3 * width * height;)
                    {
                        var hitOrMissPixel = (int)hitOrMiss[i];
                        var basePixel = (int)pixels[i];

                        newPixels[i++] = basePixel + hitOrMissPixel > 255 ? (byte)255 : basePixel + hitOrMissPixel < 0 ? (byte)0 : (byte)(basePixel + hitOrMissPixel);
                        newPixels[i++] = basePixel + hitOrMissPixel > 255 ? (byte)255 : basePixel + hitOrMissPixel < 0 ? (byte)0 : (byte)(basePixel + hitOrMissPixel);
                        newPixels[i++] = basePixel + hitOrMissPixel > 255 ? (byte)255 : basePixel + hitOrMissPixel < 0 ? (byte)0 : (byte)(basePixel + hitOrMissPixel);

                    }
                    newBitmap.WritePixels(new Int32Rect(0, 0, width, height), newPixels, width * 3, 0);
                    if (IsChangeOriginal)
                        currentBitmap = newBitmap;
                    displayedImage.Source = newBitmap;
                }
                else
                {
                    MessageBox.Show("Nie podano maski");
                    return;
                }
            }
            else
            {
                MessageBox.Show("Nie załadowano obrazu");
                return;
            }
        }

        private void Opening_Click(object sender, RoutedEventArgs e)
        {
            if (currentBitmap != null)
            {
                var newBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Rgb24, null);
                var pixels = GetPixels();

                var erodedPixels = PerformErosion(pixels, width, height);
                if (erodedPixels != null)
                {
                    var newPixels = PerformDilatation(erodedPixels, width, height);
                    newBitmap.WritePixels(new Int32Rect(0, 0, width, height), newPixels, width * 3, 0);
                    if (IsChangeOriginal)
                        currentBitmap = newBitmap; ;
                    displayedImage.Source = newBitmap;
                }
            }
            else
            {
                MessageBox.Show("Nie załadowano obrazu");
                return;
            }
        }

        private void Closing_Click(object sender, RoutedEventArgs e)
        {
            if (currentBitmap != null)
            {
                var newBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Rgb24, null);
                var pixels = GetPixels();

                var dilatedPixels = PerformDilatation(pixels, width, height);
                if (dilatedPixels != null)
                {
                    var newPixels = PerformErosion(dilatedPixels, width, height);
                    newBitmap.WritePixels(new Int32Rect(0, 0, width, height), newPixels, width * 3, 0);
                    if (IsChangeOriginal)
                        currentBitmap = newBitmap;
                    displayedImage.Source = newBitmap;
                }
            }
            else
            {
                MessageBox.Show("Nie załadowano obrazu");
                return;
            }
        }

        private byte[] PerformDilatation(byte[] pixels, int width, int height)
        {
            var newPixels = new byte[width * height * 3];
            var mask = ParseMask();
            if (mask != null)
            {
                int offset = ((mask.GetLength(0) - 1) / 2);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = (y * width + x) * 3;
                        byte maxR = 0, maxG = 0, maxB = 0;
                        for (int offsetY = -offset; offsetY <= offset; offsetY++)
                        {
                            for (int offsetX = -offset; offsetX <= offset; offsetX++)
                            {
                                int pixelY = y + offsetY;
                                int pixelX = x + offsetX;

                                if (pixelY >= 0 && pixelX >= 0 && pixelX < width && pixelY < height)
                                {
                                    int currentIndex = (pixelY * width + pixelX) * 3;
                                    if (mask[offsetY + offset, offsetX + offset] == 1)
                                    {
                                        maxR = Math.Max(maxR, pixels[currentIndex]);
                                        maxG = Math.Max(maxG, pixels[currentIndex + 1]);
                                        maxB = Math.Max(maxB, pixels[currentIndex + 2]);
                                    }
                                }
                            }
                        }

                        newPixels[index] = maxR;
                        newPixels[index + 1] = maxG;
                        newPixels[index + 2] = maxB;
                    }
                }
                return newPixels;
            }
            else
            {
                MessageBox.Show("Nie podano maski");
                return null;
            }
        }

        private byte[] PerformErosion(byte[] pixels, int width, int height)
        {
            var newPixels = new byte[width * height * 3];
            var mask = ParseMask();
            if (mask != null)
            {

                int offset = ((mask.GetLength(0) - 1) / 2);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = (y * width + x) * 3;
                        byte minR = 255, minG = 255, minB = 255;
                        for (int offsetY = -offset; offsetY <= offset; offsetY++)
                        {
                            for (int offsetX = -offset; offsetX <= offset; offsetX++)
                            {
                                int pixelY = y + offsetY;
                                int pixelX = x + offsetX;

                                if (pixelY >= 0 && pixelX >= 0 && pixelX < width && pixelY < height)
                                {
                                    int currentIndex = (pixelY * width + pixelX) * 3;
                                    if (mask[offsetY + offset, offsetX + offset] == 1)
                                    {
                                        minR = Math.Min(minR, pixels[currentIndex]);
                                        minG = Math.Min(minG, pixels[currentIndex + 1]);
                                        minB = Math.Min(minB, pixels[currentIndex + 2]);
                                    }
                                }
                            }
                        }

                        newPixels[index] = minR;
                        newPixels[index + 1] = minG;
                        newPixels[index + 2] = minB;
                    }
                }
                return newPixels;
            }
            else
            {
                MessageBox.Show("Nie podano maski");
                return null;
            }
        }

        private byte[] PerformHitOrMiss(byte[] pixels, int width, int height)
        {
            var newPixels = new byte[width * height * 3];
            var hitMask = ParseMask();
            if (hitMask != null)
            {
                var maskSize = hitMask.GetLength(0);
                var missMask = new int[maskSize, maskSize];
                for (int i = 0; i < maskSize; i++)
                {
                    for (int j = 0; j < maskSize; j++)
                    {
                        missMask[i, j] = hitMask[i, j] == 1 ? 0 : hitMask[i, j] == 0 ? 1 : 2;
                    }
                }

                int offset = ((hitMask.GetLength(0) - 1) / 2);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = (y * width + x) * 3;
                        bool HitAndMiss = true;
                        for (int offsetY = -offset; offsetY <= offset; offsetY++)
                        {
                            for (int offsetX = -offset; offsetX <= offset; offsetX++)
                            {
                                int pixelY = y + offsetY;
                                int pixelX = x + offsetX;

                                if (pixelY >= 0 && pixelX >= 0 && pixelX < width && pixelY < height)
                                {
                                    int currentIndex = (pixelY * width + pixelX) * 3;
                                    if (hitMask[offsetY + offset, offsetX + offset] == 1 && pixels[currentIndex] != 255)
                                    {
                                        HitAndMiss = false;
                                        break;
                                    }
                                    else if (missMask[offsetY + offset, offsetX + offset] == 1 && pixels[currentIndex] != 0)
                                    {
                                        HitAndMiss = false;
                                        break;
                                    }
                                }
                            }
                        }

                        newPixels[index] = HitAndMiss ? (byte)255 : (byte)0;
                        newPixels[index + 1] = HitAndMiss ? (byte)255 : (byte)0;
                        newPixels[index + 2] = HitAndMiss ? (byte)255 : (byte)0;
                    }
                }
                return newPixels;
            }
            else
            {
                MessageBox.Show("Nie podano maski");
                return null;
            }
        }

        private byte[] GetPixels()
        {
            int width = currentBitmap.PixelWidth;
            int height = currentBitmap.PixelHeight;
            var pixels = new byte[width * height * 3];
            currentBitmap.CopyPixels(pixels, width * 3, 0);
            for (int i = 0; i < 3 * width * height;)
            {
                var r = (int)pixels[i];
                var g = (int)pixels[i + 1];
                var b = (int)pixels[i + 2];

                var gray = (r + g + b) / 3;

                pixels[i++] = gray > 128 ? (byte)255 : (byte)0;
                pixels[i++] = gray > 128 ? (byte)255 : (byte)0;
                pixels[i++] = gray > 128 ? (byte)255 : (byte)0;

            }
            return pixels;
        }

        private int[,] ParseMask()
        {
            string[] mask = StructuringElementSize.Text.Split(' ');

            int row = 0, col = 0;
            var tmpSize = Math.Sqrt(mask.Length);
            if (tmpSize % 1 != 0 || tmpSize % 2 == 0 || tmpSize < 3)
            {
                MessageBox.Show($"Nieprawidłowa liczba elementów konstrukcyjnych, podaj minimum 9 elementów.\n Podano {mask.Length}");
                return null;
            }

            int kernelSize = (int)tmpSize;
            var kernel = new int[kernelSize, kernelSize];
            foreach (var val in mask)
            {
                var parsingResult = int.TryParse(val, out kernel[row, col]);
                if (!parsingResult || kernel[row, col] > 2 || kernel[row, col] < 0)
                {
                    MessageBox.Show($"Nieprawidłowe dane wejściowe maski: {val}");
                    return null;
                }
                row++;
                if (row == kernelSize)
                {
                    row = 0; col++;
                }
            }

            return kernel;
        }
    }
}
