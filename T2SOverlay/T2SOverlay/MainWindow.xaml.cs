using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi.Session;
using AudioSwitcher.AudioApi;

namespace T2SOverlay
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CoreAudioController cont;

        public MainWindow()
        {
            InitializeComponent();
            cont = new CoreAudioController();
        }
        
        
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("init");
            //cont
            foreach (CoreAudioDevice d in cont.GetDevices())
            {
                Console.WriteLine(d.Name);
                
            }
            Console.WriteLine("here");
            
                   
            Thread.Sleep(2000);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Textbox tb = new Textbox();
                tb.Show();
                tb.Activate();
                tb.Focus();
                Console.WriteLine("here");

                Console.WriteLine(GetActiveWindowTitle());
            }));
            
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
        }
    }
}
