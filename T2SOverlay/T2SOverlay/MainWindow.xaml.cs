using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

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
        public static Socket ClientSocket;

        //Server
        public static IPAddress IP = IPAddress.Loopback; //use IPAddress.Loopback if creating server
        private const int PORT = 100;
        private bool ServerHost = false;

        private SpeechSynthesizer speech;

        public List<T2SUser> ConnectedUsers;


        //Settings
        private Settings settings;
        public static Keys hotkeyMute, hotkeyDisplay, hotkeyDisableHotkeys; //Global hotkey
        private static KeyboardHook keyboard = new KeyboardHook();

        //User information
        private Profile profile;
        public byte[] ProfilePicture;
        public string Username;
        public string MacAddr { get; }

        public MainWindow()
        {
            InitializeComponent();
            DisconnectMenuItem.IsEnabled = false;
            LabelIP.Content = "Disconnected";

            ConnectedUsers = new List<T2SUser>();
            ListView_ConnectedUsers.ItemsSource = ConnectedUsers;

            //Setup text to speech synth
            speech = new SpeechSynthesizer();
            speech.Volume = 80;
            speech.Rate = 1;

            keyboard.KeyPressed += new EventHandler<KeyPressedEventArgs>(Keyboard_KeyPressed);

            //Load user information
            MacAddr = (from nic in NetworkInterface.GetAllNetworkInterfaces()
                       where nic.OperationalStatus == OperationalStatus.Up
                       select nic.GetPhysicalAddress().ToString()).FirstOrDefault();

            string json = "";
            //Load profile data
            if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Local\\T2S Gaming\\profile.json"))
            {
                json = File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Local\\T2S Gaming\\profile.json");
                T2SUser user = JsonConvert.DeserializeObject<T2SUser>(json);
                ProfilePicture = user.ProfilePicture;
                Username = user.Username;
            }
            else
            {
                //Create and load default
                Username = Environment.UserName; //Just use their username from their PC
                ProfilePicture = GetBytesFromBitmap(Properties.Resources.blank_profile); //convert image to string
                json = JsonConvert.SerializeObject(new T2SUser()
                {
                    Username = this.Username,
                    ProfilePicture = this.ProfilePicture,
                    MacAddr = this.MacAddr
                });
                Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Local\\T2S Gaming"); //Will create a directory if doesnt exist
                File.WriteAllText(@Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Local\\T2S Gaming\\profile.json", json);
            }

            json = "";
            //Load hotkeyMute and hotkeyDisplay, then register them
            if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Local\\T2S Gaming\\settings.json"))
            {
                json = File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Local\\T2S Gaming\\settings.json");
                Keys[] keys = JsonConvert.DeserializeObject<Keys[]>(json);
                hotkeyMute = keys[0];
                hotkeyDisplay = keys[1];
                hotkeyDisableHotkeys = keys[2];
            }
            else
            {
                //Create and load default
                hotkeyMute = Keys.M;
                hotkeyDisplay = Keys.U;
                hotkeyDisableHotkeys = Keys.End;
                Keys[] keys = { Keys.M, Keys.U, Keys.End };
                json = JsonConvert.SerializeObject(keys);
                Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Local\\T2S Gaming"); //Will create a directory if doesnt exist
                File.WriteAllText(@Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Local\\T2S Gaming\\settings.json", json);
            }
            LoadHotkeys();
        }

        public static void LoadHotkeys()
        {
            //Register hotkeys
            try
            {
                keyboard.RegisterHotKey(hotkeyDisplay);
                keyboard.RegisterHotKey(hotkeyMute);
                keyboard.RegisterHotKey(hotkeyDisableHotkeys);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public static void UnregisterHotkeys()
        {
            keyboard.UnregisterHotKeys();
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
                settings = new Settings(keyboard, hotkeyMute, hotkeyDisplay, hotkeyDisableHotkeys);
                settings.Show();
                settings.Activate();
                settings.Focus();
            }));
        }

        //Open new window to edit profile
        private void ProfileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                profile = new Profile(this, this.ProfilePicture, this.Username);
                profile.Show();
                profile.Activate();
                profile.Focus();
            }));
        }

        public static bool gotNewIP = false;
        //Connect to chat server
        private async void ConnectMenuItem_Click(object sender, RoutedEventArgs e)
        {
            IPForm form = new IPForm();
            form.ShowDialog();
            if (!gotNewIP)
                return;
            gotNewIP = false;
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
                ServerHost = true;
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

                    ServerHost = false;
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
            ClientSocket.Shutdown(SocketShutdown.Both);
            ClientSocket.Close();
            //If the server was created, make sure to shut it down and disconnect other sockets
            if (ServerHost)
            {
                Server.CloseAllSockets();
            }
            ConnectedUsers.Clear();
            CollectionViewSource.GetDefaultView(ConnectedUsers).Refresh();
            ChatBox.Dispatcher.Invoke(new ChatBoxClearSeparateThreadCallback(this.ChatBoxClearSeparateThread), new object[] { });
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            //Save keys
            Keys[] keys = { hotkeyMute, hotkeyDisplay, hotkeyDisableHotkeys };
            string json = JsonConvert.SerializeObject(keys);
            Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Local\\T2S Gaming"); //Will create a directory if doesnt exist
            File.WriteAllText(@Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Local\\T2S Gaming\\settings.json", json);

            //Save profile
            json = JsonConvert.SerializeObject(new T2SUser()
            {
                Username = this.Username,
                ProfilePicture = this.ProfilePicture,
                MacAddr = this.MacAddr
            });
            Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Local\\T2S Gaming"); //Will create a directory if doesnt exist
            File.WriteAllText(@Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Local\\T2S Gaming\\profile.json", json);
        }

        private async Task<bool> Connect()
        {
            ClientSocket = new Socket
            (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
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
                        SendMessage("", true, true); //First connect is true
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
                //Continuously check for response in a syncrhonous fashion in another thread
                while (true && ClientSocket.Connected)
                {
                    ReceiveResponse();
                }
            });
        }

        /// <summary>
        /// Sends a bytes to the server, simply containing a T2SClientMessage and header
        /// </summary>
        public void SendMessage(string text, bool firstConnect, bool updateProfile)
        {
            T2SClientMessage message = new T2SClientMessage()
            {
                MacAddr = this.MacAddr,
                ProfilePicture = (updateProfile) ? this.ProfilePicture : null,
                UpdateProfile = updateProfile, //change to be a bool that is set when we add Profile settings
                Username = this.Username,
                FirstConnect = firstConnect, //change to be a bool
                Message = text
            };
            byte[] buffer = ObjectToByteArray(message), temp = ObjectToByteArray(message);

            string header = buffer.Length.ToString();
            //If the header length is larger than the maximum length, we're gonna assume that the dude is trying to destroy someone with a fat receive. 
            //There's no reason for something to be this large
            //Therefore, just let the sender know that their message is waaaay too big
            if (header.Length < 32)
            {
                Console.WriteLine("Generating header...");
                for (int i = header.Length; i < 32; i++)
                {
                    header += " "; //Append empty spaces until header is max length (32)
                }
                byte[] headerBytes = Encoding.ASCII.GetBytes(header + "|");

                buffer = new byte[temp.Length + headerBytes.Length];

                Array.Copy(headerBytes, buffer, headerBytes.Length);
                Array.Copy(temp, 0, buffer, 33, temp.Length);
                //Append own text
                ChatBox.Dispatcher.Invoke(new AppendRTBSeparateThreadCallback(this.AppendChatBoxListViewSeparateThread), new object[] { message });
                ClientSocket.Send(buffer, 0, buffer.Length, SocketFlags.None);
            }
        }

        /// <summary>
        /// Receives byte array from server, converts to ASCII encoding to display
        /// Note that this is occurring on a separate thread
        /// 
        /// HEADER AND BODY
        /// Header is defined as the zeroth index of a BAR | string split
        /// The header may also never be larger than 32 bytes (if it is, then this implies the message is SUPER long and we should probably just ignore it.
        ///     The server should also recognize this failure and send an error message to the sending client that the message sent is too large (like, honestly, 32 bytes is a 32 digit integer. 
        ///     Who even would send something that big? 
        ///     RIP that person's bandwidth lol))
        /// The header will represent the TOTAL LENGTH EXCLUSIVE of the header, thereby representing the buffer length of the NEXT receive, which should by protocol be a json file.
        /// The header will also always be length 32, append pipe separator to be 33
        /// 
        /// The first index will be a byte array that is the object T2SClientMessage
        /// 
        /// </summary>
        private void ReceiveResponse()
        {
            if (!IsConnected(ClientSocket))
            {
                Disconnect();
            }
            int received = 0;
            var buffer = new byte[33];
            try
            {
                received =  ClientSocket.Receive(buffer, 33, SocketFlags.None);
            }
            catch(Exception e)
            {
                return;
            }
            if (received == 0) return; //Nothing to receive
            string text = Encoding.ASCII.GetString(buffer); //Hopefully the header
            Console.WriteLine(text);
            //If full header not grabbed, append the new bytes until header is grabbed
            while (received < 33)
            {
                byte[] tempBuffer = new byte[33 - received];
                int appendableBytes = ClientSocket.Receive(tempBuffer, 33 - received, SocketFlags.None);
                Array.Copy(tempBuffer, 0, buffer, received, tempBuffer.Length);
                received += appendableBytes;
            }
            Console.WriteLine("Received");
            int header;
            //Successfully got header
            if (Int32.TryParse(text.Split('|')[0], out header))
            {
                buffer = new byte[header];
                received = ClientSocket.Receive(buffer, header, SocketFlags.None);
                while(received < header)
                {
                    byte[] tempBuffer = new byte[header - received];
                    int appendableBytes = ClientSocket.Receive(tempBuffer, header - received, SocketFlags.None);
                    Array.Copy(tempBuffer, 0, buffer, received, tempBuffer.Length);
                    received += appendableBytes;
                }

                T2SClientMessage message = (T2SClientMessage)ByteArrayToObject(buffer);
                //Perform logic
                //In order to do anything, the client must first be connected
                if (message.Connected)
                {
                    //If this is the first connection, we're not going to display anything new in the chat
                    if (message.FirstConnect)
                    {
                        ConnectedUsers.Add(new T2SUser()
                        {
                            MacAddr = message.MacAddr,
                            Username = message.Username,
                            ProfilePicture = message.ProfilePicture
                        });
                        ListView_ConnectedUsers.Dispatcher.Invoke(new AppendListViewConnectedUsersSeparateThreadCallback(this.AppendListViewConnecteduseresSeparateThread), new object[] { message });
                    }
                    else
                    {
                        if (message.UpdateProfile)
                        {
                            bool found = false;
                            //Modify user if macaddr exists. If not, add
                            foreach (T2SUser user in ConnectedUsers)
                            {
                                if (user.MacAddr == message.MacAddr)
                                {
                                    //exists. So update the user
                                    user.ProfilePicture = (message.ProfilePicture == null) ? user.ProfilePicture : message.ProfilePicture;
                                    user.Username = message.Username; //Should never be null
                                    found = true;
                                    //Found, therefore look for the user in the ListView_ConnectedUsers and update the user
                                    ListView_ConnectedUsers.Dispatcher.Invoke(new UpdateUserListViewConnectedUsersSeparateThreadCallback(this.UpdateUserListViewConnecteduseresSeparateThread), new object[] { user });
                                    break;
                                }
                            }
                            //Not found, therefore add the user to ConnectedUser list and thingy
                            if (!found)
                            {
                                ConnectedUsers.Add(new T2SUser()
                                {
                                    MacAddr = message.MacAddr,
                                    Username = message.Username,
                                    ProfilePicture = message.ProfilePicture
                                });
                            }
                        }
                        //Append to visual and do TTS
                        speech.SpeakAsync(message.Message);
                        ChatBox.Dispatcher.Invoke(new AppendRTBSeparateThreadCallback(this.AppendChatBoxListViewSeparateThread), new object[] { message });
                    }
                }
                else
                {
                    T2SUser user = ConnectedUsers.Find(x => x.MacAddr == message.MacAddr);
                    if (user != null)
                        ConnectedUsers.Remove(user);
                    ListView_ConnectedUsers.Dispatcher.Invoke(new RemoveListViewConnectedUsersSeparateThreadCallback(this.RemoveListViewConnecteduseresSeparateThread), new object[] { message });
                }
            }
        }

        public bool IsConnected(Socket socket)
        {
            try
            {
                return !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
            }
            catch (SocketException) { return false; }
        }

        public bool IsSocketConnected()
        {
            return !((ClientSocket.Poll(1000, SelectMode.SelectRead) && (ClientSocket.Available == 0)) || !ClientSocket.Connected);
        }

        #endregion

        #region Update Delegates

        //Only really called when disconnected
        public delegate void ChatBoxClearSeparateThreadCallback();
        public void ChatBoxClearSeparateThread()
        {
            DisconnectMenuItem.IsEnabled = false;
            ConnectMenuItem.IsEnabled = true;
            CreateServerMenuItem.IsEnabled = true;
            LabelIP.Content = "Disconnected";
            ConnectedUsers.Clear();
            CollectionViewSource.GetDefaultView(ConnectedUsers).Refresh();
            ChatBox.Items.Clear();
        }

        public delegate void AppendRTBSeparateThreadCallback(T2SClientMessage message);
        public void AppendChatBoxListViewSeparateThread(T2SClientMessage message)
        {
            if (!string.IsNullOrEmpty(message.Message))
            {
                T2SUser sender = null;
                //Find profile picture from list of users
                foreach(T2SUser user in ConnectedUsers)
                {
                    if (user.MacAddr == message.MacAddr)
                    {
                        sender = user;
                    }
                }
                ChatBox.Items.Add(new MessageTemplate
                {
                    ProfilePicture = BitmapToImageSource(GetBitmapFromBytes(sender.ProfilePicture)),
                    Message = message.Message
                });
            }
        }

        public delegate void AppendListViewConnectedUsersSeparateThreadCallback(T2SClientMessage message);
        public void AppendListViewConnecteduseresSeparateThread(T2SClientMessage message)
        {
            CollectionViewSource.GetDefaultView(ConnectedUsers).Refresh();
        }

        public delegate void UpdateUserListViewConnectedUsersSeparateThreadCallback(T2SUser user);
        public void UpdateUserListViewConnecteduseresSeparateThread(T2SUser user)
        {
            CollectionViewSource.GetDefaultView(ConnectedUsers).Refresh();
        }

        public delegate void RemoveListViewConnectedUsersSeparateThreadCallback(T2SClientMessage message);
        public void RemoveListViewConnecteduseresSeparateThread(T2SClientMessage message)
        {
            CollectionViewSource.GetDefaultView(ConnectedUsers).Refresh();
        }

        #endregion

        #region OBJ to Byte and Byte to OBJ procedures

        // Convert an object to a byte array
        public static byte[] ObjectToByteArray(Object obj)
        {
            using (var stream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, obj);
                return stream.ToArray();
            }
        }

        // Convert a byte array to an Object
        public static T2SClientMessage ByteArrayToObject(byte[] arrBytes)
        {
            using (MemoryStream stream = new MemoryStream(arrBytes))
            {
                var formatter = new BinaryFormatter();
                return (T2SClientMessage)formatter.Deserialize(stream);
            }
        }

        private byte[] GetBytesFromBitmap(Bitmap bitmapPicture)
        {
            MemoryStream stream = new MemoryStream();
            bitmapPicture.Save(stream, ImageFormat.Bmp);
            return stream.ToArray();
        }

        private Bitmap GetBitmapFromBytes(byte[] bitmapPicture)
        {
            MemoryStream streamBitmap = new MemoryStream(bitmapPicture);
            return (new Bitmap((Bitmap)System.Drawing.Image.FromStream(streamBitmap)));
        }

        /// <summary>
        /// Turn the Bitmap into a usable BitmapImage
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        private BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }

        #endregion

        #region Display Textbox.xaml
        ////////////////////////////////////////////////////////////////////////////////

        //Start doing foreground window chat textbox in current active window task

        ////////////////////////////////////////////////////////////////////////////////

        private Textbox tb;
        private bool disabledHotKeys = false;
        private void Keyboard_KeyPressed(object sender, KeyPressedEventArgs e)
        {
            if (e.Key.Equals(hotkeyDisplay) && !textboxOpened && ClientSocket != null && ClientSocket.Connected) //Must be connected, and not already open in order to open a new textbox
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

            //Unregister all hotkeys EXCEPT the hotkeyDisable one
            if (e.Key.Equals(hotkeyDisableHotkeys) && !disabledHotKeys)
            {
                disabledHotKeys = true;
                keyboard.UnregisterHotKeys();
                keyboard.RegisterHotKey(hotkeyDisableHotkeys);
            }
            else if (e.Key.Equals(hotkeyDisableHotkeys) && disabledHotKeys)
            {
                disabledHotKeys = false;
                keyboard.RegisterHotKey(hotkeyDisplay);
                keyboard.RegisterHotKey(hotkeyMute);
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

        #region MessageTemplateClass
        class MessageTemplate
        {
            public BitmapImage ProfilePicture { get; set; }
            public string Message { get; set; }
        }
        #endregion

        #region ConnectedUsersTemplateClass
        class ConnectedUsersTemplateClass
        {
            public BitmapImage ProfilePicture { get; set; }
            public string Username { get; set; }
            public string MacAddr { get; set; }
        }
        #endregion
    }
}
