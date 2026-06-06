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

using System.Text;
using System.Xml;
using System.Xml.Serialization;
using APP;
using DS4WinWPF.DS4Control.DTOXml;

namespace APPTests
{
    [TestClass]
    public class ProfileTests
    {
        private string defaultProfileXml = string.Empty;

        public ProfileTests()
        {
            #region ProfileXMLString
            //<!-- APP Configuration Data. 11/30/2023 00:16:38 -->
            //< !--Made with APP version 3.2.20-- >
            //app_version=""3.2.20"" config_version=""5""
            defaultProfileXml = @"<?xml version=""1.0"" encoding=""utf-8""?>

<APP>
  <touchToggle>True</touchToggle>
  <CleanInputMode>True</CleanInputMode>
  <CompetitiveFpsStickMode>True</CompetitiveFpsStickMode>
  <HighPrecisionStickOutput>True</HighPrecisionStickOutput>
  <StickModifierOrder>0</StickModifierOrder>
  <True16BitStickPipeline>False</True16BitStickPipeline>
  <LSDirectionAwareResolution>False</LSDirectionAwareResolution>
  <RSDirectionAwareResolution>False</RSDirectionAwareResolution>
  <LSDynamicPhysicsResponse>False</LSDynamicPhysicsResponse>
  <RSDynamicPhysicsResponse>False</RSDynamicPhysicsResponse>
  <LSPredictiveJitterCleaner>False</LSPredictiveJitterCleaner>
  <RSPredictiveJitterCleaner>False</RSPredictiveJitterCleaner>
  <idleDisconnectTimeout>0</idleDisconnectTimeout>
  <outputDataToDS4>True</outputDataToDS4>
  <Color>0,0,255</Color>
  <LeftStickDriftXAxis>0</LeftStickDriftXAxis>
  <LeftStickDriftYAxis>0</LeftStickDriftYAxis>
  <RightStickDriftXAxis>0</RightStickDriftXAxis>
  <RightStickDriftYAxis>0</RightStickDriftYAxis>
  <DebouncingMs>0</DebouncingMs>
  <InverseRumbleMotors>false</InverseRumbleMotors>
  <UseDs3PitchRollSim>false</UseDs3PitchRollSim>
  <RumbleBoost>100</RumbleBoost>
  <RumbleAutostopTime>0</RumbleAutostopTime>
  <LightbarMode>DS4Win</LightbarMode>
  <ledAsBatteryIndicator>False</ledAsBatteryIndicator>
  <FlashType>0</FlashType>
  <flashBatteryAt>0</flashBatteryAt>
  <touchSensitivity>100</touchSensitivity>
  <LowColor>0,0,0</LowColor>
  <ChargingColor>0,0,0</ChargingColor>
  <FlashColor>0,0,0</FlashColor>
  <touchpadJitterCompensation>True</touchpadJitterCompensation>
  <lowerRCOn>False</lowerRCOn>
  <tapSensitivity>0</tapSensitivity>
  <doubleTap>False</doubleTap>
  <scrollSensitivity>0</scrollSensitivity>
  <LeftTriggerMiddle>0</LeftTriggerMiddle>
  <RightTriggerMiddle>0</RightTriggerMiddle>
  <TouchpadInvert>0</TouchpadInvert>
  <TouchpadClickPassthru>False</TouchpadClickPassthru>
  <L2AntiDeadZone>0</L2AntiDeadZone>
  <R2AntiDeadZone>0</R2AntiDeadZone>
  <L2MaxZone>100</L2MaxZone>
  <R2MaxZone>100</R2MaxZone>
  <L2MaxOutput>100</L2MaxOutput>
  <R2MaxOutput>100</R2MaxOutput>
  <ButtonMouseSensitivity>25</ButtonMouseSensitivity>
  <ButtonMouseOffset>0.008</ButtonMouseOffset>
  <Rainbow>0</Rainbow>
  <MaxSatRainbow>100</MaxSatRainbow>
  <LSDeadZone>10</LSDeadZone>
  <RSDeadZone>10</RSDeadZone>
  <LSAntiDeadZone>20</LSAntiDeadZone>
  <RSAntiDeadZone>20</RSAntiDeadZone>
  <LSAdvancedRadialDeadzone>true</LSAdvancedRadialDeadzone>
  <RSAdvancedRadialDeadzone>true</RSAdvancedRadialDeadzone>
  <LSOuterDeadzone>0</LSOuterDeadzone>
  <RSOuterDeadzone>0</RSOuterDeadzone>
  <LSInnerZoneSoftness>0.18</LSInnerZoneSoftness>
  <RSInnerZoneSoftness>0.18</RSInnerZoneSoftness>
  <LSDynamicDeadzoneScaling>false</LSDynamicDeadzoneScaling>
  <RSDynamicDeadzoneScaling>false</RSDynamicDeadzoneScaling>
  <LSAdaptiveAntiDeadzone>true</LSAdaptiveAntiDeadzone>
  <RSAdaptiveAntiDeadzone>true</RSAdaptiveAntiDeadzone>
  <LSDynamicCenterCalibration>false</LSDynamicCenterCalibration>
  <RSDynamicCenterCalibration>false</RSDynamicCenterCalibration>
  <LSMaxZone>100</LSMaxZone>
  <RSMaxZone>100</RSMaxZone>
  <LSVerticalScale>100</LSVerticalScale>
  <RSVerticalScale>100</RSVerticalScale>
  <LSMaxOutput>100</LSMaxOutput>
  <RSMaxOutput>100</RSMaxOutput>
  <LSMaxOutputForce>False</LSMaxOutputForce>
  <RSMaxOutputForce>False</RSMaxOutputForce>
  <LSDeadZoneType>Radial</LSDeadZoneType>
  <RSDeadZoneType>Radial</RSDeadZoneType>
  <LSAxialDeadOptions>
    <DeadZoneX>10</DeadZoneX>
    <DeadZoneY>10</DeadZoneY>
    <MaxZoneX>100</MaxZoneX>
    <MaxZoneY>100</MaxZoneY>
    <AntiDeadZoneX>20</AntiDeadZoneX>
    <AntiDeadZoneY>20</AntiDeadZoneY>
    <MaxOutputX>100</MaxOutputX>
    <MaxOutputY>100</MaxOutputY>
  </LSAxialDeadOptions>
  <RSAxialDeadOptions>
    <DeadZoneX>10</DeadZoneX>
    <DeadZoneY>10</DeadZoneY>
    <MaxZoneX>100</MaxZoneX>
    <MaxZoneY>100</MaxZoneY>
    <AntiDeadZoneX>20</AntiDeadZoneX>
    <AntiDeadZoneY>20</AntiDeadZoneY>
    <MaxOutputX>100</MaxOutputX>
    <MaxOutputY>100</MaxOutputY>
  </RSAxialDeadOptions>
  <LSRotation>8</LSRotation>
  <RSRotation>0</RSRotation>
  <LSFuzz>0</LSFuzz>
  <RSFuzz>0</RSFuzz>
  <LSOuterBindDead>75</LSOuterBindDead>
  <RSOuterBindDead>75</RSOuterBindDead>
  <LSOuterBindInvert>False</LSOuterBindInvert>
  <RSOuterBindInvert>False</RSOuterBindInvert>
  <LSDeltaAccelSettings>
    <Enabled>False</Enabled>
    <Multiplier>4</Multiplier>
    <MaxTravel>0.2</MaxTravel>
    <MinTravel>0.01</MinTravel>
    <EasingDuration>0.2</EasingDuration>
    <MinFactor>1</MinFactor>
  </LSDeltaAccelSettings>
  <RSDeltaAccelSettings>
    <Enabled>False</Enabled>
    <Multiplier>4</Multiplier>
    <MaxTravel>0.2</MaxTravel>
    <MinTravel>0.01</MinTravel>
    <EasingDuration>0.2</EasingDuration>
    <MinFactor>1</MinFactor>
  </RSDeltaAccelSettings>
  <SXDeadZone>0.25</SXDeadZone>
  <SZDeadZone>0.25</SZDeadZone>
  <SXMaxZone>100</SXMaxZone>
  <SZMaxZone>100</SZMaxZone>
  <SXAntiDeadZone>0</SXAntiDeadZone>
  <SZAntiDeadZone>0</SZAntiDeadZone>
  <Sensitivity>1|1|1|1|1|1</Sensitivity>
  <ChargingType>0</ChargingType>
  <MouseAcceleration>False</MouseAcceleration>
  <ButtonMouseVerticalScale>100</ButtonMouseVerticalScale>
  <LaunchProgram />
  <DinputOnly>False</DinputOnly>
  <StartTouchpadOff>False</StartTouchpadOff>
  <TouchpadOutputMode>Controls</TouchpadOutputMode>
  <SATriggers>-1</SATriggers>
  <SATriggerCond>and</SATriggerCond>
  <SASteeringWheelEmulationAxis>None</SASteeringWheelEmulationAxis>
  <SASteeringWheelEmulationRange>360</SASteeringWheelEmulationRange>
  <SASteeringWheelFuzz>0</SASteeringWheelFuzz>
  <SASteeringWheelSmoothingOptions>
    <SASteeringWheelUseSmoothing>False</SASteeringWheelUseSmoothing>
    <SASteeringWheelSmoothMinCutoff>0.1</SASteeringWheelSmoothMinCutoff>
    <SASteeringWheelSmoothBeta>0.1</SASteeringWheelSmoothBeta>
  </SASteeringWheelSmoothingOptions>
  <TouchDisInvTriggers>-1</TouchDisInvTriggers>
  <GyroSensitivity>100</GyroSensitivity>
  <GyroSensVerticalScale>100</GyroSensVerticalScale>
  <GyroInvert>0</GyroInvert>
  <GyroTriggerTurns>True</GyroTriggerTurns>
  <GyroControlsSettings>
    <Triggers>-1</Triggers>
    <TriggerCond>and</TriggerCond>
    <TriggerTurns>True</TriggerTurns>
    <Toggle>False</Toggle>
  </GyroControlsSettings>
  <GyroMouseSmoothingSettings>
    <UseSmoothing>False</UseSmoothing>
    <SmoothingMethod>none</SmoothingMethod>
    <SmoothingWeight>50</SmoothingWeight>
    <SmoothingMinCutoff>1</SmoothingMinCutoff>
    <SmoothingBeta>0.7</SmoothingBeta>
  </GyroMouseSmoothingSettings>
  <GyroMouseHAxis>0</GyroMouseHAxis>
  <GyroMouseDeadZone>10</GyroMouseDeadZone>
  <GyroMouseMinThreshold>1</GyroMouseMinThreshold>
  <GyroMouseToggle>False</GyroMouseToggle>
  <GyroMouseJitterCompensation>True</GyroMouseJitterCompensation>
  <GyroOutputMode>Controls</GyroOutputMode>
  <GyroMouseStickTriggers>-1</GyroMouseStickTriggers>
  <GyroMouseStickTriggerCond>and</GyroMouseStickTriggerCond>
  <GyroMouseStickTriggerTurns>True</GyroMouseStickTriggerTurns>
  <GyroMouseStickHAxis>0</GyroMouseStickHAxis>
  <GyroMouseStickDeadZone>30</GyroMouseStickDeadZone>
  <GyroMouseStickMaxZone>830</GyroMouseStickMaxZone>
  <GyroMouseStickOutputStick>RightStick</GyroMouseStickOutputStick>
  <GyroMouseStickOutputStickAxes>XY</GyroMouseStickOutputStickAxes>
  <GyroMouseStickAntiDeadX>0.4</GyroMouseStickAntiDeadX>
  <GyroMouseStickAntiDeadY>0.4</GyroMouseStickAntiDeadY>
  <GyroMouseStickInvert>0</GyroMouseStickInvert>
  <GyroMouseStickToggle>False</GyroMouseStickToggle>
  <GyroMouseStickMaxOutput>100</GyroMouseStickMaxOutput>
  <GyroMouseStickMaxOutputEnabled>False</GyroMouseStickMaxOutputEnabled>
  <GyroMouseStickVerticalScale>100</GyroMouseStickVerticalScale>
  <GyroMouseStickJitterCompensation>False</GyroMouseStickJitterCompensation>
  <GyroMouseStickSmoothingSettings>
    <UseSmoothing>False</UseSmoothing>
    <SmoothingMethod>none</SmoothingMethod>
    <SmoothingWeight>50</SmoothingWeight>
    <SmoothingMinCutoff>0.4</SmoothingMinCutoff>
    <SmoothingBeta>0.7</SmoothingBeta>
  </GyroMouseStickSmoothingSettings>
  <GyroSwipeSettings>
    <DeadZoneX>80</DeadZoneX>
    <DeadZoneY>80</DeadZoneY>
    <Triggers>-1</Triggers>
    <TriggerCond>and</TriggerCond>
    <TriggerTurns>True</TriggerTurns>
    <XAxis>Yaw</XAxis>
    <DelayTime>0</DelayTime>
  </GyroSwipeSettings>
  <BTPollRate>4</BTPollRate>
  <LSOutputCurveMode>linear</LSOutputCurveMode>
  <LSOutputCurveCustom />
  <RSOutputCurveMode>linear</RSOutputCurveMode>
  <RSOutputCurveCustom />
  <LSSquareStick>False</LSSquareStick>
  <RSSquareStick>False</RSSquareStick>
  <SquareStickRoundness>5</SquareStickRoundness>
  <SquareRStickRoundness>5</SquareRStickRoundness>
  <LSAntiSnapback>False</LSAntiSnapback>
  <RSAntiSnapback>False</RSAntiSnapback>
  <LSAntiSnapbackDelta>135</LSAntiSnapbackDelta>
  <RSAntiSnapbackDelta>135</RSAntiSnapbackDelta>
  <LSAntiSnapbackTimeout>50</LSAntiSnapbackTimeout>
  <RSAntiSnapbackTimeout>50</RSAntiSnapbackTimeout>
  <LSMicroAimPrecision>False</LSMicroAimPrecision>
  <RSMicroAimPrecision>False</RSMicroAimPrecision>
  <LSMicroAimPrecisionZone>0.15</LSMicroAimPrecisionZone>
  <RSMicroAimPrecisionZone>0.15</RSMicroAimPrecisionZone>
  <LSMicroAimTransitionZone>0.2</LSMicroAimTransitionZone>
  <RSMicroAimTransitionZone>0.2</RSMicroAimTransitionZone>
  <LSMicroAimPrecisionExponent>1.35</LSMicroAimPrecisionExponent>
  <RSMicroAimPrecisionExponent>1.35</RSMicroAimPrecisionExponent>
  <LSDynamicStickResponse>False</LSDynamicStickResponse>
  <RSDynamicStickResponse>True</RSDynamicStickResponse>
  <LSDynamicStickSlowSpeed>1.5</LSDynamicStickSlowSpeed>
  <RSDynamicStickSlowSpeed>1.5</RSDynamicStickSlowSpeed>
  <LSDynamicStickFastSpeed>12</LSDynamicStickFastSpeed>
  <RSDynamicStickFastSpeed>12</RSDynamicStickFastSpeed>
  <LSTmrHallStickMode>False</LSTmrHallStickMode>
  <RSTmrHallStickMode>False</RSTmrHallStickMode>
  <LSTmrHallJitterThreshold>2.5</LSTmrHallJitterThreshold>
  <RSTmrHallJitterThreshold>2.5</RSTmrHallJitterThreshold>
  <RSSoftAntiDeadzone>False</RSSoftAntiDeadzone>
  <RSSoftAntiDeadzoneSize>12</RSSoftAntiDeadzoneSize>
  <RSSoftAntiDeadzoneRamp>0.25</RSSoftAntiDeadzoneRamp>
  <RSSoftAntiDeadzoneExponent>2.2</RSSoftAntiDeadzoneExponent>
  <RSInGameDeadzoneRemapper>False</RSInGameDeadzoneRemapper>
  <RSInGameDeadzoneSize>0.05</RSInGameDeadzoneSize>
  <RSInGameDeadzoneExponent>1.4</RSInGameDeadzoneExponent>
  <RSInGameDeadzoneSoftEntry>True</RSInGameDeadzoneSoftEntry>
  <RSInGameDeadzoneEntryRange>0.08</RSInGameDeadzoneEntryRange>
  <RSMicroVelocityQuantizationSmoother>False</RSMicroVelocityQuantizationSmoother>
  <RSMicroVelocitySmootherStrength>0.2</RSMicroVelocitySmootherStrength>
  <RSMicroVelocitySmootherRange>0.12</RSMicroVelocitySmootherRange>
  <LSSoftCenterExit>False</LSSoftCenterExit>
  <RSSoftCenterExit>False</RSSoftCenterExit>
  <LSSoftCenterExitRange>0.08</LSSoftCenterExitRange>
  <RSSoftCenterExitRange>0.08</RSSoftCenterExitRange>
  <LSSoftCenterExitExponent>1.4</LSSoftCenterExitExponent>
  <RSSoftCenterExitExponent>1.4</RSSoftCenterExitExponent>
  <LSOutputMode>Controls</LSOutputMode>
  <RSOutputMode>Controls</RSOutputMode>
  <LSOutputSettings>
    <FlickStickSettings>
      <RealWorldCalibration>5.3</RealWorldCalibration>
      <FlickThreshold>0.9</FlickThreshold>
      <FlickTime>0.1</FlickTime>
      <MinAngleThreshold>0</MinAngleThreshold>
    </FlickStickSettings>
  </LSOutputSettings>
  <RSOutputSettings>
    <FlickStickSettings>
      <RealWorldCalibration>5.3</RealWorldCalibration>
      <FlickThreshold>0.9</FlickThreshold>
      <FlickTime>0.1</FlickTime>
      <MinAngleThreshold>0</MinAngleThreshold>
    </FlickStickSettings>
  </RSOutputSettings>
  <DualSenseControllerSettings>
    <RumbleSettings>
      <EmulationMode>Accurate</EmulationMode>
      <EnableGenericRumbleRescale>False</EnableGenericRumbleRescale>
      <HapticPowerLevel>0</HapticPowerLevel>
    </RumbleSettings>
  </DualSenseControllerSettings>
  <L2OutputCurveMode>linear</L2OutputCurveMode>
  <L2OutputCurveCustom />
  <L2TwoStageMode>Disabled</L2TwoStageMode>
  <R2TwoStageMode>Disabled</R2TwoStageMode>
  <L2HipFireTime>100</L2HipFireTime>
  <R2HipFireTime>100</R2HipFireTime>
  <L2TriggerEffect>None</L2TriggerEffect>
  <R2TriggerEffect>None</R2TriggerEffect>
  <R2OutputCurveMode>linear</R2OutputCurveMode>
  <R2OutputCurveCustom />
  <SXOutputCurveMode>linear</SXOutputCurveMode>
  <SXOutputCurveCustom />
  <SZOutputCurveMode>linear</SZOutputCurveMode>
  <SZOutputCurveCustom />
  <TrackballMode>False</TrackballMode>
  <TrackballFriction>10</TrackballFriction>
  <TouchRelMouseRotation>0</TouchRelMouseRotation>
  <TouchRelMouseMinThreshold>1</TouchRelMouseMinThreshold>
  <TouchpadAbsMouseSettings>
    <MaxZoneX>90</MaxZoneX>
    <MaxZoneY>90</MaxZoneY>
    <SnapToCenter>False</SnapToCenter>
  </TouchpadAbsMouseSettings>
  <TouchpadMouseStick>
    <DeadZone>0</DeadZone>
    <MaxZone>8</MaxZone>
    <OutputStick>RightStick</OutputStick>
    <OutputStickAxes>XY</OutputStickAxes>
    <AntiDeadX>0.4</AntiDeadX>
    <AntiDeadY>0.4</AntiDeadY>
    <Invert>0</Invert>
    <MaxOutput>100</MaxOutput>
    <MaxOutputEnabled>False</MaxOutputEnabled>
    <VerticalScale>100</VerticalScale>
    <OutputCurve>Linear</OutputCurve>
    <Rotation>0</Rotation>
    <SmoothingSettings>
      <SmoothingMethod>None</SmoothingMethod>
      <SmoothingMinCutoff>0.8</SmoothingMinCutoff>
      <SmoothingBeta>0.7</SmoothingBeta>
    </SmoothingSettings>
  </TouchpadMouseStick>
  <TouchpadButtonMode>Click</TouchpadButtonMode>
  <AbsMouseRegionSettings>
    <AbsWidth>1</AbsWidth>
    <AbsHeight>1</AbsHeight>
    <AbsXCenter>0.5</AbsXCenter>
    <AbsYCenter>0.5</AbsYCenter>
    <AntiRadius>0</AntiRadius>
    <SnapToCenter>True</SnapToCenter>
  </AbsMouseRegionSettings>
  <OutputContDevice>X360</OutputContDevice>
  <ProfileActions>Disconnect Controller</ProfileActions>
  <Control />
  <ShiftControl />
</APP>";
            #endregion
        }

        private System.Globalization.CultureInfo _prevCulture;

        // Profile (de)serialization must be culture-invariant; force it for deterministic results
        // regardless of the machine's locale (e.g. tr-TR decimal separators / date formats).
        [TestInitialize]
        public void TestInitialize()
        {
            _prevCulture = System.Globalization.CultureInfo.CurrentCulture;
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            System.Globalization.CultureInfo.CurrentCulture = _prevCulture;
        }

        private static string NormalizeLineEndings(string value)
        {
            return value.Replace("\r\n", "\n");
        }

        [TestMethod]
        public void CheckReadProfile()
        {
            // Test profile reading. Will fail if an XML exception is thrown
            XmlSerializer serializer = new XmlSerializer(typeof(ProfileDTO),
                   ProfileDTO.GetAttributeOverrides());
            using StringReader sr = new StringReader(defaultProfileXml);
            BackingStore tempStore = new BackingStore();
            ProfileDTO dto = serializer.Deserialize(sr) as ProfileDTO;
            dto.DeviceIndex = 0; // Use default slot
            dto.MapTo(tempStore);

            // Check settings
            Assert.AreEqual(OutContType.X360, dto.OutputContDevice);
            Assert.AreEqual(OutContType.X360, tempStore.outputDevType[0]);
        }

        [TestMethod]
        public void OldProfile_WithVelocityBypass_LoadsSafely()
        {
            const string lsTag = "  <LSSoftCenterExitVelocityBypass>0.75</LSSoftCenterExitVelocityBypass>";
            const string rsTag = "  <RSSoftCenterExitVelocityBypass>0.85</RSSoftCenterExitVelocityBypass>";
            string legacyXml = defaultProfileXml.Replace(
                "  <LSSoftCenterExitExponent>",
                lsTag + Environment.NewLine + "  <LSSoftCenterExitExponent>");
            legacyXml = legacyXml.Replace(
                "  <RSSoftCenterExitExponent>",
                rsTag + Environment.NewLine + "  <RSSoftCenterExitExponent>");

            XmlSerializer serializer = new XmlSerializer(typeof(ProfileDTO),
                ProfileDTO.GetAttributeOverrides());
            using StringReader sr = new StringReader(legacyXml);
            BackingStore tempStore = new BackingStore();
            ProfileDTO dto = serializer.Deserialize(sr) as ProfileDTO;
            dto.DeviceIndex = 0;
            dto.MapTo(tempStore);

            Assert.AreEqual(0.75, dto.LSSoftCenterExitVelocityBypass, 1e-6);
            Assert.AreEqual(0.85, dto.RSSoftCenterExitVelocityBypass, 1e-6);
            Assert.AreEqual(0.75, tempStore.lsSoftCenterExitInfo[0].velocityBypass, 1e-6);
            Assert.AreEqual(0.85, tempStore.rsSoftCenterExitInfo[0].velocityBypass, 1e-6);
        }

        [TestMethod]
        public void CheckWriteProfile()
        {
            BackingStore tempStore = new BackingStore();
            // Test profile reading. Will fail if an XML exception is thrown
            XmlSerializer serializer = new XmlSerializer(typeof(ProfileDTO),
                   ProfileDTO.GetAttributeOverrides());
            using (StringReader sr = new StringReader(defaultProfileXml))
            {
                ProfileDTO dto = serializer.Deserialize(sr) as ProfileDTO;
                dto.DeviceIndex = 0; // Use default slot
                dto.MapTo(tempStore);
            }

            string testStr = string.Empty;
            serializer = new XmlSerializer(typeof(ProfileDTO),
                ProfileDTO.GetAttributeOverrides());
            using (Utf8StringWriter strWriter = new Utf8StringWriter())
            {
                using XmlWriter xmlWriter = XmlWriter.Create(strWriter,
                    new XmlWriterSettings()
                    {
                        Encoding = Encoding.UTF8,
                        Indent = true,
                    });

                // Write header explicitly
                //xmlWriter.WriteStartDocument();
                //xmlWriter.WriteComment(string.Format(" APP Configuration Data. {0} ", DateTime.Now));
                //xmlWriter.WriteComment(string.Format(" Made with APP version {0} ", Global.exeversion));
                xmlWriter.WriteWhitespace("\r\n");
                xmlWriter.WriteWhitespace("\r\n");

                // Write root element and children
                ProfileDTO dto = new ProfileDTO();
                dto.DeviceIndex = 0; // Use default slot
                dto.SerializeAppAttrs = false;
                dto.MapFrom(tempStore);
                // Omit xmlns:xsi and xmlns:xsd from output
                serializer.Serialize(xmlWriter, dto,
                    new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty }));
                xmlWriter.Flush();
                xmlWriter.Close();

                testStr = strWriter.ToString();
                //Trace.WriteLine("TEST OUTPUT");
                //Trace.WriteLine(testStr);
            }

            Assert.AreEqual(true, !string.IsNullOrEmpty(testStr));
            Assert.AreEqual(NormalizeLineEndings(defaultProfileXml), NormalizeLineEndings(testStr));
        }
    }
}
