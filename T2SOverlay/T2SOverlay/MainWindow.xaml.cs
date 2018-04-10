using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

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
        //Client
        public bool textboxOpened = false; //Method to keep from opening multiple textboxes in case they use a common character key as their hotkey, and press it while typing their message

        //Server
        private const int BUFFER_SIZE = 2048;
        private IPAddress IP = IPAddress.Loopback;
        private const int PORT = 100;
        private static readonly byte[] buffer = new byte[BUFFER_SIZE];

        private static readonly Socket ClientSocket = new Socket
            (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private SpeechSynthesizer speech;

        //Settings
        private Settings settings;
        public static Keys hotkeyMute, hotkeyDisplay; //Global hotkey
        private KeyboardHook keyboard = new KeyboardHook();

        public MainWindow()
        {
            InitializeComponent();
            DisconnectMenuItem.IsEnabled = false;
            LabelIP.Content = "Disconnected";

            //Setup text to speech synth
            speech = new SpeechSynthesizer();
            speech.Volume = 80;
            speech.Rate = 1;

            keyboard.KeyPressed += new EventHandler<KeyPressedEventArgs>(Keyboard_KeyPressed);

            string json = "";
            //Load hotkeyMute and hotkeyDisplay, then register them
            if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Local\\T2S Gaming\\settings.json"))
            {
                json = File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Local\\T2S Gaming\\settings.json");
                Keys[] keys = JsonConvert.DeserializeObject<Keys[]>(json);
                hotkeyMute = keys[0];
                hotkeyDisplay = keys[1];
            }
            else
            {
                //Create and load default
                hotkeyMute = Keys.M;
                hotkeyDisplay = Keys.U;
                Keys[] keys = { Keys.M, Keys.U };
                json = JsonConvert.SerializeObject(keys);
                Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Local\\T2S Gaming"); //Will create a directory if doesnt exist
                File.WriteAllText(@Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Local\\T2S Gaming\\settings.json", json);
            }

            //Register hotkeys
            keyboard.RegisterHotKey(hotkeyDisplay);
            keyboard.RegisterHotKey(hotkeyMute);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            //Unregister hotkeys and save current ones in settings
            keyboard.UnregisterHotKeys();

            base.OnClosing(e);
        }

        #region Toolbar

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                settings = new Settings(keyboard, hotkeyMute, hotkeyDisplay);
                settings.Show();
                settings.Activate();
                settings.Focus();
            }));
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
                System.Windows.MessageBox.Show("Could not connect to server", "ERROR");
            }
        }

        //Disconnect from chat server
        private void DisconnectMenuItem_Click(object sender, RoutedEventArgs e)
        {
            //Do disconnect
            if (System.Windows.MessageBox.Show("Are you sure?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
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
                    System.Windows.MessageBox.Show("Could not connect to server", "ERROR");
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
                        MakeRequests();
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
            Console.WriteLine("Now making requests to receive responses");
            Task.Run(() =>
            {
                //Continuously check for response
                while (true)
                {
                    ReceiveResponse();
                }
            });
        }

        /// <summary>
        /// Sends a string to the server with ASCII encoding.
        /// </summary>
        public void SendMessage(string text)
        {
            //Append name
            //TODO Use real username
            text = "username: " + text;
            byte[] buffer = Encoding.ASCII.GetBytes(text);
            //Append own text
            ChatBox.AppendText("Me: " + text + "\n");
            ClientSocket.Send(buffer, 0, buffer.Length, SocketFlags.None);
        }

        /// <summary>
        /// Receives byte array from server, converts to ASCII encoding to display
        /// Note that this is occurring on a separate thread
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
            speech.SpeakAsync(text);
            ChatBox.Dispatcher.Invoke(new AppendRTBSeparateThreadCallback(this.AppendRTBSeparateThread), new object[] { text });
        }

        public delegate void AppendRTBSeparateThreadCallback(string message);
        public void AppendRTBSeparateThread(string text)
        {
            ChatBox.AppendText(text + "\n");
        }

        #endregion

        #region Display Textbox.xaml
        ////////////////////////////////////////////////////////////////////////////////

        //Start doing foreground window chat textbox in current active window task

        ////////////////////////////////////////////////////////////////////////////////

        private Textbox tb;
        private void Keyboard_KeyPressed(object sender, KeyPressedEventArgs e)
        {
            if (e.Key.Equals(hotkeyDisplay) && !textboxOpened)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    tb = new Textbox(this);
                    tb.Show();
                    tb.Activate();
                    tb.Focus();
                    textboxOpened = true;
                }));
            }
            if(e.Key.Equals(hotkeyDisplay) && textboxOpened && tb != null)
            {
                tb.addHotkeyPressedButton(e.Key.ToString().ToLower());
            }
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
