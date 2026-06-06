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

using APP;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    public class FirstLauchUtilViewModel : INotifyPropertyChanged
    {
        private ControlServiceDeviceOptions serviceDeviceOpts;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool EnableDS4
        {
            get => serviceDeviceOpts.DS4DeviceOpts.Enabled;
            set => serviceDeviceOpts.DS4DeviceOpts.Enabled = value;
        }

        public bool EnableDualSense
        {
            get => serviceDeviceOpts.DualSenseOpts.Enabled;
            set => serviceDeviceOpts.DualSenseOpts.Enabled = value;
        }

        public bool EnableSwitchPro
        {
            get => serviceDeviceOpts.SwitchProDeviceOpts.Enabled;
            set => serviceDeviceOpts.SwitchProDeviceOpts.Enabled = value;
        }

        public bool EnableJoyCon
        {
            get => serviceDeviceOpts.JoyConDeviceOpts.Enabled;
            set => serviceDeviceOpts.JoyConDeviceOpts.Enabled = value;
        }

        public bool EnableDS3
        {
            get => serviceDeviceOpts.DS3DeviceOpts.Enabled;
            set => serviceDeviceOpts.DS3DeviceOpts.Enabled = value;
        }

        public bool EnableMoonlight
        {
            get => Global.UseMoonlight;
            set
            {
                if (Global.UseMoonlight == value)
                {
                    return;
                }

                Global.UseMoonlight = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EnableAdvancedMoonlight));
            }
        }

        public bool EnableAdvancedMoonlight
        {
            get => Global.UseAdvancedMoonlight;
            set
            {
                if (Global.UseAdvancedMoonlight == value)
                {
                    return;
                }

                Global.UseAdvancedMoonlight = value;
                OnPropertyChanged();
            }
        }

        public bool VerboseLogMessages
        {
            get => serviceDeviceOpts.VerboseLogMessages;
            set => serviceDeviceOpts.VerboseLogMessages = value;
        }

        public FirstLauchUtilViewModel(ControlServiceDeviceOptions serviceDeviceOpts)
        {
            this.serviceDeviceOpts = serviceDeviceOpts;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
