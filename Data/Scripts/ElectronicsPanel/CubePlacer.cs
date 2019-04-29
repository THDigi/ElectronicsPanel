using System;
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
}