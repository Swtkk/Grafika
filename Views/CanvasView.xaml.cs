using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Serialization;
using Graf.Models; // ShapeType, ResizeDirection, ShapeData/LineData/RectangleData/CircleData/CanvasData

namespace Graf.Views
{
    public partial class CanvasView : Window
    {
        private bool isDrawing = false;
        private bool isDragging = false;
        private bool isResizing = false;

        private Point startPoint;
        private ShapeType selectedShape = ShapeType.Line;
        private ResizeDirection resizeDirection = ResizeDirection.None;

        private Shape? currentShape;
        private readonly List<Shape> shapes = new();

        // margines trafienia (px)
        private const double Hit = 8.0;

        public CanvasView()
        {
            InitializeComponent();
            WindowState = WindowState.Maximized;

            // podgląd kursora przy krawędziach
            canvas.MouseMove += (_, e) => UpdateCursorForPosition(e.GetPosition(canvas));
            canvas.MouseLeave += (_, __) => Cursor = Cursors.Arrow;
        }

        // ======================= UI – wybór prymitywu =======================
        private void ShapeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb) return;
            int shapeNumber = 0;
            if (rb.Tag != null) int.TryParse(rb.Tag.ToString(), out shapeNumber);

            switch (shapeNumber)
            {
                case 1:
                    selectedShape = ShapeType.Line;
                    SizeStackPanel2.Visibility = Visibility.Visible;
                    setInputs(new Line(), false);
                    break;
                case 2:
                    selectedShape = ShapeType.Rectangle;
                    SizeStackPanel2.Visibility = Visibility.Visible;
                    setInputs(new Rectangle(), false);
                    break;
                case 3:
                    selectedShape = ShapeType.Circle;
                    SizeStackPanel2.Visibility = Visibility.Collapsed;
                    setInputs(new Ellipse(), false);
                    break;
            }
        }

        // ======================= RYSOWANIE z pól =======================
        private void DrawButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double x = Convert.ToDouble(XTextBox.Text);
                double y = Convert.ToDouble(YTextBox.Text);
                double size1 = Convert.ToDouble(SizeTextBox1.Text);
                double size2 = selectedShape == ShapeType.Circle ? size1 : Convert.ToDouble(SizeTextBox2.Text);

                Shape newShape = selectedShape switch
                {
                    ShapeType.Line => new Line(),
                    ShapeType.Rectangle => new Rectangle(),
                    ShapeType.Circle => new Ellipse(),
                    _ => new Line()
                };

                Draw(newShape, x, y, size1, size2);
            }
            catch
            {
                MessageBox.Show("Nieprawidłowe wartości wprowadzone do utworzenia kształtu.");
            }
        }

        private void Draw(Shape shape, double x1, double y1, double size1, double size2)
        {
            if (shape is Line line)
            {
                line.X1 = x1; line.Y1 = y1;
                line.X2 = size1; line.Y2 = size2;
                line.Stroke = Brushes.Black;
                shapes.Add(line);
                canvas.Children.Add(line);
            }
            else if (shape is Rectangle r)
            {
                r.Width = size1; r.Height = size2;
                r.Stroke = Brushes.Black; r.Fill = Brushes.Green;
                Canvas.SetLeft(r, x1); Canvas.SetTop(r, y1);
                shapes.Add(r);
                canvas.Children.Add(r);
            }
            else if (shape is Ellipse el)
            {
                el.Width = size1; el.Height = size1;
                el.Stroke = Brushes.Black; el.Fill = Brushes.Blue;
                Canvas.SetLeft(el, x1); Canvas.SetTop(el, y1);
                shapes.Add(el);
                canvas.Children.Add(el);
            }
        }

        // ======================= MYSZ – rysowanie/drag/resize =======================
        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            canvas.Focus();
            startPoint = e.GetPosition(canvas);
            currentShape = GetShapeUnderMouse(startPoint);

            isDrawing = isDragging = isResizing = false;
            resizeDirection = ResizeDirection.None;

            if (currentShape != null)
            {
                // Linie – złapanie za końce
                if (currentShape is Line l)
                {
                    var ends = IsPointNearLineEnds(startPoint, l, Hit);
                    if (ends.isNear)
                    {
                        isResizing = true;
                        resizeDirection = ends.nearStart ? ResizeDirection.Left : ResizeDirection.Right;
                        PopulateEditFields(currentShape);
                        return;
                    }
                    // jeśli nie za końce – złap całą linię
                    isDragging = true;
                    PopulateEditFields(currentShape);
                    return;
                }

                // Okrąg – krawędź
                if (IsPointOnEllipseEdge(startPoint, currentShape))
                {
                    isResizing = true;
                    resizeDirection = ResizeDirection.Ellipse;
                    PopulateEditFields(currentShape);
                    return;
                }

                // Prostokąt – krawędzie/narożniki
                if (IsPointNearTopEdge(startPoint, currentShape))
                {
                    isResizing = true;
                    resizeDirection = IsPointNearLeftEdge(startPoint, currentShape) ? ResizeDirection.TopLeft
                                   : IsPointNearRightEdge(startPoint, currentShape) ? ResizeDirection.TopRight
                                   : ResizeDirection.Top;
                }
                else if (IsPointNearBottomEdge(startPoint, currentShape))
                {
                    isResizing = true;
                    resizeDirection = IsPointNearLeftEdge(startPoint, currentShape) ? ResizeDirection.BottomLeft
                                   : IsPointNearRightEdge(startPoint, currentShape) ? ResizeDirection.BottomRight
                                   : ResizeDirection.Bottom;
                }
                else if (IsPointNearLeftEdge(startPoint, currentShape))
                {
                    isResizing = true; resizeDirection = ResizeDirection.Left;
                }
                else if (IsPointNearRightEdge(startPoint, currentShape))
                {
                    isResizing = true; resizeDirection = ResizeDirection.Right;
                }
                else
                {
                    isDragging = true;
                }

                PopulateEditFields(currentShape);
            }
            else
            {
                // zaczynamy rysowanie „z ręki”
                isDrawing = true;
                currentShape = null;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point endPoint = e.GetPosition(canvas);

            // RYSOWANIE
            if (isDrawing)
            {
                switch (selectedShape)
                {
                    case ShapeType.Line:
                        if (currentShape == null)
                        {
                            currentShape = new Line { Stroke = Brushes.Black };
                            shapes.Add(currentShape);
                            canvas.Children.Add(currentShape);
                        }
                        var ln = (Line)currentShape;
                        ln.X1 = startPoint.X; ln.Y1 = startPoint.Y;
                        ln.X2 = endPoint.X; ln.Y2 = endPoint.Y;
                        break;

                    case ShapeType.Rectangle:
                        if (currentShape == null)
                        {
                            currentShape = new Rectangle { Fill = Brushes.Green, Stroke = Brushes.Black };
                            shapes.Add(currentShape);
                            canvas.Children.Add(currentShape);
                        }
                        double w = Math.Abs(endPoint.X - startPoint.X);
                        double h = Math.Abs(endPoint.Y - startPoint.Y);
                        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                            h = w; // zachowaj proporcje
                        currentShape.Width = w;
                        currentShape.Height = h;
                        Canvas.SetLeft(currentShape, Math.Min(startPoint.X, endPoint.X));
                        Canvas.SetTop(currentShape, Math.Min(startPoint.Y, endPoint.Y));
                        break;

                    case ShapeType.Circle:
                        if (currentShape == null)
                        {
                            currentShape = new Ellipse { Fill = Brushes.Blue, Stroke = Brushes.Black };
                            shapes.Add(currentShape);
                            canvas.Children.Add(currentShape);
                        }
                        double d = Math.Min(Math.Abs(endPoint.X - startPoint.X), Math.Abs(endPoint.Y - startPoint.Y));
                        currentShape.Width = d; currentShape.Height = d;
                        Canvas.SetLeft(currentShape, Math.Min(startPoint.X, endPoint.X));
                        Canvas.SetTop(currentShape, Math.Min(startPoint.Y, endPoint.Y));
                        break;
                }
                return;
            }

            // PRZESUWANIE
            if (isDragging && currentShape != null)
            {
                double dx = endPoint.X - startPoint.X;
                double dy = endPoint.Y - startPoint.Y;

                if (currentShape is Line l)
                {
                    l.X1 += dx; l.Y1 += dy; l.X2 += dx; l.Y2 += dy;
                }
                else
                {
                    Canvas.SetLeft(currentShape, Canvas.GetLeft(currentShape) + dx);
                    Canvas.SetTop(currentShape, Canvas.GetTop(currentShape) + dy);
                }

                startPoint = endPoint;
                return;
            }

            // ZMIANA ROZMIARU
            if (isResizing && currentShape != null)
            {
                if (currentShape is Rectangle rect)
                {
                    double left = Canvas.GetLeft(rect);
                    double top = Canvas.GetTop(rect);
                    double right = left + rect.Width;
                    double bottom = top + rect.Height;

                    if (resizeDirection is ResizeDirection.Left or ResizeDirection.TopLeft or ResizeDirection.BottomLeft)
                        left = endPoint.X;
                    if (resizeDirection is ResizeDirection.Right or ResizeDirection.TopRight or ResizeDirection.BottomRight)
                        right = endPoint.X;
                    if (resizeDirection is ResizeDirection.Top or ResizeDirection.TopLeft or ResizeDirection.TopRight)
                        top = endPoint.Y;
                    if (resizeDirection is ResizeDirection.Bottom or ResizeDirection.BottomLeft or ResizeDirection.BottomRight)
                        bottom = endPoint.Y;

                    // zachowaj proporcje przy SHIFT
                    if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                    {
                        double newW = Math.Abs(right - left);
                        double newH = Math.Abs(bottom - top);
                        double side = Math.Max(newW, newH);
                        // dopasuj w zależności od kierunku
                        switch (resizeDirection)
                        {
                            case ResizeDirection.TopLeft: left = right - side; top = bottom - side; break;
                            case ResizeDirection.TopRight: right = left + side; top = bottom - side; break;
                            case ResizeDirection.BottomLeft: left = right - side; bottom = top + side; break;
                            case ResizeDirection.BottomRight: right = left + side; bottom = top + side; break;
                            case ResizeDirection.Left: left = right - side; break;
                            case ResizeDirection.Right: right = left + side; break;
                            case ResizeDirection.Top: top = bottom - side; break;
                            case ResizeDirection.Bottom: bottom = top + side; break;
                        }
                    }

                    // normalizacja
                    double newLeft = Math.Min(left, right);
                    double newTop = Math.Min(top, bottom);
                    double newWidth = Math.Abs(right - left);
                    double newHeight = Math.Abs(bottom - top);

                    Canvas.SetLeft(rect, newLeft);
                    Canvas.SetTop(rect, newTop);
                    rect.Width = Math.Max(0, newWidth);
                    rect.Height = Math.Max(0, newHeight);
                }
                else if (currentShape is Ellipse ellipse)
                {
                    double cx = Canvas.GetLeft(ellipse) + ellipse.Width / 2.0;
                    double cy = Canvas.GetTop(ellipse) + ellipse.Height / 2.0;
                    double radius = Math.Max(Math.Abs(endPoint.X - cx), Math.Abs(endPoint.Y - cy));
                    Canvas.SetLeft(ellipse, cx - radius);
                    Canvas.SetTop(ellipse, cy - radius);
                    ellipse.Width = radius * 2.0;
                    ellipse.Height = ellipse.Width; // zawsze koło
                }
                else if (currentShape is Line line)
                {
                    double dx = endPoint.X - startPoint.X;
                    double dy = endPoint.Y - startPoint.Y;

                    if (resizeDirection == ResizeDirection.Left)
                    {
                        line.X1 += dx; line.Y1 += dy;
                    }
                    else if (resizeDirection == ResizeDirection.Right)
                    {
                        line.X2 += dx; line.Y2 += dy;
                    }
                }

                startPoint = endPoint;
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDrawing = isDragging = isResizing = false;
            currentShape = GetShapeUnderMouse(e.GetPosition(canvas));
            PopulateEditFields(currentShape);
        }

        // Klawiatura – strzałki (przesuw), Shift+strzałki (rozmiar)
        private void Canvas_KeyDown(object sender, KeyEventArgs e)
        {
            if (currentShape == null) return;

            double dx = 0, dy = 0;
            switch (e.Key)
            {
                case Key.Up: dy = -2; break;
                case Key.Down: dy = 2; break;
                case Key.Left: dx = -2; break;
                case Key.Right: dx = 2; break;
                default: return;
            }

            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                if (currentShape is Rectangle r)
                {
                    r.Width = Math.Max(0, r.Width + dx);
                    r.Height = Math.Max(0, r.Height + dy);
                }
                else if (currentShape is Ellipse el)
                {
                    double d = Math.Max(0, el.Width + Math.Max(dx, dy));
                    el.Width = d; el.Height = d;
                }
                else if (currentShape is Line l)
                {
                    l.X2 += dx; l.Y2 += dy;
                }
            }
            else
            {
                if (currentShape is Line l)
                {
                    l.X1 += dx; l.Y1 += dy; l.X2 += dx; l.Y2 += dy;
                }
                else
                {
                    Canvas.SetLeft(currentShape, Canvas.GetLeft(currentShape) + dx);
                    Canvas.SetTop(currentShape, Canvas.GetTop(currentShape) + dy);
                }
            }
        }

        // ======================= Zapis/Odczyt XML =======================
        private void SaveToFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "XML Files|*.xml" };
            if (dlg.ShowDialog() == true)
            {
                var list = new List<ShapeData>();
                foreach (var s in shapes)
                {
                    if (s is Line ln)
                        list.Add(new LineData { X1 = ln.X1, Y1 = ln.Y1, X2 = ln.X2, Y2 = ln.Y2 });
                    else if (s is Rectangle r)
                        list.Add(new RectangleData { X = Canvas.GetLeft(r), Y = Canvas.GetTop(r), Width = r.Width, Height = r.Height });
                    else if (s is Ellipse el)
                        list.Add(new CircleData { X = Canvas.GetLeft(el), Y = Canvas.GetTop(el), Diameter = el.Width });
                }
                var data = new CanvasData { Shapes = list };

                using var sw = new StreamWriter(dlg.FileName);
                var serializer = new XmlSerializer(typeof(CanvasData));
                serializer.Serialize(sw, data);
            }
        }

        private void LoadFromFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "XML Files|*.xml" };
            if (dlg.ShowDialog() == true)
            {
                using var sr = new StreamReader(dlg.FileName);
                var serializer = new XmlSerializer(typeof(CanvasData));
                var data = (CanvasData)serializer.Deserialize(sr)!;

                foreach (var sd in data.Shapes)
                {
                    if (sd is LineData ld)
                        Draw(new Line(), ld.X1, ld.Y1, ld.X2, ld.Y2);
                    else if (sd is RectangleData rd)
                        Draw(new Rectangle(), rd.X, rd.Y, rd.Width, rd.Height);
                    else if (sd is CircleData cd)
                        Draw(new Ellipse(), cd.X, cd.Y, cd.Diameter, cd.Diameter);
                }
            }
        }

        // ======================= Selekcja i edycja z pól =======================
        private Shape? GetShapeUnderMouse(Point p)
        {
            // iteruj od góry – chwytaj „wierzchni” obiekt
            for (int i = shapes.Count - 1; i >= 0; i--)
            {
                var s = shapes[i];
                if (s is Ellipse el && IsPointInsideEllipse(p, el)) return s;
                if (s is Rectangle r && IsPointInsideRectangle(p, r)) return s;
                if (s is Line ln && (IsPointNearLine(p, ln) || IsPointNearLineEnds(p, ln, Hit).isNear)) return s;
            }
            return null;
        }

        private void PopulateEditFields(Shape? s)
        {
            if (s != null) setInputs(s, true);
            else
            {
                XTextBox.Text = YTextBox.Text = SizeTextBox1.Text = SizeTextBox2.Text = "";
            }
        }

        // ======================= Hit testy =======================
        private bool IsPointInsideEllipse(Point p, Ellipse el)
        {
            double a = el.Width / 2.0, b = el.Height / 2.0;
            double cx = Canvas.GetLeft(el) + a, cy = Canvas.GetTop(el) + b;
            double nx = (p.X - cx) / a, ny = (p.Y - cy) / b;
            return nx * nx + ny * ny <= 1.0;
        }

        private bool IsPointInsideRectangle(Point p, Rectangle r)
        {
            double left = Canvas.GetLeft(r), top = Canvas.GetTop(r);
            return p.X >= left && p.X <= left + r.Width && p.Y >= top && p.Y <= top + r.Height;
        }

        private bool IsPointNearLine(Point p, Line l)
        {
            double x1 = l.X1, y1 = l.Y1, x2 = l.X2, y2 = l.Y2;
            double dist = Math.Abs((y2 - y1) * p.X - (x2 - x1) * p.Y + x2 * y1 - y2 * x1) /
                          Math.Sqrt(Math.Pow(y2 - y1, 2) + Math.Pow(x2 - x1, 2));
            // dodatkowo w obrębie odcinka
            bool within = p.X >= Math.Min(x1, x2) - Hit && p.X <= Math.Max(x1, x2) + Hit &&
                          p.Y >= Math.Min(y1, y2) - Hit && p.Y <= Math.Max(y1, y2) + Hit;
            return within && dist <= Hit;
        }

        private (bool isNear, bool nearStart) IsPointNearLineEnds(Point p, Line l, double tol)
        {
            var s = new Point(l.X1, l.Y1);
            var e = new Point(l.X2, l.Y2);
            bool ns = (Math.Abs(p.X - s.X) <= tol && Math.Abs(p.Y - s.Y) <= tol);
            bool ne = (Math.Abs(p.X - e.X) <= tol && Math.Abs(p.Y - e.Y) <= tol);
            if (ns) return (true, true);
            if (ne) return (true, false);
            return (false, false);
        }

        private bool IsPointNearTopEdge(Point p, Shape s)
        {
            if (s is Rectangle r)
            {
                double top = Canvas.GetTop(r);
                return Math.Abs(p.Y - top) <= Hit && p.X >= Canvas.GetLeft(r) - Hit && p.X <= Canvas.GetLeft(r) + r.Width + Hit;
            }
            return false;
        }
        private bool IsPointNearBottomEdge(Point p, Shape s)
        {
            if (s is Rectangle r)
            {
                double bottom = Canvas.GetTop(r) + r.Height;
                return Math.Abs(p.Y - bottom) <= Hit && p.X >= Canvas.GetLeft(r) - Hit && p.X <= Canvas.GetLeft(r) + r.Width + Hit;
            }
            return false;
        }
        private bool IsPointNearLeftEdge(Point p, Shape s)
        {
            if (s is Rectangle r)
            {
                double left = Canvas.GetLeft(r);
                return Math.Abs(p.X - left) <= Hit && p.Y >= Canvas.GetTop(r) - Hit && p.Y <= Canvas.GetTop(r) + r.Height + Hit;
            }
            else if (s is Line l)
            {
                double minX = Math.Min(l.X1, l.X2);
                return Math.Abs(p.X - minX) <= Hit;
            }
            return false;
        }
        private bool IsPointNearRightEdge(Point p, Shape s)
        {
            if (s is Rectangle r)
            {
                double right = Canvas.GetLeft(r) + r.Width;
                return Math.Abs(p.X - right) <= Hit && p.Y >= Canvas.GetTop(r) - Hit && p.Y <= Canvas.GetTop(r) + r.Height + Hit;
            }
            else if (s is Line l)
            {
                double maxX = Math.Max(l.X1, l.X2);
                return Math.Abs(p.X - maxX) <= Hit;
            }
            return false;
        }
        private bool IsPointOnEllipseEdge(Point p, Shape s)
        {
            if (s is Ellipse el)
            {
                double cx = Canvas.GetLeft(el) + el.Width / 2.0;
                double cy = Canvas.GetTop(el) + el.Height / 2.0;
                double r = el.Width / 2.0;
                double d = Math.Sqrt(Math.Pow(p.X - cx, 2) + Math.Pow(p.Y - cy, 2));
                return Math.Abs(d - r) <= Hit;
            }
            return false;
        }

        // ======================= Kursor =======================
        private void UpdateCursorForPosition(Point p)
        {
            var s = GetShapeUnderMouse(p);
            if (s is Rectangle)
            {
                bool top = IsPointNearTopEdge(p, s), bottom = IsPointNearBottomEdge(p, s),
                     left = IsPointNearLeftEdge(p, s), right = IsPointNearRightEdge(p, s);

                if ((top && left) || (bottom && right)) { Cursor = Cursors.SizeNWSE; return; }
                if ((top && right) || (bottom && left)) { Cursor = Cursors.SizeNESW; return; }
                if (left || right) { Cursor = Cursors.SizeWE; return; }
                if (top || bottom) { Cursor = Cursors.SizeNS; return; }
                Cursor = Cursors.SizeAll; return;
            }
            if (s is Ellipse && IsPointOnEllipseEdge(p, s)) { Cursor = Cursors.SizeAll; return; }
            if (s is Line l)
            {
                var ends = IsPointNearLineEnds(p, l, Hit);
                if (ends.isNear) { Cursor = Cursors.Cross; return; }
                if (IsPointNearLine(p, l)) { Cursor = Cursors.SizeAll; return; }
            }
            Cursor = Cursors.Arrow;
        }

        // ======================= Inne – czyszczenie/edycja pól =======================
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            canvas.Children.Clear();
            shapes.Clear();

            XTextBox.Text = YTextBox.Text = SizeTextBox1.Text = SizeTextBox2.Text = "";
            XEditTextBox.Text = YEditTextBox.Text = SizeEditTextBox1.Text = SizeEditTextBox2.Text = "";
        }

        public void setInputs(Shape shape, bool isEdit)
        {
            if (!isEdit)
            {
                if (shape is Line)
                {
                    XLabel.Content = "X1"; YLabel.Content = "Y1";
                    SizeLabel1.Content = "X2"; SizeLabel2.Content = "Y2";
                }
                else if (shape is Rectangle)
                {
                    XLabel.Content = "X"; YLabel.Content = "Y";
                    SizeLabel1.Content = "Szerokość"; SizeLabel2.Content = "Wysokość";
                }
                else if (shape is Ellipse)
                {
                    XLabel.Content = "X"; YLabel.Content = "Y";
                    SizeLabel1.Content = "Średnica"; SizeLabel2.Content = "";
                }
                return;
            }

            if (shape is Line line)
            {
                XEditTextBox.Text = line.X1.ToString();
                YEditTextBox.Text = line.Y1.ToString();
                SizeEditTextBox1.Text = line.X2.ToString();
                SizeEditTextBox2.Text = line.Y2.ToString();
                XEditLabel.Content = "X1"; YEditLabel.Content = "Y1";
                SizeEditLabel1.Content = "X2";
                SizeEditLabel2.Visibility = Visibility.Visible;
                SizeEditTextBox2.Visibility = Visibility.Visible;
                SizeEditLabel2.Content = "Y2";
            }
            else if (shape is Rectangle r)
            {
                XEditTextBox.Text = Canvas.GetLeft(r).ToString();
                YEditTextBox.Text = Canvas.GetTop(r).ToString();
                SizeEditTextBox1.Text = r.Width.ToString();
                SizeEditTextBox2.Text = r.Height.ToString();
                XEditLabel.Content = "X"; YEditLabel.Content = "Y";
                SizeEditLabel1.Content = "Szerokość";
                SizeEditLabel2.Visibility = Visibility.Visible;
                SizeEditTextBox2.Visibility = Visibility.Visible;
                SizeEditLabel2.Content = "Wysokość";
            }
            else if (shape is Ellipse el)
            {
                XEditTextBox.Text = Canvas.GetLeft(el).ToString();
                YEditTextBox.Text = Canvas.GetTop(el).ToString();
                SizeEditTextBox1.Text = el.Width.ToString();
                SizeEditTextBox2.Text = "";
                XEditLabel.Content = "X"; YEditLabel.Content = "Y";
                SizeEditLabel1.Content = "Średnica";
                SizeEditLabel2.Content = "";
                SizeEditLabel2.Visibility = Visibility.Collapsed;
                SizeEditTextBox2.Visibility = Visibility.Collapsed;
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentShape == null) return;

            try
            {
                if (double.TryParse(XEditTextBox.Text, out double x) &&
                    double.TryParse(YEditTextBox.Text, out double y) &&
                    double.TryParse(SizeEditTextBox1.Text, out double s1))
                {
                    double.TryParse(SizeEditTextBox2.Text, out double s2);

                    if (currentShape is Rectangle r)
                    {
                        Canvas.SetLeft(r, x); Canvas.SetTop(r, y);
                        r.Width = s1; r.Height = s2;
                    }
                    else if (currentShape is Ellipse el)
                    {
                        Canvas.SetLeft(el, x); Canvas.SetTop(el, y);
                        el.Width = s1; el.Height = s1;
                    }
                    else if (currentShape is Line l)
                    {
                        l.X1 = x; l.Y1 = y; l.X2 = s1; l.Y2 = s2;
                    }
                }
            }
            catch
            {
                MessageBox.Show("Nieprawidłowe wartości wprowadzone do edycji kształtu.");
            }
        }
    }
}
