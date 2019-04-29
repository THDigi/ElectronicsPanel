using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace Digi.ElectronicsPanel
{
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
                ElectronicsPanelMod.SetupTerminalControls();

                is4x4 = stator.BlockDefinition.SubtypeId == ElectronicsPanelMod.PANEL_BASE_4X4;

                if(stator.CubeGrid.Physics != null)
                {
                    NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;

                    stator.LowerLimitDeg = 0;
                    stator.UpperLimitDeg = 0;
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
}