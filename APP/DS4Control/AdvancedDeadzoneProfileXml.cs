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
using System.Xml;

namespace APP
{
    internal static class AdvancedDeadzoneProfileXml
    {
        public static void LoadStick(XmlDocument doc, string rootName, string prefix, StickDeadZoneInfo mod)
        {
            try
            {
                XmlNode item = doc.SelectSingleNode($"/{rootName}/{prefix}AdvancedRadialDeadzone");
                if (item != null && bool.TryParse(item.InnerText, out bool enabled))
                {
                    mod.advancedRadialProcessing = enabled;
                }
            }
            catch { }

            try
            {
                XmlNode item = doc.SelectSingleNode($"/{rootName}/{prefix}InnerZoneSoftness");
                if (item != null && double.TryParse(item.InnerText, out double temp))
                {
                    mod.innerZoneSoftness = Math.Clamp(temp, 0.0, 0.45);
                }
            }
            catch { }

            try
            {
                XmlNode item = doc.SelectSingleNode($"/{rootName}/{prefix}OuterDeadzone");
                if (item != null && int.TryParse(item.InnerText, out int temp))
                {
                    mod.outerDeadzone = Math.Clamp(temp, 0, 127);
                }
            }
            catch { }

            try
            {
                XmlNode item = doc.SelectSingleNode($"/{rootName}/{prefix}DynamicDeadzoneScaling");
                if (item != null && bool.TryParse(item.InnerText, out bool temp))
                {
                    mod.dynamicDeadzoneScaling = temp;
                }
            }
            catch { }

            try
            {
                XmlNode item = doc.SelectSingleNode($"/{rootName}/{prefix}AdaptiveAntiDeadzone");
                if (item != null && bool.TryParse(item.InnerText, out bool temp))
                {
                    mod.adaptiveAntiDeadzone = temp;
                }
            }
            catch { }

            try
            {
                XmlNode item = doc.SelectSingleNode($"/{rootName}/{prefix}DynamicCenterCalibration");
                if (item != null && bool.TryParse(item.InnerText, out bool temp))
                {
                    mod.dynamicCenterCalibration = temp;
                }
            }
            catch { }

            try
            {
                XmlNode item = doc.SelectSingleNode($"/{rootName}/{prefix}CenterCalStrength");
                if (item != null && double.TryParse(item.InnerText, out double temp))
                {
                    mod.centerCalStrength = Math.Clamp(temp, 0.05, 1.0);
                }
            }
            catch { }
        }

        public static void SaveStick(XmlDocument doc, XmlElement root, string prefix, StickDeadZoneInfo mod)
        {
            AppendBool(doc, root, $"{prefix}AdvancedRadialDeadzone", mod.advancedRadialProcessing);
            AppendDouble(doc, root, $"{prefix}InnerZoneSoftness", mod.innerZoneSoftness);
            AppendInt(doc, root, $"{prefix}OuterDeadzone", mod.outerDeadzone);
            AppendBool(doc, root, $"{prefix}DynamicDeadzoneScaling", mod.dynamicDeadzoneScaling);
            AppendBool(doc, root, $"{prefix}AdaptiveAntiDeadzone", mod.adaptiveAntiDeadzone);
            AppendBool(doc, root, $"{prefix}DynamicCenterCalibration", mod.dynamicCenterCalibration);
            AppendDouble(doc, root, $"{prefix}CenterCalStrength", mod.centerCalStrength);
        }

        private static void AppendBool(XmlDocument doc, XmlElement root, string name, bool value)
        {
            XmlNode node = doc.CreateNode(XmlNodeType.Element, name, null);
            node.InnerText = value.ToString();
            root.AppendChild(node);
        }

        private static void AppendInt(XmlDocument doc, XmlElement root, string name, int value)
        {
            XmlNode node = doc.CreateNode(XmlNodeType.Element, name, null);
            node.InnerText = value.ToString();
            root.AppendChild(node);
        }

        private static void AppendDouble(XmlDocument doc, XmlElement root, string name, double value)
        {
            XmlNode node = doc.CreateNode(XmlNodeType.Element, name, null);
            node.InnerText = value.ToString();
            root.AppendChild(node);
        }
    }
}
