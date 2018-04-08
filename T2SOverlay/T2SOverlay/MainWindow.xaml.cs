using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace T2SOverlay
{
    //TODO
    //Replace ChatBox with Listbox
    //Inside, will be a child StackPanel (Orientation: Vertical, HorizontalAlignment: Center)
    //This will contain the text the user sent and their profile picture

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Server server;

        private static object lockObj;

        private const int BUFFER_SIZE = 2048;
        private IPAddress IP = IPAddress.Loopback;
        private const int PORT = 100;
        private static readonly byte[] buffer = new byte[BUFFER_SIZE];

        private static readonly Socket ClientSocket = new Socket
            (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private SpeechSynthesizer speech;

        public MainWindow()
        {
            InitializeComponent();
            DisconnectMenuItem.IsEnabled = false;
            LabelIP.Content = "Disconnected";

            //Setup text to speech synth
            speech = new SpeechSynthesizer();
            speech.Volume = 80;
            speech.Rate = 1;

            //Setup lockObj for critical section regarding connection
        }

        #region Toolbar

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        //Open new window to edit profile
        private void ProfileMenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        //Connect to chat server
        private async void ConnectMenuItem_Click(object sender, RoutedEventArgs e)
        {
            bool conn = await Connect();
            //Set label to connected IP
            if (conn)
            {
                //If successful connect
                ConnectMenuItem.IsEnabled = false;
                CreateServerMenuItem.IsEnabled = false;
                DisconnectMenuItem.IsEnabled = true;
                LabelIP.Content = "Connected to IP: " + IP;
            }
            else
            {
                MessageBox.Show("Could not connect to server", "ERROR");
            }
        }

        //Disconnect from chat server
        private void DisconnectMenuItem_Click(object sender, RoutedEventArgs e)
        {
            //Do disconnect
            if (MessageBox.Show("Are you sure?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Disconnect();
                DisconnectMenuItem.IsEnabled = false;
                ConnectMenuItem.IsEnabled = true;
                CreateServerMenuItem.IsEnabled = true;
                LabelIP.Content = "Disconnected";
            }
        }

        //Create a server
        private async void CreateServerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ConnectMenuItem.IsEnabled
                && CreateServerMenuItem.IsEnabled
                && !DisconnectMenuItem.IsEnabled)
            {
                Server.SetupServer();
                ConnectMenuItem.IsEnabled = false;
                CreateServerMenuItem.IsEnabled = false;
                DisconnectMenuItem.IsEnabled = true;
                LabelIP.Content = "Connected to IP: 127.0.0.1 (localhost)";

                bool conn = await Connect();
                //Set label to connected IP
                if (!conn)
                {
                    MessageBox.Show("Could not connect to server", "ERROR");
                    Disconnect();
                    DisconnectMenuItem.IsEnabled = false;
                    ConnectMenuItem.IsEnabled = true;
                    CreateServerMenuItem.IsEnabled = true;
                    LabelIP.Content = "Disconnected";
                }
            }
        }

        //Close app on click Exit on toolbar Exit item
        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Environment.Exit(1);
        }

        #endregion

        #region Client socket connection to remote

        private void Disconnect()
        {
            //If the server was created, make sure to shut it down and disconnect other sockets
            if(!CreateServerMenuItem.IsEnabled)
            {
                Server.CloseAllSockets();
            }
            ClientSocket.Shutdown(SocketShutdown.Both);
            ClientSocket.Close();
        }

        private async Task<bool> Connect()
        {
            int attempts = 0;
            bool success = false;
            while (attempts++ < 5 && !ClientSocket.Connected)
            {
                // Change IPAddress.Loopback to a remote IP to connect to a remote host.
                success = await Task<bool>.Run(() =>
                {
                    try
                    {
                        ClientSocket.Connect(IP, PORT);
                        Console.WriteLine("Connected");
                        return true;
                    }
                    catch (SocketException)
                    {
                        return false;
                    }
                });
            }
            return success;
        }

        private void MakeRequests()
        {
            Task.Run(() =>
            {
                //Continuously check for response
                while (true)
                {
                    ReceiveResponse();
                }
            });
        }

        public void SendMessage(string message)
        {
            SendString(message);
        }

        /// <summary>
        /// Sends a string to the server with ASCII encoding.
        /// </summary>
        private void SendString(string text)
        {
            //Append name
            //TODO Use real username
            text = "username: " + text;
            byte[] buffer = Encoding.ASCII.GetBytes(text);
            ClientSocket.Send(buffer, 0, buffer.Length, SocketFlags.None);
            //Append own text
            ChatBox.AppendText("Me: " + text + "\n");
        }

        /// <summary>
        /// Receives byte array from server, converts to ASCII encoding to display
        /// </summary>
        private void ReceiveResponse()
        {
            var buffer = new byte[2048];
            int received = ClientSocket.Receive(buffer, SocketFlags.None);
            if (received == 0) return;
            var data = new byte[received];
            Array.Copy(buffer, data, received);
            string text = Encoding.ASCII.GetString(data);

            //Append to visual and do TTS
            ChatBox.AppendText(text + "\n");
            speech.SpeakAsync(text);
        }

        #endregion

        #region Display Textbox.xaml
        ////////////////////////////////////////////////////////////////////////////////

        //Start doing foreground window chat textbox in current active window task

        ////////////////////////////////////////////////////////////////////////////////

        //TODO
        //Convert this to keyboard event handler invoking user32.dll
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Thread.Sleep(2000);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Textbox tb = new Textbox(this);
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


        ////////////////////////////////////////////////////////////////////////////////

        //End doing foreground window chat textbox in current active window task

        ////////////////////////////////////////////////////////////////////////////////

        #endregion
    }
}
