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
using System.Windows.Shapes;
using System.Net;

namespace T2SOverlay
{
    /// <summary>
    /// Interaction logic for IPForm.xaml
    /// </summary>
    public partial class IPForm : Window
    {
        public IPForm()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(IPAddress.Text))
            {
                MainWindow.IP = System.Net.IPAddress.Loopback;
                this.Close();
            }
            else
            {
                IPAddress addr = System.Net.IPAddress.Loopback;
                if (System.Net.IPAddress.TryParse(IPAddress.Text, out addr))
                {
                    MainWindow.IP = addr;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Invalid IP!\nExample IP Format: 127.0.0.1", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
