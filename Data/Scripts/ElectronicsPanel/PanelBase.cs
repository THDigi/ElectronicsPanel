using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.ElectronicsPanel
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorStator), false, ElectronicsPanelMod.PANEL_BASE, ElectronicsPanelMod.PANEL_BASE_4X4)]
    public class PanelBase : MyGameLogicComponent
    {
        private IMyMotorStator stator;
        private bool is4x4 = false;
        private int attachCooldown;

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

        public override void UpdateBeforeSimulation()
        {
            try
            {
                if(stator.CubeGrid.Physics == null || stator.PendingAttachment || stator.Top == null || stator.Top.Closed)
                    return;

                MatrixD matrix = stator.WorldMatrix;

                if(is4x4)
                    matrix.Translation += matrix.Down * (1 - stator.Displacement) + matrix.Forward * 0.75 + matrix.Left * 0.75;
                else
                    matrix.Translation += matrix.Down * (1 - stator.Displacement);

                stator.TopGrid.SetWorldMatrix(matrix);

                if(MyAPIGateway.Multiplayer.IsServer)
                {
                    if(Math.Abs(stator.Angle) > 0)
                    {
                        stator.RotorLock = false;
                        stator.TargetVelocityRPM = (stator.Angle > 0 ? 1000 : -1000); // TODO does nothing?!
                    }
                    else
                    {
                        stator.RotorLock = true;
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        /// <summary>
        /// Finds a suitable hinge to attach to
        /// </summary>
        public void FindAndAttach(bool showMessages = false)
        {
            if(stator.IsAttached)
                return;

            int tick = MyAPIGateway.Session.GameplayFrameCounter;
            if(attachCooldown > tick)
                return;
            attachCooldown = tick + 15; // prevent this method from re-executing for this many ticks

            const double attachRadius = 0.5;
            const double radiusSq = attachRadius * attachRadius;

            MatrixD matrix = stator.WorldMatrix;
            BoundingSphereD sphere = new BoundingSphereD(matrix.Translation + matrix.Down * (1 - stator.Displacement), attachRadius);

            List<MyEntity> ents = ElectronicsPanelMod.Instance.Entitites;
            ents.Clear();
            MyGamePruningStructure.GetAllEntitiesInSphere(ref sphere, ents, MyEntityQueryType.Both);

            int messageType = 0;

            foreach(MyEntity ent in ents)
            {
                IMyMotorRotor topPart = ent as IMyMotorRotor;
                if(topPart?.CubeGrid?.Physics == null
                || topPart.Base != null
                || topPart.CubeGrid == stator.CubeGrid
                || Vector3D.DistanceSquared(sphere.Center, topPart.GetPosition()) > radiusSq)
                    continue;

                if(topPart.BlockDefinition.SubtypeName != ElectronicsPanelMod.PANEL_TOP_4X4 && topPart.BlockDefinition.SubtypeName != ElectronicsPanelMod.PANEL_TOP)
                {
                    messageType = 1;
                    continue;
                }

                messageType = -1;

                if(MyAPIGateway.Multiplayer.IsServer)
                {
                    stator.Attach(topPart);
                }
                else
                {
                    byte[] bytes = BitConverter.GetBytes(stator.EntityId);
                    MyAPIGateway.Multiplayer.SendMessageToServer(ElectronicsPanelMod.ChannelId, bytes);
                }
                break;
            }

            ents.Clear();

            if(showMessages)
            {
                switch(messageType)
                {
                    case 0: MyAPIGateway.Utilities.ShowNotification("No nearby electronics PCB top to attach to.", 3000, MyFontEnum.White); break;
                    case 1: MyAPIGateway.Utilities.ShowNotification("Can only attach to electronics panel top parts (PCBs)!", 3000, MyFontEnum.Red); break;
                }
            }
        }
    }
}