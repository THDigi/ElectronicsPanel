using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace Digi.ElectronicsPanel
{
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
}