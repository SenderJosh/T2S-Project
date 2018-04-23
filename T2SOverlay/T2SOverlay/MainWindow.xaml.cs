using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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

        //Server
        private IPAddress IP = IPAddress.Loopback;
        private const int PORT = 100;

        private static readonly Socket ClientSocket = new Socket
            (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private SpeechSynthesizer speech;

        private List<T2SUser> ConnectedUsers;

        //Settings
        private Settings settings;
        public static Keys hotkeyMute, hotkeyDisplay, hotkeyDisableHotkeys; //Global hotkey
        private KeyboardHook keyboard = new KeyboardHook();

        //User information
        private byte[] ProfilePicture;
        private string Username, MacAddr;

        public MainWindow()
        {
            InitializeComponent();
            DisconnectMenuItem.IsEnabled = false;
            LabelIP.Content = "Disconnected";

            ConnectedUsers = new List<T2SUser>();

            //Setup text to speech synth
            speech = new SpeechSynthesizer();
            speech.Volume = 80;
            speech.Rate = 1;

            keyboard.KeyPressed += new EventHandler<KeyPressedEventArgs>(Keyboard_KeyPressed);

            //Load user information
            MacAddr = (from nic in NetworkInterface.GetAllNetworkInterfaces() where nic.OperationalStatus == OperationalStatus.Up
                            select nic.GetPhysicalAddress().ToString()).FirstOrDefault();

            string json = "";
            //Load profile data
            if(File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Local\\T2S Gaming\\profile.json"))
            {
                json = File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Local\\T2S Gaming\\profile.json");
                T2SUser user = JsonConvert.DeserializeObject<T2SUser>(json);
                ProfilePicture = user.ProfilePicture;
                Username = user.Username;
            }
            else
            {
                //Create and load default
                Username = System.Security.Principal.WindowsIdentity.GetCurrent().Name; //Just use their username from their PC
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
                while (true)
                {
                    ReceiveResponse();
                }
            });
        }

        /// <summary>
        /// Sends a JSON string to the server, simply containing a T2SClientMessage
        /// </summary>
        public void SendMessage(string text, bool firstConnect, bool updateProfile)
        {
            T2SClientMessage message = new T2SClientMessage()
            {
                MacAddr = this.MacAddr,
                ProfilePicture = this.ProfilePicture,
                UpdateProfile = updateProfile, //change to be a bool that is set when we add Profile settings
                Username = this.Username,
                FirstConnect = firstConnect, //change to be a bool
                Message = text
            };
            byte[] buffer = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(message));

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
                Console.WriteLine(header.Length + " : Header length");
                buffer = Encoding.ASCII.GetBytes((header + "|" + Encoding.ASCII.GetString(buffer)));
                //Append own text
                Console.WriteLine("First: " + Encoding.ASCII.GetString(buffer));
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
        /// The first index will be a json 
        /// We follow forward with the assumption that we are working with ASCII text. Perhaps later for augmentation we switch to Encoding.Unicode and double/quadruple the header size
        /// due to the nature of unicode characters being larger
        /// 
        /// </summary>
        private void ReceiveResponse()
        {
            var buffer = new byte[33];
            int received = ClientSocket.Receive(buffer, 33, SocketFlags.None);
            if (received == 0) return; //Nothing to receive
            string text = Encoding.ASCII.GetString(buffer); //Hopefully the header
            int header;
            //Successfully got header
            if (Int32.TryParse(text.Split('|')[0], out header))
            {
                buffer = new byte[header];
                received = ClientSocket.Receive(buffer, header, SocketFlags.None);
                if (received == 0)
                {
                    Console.WriteLine("--------------ERROR--------------");
                    Console.WriteLine("Receive Response failed");
                    Console.WriteLine("Expected to receive bytes: " + header);
                    return; //Nothing to receive (which would be REALLY weird because it's expecting to receive something so print stuff)
                }
                T2SClientMessage message = JsonConvert.DeserializeObject<T2SClientMessage>(Encoding.ASCII.GetString(buffer));
                Console.WriteLine("got header");
                //Perform logic
                //If this is the first connection, we're not going to display anything new in the chat
                if (message.FirstConnect)
                {
                    ConnectedUsers.Add(new T2SUser()
                    {
                        MacAddr = message.MacAddr,
                        Username = message.Username,
                        ProfilePicture = message.ProfilePicture
                    });
                    ListView_ConnectedUsers.Items.Add(new ConnectedUsersTemplateClass()
                    {
                        Username = message.Username,
                        ProfilePicture = BitmapToImageSource(GetBitmapFromBytes(message.ProfilePicture)),
                    });
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
                    speech.SpeakAsync(text);
                    ChatBox.Dispatcher.Invoke(new AppendRTBSeparateThreadCallback(this.AppendChatBoxListViewSeparateThread), new object[] { message });
                }
            }
        }

        public delegate void AppendRTBSeparateThreadCallback(T2SClientMessage message);
        public void AppendChatBoxListViewSeparateThread(T2SClientMessage message)
        {
            if(!string.IsNullOrEmpty(message.Message))
            {
                ChatBox.Items.Add(new MessageTemplate
                {
                    ProfilePicture = BitmapToImageSource(GetBitmapFromBytes(message.ProfilePicture)),
                    Message = message.Message
                });
            }
        }

        public delegate void AppendListViewConnectedUsersSeparateThreadCallback(T2SClientMessage message);
        public void AppendListViewConnecteduseresSeparateThread(T2SClientMessage message)
        {
            ListView_ConnectedUsers.Items.Add(new ConnectedUsersTemplateClass
            {
                ProfilePicture = BitmapToImageSource(GetBitmapFromBytes(message.ProfilePicture)),
                Username = message.Username
            });
        }

        /// <summary>
        /// Converts a bitmap image to a string to be saved for json
        /// </summary>
        /// <param name="bitmapPicture"></param>
        /// <returns>Base64 string of a bitmap that can be converted</returns>
        private string GetString64FromBitmap(Bitmap bitmapPicture)
        {
            MemoryStream stream = new MemoryStream();
            bitmapPicture.Save(stream, ImageFormat.Bmp);
            return Convert.ToBase64String(stream.ToArray());
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
            return (new Bitmap((Bitmap)Image.FromStream(streamBitmap)));
        }
        
        /// <summary>
        /// Does the opposite of the above
        /// </summary>
        /// <param name="bitmapPicture"></param>
        /// <returns></returns>
        private Bitmap GetBitmapFromString64(string bitmapPicture)
        {
            byte[] decoded = Convert.FromBase64String(FixBase64ForImage(bitmapPicture));
            MemoryStream streamBitmap = new MemoryStream(decoded);
            return (new Bitmap((Bitmap)Image.FromStream(streamBitmap)));
        }

        private string FixBase64ForImage(string Image)
        {
            StringBuilder sbText = new StringBuilder(Image, Image.Length);
            sbText.Replace("\r\n", String.Empty); sbText.Replace(" ", String.Empty);
            return sbText.ToString();
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
            if (e.Key.Equals(hotkeyDisplay) && !textboxOpened && ClientSocket.Connected) //Must be connected, and not already open in order to open a new textbox
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
            //Unregister all hotkeys EXCEPT the hotkeyDisable one
            if(e.Key.Equals(hotkeyDisableHotkeys) && !disabledHotKeys)
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
        }
        #endregion
    }
}
