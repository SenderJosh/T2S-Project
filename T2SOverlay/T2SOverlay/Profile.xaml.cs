using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace T2SOverlay
{
    /// <summary>
    /// Interaction logic for Profile.xaml
    /// </summary>
    public partial class Profile : Window
    {
        private MainWindow instance;
        private byte[] profilePicture;
        
        public Profile(MainWindow instance, byte[] profilePicture, string username)
        {
            InitializeComponent();
            this.instance = instance;
            this.profilePicture = profilePicture;
            Username.Text = username;
            
            ProfilePictureSrc.ImageSource = ImageSourceFromBitmap(GetBitmapFromBytes(this.profilePicture));
            MainWindow.UnregisterHotkeys(); //Unregister hotkeys for now
        }

        //Load new image
        private void Ellipse_MouseDown(object sender, MouseButtonEventArgs e)
        {
            OpenFileDialog file = new OpenFileDialog()
            {
                Filter = "Image files (*.jpg, *.jpeg, *.jpe, *.jfif, *.png) | *.jpg; *.jpeg; *.jpe; *.jfif; *.png",
                Title = "Profile Picture"
            };

            if (file.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ProfilePictureSrc.ImageSource = ImageSourceFromBitmap(new Bitmap(file.FileName));
                this.profilePicture = GetBytesFromBitmap(new Bitmap(file.FileName));
            }
        }

        //Save
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            instance.Username = Username.Text;
            instance.ProfilePicture = this.profilePicture;
            this.Close();
        }

        //Closed
        private void Window_Closed(object sender, EventArgs e)
        {
            MainWindow.LoadHotkeys();
            if(MainWindow.ClientSocket.Connected)
            {
                instance.SendMessage("", false, false); //Send update message
            }
        }

        //If you get 'dllimport unknown'-, then add 'using System.Runtime.InteropServices;'
        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        private ImageSource ImageSourceFromBitmap(Bitmap bmp)
        {
            var handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(handle); } //Delete handle (no mem leak)
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

    }
}
