using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
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

        public const ushort ChannelId = 60877;

        public const string ALLOWED_TYPES_LINE1 = "Allowed: PB, Timer, LCD, Light, Battery, Button, Speaker, Sensor, Antenna, Beacon,";
        public const string ALLOWED_TYPES_LINE2 = "                Camera, Projector, ControlPanel, TurretControlBlock, EventController, AI Blocks.";

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
            typeof(MyObjectBuilder_LCDPanelsBlock),
            typeof(MyObjectBuilder_SensorBlock),
            typeof(MyObjectBuilder_RadioAntenna),
            typeof(MyObjectBuilder_LaserAntenna),
            typeof(MyObjectBuilder_Beacon),
            typeof(MyObjectBuilder_CameraBlock),
            typeof(MyObjectBuilder_Projector),
            typeof(MyObjectBuilder_TurretControlBlock),

            // HACK: backwards compatibility
#if !(VERSION_201 || VERSION_200 || VERSION_199 || VERSION_198 || VERSION_197 || VERSION_196 || VERSION_195 || VERSION_194 || VERSION_193 || VERSION_192 || VERSION_191 || VERSION_190)
            typeof(MyObjectBuilder_EventControllerBlock),
            typeof(MyObjectBuilder_PathRecorderBlock),
            typeof(MyObjectBuilder_BasicMissionBlock),
            typeof(MyObjectBuilder_FlightMovementBlock),
            typeof(MyObjectBuilder_DefensiveCombatBlock),
            typeof(MyObjectBuilder_OffensiveCombatBlock),
            typeof(MyObjectBuilder_EmotionControllerBlock),
#endif

            // TODO allow?
            //typeof(MyObjectBuilder_OreDetector),
            //typeof(MyObjectBuilder_SolarPanel),
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

        class ModInfo
        {
            public string[] BlockNames;
            public MyDefinitionId[] BlockDefIds;
        }

        public readonly HashSet<MyStringHash> panelSubtypeIds = new HashSet<MyStringHash>()
        {
            MyStringHash.GetOrCompute(PANEL_BASE),
            MyStringHash.GetOrCompute(PANEL_BASE_4X4),
        };

        internal static ElectronicsPanelMod Instance;
        internal string AllowedModdedBlocks = null;
        internal List<MyEntity> Entitites = new List<MyEntity>();

        private Action<IMyTerminalBlock> AttachAction;
        private bool ModifiedTerminalControls = false;
        private IMyHudNotification[] hudNotifications = new IMyHudNotification[4];
        private readonly HashSet<long> electronicPanelGrids = new HashSet<long>();

        public override void LoadData()
        {
            Instance = this;
            Log.ModName = "Electronics Panel";
            Log.AutoClose = true;
        }

        public override void BeforeStart()
        {
            try
            {
                if(MyAPIGateway.Multiplayer.IsServer)
                    MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ChannelId, ReceivedPacket);

                ComputeAllowedBlocks();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        protected override void UnloadData()
        {
            Instance = null;

            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ChannelId, ReceivedPacket);
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

        private void ReceivedPacket(ushort channelId, byte[] data, ulong sender, bool isSenderServer)
        {
            try
            {
                if(!MyAPIGateway.Multiplayer.IsServer)
                    return;

                long entityId = BitConverter.ToInt64(data, 0);

                MyEntity ent = MyEntities.GetEntityById(entityId);
                PanelBase logic = ent?.GameLogic?.GetAs<PanelBase>();
                if(logic == null)
                    return;

                logic.FindAndAttach(showMessages: false);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public static bool IsBlockAllowed(MyDefinitionId defId)
        {
            if(Instance.allowedBlockTypes.Contains(defId.TypeId))
                return true;

            if(Instance.allowedBlockIds.Contains(defId))
                return true;

            return false;
        }

        public static bool IsElectronicsPanel(MyDefinitionId id)
        {
            return Instance.panelSubtypeIds.Contains(id.SubtypeId);
        }

        public static bool IsElectronicsPanelGrid(long entityId)
        {
            return Instance.electronicPanelGrids.Contains(entityId);
        }

        public static void AddElectronicsPanelGrid(IMyCubeGrid grid)
        {
            Instance.electronicPanelGrids.Add(grid.EntityId);
        }

        public static void RemoveElectronicsPanelGrid(IMyCubeGrid grid)
        {
            Instance.electronicPanelGrids.Remove(grid.EntityId);
        }

        public static void Notify(int index, string text, string font, int aliveTime = 200)
        {
            var hudNotifications = Instance.hudNotifications;

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
            if(Instance.ModifiedTerminalControls)
                return;

            Instance.ModifiedTerminalControls = true;

            SetupActions();
            SetupControls();
        }

        private static void SetupControls()
        {
            List<IMyTerminalControl> controls;
            MyAPIGateway.TerminalControls.GetControls<IMyMotorAdvancedStator>(out controls); // HACK: IMyMotorStator doesn't work for some reason

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
                        c.Visible = CombineFunc.Create(c.Visible, RotorControlsVisible);
                        break;

                    case "Attach":
                        var button = (IMyTerminalControlButton)c;
                        if(Instance.AttachAction == null && button.Action != null)
                            Instance.AttachAction = button.Action;

                        button.Action = Action_Attach;
                        break;
                }
            }
        }

        private static void SetupActions()
        {
            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<IMyMotorAdvancedStator>(out actions); // HACK: IMyMotorStator doesn't work for some reason

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
                        a.Enabled = CombineFunc.Create(a.Enabled, RotorControlsVisible);
                        break;

                    case "Attach":
                        if(a.Action != null)
                            Instance.AttachAction = a.Action;
                        a.Action = Action_Attach;
                        break;
                }
            }
        }

        private static bool RotorControlsVisible(IMyTerminalBlock b)
        {
            return b?.GameLogic?.GetAs<PanelBase>() == null;
        }

        private static void Action_Attach(IMyTerminalBlock b)
        {
            try
            {
                PanelBase logic = b?.GameLogic?.GetAs<PanelBase>();
                if(logic != null)
                {
                    logic.FindAndAttach(showMessages: true);
                }
                else
                {
                    Instance.AttachAction?.Invoke(b);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
