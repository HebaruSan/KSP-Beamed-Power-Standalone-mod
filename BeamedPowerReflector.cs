﻿using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace BeamedPowerStandalone
{
    public class WirelessReflector : PartModule
    {
        // parameters set in part.cfg file
        [KSPField(isPersistant = false)]
        public float Reflectivity;

        [KSPField(isPersistant = false)]
        public float ReflectorDiameter;

        // optional parameters also set in part.cfg, only needed if the reflector can amplify
        [KSPField(isPersistant = false)]
        public string CanAmplify;

        [KSPField(isPersistant = false)]
        public float Efficiency;

        [KSPField(isPersistant = false)]
        public string wavelength;

        [KSPField(isPersistant = false)]
        public float maxCoreTemp = 900f;

        [KSPField(isPersistant = false)]
        public float maxSkinTemp = 1200f;

        // counter variables used to cycle through transmitter and receiver lists respectively
        [KSPField(isPersistant = true)]
        public int transmitterCounter;

        [KSPField(isPersistant = true)]
        public int receiverCounter;

        // variables on transmitter
        [KSPField(isPersistant = true)]
        public float excess;

        [KSPField(isPersistant = true)]
        public float constant;

        [KSPField(isPersistant = true)]
        public string Wavelength;

        [KSPField(isPersistant = true)]
        public float resourceConsumption;

        // reflector specific variables
        [KSPField(guiName = "Beam Reflector", isPersistant = true, guiActive = true, guiActiveEditor = false), UI_Toggle(scene = UI_Scene.Flight)]
        public bool IsEnabled;

        [KSPField(guiName = "Power Reflected", guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "kW")]
        public float PowerReflected;

        [KSPField(guiName = "Amplify power", guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "x"), UI_FloatRange(minValue = 1, maxValue = 5, stepIncrement = 0.05f, scene = UI_Scene.Flight)]
        public float AmplifyMult = 1f;

        [KSPField(guiName = "Status", guiActive = true, guiActiveEditor = false, isPersistant = false)]
        public string State;

        [KSPField(guiName = "Core Temperature", groupName = "HeatInfo", groupDisplayName = "Heat Info", groupStartCollapsed = false, guiActive = true, guiActiveEditor = false, isPersistant = false)]
        public float CoreTemp;

        [KSPField(guiName = "Skin Temperature", groupName = "HeatInfo", guiActive = true, guiActiveEditor = false, isPersistant = false)]
        public float SkinTemp;

        [KSPField(guiName = "Waste Heat", groupName = "HeatInfo", guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "kW")]
        public float WasteHeat;

        // adding vessel names for 'from' and 'to' to part right-click menu in flight
        [KSPField(guiName = "From", guiActive = true, guiActiveEditor = false, isPersistant = false)]
        public string TransmitterName = Localizer.Format("#LOC_BeamedPower_Vessel_None");

        [KSPField(guiName = "To", guiActive = true, guiActiveEditor = false, isPersistant = true)]
        public string TransmittingTo = Localizer.Format("#LOC_BeamedPower_Vessel_None");

        // declaring frequently used variables
        VesselFinder vesselFinder = new VesselFinder();
        readonly int EChash = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id; int initFrames;
        ModuleCoreHeat coreHeat; ReceivedPower receiver = new ReceivedPower(); double heatModifier;

        string operational = Localizer.Format("#LOC_BeamedPower_status_Operational");
        string ExceedTempLimit = Localizer.Format("#LOC_BeamedPower_status_ExceededTempLimit");

        List<ConfigNode> receiverConfigList;

        // KSPEvent buttons to cycle through vessels lists
        [KSPEvent(guiName = "Cycle through transmitter vessels", guiActive = true, guiActiveEditor = false, requireFullControl = true)]
        public void TransmitterCounter()
        {
            transmitterCounter += 1;
        }

        [KSPEvent(guiName = "Cycle through receiver vessels", guiActive = true, guiActiveEditor = false, requireFullControl = true)]
        public void ReceiverCounter()
        {
            receiverCounter = (receiverCounter < receiverConfigList.Count - 1) ? receiverCounter += 1 : receiverCounter = 0;
        }

        // initialise variables
        public void Start()
        {
            initFrames = 0;
            receiverConfigList = new List<ConfigNode>();
            Fields["CoreTemp"].guiUnits = "K/" + maxCoreTemp.ToString() + "K";
            Fields["SkinTemp"].guiUnits = "K/" + maxSkinTemp.ToString() + "K";

            if (CanAmplify == "False")
            {
                Fields["AmplifyMult"].guiActive = false;
            }

            SetHeatParams();
            SetLocalization();
        }

        private void SetLocalization()
        {
            //flight
            Fields["Beam Reflector"].guiName = Localizer.Format("#LOC_BeamedPower_WirelessReflector_BeamReflector");
            Fields["Power Reflected"].guiName = Localizer.Format("#LOC_BeamedPower_WirelessReflector_PowerReflected");
            Fields["Amplify power"].guiName = Localizer.Format("#LOC_BeamedPower_WirelessReflector_AmplifyPower");
            Fields["Status"].guiName = Localizer.Format("#LOC_BeamedPower_Status");
            Fields["CoreTemp"].guiName = Localizer.Format("#LOC_BeamedPower_CoreTemp");
            Fields["SkinTemp"].guiName = Localizer.Format("#LOC_BeamedPower_SkinTemp");
            Fields["WasteHeat"].guiName = Localizer.Format("#LOC_BeamedPower_WasteHeat");
            Fields["CoreTemp"].group.displayName = Localizer.Format("#LOC_BeamedPower_HeatInfo");
            Fields["From"].guiName = Localizer.Format("#LOC_BeamedPower_WirelessReflector_From");
            Fields["To"].guiName = Localizer.Format("#LOC_BeamedPower_WirelessReflector_To");
            Events["Cycle through transmitter vessels"].guiName = Localizer.Format("#LOC_BeamedPower_WirelessReflector_CycleTransmitters");
            Events["Cycle through receiver vessels"].guiName = Localizer.Format("#LOC_BeamedPower_WirelessReflector_CycleReceivers");
            Actions["Toggle Power Reflector"].guiName = Localizer.Format("#LOC_BeamedPower_Actions_ToggleReflector");
            Actions["Activate Power Reflector"].guiName = Localizer.Format("#LOC_BeamedPower_Actions_ActivateReflector");
            Actions["Deactivate Power Reflector"].guiName = Localizer.Format("#LOC_BeamedPower_Actions_DeactivateReflector");
            //editor
            Fields["Received Power"].guiName = Localizer.Format("#LOC_BeamedPower_RecvPower");
            Fields["Amplify"].guiName = Localizer.Format("#LOC_BeamedPower_WirelessReflector_AmplifyPower");
            Fields["Result"].guiName = Localizer.Format("#LOC_BeamedPower_CalcResult");
        }

        private void SetHeatParams()
        {
            this.part.AddModule("ModuleCoreHeat");
            coreHeat = this.part.Modules.GetModule<ModuleCoreHeat>();
            coreHeat.CoreTempGoal = maxCoreTemp * 1.4;  // placeholder value, there is no optimum temperature
            coreHeat.CoolantTransferMultiplier *= 2d;
            coreHeat.HeatRadiantMultiplier *= 2d;
        }

        private void AddHeatToCore()
        {
            CoreTemp = (float)(Math.Round(coreHeat.CoreTemperature, 1));
            SkinTemp = (float)(Math.Round(this.part.skinTemperature, 1));

            if (CoreTemp > maxCoreTemp | SkinTemp > maxSkinTemp)
            {
                State = ExceedTempLimit;
                IsEnabled = false;
            }
            if (State == ExceedTempLimit & (CoreTemp >= maxCoreTemp * 0.7 | SkinTemp >= maxSkinTemp * 0.7))
            {
                IsEnabled = false;
            }
            else if (CoreTemp < maxCoreTemp * 0.7 & SkinTemp < maxSkinTemp * 0.7)
            {
                State = operational;
            }
            heatModifier = (double)HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().PercentHeat / 100;
            double heatExcess = (1 - Efficiency) * Mathf.Clamp(resourceConsumption, 0f, 50000f/Efficiency) * heatModifier;
            WasteHeat = (float)Math.Round(heatExcess, 1);
            coreHeat.AddEnergyToCore(heatExcess * 0.7 * TimeWarp.fixedDeltaTime);  // first converted to kJ
            this.part.AddSkinThermalFlux(heatExcess * 0.2);     // add some heat to skin
        }

        [KSPAction(guiName = "Toggle Power Reflector")]
        public void ToggleReflector(KSPActionParam param)
        {
            IsEnabled = (IsEnabled) ? false : true;
        }
        [KSPAction(guiName = "Activate Power Reflector")]
        public void ActivateReflector(KSPActionParam param)
        {
            IsEnabled = (IsEnabled) ? true : true;
        }
        [KSPAction(guiName = "Deactivate Power Reflector")]
        public void DeactivateReflector(KSPActionParam param)
        {
            IsEnabled = (IsEnabled) ? false : false;
        }

        private void SyncAnimationState()
        {
            if (this.part.Modules.Contains<ModuleDeployableAntenna>() &&
                this.part.Modules.GetModule<ModuleDeployableAntenna>().deployState != ModuleDeployableAntenna.DeployState.EXTENDED)
            {
                IsEnabled = false;
            }
            else if (this.part.Modules.Contains<ModuleDeployablePart>() &&
                this.part.Modules.GetModule<ModuleDeployablePart>().deployState != ModuleDeployablePart.DeployState.EXTENDED)
            {
                IsEnabled = false;
            }
        }

        // adding part info to part description tab in editor
        public string GetModuleTitle()
        {
            return "BeamedPowerReflector";
        }
        public override string GetModuleDisplayName()
        {
            return Localizer.Format("#LOC_BeamedPower_WirelessReflector_ModuleName");
        }
        public override string GetInfo()
        {
            string Long = Localizer.Format("#LOC_BeamedPower_Wavelength_long");
            string Short = Localizer.Format("#LOC_BeamedPower_Wavelength_short");
            string wavelengthLocalized = (wavelength == "Long") ? Long : Short;

            return Localizer.Format("#LOC_BeamedPower_WirelessReflector_ModuleInfo",
                ReflectorDiameter.ToString(),
                (Reflectivity * 100).ToString(),
                CanAmplify,
                (Efficiency * 100).ToString(),
                wavelengthLocalized);
        }

        // main block of code- runs every physics frame
        public void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                // we wait ~60 frames before starting to add heat, to stop overheating bugs
                if (initFrames < 60)
                {
                    initFrames += 1;
                }
                else
                {
                    AddHeatToCore();
                }
                SyncAnimationState();

                // fail-safe mechanism (ie if amplify setting is set too high and craft runs out of power, amplifier will be shutdown)
                this.vessel.GetConnectedResourceTotals(EChash, out double amount, out double maxAmount);
                if (amount/maxAmount < 0.2d)
                {
                    IsEnabled = false;
                }

                receiver.Directional(this.part, transmitterCounter, IsEnabled, 100f, ReflectorDiameter, Reflectivity, false, false,
                    State, out State, out TransmitterName, out double recvPower, out transmitterCounter);
                try
                {
                    Wavelength = receiver.wavelengthList[transmitterCounter];
                }
                catch
                {
                    Wavelength = "Long";
                }
                
                excess = (float)Math.Round(recvPower, 1);
                this.part.AddSkinThermalFlux((excess / Reflectivity) * (1 - Reflectivity) * (heatModifier / 100));

                if (CanAmplify == "True" && wavelength == Wavelength)
                {
                    bool background = HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().BackgroundProcessing;
                    resourceConsumption = (float)(recvPower * (AmplifyMult - 1));
                    if (background == false)
                    {
                        this.part.RequestResource(EChash, (double)resourceConsumption * Time.fixedDeltaTime);
                    }
                    excess += Mathf.Clamp((resourceConsumption * Efficiency), 0f, 50000f);
                }
                else
                {
                    AmplifyMult = 1f;
                }
                PowerReflected = (float)Math.Round(excess, 1);

                constant = (float)((Wavelength == "Long") ? 1.44 * Math.Pow(10, -3) / ReflectorDiameter : 
                    1.44 * 5 * Math.Pow(10, -8) / ReflectorDiameter);
                vesselFinder.ReceiverData(out receiverConfigList);
                TransmittingTo = receiverConfigList[receiverCounter].GetValue("name");
            }
        }

        // reflected power calculator in part right-click menu
        [KSPField(guiName = "Received Power", guiActive = false, guiActiveEditor = true, groupName = "reflectedpowerCalc", groupDisplayName = "Reflected Power Calculator", groupStartCollapsed = true, isPersistant = false, guiUnits = "kW"), UI_FloatRange(scene = UI_Scene.Editor, minValue = 0, maxValue = 100000, stepIncrement = 1)]
        public float ReceivedPower;

        [KSPField(guiName = "Amplify", guiActive = false, guiActiveEditor = true, groupName = "reflectedpowerCalc", isPersistant = false, guiUnits = "x"), UI_FloatRange(scene = UI_Scene.Editor, minValue = 1, maxValue = 5, stepIncrement = 0.05f)]
        public float Amplify = 1f;

        [KSPField(guiName = "Result", guiActive = false, guiActiveEditor = true, groupName = "reflectedpowerCalc", isPersistant = false, guiUnits = "kW")]
        public float ReflectedPower;

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                ReflectedPower = ReceivedPower * Reflectivity;
                if (CanAmplify == "True")
                {
                    ReflectedPower += Mathf.Clamp((ReceivedPower * (Amplify - 1) * Efficiency), 0f, 50000f);
                }
                else
                {
                    Amplify = 1f;
                }
                ReflectedPower = (float)Math.Round(ReflectedPower, 1);
            }
            else if (HighLogic.LoadedSceneIsFlight)
            {
                if (CanAmplify != "True" | Wavelength != wavelength)
                {
                    AmplifyMult = 1f;
                }
            }
        }
    }
}
