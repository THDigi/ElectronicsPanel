using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.ElectronicsPanel
{
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

                if(gridObj.CubeBlocks.Count > 1) // most likely someone placed this block on a largegrid...
                {
                    grid.RemoveBlock(rotor.SlimBlock);
                    return;
                }

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
}