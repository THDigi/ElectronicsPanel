using System;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace Digi.ElectronicsPanel
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CubePlacer), false)]
    public class CubePlacer : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        public override void UpdateBeforeSimulation10()
        {
            try
            {
                MyCubeBuilder builder = MyCubeBuilder.Static;
                MyCubeBlockDefinition def = builder?.CubeBuilderState?.CurrentBlockDefinition;

                if(def != null && def.CubeSize == MyCubeSize.Small && !ElectronicsPanelMod.IsBlockAllowed(def.Id))
                {
                    IHitInfo hit = (IHitInfo)builder.HitInfo;
                    IMyCubeGrid grid = hit?.HitEntity as IMyCubeGrid;

                    if(grid != null && ElectronicsPanelMod.IsElectronicsPanelGrid(grid.EntityId))
                    {
                        ElectronicsPanelMod.Notify(0, "Can't build '" + def.DisplayNameText + "' on an Electronics Panel!", MyFontEnum.Red);
                        ElectronicsPanelMod.Notify(1, ElectronicsPanelMod.ALLOWED_TYPES_LINE1, MyFontEnum.White);
                        ElectronicsPanelMod.Notify(2, ElectronicsPanelMod.ALLOWED_TYPES_LINE2, MyFontEnum.White);

                        if(ElectronicsPanelMod.Instance.AllowedModdedBlocks != null)
                            ElectronicsPanelMod.Notify(3, ElectronicsPanelMod.Instance.AllowedModdedBlocks, MyFontEnum.White);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}