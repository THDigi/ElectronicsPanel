using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace Digi.ElectronicsPanel
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class ElectronicsPanelMod : MySessionComponentBase
    {
        public static ElectronicsPanelMod instance;

        public bool init = false;
        public bool modifiedTerminalControls = false;
        public IMyHudNotification[] notify = new IMyHudNotification[2];

        private readonly HashSet<long> electronicPanelGrids = new HashSet<long>();

        public const string PANEL_BASE = "ElectronicsPanel";
        public const string PANEL_BASE_4X4 = "ElectronicsPanel4x4";
        public const string PANEL_TOP = "ElectronicsPanelHead";
        public const string PANEL_TOP_4X4 = "ElectronicsPanelHead4x4";
        public const string PANEL_TOP_DELETE = "ElectronicsPanelHeadDelete";
        public const string PANEL_TOP_4X4_DELETE = "ElectronicsPanelHead4x4Delete";

        public readonly HashSet<MyStringHash> panelSubtypeIds = new HashSet<MyStringHash>()
        {
            MyStringHash.GetOrCompute(PANEL_BASE),
            MyStringHash.GetOrCompute(PANEL_BASE_4X4),
        };

        public readonly Dictionary<string, Func<IMyTerminalBlock, bool>> actionEnabledFunc = new Dictionary<string, Func<IMyTerminalBlock, bool>>();
        public readonly Dictionary<string, Func<IMyTerminalBlock, bool>> controlVisibleFunc = new Dictionary<string, Func<IMyTerminalBlock, bool>>();

        public const string ALLOWED_TYPES_STRING = "Allowed: PB, Timer, LCD, Light, Battery, Button, Speaker, Sensor, Antenna, Beacon, Camera, Projector and ControlPanel.";

        private readonly MyStringHash CONTROLPANEL_SUBTYPEID = MyStringHash.GetOrCompute("SmallControlPanel");
        private readonly HashSet<MyObjectBuilderType> allowedBlockTypes = new HashSet<MyObjectBuilderType>()
        {
            typeof(MyObjectBuilder_MyProgrammableBlock),
            typeof(MyObjectBuilder_TimerBlock),
            typeof(MyObjectBuilder_InteriorLight),
            typeof(MyObjectBuilder_ReflectorLight),
            typeof(MyObjectBuilder_BatteryBlock),
            typeof(MyObjectBuilder_ButtonPanel),
            typeof(MyObjectBuilder_SoundBlock),
            typeof(MyObjectBuilder_TextPanel),
            typeof(MyObjectBuilder_SensorBlock),
            typeof(MyObjectBuilder_RadioAntenna),
            typeof(MyObjectBuilder_LaserAntenna),
            typeof(MyObjectBuilder_Beacon),
            typeof(MyObjectBuilder_CameraBlock),
            typeof(MyObjectBuilder_Projector),
        };

        public override void LoadData()
        {
            instance = this;
            Log.ModName = "Electronics Panel";
            Log.AutoClose = true;
        }

        protected override void UnloadData()
        {
            instance = null;
        }

        public static bool IsBlockAllowed(MyDefinitionId defId)
        {
            if(defId.TypeId == typeof(MyObjectBuilder_TerminalBlock) && defId.SubtypeId == instance.CONTROLPANEL_SUBTYPEID)
                return true;

            return instance.allowedBlockTypes.Contains(defId.TypeId);
        }

        public static bool IsElectronicsPanel(MyDefinitionId id)
        {
            return instance.panelSubtypeIds.Contains(id.SubtypeId);
        }

        public static bool IsElectronicsPanelGrid(long entityId)
        {
            return instance.electronicPanelGrids.Contains(entityId);
        }

        public static void AddElectronicsPanelGrid(IMyCubeGrid grid)
        {
            instance.electronicPanelGrids.Add(grid.EntityId);
        }

        public static void RemoveElectronicsPanelGrid(IMyCubeGrid grid)
        {
            instance.electronicPanelGrids.Remove(grid.EntityId);
        }

        public static void Notify(int index, string text, string font, int aliveTime = 200)
        {
            if(index >= instance.notify.Length)
            {
                Log.Error($"Too high notify index: {index}");
                return;
            }

            var notify = instance.notify[index];

            if(notify == null)
            {
                instance.notify[index] = notify = MyAPIGateway.Utilities.CreateNotification(text, aliveTime, font);
            }
            else
            {
                notify.Font = font;
                notify.Text = text;
                notify.AliveTime = aliveTime;
            }

            notify.Show();
        }

        public static void SetupTerminalControls()
        {
            if(instance.modifiedTerminalControls)
                return;

            instance.modifiedTerminalControls = true;

            SetupControls();
            SetupActions();
        }

        private static void SetupControls()
        {
            var controlIds = new HashSet<string>()
            {
                "Add Small Top Part", // HACK using large top part because "add small top part" causes the 5x5 panel to be attached wrong on normal rotor stators.
                "Reverse",
                "Torque",
                "BrakingTorque",
                "Velocity",
                "LowerLimit",
                "UpperLimit",
                "Displacement",
                "RotorLock",

                // no longer exist...
                "Weld speed",
                "Force weld",
            };

            List<IMyTerminalControl> controls;
            MyAPIGateway.TerminalControls.GetControls<IMyMotorAdvancedStator>(out controls);

            foreach(var c in controls)
            {
                string id = c.Id;

                if(controlIds.Contains(id))
                {
                    if(c.Visible != null)
                        instance.controlVisibleFunc[id] = c.Visible; // preserve the existing visible condition

                    c.Visible = (b) =>
                    {
                        var func = instance.controlVisibleFunc.GetValueOrDefault(id, null);
                        return (func == null ? true : func.Invoke(b)) && !IsElectronicsPanel(b.SlimBlock.BlockDefinition.Id);
                    };
                }
            }
        }

        private static void SetupActions()
        {
            var actionIds = new HashSet<string>()
            {
                "Add Small Top Part",
                "Reverse",
                "RotorLock",
                "IncreaseTorque",
                "DecreaseTorque",
                "ResetTorque",
                "IncreaseBrakingTorque",
                "DecreaseBrakingTorque",
                "ResetBrakingTorque",
                "IncreaseVelocity",
                "DecreaseVelocity",
                "ResetVelocity",
                "IncreaseLowerLimit",
                "DecreaseLowerLimit",
                "ResetLowerLimit",
                "IncreaseUpperLimit",
                "DecreaseUpperLimit",
                "ResetUpperLimit",
                "IncreaseDisplacement",
                "DecreaseDisplacement",
                "ResetDisplacement",
            };

            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<IMyMotorAdvancedStator>(out actions);

            foreach(var a in actions)
            {
                string id = a.Id;

                if(actionIds.Contains(id))
                {
                    if(a.Enabled != null)
                        instance.actionEnabledFunc[id] = a.Enabled;

                    a.Enabled = (b) =>
                    {
                        var func = instance.actionEnabledFunc.GetValueOrDefault(id, null);
                        return (func == null ? true : func.Invoke(b)) && !IsElectronicsPanel(b.SlimBlock.BlockDefinition.Id);
                    };
                }
            }
        }
    }
}
