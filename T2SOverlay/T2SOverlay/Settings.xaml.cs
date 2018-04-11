using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace T2SOverlay
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings : Window
    {
        private KeyboardHook keyboard;
        private bool listen = false, changed = false;

        private Keys hotkeyMute, hotkeyDisplay, hotkeyDisableHotkeys;
        private Keys hotkeyMuteOld, hotkeyDisplayOld, hotkeyDisableHotkeysOld;

        public Settings(KeyboardHook keyboard, Keys hotkeyMute, Keys hotkeyDisplay, Keys hotkeyDisableHotkeys)
        {
            InitializeComponent();
            this.keyboard = keyboard;
            this.hotkeyMute = hotkeyMute;
            this.hotkeyDisplay = hotkeyDisplay;
            this.hotkeyDisableHotkeys = hotkeyDisableHotkeys;

            //Display current settings
            HotkeyDisplayChat.Content = this.hotkeyDisplay.ToString();
            HotkeyMuteT2S.Content = this.hotkeyMute.ToString();
            HotkeyDisableHotkey.Content = this.hotkeyDisableHotkeys.ToString();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            Keys key = (Keys)KeyInterop.VirtualKeyFromKey(e.Key);
            if (listen)
            {
                listen = false;
                if (key != Keys.Escape)
                {
                    changed = true;
                    if (HotkeyDisplayChat.Content.ToString() == "Press [ESC] to Cancel")
                    {
                        hotkeyDisplayOld = hotkeyDisplay;
                        hotkeyDisplay = key;
                        HotkeyDisplayChat.Content = key.ToString();
                    }
                    else if (HotkeyMuteT2S.Content.ToString() == "Press [ESC] to Cancel")
                    {
                        hotkeyMuteOld = hotkeyMute;
                        hotkeyMute = key;
                        HotkeyMuteT2S.Content = key.ToString();
                    }
                    else
                    {
                        hotkeyDisableHotkeysOld = hotkeyDisableHotkeys;
                        hotkeyDisableHotkeys = key;
                        HotkeyDisableHotkey.Content = key.ToString();
                    }
                }
                else
                {
                    if (HotkeyDisplayChat.Content.ToString() == "Press [ESC] to Cancel")
                    {
                        HotkeyDisplayChat.Content = hotkeyDisplay.ToString();
                    }
                    else if(HotkeyMuteT2S.Content.ToString() == "Press [ESC] to Cancel")
                    {
                        HotkeyMuteT2S.Content = hotkeyMute.ToString();
                    }
                    else
                    {
                        HotkeyDisableHotkey.Content = hotkeyDisableHotkeys.ToString();
                    }
                }
            }
        }

        private void HotkeyDisableHotkey_Click(object sender, RoutedEventArgs e)
        {
            listen = true;
            HotkeyDisableHotkey.Content = "Press [ESC] to Cancel";
        }

        private void HotkeyMuteT2S_Click(object sender, RoutedEventArgs e)
        {
            listen = true;
            HotkeyMuteT2S.Content = "Press [ESC] to Cancel";
        }

        private void HotkeyDisplayChat_Click(object sender, RoutedEventArgs e)
        {
            listen = true;
            HotkeyDisplayChat.Content = "Press [ESC] to Cancel";
        }

        private void HotKeyApply_Click(object sender, RoutedEventArgs e)
        {
            //Save if changed settings
            if(changed)
            {
                changed = false;
                keyboard.UnregisterHotKeys(); //Unregister all hotkeys
                keyboard.RegisterHotKey(hotkeyDisplay);
                keyboard.RegisterHotKey(hotkeyMute);
                keyboard.RegisterHotKey(hotkeyDisableHotkeys);
                //Update global static hotkey
                MainWindow.hotkeyDisplay = hotkeyDisplay;
                MainWindow.hotkeyMute = hotkeyMute;
                MainWindow.hotkeyDisableHotkeys = hotkeyDisableHotkeys;
            }
        }
    }
}
