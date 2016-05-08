using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Timers;
using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Engine;
using Sandbox.Engine.Physics;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Common.Utils;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;
using VRage;
using VRage.ObjectBuilders;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;
using Digi.Utils;

namespace Digi.ElectronicsPanel
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class Panels : MySessionComponentBase
    {
        public static bool init { get; private set; }
        
        private static HashSet<string> allowedBlockTypes = new HashSet<string>()
        {
            "MyObjectBuilder_MyProgrammableBlock",
            "MyObjectBuilder_TimerBlock",
            "MyObjectBuilder_InteriorLight",
            "MyObjectBuilder_ReflectorLight",
            "MyObjectBuilder_BatteryBlock",
            "MyObjectBuilder_ButtonPanel",
            "MyObjectBuilder_SoundBlock",
            "MyObjectBuilder_TextPanel",
            "MyObjectBuilder_SensorBlock",
            "MyObjectBuilder_RadioAntenna",
            "MyObjectBuilder_LaserAntenna",
            "MyObjectBuilder_Beacon",
            "MyObjectBuilder_CameraBlock",
            "MyObjectBuilder_Projector",
        };
        
        public static bool IsBlockAllowed(string typeId, string subTypeId)
        {
            if(typeId == "MyObjectBuilder_TerminalBlock" && subTypeId == "SmallControlPanel")
                return true;
            
            return allowedBlockTypes.Contains(typeId);
        }
        
        public void Init()
        {
            Log.Info("Initialized.");
            init = true;
        }
        
        protected override void UnloadData()
        {
            Log.Info("Mod unloaded.");
            Log.Close();
            init = false;
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
    }
    
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorStator), "ElectronicsPanel", "ElectronicsPanel4x4")]
    public class ElectronicsPanel : MyGameLogicComponent
    {
        private static HashSet<string> panelTops = new HashSet<string>()
        {
            "ElectronicsPanelHead",
            "ElectronicsPanelHead4x4"
        };
        
        private MyObjectBuilder_EntityBase objectBuilder;
        private IMyEntity topEnt;
        //private string status;
        private bool is4x4 = false;
        private int skip = 999;
        
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            this.objectBuilder = objectBuilder;
            
            Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_FRAME;
        }
        
        public override void UpdateOnceBeforeFrame()
        {
            var block = Entity as IMyTerminalBlock;
            //block.AppendingCustomInfo += CustomInfo; // issues with this...
            is4x4 = (block.BlockDefinition.SubtypeId == "ElectronicsPanel4x4");
        }
        
        /*
        public void CustomInfo(IMyTerminalBlock block, StringBuilder info)
        {
            info.Clear(); // workaround to some issue :(
            info.AppendLine();
            info.Append(status);
        }
         */
        
        private void SetStatus(string status)
        {
            /*
            if(this.status != status)
            {
                this.status = status;
                (Entity as IMyTerminalBlock).RefreshCustomInfo();
            }
             */
        }
        
        public override void UpdateAfterSimulation()
        {
            try
            {
                if(topEnt == null)
                {
                    if(++skip >= 10)
                    {
                        skip = 0;
                        
                        var obj = (Entity as IMyCubeBlock).GetObjectBuilderCubeBlock(false) as MyObjectBuilder_MotorBase;
                        
                        if(obj.RotorEntityId.HasValue && obj.RotorEntityId.Value != 0)
                        {
                            IMyEntity headEnt;
                            
                            if(!MyAPIGateway.Entities.TryGetEntityById(obj.RotorEntityId.Value, out headEnt) || headEnt.Closed || headEnt.MarkedForClose)
                            {
                                SetStatus("Invalid attached panel entity!");
                                return;
                            }
                            
                            var headBlock = headEnt as IMyCubeBlock;
                            
                            if(!panelTops.Contains(headBlock.BlockDefinition.SubtypeId))
                            {
                                SetStatus("Unknown attached panel!");
                                return;
                            }
                            
                            topEnt = headEnt;
                            SetStatus("Panel is attached.");
                        }
                        else
                        {
                            (Entity as IMyMotorStator).ApplyAction("Attach");
                            SetStatus("Not attached, trying to attach...\n\nPlease align the smallship panel in the center of the largeship block's space.");
                        }
                    }
                    
                    return;
                }
                
                if(topEnt.Closed || topEnt.MarkedForClose)
                {
                    topEnt = null;
                    return;
                }
                
                var baseBlock = Entity as IMyMotorStator;
                
                if(!baseBlock.IsAttached)
                {
                    topEnt = null;
                    return;
                }
                
                var topBlock = topEnt as IMyCubeBlock;
                var topGrid = topBlock.CubeGrid as IMyCubeGrid;
                var matrix = baseBlock.WorldMatrix;
                
                if(is4x4)
                    matrix.Translation += matrix.Down * (1 - baseBlock.Displacement) + matrix.Forward * 0.75 + matrix.Left * 0.75;
                else
                    matrix.Translation += matrix.Up * baseBlock.Displacement; // displacement is negative
                
                topGrid.SetWorldMatrix(matrix);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
        
        public override void Close()
        {
            objectBuilder = null;
            
            var block = Entity as IMyTerminalBlock;
            //block.AppendingCustomInfo -= CustomInfo;
        }
        
        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return copy ? (MyObjectBuilder_EntityBase)objectBuilder.Clone() : objectBuilder;
        }
    }
    
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorRotor), "ElectronicsPanelHead", "ElectronicsPanelHead4x4")]
    public class PanelHead : MyGameLogicComponent
    {
        private MyObjectBuilder_EntityBase objectBuilder;
        
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            this.objectBuilder = objectBuilder;
            
            Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_FRAME;
        }
        
        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                var block = Entity as IMyCubeBlock;
                var grid = block.CubeGrid as IMyCubeGrid;
                var logic = grid.GameLogic.GetAs<ElectronicsPanelGrid>();
                
                if(logic == null)
                {
                    //Log.Error("Unable to initialize electronics panel grid!");
                    return;
                }
                
                logic.AddedElectronicsPanel();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
        
        public override void Close()
        {
            objectBuilder = null;
            
            try
            {
                var block = Entity as IMyCubeBlock;
                
                if(block == null)
                    return;
                
                var grid = block.CubeGrid as IMyCubeGrid;
                
                if(grid == null)
                    return;
                
                var logic = grid.GameLogic.GetAs<ElectronicsPanelGrid>();
                
                if(logic == null)
                    return;
                
                logic.RemovedElectronicsPanel();
            }
            catch(Exception)
            {
                // ignore exceptions here because they might be when game is closing
            }
        }
        
        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return copy ? (MyObjectBuilder_EntityBase)objectBuilder.Clone() : objectBuilder;
        }
    }
    
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorRotor), "ElectronicsPanelHeadDelete", "ElectronicsPanelHead4x4Delete")]
    public class PanelHeadDelete : MyGameLogicComponent
    {
        private MyObjectBuilder_EntityBase objectBuilder;
        
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            this.objectBuilder = objectBuilder;
            
            Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }
        
        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                if(!MyAPIGateway.Multiplayer.IsServer)
                    return;
                
                MyObjectBuilder_CubeGrid gridObj = null;
                Vector3D center;
                string subTypeId;
                
                {
                    var block = Entity as IMyCubeBlock;
                    subTypeId = block.BlockDefinition.SubtypeId;
                    var grid = block.CubeGrid as IMyCubeGrid;
                    center = block.WorldMatrix.Translation + block.WorldMatrix.Up * 1.2;
                    gridObj = grid.GetObjectBuilder(false) as MyObjectBuilder_CubeGrid;
                    grid.SyncObject.SendCloseRequest();
                }
                
                if(gridObj == null)
                {
                    Log.Error("Unable to get the rotor head grid's object builder!");
                    return;
                }
                
                gridObj.GridSizeEnum = MyCubeSize.Small;
                
                if(subTypeId == "ElectronicsPanelHead4x4Delete")
                {
                    gridObj.CubeBlocks[0].SubtypeName = "ElectronicsPanelHead4x4";
                }
                else
                {
                    gridObj.CubeBlocks[0].SubtypeName = "ElectronicsPanelHead";
                    gridObj.CubeBlocks[0].Min = new SerializableVector3I(-2, 0, -2);
                }
                
                MyAPIGateway.Entities.RemapObjectBuilder(gridObj);
                var ent = MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(gridObj);
                ent.SetPosition(center);
                
                MyAPIGateway.Multiplayer.SendEntitiesCreated(new List<MyObjectBuilder_EntityBase>(1) { gridObj });
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
        
        public override void Close()
        {
            objectBuilder = null;
        }
        
        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return copy ? (MyObjectBuilder_EntityBase)objectBuilder.Clone() : objectBuilder;
        }
    }
    
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CubeGrid))]
    public class ElectronicsPanelGrid : MyGameLogicComponent
    {
        private MyObjectBuilder_EntityBase objectBuilder;
        public bool isElectronicsPanel = false;
        
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            this.objectBuilder = objectBuilder;
        }
        
        public void AddedElectronicsPanel()
        {
            isElectronicsPanel = true;
            
            var grid = Entity as IMyCubeGrid;
            grid.OnBlockAdded += BlockAdded;
        }
        
        public void RemovedElectronicsPanel()
        {
            isElectronicsPanel = false;
            
            var grid = Entity as IMyCubeGrid;
            grid.OnBlockAdded -= BlockAdded;
        }
        
        public void BlockAdded(IMySlimBlock block)
        {
            try
            {
                if(!isElectronicsPanel)
                    return;
                
                if(block.FatBlock != null)
                {
                    var def = block.FatBlock.BlockDefinition;
                    
                    if(Panels.IsBlockAllowed(def.TypeIdString, def.SubtypeId))
                        return;
                }
                
                var grid = block.CubeGrid as IMyCubeGrid;
                grid.RazeBlock(block.Position);
                
                CubeBuilder.lastBlockRemoved = true;
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
        
        public override void Close()
        {
            objectBuilder = null;
        }
        
        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return copy ? (MyObjectBuilder_EntityBase)objectBuilder.Clone() : objectBuilder;
        }
    }
    
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CubePlacer))]
    public class CubeBuilder : MyGameLogicComponent
    {
        private MyObjectBuilder_EntityBase objectBuilder;
        private static IMyHudNotification notify = null;
        public static bool lastBlockRemoved = false;
        
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            this.objectBuilder = objectBuilder;
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }
        
        public override void UpdateBeforeSimulation()
        {
            try
            {
                var builder = MyAPIGateway.CubeBuilder as Sandbox.Game.Entities.MyCubeBuilder;
                
                if(builder == null)
                    return;
                
                var block = builder.HudBlockDefinition;
                
                if(block == null)
                    return;
                
                if(Panels.IsBlockAllowed(block.Id.TypeId.ToString(), block.Id.SubtypeName))
                    return;
                
                var grid = builder.FindClosestGrid();
                
                if(grid == null)
                    return;
                
                var logic = grid.GameLogic.GetAs<ElectronicsPanelGrid>();
                
                if(logic == null || !logic.isElectronicsPanel)
                    return;
                
                if(lastBlockRemoved)
                {
                    lastBlockRemoved = false;
                    builder.DeactivateBlockCreation();
                }
                
                Notify("Can't build '" + block.DisplayNameText + "' on an Electronics Panel!", MyFontEnum.Red);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
        
        public static void Notify(string text, MyFontEnum font, int aliveTime = 50)
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
        
        public override void Close()
        {
            objectBuilder = null;
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return copy ? Entity.GetObjectBuilder() : objectBuilder;
        }
    }
}