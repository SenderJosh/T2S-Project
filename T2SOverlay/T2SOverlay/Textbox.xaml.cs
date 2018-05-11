using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
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

namespace T2SOverlay
{
    /// <summary>
    /// Interaction logic for Textbox.xaml
    /// </summary>
    public partial class Textbox : Window
    {
        private MainWindow instance;

        public Textbox(MainWindow instance)
        {
            InitializeComponent();
            MainWindow.UnregisterHotkeys(); //Do this temporarily until closed
            this.instance = instance;
        }
        
        /// <summary>
        /// If the user hits ESC, don't send anything and close the textbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return || e.Key == Key.Enter)
            {
                instance.SendMessage(textbox.Text, false, false); //We will never update profile or do a first connect when a user sends a message
                instance.textboxOpened = false;
                this.Close();
            }
            else if (e.Key == Key.Escape)
            {
                instance.textboxOpened = false;
                this.Close();
            }
        }

        //Focus on the textbox
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.textbox.Focus();
        }
        
        //Reload hotkeys
        private void Window_Closed(object sender, EventArgs e)
        {
            MainWindow.LoadHotkeys(); //Load hotkeys back once closed
        }
    }
}
