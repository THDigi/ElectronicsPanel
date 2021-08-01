using System;
using System.Collections.Generic;
using System.Text;
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

        private readonly HashSet<MyDefinitionId> allowedBlockIds = new HashSet<MyDefinitionId>()
        {
            new MyDefinitionId(typeof(MyObjectBuilder_TerminalBlock), "SmallControlPanel"),
        };

        private readonly Dictionary<ulong, ModInfo> allowedBlocksFromMods = new Dictionary<ulong, ModInfo>()
        {
            [728555954] = new ModInfo()
            {
                BlockNames = new string[]
                {
                    "Hacking Computer",
                },
                BlockDefIds = new MyDefinitionId[]
                {
                    new MyDefinitionId(typeof(MyObjectBuilder_UpgradeModule), "SmallHackingBlock"),
                },
            },
        };

        public const string ALLOWED_TYPES_STRING = "Allowed: PB, Timer, LCD, Light, Battery, Button, Speaker, Sensor, Antenna, Beacon, Camera, Projector and ControlPanel.";

        class ModInfo
        {
            public string[] BlockNames;
            public MyDefinitionId[] BlockDefIds;
        }

        internal static ElectronicsPanelMod instance;
        internal string AllowedModdedBlocks = null;

        private bool modifiedTerminalControls = false;
        private IMyHudNotification[] hudNotifications = new IMyHudNotification[3];
        private readonly HashSet<long> electronicPanelGrids = new HashSet<long>();

        public override void LoadData()
        {
            instance = this;
            Log.ModName = "Electronics Panel";
            Log.AutoClose = true;
        }

        public override void BeforeStart()
        {
            try
            {
                ComputeAllowedBlocks();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        protected override void UnloadData()
        {
            instance = null;
        }

        private void ComputeAllowedBlocks()
        {
            if(MyAPIGateway.Utilities.IsDedicated)
                return;

            StringBuilder sb = null;
            ModInfo modInfo;

            foreach(var mod in MyAPIGateway.Session.Mods)
            {
                if(mod.PublishedFileId == 0)
                    continue;

                if(!allowedBlocksFromMods.TryGetValue(mod.PublishedFileId, out modInfo))
                    continue;

                if(sb == null)
                    sb = new StringBuilder("Allowed from mods: ");

                foreach(var name in modInfo.BlockNames)
                {
                    sb.Append(name).Append(", ");
                }

                foreach(var id in modInfo.BlockDefIds)
                {
                    allowedBlockIds.Add(id);
                }
            }

            if(sb != null)
            {
                sb.Length -= 2; // remove trailing ", "
                AllowedModdedBlocks = sb.ToString();
            }
        }

        public static bool IsBlockAllowed(MyDefinitionId defId)
        {
            if(instance.allowedBlockTypes.Contains(defId.TypeId))
                return true;

            if(instance.allowedBlockIds.Contains(defId))
                return true;

            return false;
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
            var hudNotifications = instance.hudNotifications;

            if(index < 0 || index >= hudNotifications.Length)
                throw new ArgumentException($"Too high notify index: {index}");

            var notify = hudNotifications[index];

            if(notify == null)
            {
                hudNotifications[index] = notify = MyAPIGateway.Utilities.CreateNotification(text, aliveTime, font);
            }
            else
            {
                notify.Hide(); // required since SE v1.194
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
            List<IMyTerminalControl> controls;
            MyAPIGateway.TerminalControls.GetControls<IMyMotorAdvancedStator>(out controls);

            foreach(var c in controls)
            {
                switch(c.Id)
                {
                    // hide these controls for this mods' blocks

                    // HACK not allowing this for adding small part as it causes the 5x5 panel to be attached wrong and self-spins
                    case "Add Small Top Part":
                    case "AddSmallRotorTopPart":
                    case "AddSmallHingeTopPart":

                    case "Reverse":
                    case "Torque":
                    case "BrakingTorque":
                    case "Velocity":
                    case "LowerLimit":
                    case "UpperLimit":
                    case "Displacement":
                    case "RotorLock":
                    case "HingeLock":

                    // no longer exist visible to user but are still in code...
                    case "Weld speed":
                    case "Force weld":
                        c.Visible = CombineFunc.Create(c.Visible, Visible);
                        break;

                        //case "Attach":
                        // TODO: needs custom code to allow it to actually attach
                }
            }
        }

        private static void SetupActions()
        {
            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<IMyMotorAdvancedStator>(out actions);

            foreach(var a in actions)
            {
                switch(a.Id)
                {
                    // hide these actions for this mods' blocks
                    case "Add Small Top Part":
                    case "AddSmallRotorTopPart":
                    case "AddSmallHingeTopPart":
                    case "Reverse":
                    case "RotorLock":
                    case "HingeLock":
                    case "IncreaseTorque":
                    case "DecreaseTorque":
                    case "ResetTorque":
                    case "IncreaseBrakingTorque":
                    case "DecreaseBrakingTorque":
                    case "ResetBrakingTorque":
                    case "IncreaseVelocity":
                    case "DecreaseVelocity":
                    case "ResetVelocity":
                    case "IncreaseLowerLimit":
                    case "DecreaseLowerLimit":
                    case "ResetLowerLimit":
                    case "IncreaseUpperLimit":
                    case "DecreaseUpperLimit":
                    case "ResetUpperLimit":
                    case "IncreaseDisplacement":
                    case "DecreaseDisplacement":
                    case "ResetDisplacement":
                        a.Enabled = CombineFunc.Create(a.Enabled, Visible);
                        break;

                        //case "Attach":
                        // TODO: needs custom code to allow it to actually attach
                }
            }
        }

        private static bool Visible(IMyTerminalBlock block)
        {
            return !IsElectronicsPanel(block.SlimBlock.BlockDefinition.Id);
        }
    }
}
