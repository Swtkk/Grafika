using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Graf.Views;

namespace Graf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenCanvasBtn_Click(object sender, RoutedEventArgs e)
        {
            var win = new CanvasView(); // Twoje okno rysowania
            win.Owner = this;
            win.ShowDialog();
        }

        private void OpenPpmBtn_Click(object sender, RoutedEventArgs e)
        {
            var win = new Cmyk();
            win.Owner = this;
            win.ShowDialog();
        }

        private void PPM_Click(object sender, RoutedEventArgs e)
        {
            PPM pPM = new PPM();
            pPM.Show();
            this.Hide();
        }
    }
}