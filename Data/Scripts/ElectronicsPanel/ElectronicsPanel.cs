using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;
using VRage;
using VRage.ObjectBuilders;
using VRage.Game.Components;
using VRage.ModAPI;

namespace Digi.ElectronicsPanel
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class ElectronicsPanelMod : MySessionComponentBase
    {
        public override void LoadData()
        {
            Log.SetUp("Electronics Panel", 514760877, "ElectronicsPanel");
        }

        public static bool init { get; private set; }

        public const string PANEL_BASE = "ElectronicsPanel";
        public const string PANEL_BASE_4X4 = "ElectronicsPanel4x4";
        public const string PANEL_TOP = "ElectronicsPanelHead";
        public const string PANEL_TOP_4X4 = "ElectronicsPanelHead4x4";
        public const string PANEL_TOP_DELETE = "ElectronicsPanelHeadDelete";

        private const string CONTROLPANEL_SUBTYPEID = "SmallControlPanel";

        public static readonly HashSet<long> electronicPanelGrids = new HashSet<long>();

        private static readonly HashSet<MyObjectBuilderType> allowedBlockTypes = new HashSet<MyObjectBuilderType>()
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
            init = false;
            electronicPanelGrids.Clear();
            Log.Close();
        }

        public override void UpdateAfterSimulation()
        {
            if(!init)
            {
                if(MyAPIGateway.Session == null)
                    return;

                Init();
            }
        }

        public static bool IsBlockAllowed(MyObjectBuilderType typeId, string subTypeId)
        {
            if(typeId == typeof(MyObjectBuilder_TerminalBlock) && subTypeId == CONTROLPANEL_SUBTYPEID)
                return true;

            return allowedBlockTypes.Contains(typeId);
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorStator), ElectronicsPanelMod.PANEL_BASE, ElectronicsPanelMod.PANEL_BASE_4X4)]
    public class ElectronicsPanel : MyGameLogicComponent
    {
        private bool is4x4 = false;
        private byte skip = 200;
        private byte justAttached = 0;

        private static BoundingSphereD sphere = new BoundingSphereD(Vector3D.Zero, 1);

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            is4x4 = (Entity as IMyCubeBlock).BlockDefinition.SubtypeId == ElectronicsPanelMod.PANEL_BASE_4X4;
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                var stator = (IMyMotorStator)Entity;

                //MyAPIGateway.Utilities.ShowNotification("rotor="+(stator.Rotor != null)+"; "+(stator.IsAttached?"IsAttached; ":"")+(stator.PendingAttachment?"Pending; ":"")+(stator.IsLocked?"IsLocked":""), 16, MyFontEnum.Red);

                if(stator.PendingAttachment || stator.Rotor == null || stator.Rotor.Closed)
                {
                    if(!stator.Enabled || ++skip < 15)
                        return;

                    skip = 0;
                    sphere.Center = stator.WorldMatrix.Translation;
                    var ents = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);

                    foreach(var ent in ents)
                    {
                        var rotor = ent as IMyMotorRotor;

                        if(rotor?.CubeGrid.Physics != null && (is4x4 ? rotor.BlockDefinition.SubtypeId == ElectronicsPanelMod.PANEL_TOP_4X4 : rotor.BlockDefinition.SubtypeId == ElectronicsPanelMod.PANEL_TOP))
                        {
                            stator.Attach(rotor);
                            justAttached = 5; // check if it's locked for the next 5 update frames including this one
                            break;
                        }
                    }

                    return;
                }

                if(justAttached > 0)
                {
                    justAttached--;

                    if(stator.IsLocked)
                    {
                        justAttached = 0;
                        stator.ApplyAction("Force weld"); // disable safety lock after attaching it because the IsLocked check doesn't work when not attached
                    }
                }

                if(stator.IsLocked)
                    return;

                var matrix = stator.WorldMatrix;

                if(is4x4)
                    matrix.Translation += matrix.Down * (1 - stator.Displacement) + matrix.Forward * 0.75 + matrix.Left * 0.75;
                else
                    matrix.Translation += matrix.Up * stator.Displacement; // displacement is negative

                stator.RotorGrid.SetWorldMatrix(matrix);

                stator.ApplyAction("Force weld"); // re-enable safety lock after we know the top part is aligned properly
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return Entity.GetObjectBuilder(copy);
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorRotor), ElectronicsPanelMod.PANEL_TOP, ElectronicsPanelMod.PANEL_TOP_4X4)]
    public class PanelHead : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                var block = (IMyCubeBlock)Entity;
                block.CubeGrid.OnBlockAdded += GridBlockAdded;

                if(!ElectronicsPanelMod.electronicPanelGrids.Contains(block.CubeGrid.EntityId))
                    ElectronicsPanelMod.electronicPanelGrids.Add(block.CubeGrid.EntityId);
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
                block.CubeGrid.OnBlockAdded -= GridBlockAdded;

                ElectronicsPanelMod.electronicPanelGrids.Remove(block.CubeGrid.EntityId);
            }
            catch(Exception)
            {
                // ignore exceptions here because they might be when game is closing
            }
        }

        private static void GridBlockAdded(IMySlimBlock block)
        {
            try
            {
                if(block.FatBlock != null)
                {
                    var def = block.FatBlock.BlockDefinition;

                    if(ElectronicsPanelMod.IsBlockAllowed(def.TypeId, def.SubtypeName))
                        return;
                }

                block.CubeGrid.RazeBlock(block.Position);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return Entity.GetObjectBuilder(copy);
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorRotor), ElectronicsPanelMod.PANEL_TOP_DELETE)]
    public class PanelHeadDelete : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                if(!MyAPIGateway.Multiplayer.IsServer)
                    return;

                MyObjectBuilder_CubeGrid gridObj = null;

                var rotor = (IMyMotorRotor)Entity;
                var stator = rotor.Stator;
                var grid = rotor.CubeGrid;
                gridObj = grid.GetObjectBuilder(false) as MyObjectBuilder_CubeGrid;
                grid.Close();

                if(gridObj == null)
                {
                    Log.Error("Unable to get the rotor head grid's object builder!");
                    return;
                }

                gridObj.GridSizeEnum = MyCubeSize.Small;

                if(stator.BlockDefinition.SubtypeId == ElectronicsPanelMod.PANEL_BASE_4X4)
                {
                    gridObj.CubeBlocks[0].SubtypeName = ElectronicsPanelMod.PANEL_TOP_4X4;
                    var matrix = stator.WorldMatrix;
                    var pos = matrix.Translation + matrix.Down + matrix.Forward * 0.75 + matrix.Left * 0.75;

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
                var newRotor = MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(gridObj) as IMyMotorRotor;

                stator.Attach(newRotor);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return Entity.GetObjectBuilder(copy);
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CubePlacer))]
    public class CubeBuilder : MyGameLogicComponent
    {
        private static IMyHudNotification notify = null;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                var builder = MyCubeBuilder.Static;
                var def = builder?.ToolbarBlockDefinition;

                if(def == null || ElectronicsPanelMod.IsBlockAllowed(def.Id.TypeId, def.Id.SubtypeName))
                    return;

                var grid = builder.FindClosestGrid();

                if(grid == null || !ElectronicsPanelMod.electronicPanelGrids.Contains(grid.EntityId))
                    return;

                Notify("Can't build '" + def.DisplayNameText + "' on an Electronics Panel!", MyFontEnum.Red);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

#if STABLE // HACK >>> STABLE condition
        public static void Notify(string text, MyFontEnum font, int aliveTime = 50)
#else
        public static void Notify(string text, string font, int aliveTime = 50)
#endif
        {
            if(notify == null)
            {
                notify = MyAPIGateway.Utilities.CreateNotification(text, aliveTime, font);
            }
            else
            {
                notify.Font = font;
                notify.Text = text;
                notify.AliveTime = aliveTime;
            }

            notify.Show();
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return Entity.GetObjectBuilder(copy);
        }
    }
}