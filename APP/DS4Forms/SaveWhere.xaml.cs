/*
APP
Copyright (C) 2023  Travis Nickles

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.ComponentModel;
using System.IO;
using System.Windows;


namespace DS4WinWPF.DS4Forms
{
    /// <summary>
    /// Interaction logic for SaveWhere.xaml
    /// </summary>
    public partial class SaveWhere : Window
    {
        private bool multisaves;
        public bool ChoiceMade { get; set; }

        public SaveWhere(bool multisavespots)
        {
            InitializeComponent();
            multisaves = multisavespots;
            if (!multisavespots)
            {
                multipleSavesDockP.Visibility = Visibility.Collapsed;
                pickWhereTxt.Text += Properties.Resources.OtherFileLocation;
            }

            if (APP.Global.AdminNeeded())
            {
                progFolderPanel.IsEnabled = false;
            }

            Topmost = true;
            ShowInTaskbar = true;
            ShowActivated = true;
            Loaded += SaveWhere_Loaded;
            Closing += SaveWhere_Closing;
        }

        private void SaveWhere_Loaded(object sender, RoutedEventArgs e)
        {
            Activate();
            Focus();
        }

        private void SaveWhere_Closing(object sender, CancelEventArgs e)
        {
            if (ChoiceMade)
            {
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                Translations.Strings.SaveWhere_NoChoicePrompt,
                Translations.Strings.App_Title,
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                AppdataBtn_Click(sender, new RoutedEventArgs());
            }
            else if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
            }
        }

        private void ProgFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            APP.Global.SaveWhere(APP.Global.exedirpath);
            if (multisaves && dontDeleteCk.IsChecked == false)
            {
                try
                {
                    if (Directory.Exists(APP.Global.appDataPpath))
                    {
                        Directory.Delete(APP.Global.appDataPpath, true);
                    }
                }
                catch { }
            }
            else if (!multisaves)
            {
                APP.Global.SaveDefault(Path.Combine(APP.Global.exedirpath, "Profiles.xml"));
            }

            ChoiceMade = true;
            Close();
        }

        private void AppdataBtn_Click(object sender, RoutedEventArgs e)
        {
            if (multisaves && dontDeleteCk.IsChecked == false)
            {
                try
                {
                    Directory.Delete(APP.Global.exedirpath + "\\Profiles", true);
                    File.Delete(APP.Global.exedirpath + "\\Profiles.xml");
                    File.Delete(APP.Global.exedirpath + "\\Auto Profiles.xml");
                }
                catch (UnauthorizedAccessException)
                {
                    MessageBox.Show(Translations.Strings.SaveWhere_CannotDeleteOldSettings, Translations.Strings.App_Title);
                }
            }
            else if (!multisaves)
            {
                Directory.CreateDirectory(APP.Global.appDataPpath);
                APP.Global.SaveDefault(Path.Combine(APP.Global.appDataPpath, "Profiles.xml"));
            }

            Directory.CreateDirectory(APP.Global.appDataPpath);
            APP.Global.SaveWhere(APP.Global.appDataPpath);
            ChoiceMade = true;
            Close();
        }
    }
}
