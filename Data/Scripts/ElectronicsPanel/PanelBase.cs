using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
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
                        string id = c.Id;

                        if(controlIds.Contains(id))
                        {
                            if(c.Visible != null)
                                ElectronicsPanelMod.instance.controlVisibleFunc[id] = c.Visible; // preserve the existing visible condition

                            c.Visible = (b) =>
                            {
                                var func = ElectronicsPanelMod.instance.controlVisibleFunc.GetValueOrDefault(id, null);
                                return (func == null ? true : func.Invoke(b)) && !ElectronicsPanelMod.IsElectronicsPanel(b.SlimBlock.BlockDefinition.Id);
                            };
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
}