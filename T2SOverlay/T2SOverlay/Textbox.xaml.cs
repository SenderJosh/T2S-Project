﻿using System;
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

            this.instance = instance;
        }
        
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

        public void addHotkeyPressedButton(string key)
        {
            textbox.Text += key;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.textbox.Focus();
        }
    }
}
