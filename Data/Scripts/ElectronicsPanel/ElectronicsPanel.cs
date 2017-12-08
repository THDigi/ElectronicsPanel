using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Digi.ElectronicsPanel
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class ElectronicsPanelMod : MySessionComponentBase
    {
        public override void LoadData()
        {
            instance = this;
            Log.SetUp("Electronics Panel", 514760877, "ElectronicsPanel");
        }

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

        public Action<IMyTerminalBlock> addSmallTopAction;

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

        public void Init()
        {
            Log.Init();
            init = true;
        }

        protected override void UnloadData()
        {
            instance = null;
            init = false;
            Log.Close();
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if(!init)
                {
                    if(MyAPIGateway.Session == null)
                        return;

                    Init();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
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
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorStator), false, ElectronicsPanelMod.PANEL_BASE, ElectronicsPanelMod.PANEL_BASE_4X4)]
    public class PanelBase : MyGameLogicComponent
    {
        private IMyMotorStator stator;
        private bool is4x4 = false;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            stator = (IMyMotorStator)Entity;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                is4x4 = stator.BlockDefinition.SubtypeId == ElectronicsPanelMod.PANEL_BASE_4X4;

                if(stator.CubeGrid.Physics != null)
                {
                    NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;

                    stator.LowerLimitDeg = 0;
                    stator.UpperLimitDeg = 0;
                }

                if(!ElectronicsPanelMod.instance.modifiedTerminalControls)
                {
                    ElectronicsPanelMod.instance.modifiedTerminalControls = true;

                    #region Remove controls on this mod's blocks
                    var controls = new List<IMyTerminalControl>();
                    MyAPIGateway.TerminalControls.GetControls<IMyMotorAdvancedStator>(out controls);

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

                    foreach(var c in controls)
                    {
                        if(controlIds.Contains(c.Id))
                        {
                            string id = c.Id;

                            if(c.Visible != null)
                                ElectronicsPanelMod.instance.controlVisibleFunc[id] = c.Visible; // preserve the existing visible condition

                            c.Visible = (b) =>
                            {
                                var func = ElectronicsPanelMod.instance.controlVisibleFunc.GetValueOrDefault(id, null);
                                return (func == null ? true : func.Invoke(b)) && !ElectronicsPanelMod.IsElectronicsPanel(b.SlimBlock.BlockDefinition.Id);
                            };
                        }
                        else if(c.Id == "Add Small Top Part")
                        {
                            var cb = (IMyTerminalControlButton)c;
                            ElectronicsPanelMod.instance.addSmallTopAction = cb.Action;
                        }
                    }
                    #endregion

                    #region Remove actions on this mod's blocks
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

                    var actions = new List<IMyTerminalAction>();
                    MyAPIGateway.TerminalControls.GetActions<IMyMotorAdvancedStator>(out actions);

                    foreach(var a in actions)
                    {
                        string id = a.Id;

                        if(actionIds.Contains(id))
                        {
                            if(a.Enabled != null)
                                ElectronicsPanelMod.instance.actionEnabledFunc[id] = a.Enabled;

                            a.Enabled = (b) =>
                            {
                                var func = ElectronicsPanelMod.instance.actionEnabledFunc.GetValueOrDefault(id, null);
                                return (func == null ? true : func.Invoke(b)) && !ElectronicsPanelMod.IsElectronicsPanel(b.SlimBlock.BlockDefinition.Id);
                            };
                        }
                    }
                    #endregion
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if(stator.CubeGrid.Physics == null || stator.PendingAttachment || stator.Top == null || stator.Top.Closed)
                    return;

                var matrix = stator.WorldMatrix;

                if(is4x4)
                    matrix.Translation += matrix.Down * (1 - stator.Displacement) + matrix.Forward * 0.75 + matrix.Left * 0.75;
                else
                    matrix.Translation += matrix.Up * stator.Displacement; // displacement is negative

                stator.TopGrid.SetWorldMatrix(matrix);

                if(MyAPIGateway.Multiplayer.IsServer)
                {
                    if(Math.Abs(stator.Angle) > 0)
                    {
                        if(stator.GetValueBool("RotorLock"))
                            stator.SetValueBool("RotorLock", false);

                        stator.TargetVelocityRPM = (stator.Angle > 0 ? 1000 : -1000); // TODO does nothing?!
                    }
                    else
                    {
                        if(!stator.GetValueBool("RotorLock"))
                            stator.SetValueBool("RotorLock", true);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorRotor), false, ElectronicsPanelMod.PANEL_TOP, ElectronicsPanelMod.PANEL_TOP_4X4)]
    public class PanelTop : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                var block = (IMyCubeBlock)Entity;

                if(block.CubeGrid.Physics == null)
                    return;

                if(MyAPIGateway.Multiplayer.IsServer)
                    block.CubeGrid.OnBlockAdded += GridBlockAdded;

                ElectronicsPanelMod.AddElectronicsPanelGrid(block.CubeGrid);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void Close()
        {
            try
            {
                var block = (IMyCubeBlock)Entity;

                if(MyAPIGateway.Multiplayer.IsServer)
                    block.CubeGrid.OnBlockAdded -= GridBlockAdded;

                ElectronicsPanelMod.RemoveElectronicsPanelGrid(block.CubeGrid);
            }
            catch(Exception)
            {
                // ignore exceptions here because they might be when game is closing and we just don't care then.
                // NOTE: try-catch should never be used to control code flow, this is one rare exception.
            }
        }

        private static void GridBlockAdded(IMySlimBlock slim)
        {
            try
            {
                var defId = slim.BlockDefinition.Id;

                if(!ElectronicsPanelMod.IsBlockAllowed(defId))
                    slim.CubeGrid.RemoveBlock(slim);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorRotor), false, ElectronicsPanelMod.PANEL_TOP_DELETE, ElectronicsPanelMod.PANEL_TOP_4X4_DELETE)]
    public class PanelTopDelete : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                if(!MyAPIGateway.Multiplayer.IsServer)
                    return;

                var rotor = (IMyMotorRotor)Entity;
                var stator = rotor.Base;
                var grid = rotor.CubeGrid;
                var gridObj = (MyObjectBuilder_CubeGrid)grid.GetObjectBuilder(false);

                grid.Close();
                gridObj.GridSizeEnum = MyCubeSize.Small;

                if(stator.BlockDefinition.SubtypeId == ElectronicsPanelMod.PANEL_BASE_4X4)
                {
                    gridObj.CubeBlocks[0].SubtypeName = ElectronicsPanelMod.PANEL_TOP_4X4;
                    var matrix = stator.WorldMatrix;
                    var pos = matrix.Translation + matrix.Down + matrix.Forward * 0.75 + matrix.Left * 0.75; // HACK hardcoded

                    if(gridObj.PositionAndOrientation.HasValue)
                        gridObj.PositionAndOrientation = new MyPositionAndOrientation(pos, gridObj.PositionAndOrientation.Value.Forward, gridObj.PositionAndOrientation.Value.Up);
                    else
                        gridObj.PositionAndOrientation = new MyPositionAndOrientation(pos, Vector3.Forward, Vector3.Up);
                }
                else
                {
                    gridObj.CubeBlocks[0].SubtypeName = ElectronicsPanelMod.PANEL_TOP;
                    gridObj.CubeBlocks[0].Min = new SerializableVector3I(-2, 0, -2);
                }

                MyAPIGateway.Entities.RemapObjectBuilder(gridObj);

                var newGrid = (IMyCubeGrid)MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(gridObj);
                var newRotor = (IMyMotorRotor)newGrid.GetCubeBlock(gridObj.CubeBlocks[0].Min).FatBlock;

                var linearVel = stator.CubeGrid.Physics.LinearVelocity;
                var angularVel = stator.CubeGrid.Physics.AngularVelocity;

                // execute next tick
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    if(newRotor.Closed || stator.Closed)
                        return;

                    stator.Attach(newRotor);

                    // TODO needed?
                    stator.CubeGrid.Physics.LinearVelocity = linearVel;
                    stator.CubeGrid.Physics.AngularVelocity = angularVel;
                });
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }

    #region CubeBuilder - notify if allowed to place
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CubePlacer), false)]
    public class CubeBuilder : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        public override void UpdateBeforeSimulation10()
        {
            try
            {
                var builder = MyCubeBuilder.Static;
                var def = builder?.CubeBuilderState?.CurrentBlockDefinition;

                if(def != null && !ElectronicsPanelMod.IsBlockAllowed(def.Id))
                {
                    var hit = (IHitInfo)builder.HitInfo;
                    var grid = hit?.HitEntity as IMyCubeGrid;

                    if(grid != null && ElectronicsPanelMod.IsElectronicsPanelGrid(grid.EntityId))
                    {
                        ElectronicsPanelMod.Notify(0, "Can't build '" + def.DisplayNameText + "' on an Electronics Panel!", MyFontEnum.Red);
                        ElectronicsPanelMod.Notify(1, ElectronicsPanelMod.ALLOWED_TYPES_STRING, MyFontEnum.White);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
    #endregion
}