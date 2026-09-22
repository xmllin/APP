using System;
using System.Windows;
using System.Windows.Controls;
using Nexora.Services;

namespace Nexora.Pages
{
    public partial class SettingsPage : UserControl
    {
        private readonly MainWindow _main;

        public SettingsPage(MainWindow main)
        {
            InitializeComponent();
            _main = main;
        }

    }
}
