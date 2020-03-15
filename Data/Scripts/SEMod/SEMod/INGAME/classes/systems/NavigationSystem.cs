using Sandbox.ModAPI.Interfaces;
using System;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace SEMod.INGAME.classes.systems
{
    //////
    public class NavigationSystem : BasicNavigationSystem
    {
        public void AlignAcrossGravity()
        {
            var align = RemoteControl.GetPosition().Cross(RemoteControl.GetNaturalGravity() * 100);

            //double altitude = _remoteControl.GetValue<float>("Altitude");
            //LOG.Debug("Plantary Alignment Vector: "+ altitude);
            AlignTo(align);
        }

        public Vector3D GetSpeedVector()
        {
            return RemoteControl.GetShipVelocities().LinearVelocity;
        }

        public void EnableDockedMode()
        {
            foreach (var thruster in components.Thrusters)
            {
                thruster.SetValueFloat("Override", 0);
                thruster.GetActionWithName("OnOff_Off").Apply(thruster);
            }
            foreach (var gyro in components.Gyros)
            {
                gyro.GetActionWithName("OnOff_Off").Apply(gyro);
            }
        }

        public void EnableFlightMode()
        {
            foreach (var thruster in components.Thrusters)
            {
                thruster.GetActionWithName("OnOff_On").Apply(thruster);
            }
            foreach (var gyro in components.Gyros)
            {
                gyro.GetActionWithName("OnOff_On").Apply(gyro);
            }
        }

        internal void AlignUpWithGravity()
        {
            Vector3D up = -RemoteControl.GetNaturalGravity();
            AlignUp(_grid.GetPosition() + (up * 100));
        }

        public NavigationSystem(Logger LOG, IMyCubeGrid entity, ShipComponents components) : base(LOG, entity, components)
        {

        }

        public double AlignToWobble(Vector3D position)
        {
            TurnOffGyros(true);
            PointToVector(position, 0.00, true);
            var angoff = (_degreesToVectorPitch + _degreesToVectorYaw);
            //LOG.Debug(angoff + " AlignUp Angle"); 
            return Math.Abs(angoff);
        }

        internal bool HoverApproach(Vector3D vector3D, double speed, int hoverHeight, Vector3D altitudeVector)
        {
            RemoteControl.DampenersOverride = true;
            var successful = false;

            var upAng = AngleBetween(RemoteControl.WorldMatrix.Forward, _grid.GetPosition() - vector3D, true);
            var downAng = AngleBetween(RemoteControl.WorldMatrix.Backward, _grid.GetPosition() - vector3D, true);

            var height = altitudeVector.Length();
            var dirToTarget = RemoteControl.GetPosition() - vector3D;
            var dist = dirToTarget.Length();
            dirToTarget.Normalize();
            altitudeVector.Normalize();

            if (GetSpeed() > speed)
            {
                SlowDown();
            }
            else
            {
                //MaintainAltitude(height, hoverHeight, speed);
                successful = ThrustInDirection(dirToTarget, false, false);
            }
            return successful;
        }

        internal bool Approach(Vector3D vector3D, double speed)
        {
            RemoteControl.DampenersOverride = true;
            var successful = false;

            var upAng = AngleBetween(RemoteControl.WorldMatrix.Forward, _grid.GetPosition() - vector3D, true);
            var downAng = AngleBetween(RemoteControl.WorldMatrix.Backward, _grid.GetPosition() - vector3D, true);

            var dirToTarget = RemoteControl.GetPosition() - vector3D;
            var dist = dirToTarget.Length();
            dirToTarget.Normalize();

            if (GetSpeed() > speed)
            {
                SlowDown();
            }
            else
            {
                successful = ThrustInDirection(dirToTarget);
            }
            return successful;
        }

        internal bool DockApproach(Vector3D droneConnector, Vector3D vector3D)
        {
            RemoteControl.DampenersOverride = true;
            var successful = false;

            var dirToTarget = droneConnector - vector3D;
            var dist = dirToTarget.Length();

            if (GetSpeed() > (dist > 300 ? 10 : dist > 50 ? 5 : dist < 1 ? dist : 1))
            {
                SlowDown();
            }
            else
            {
                successful = ThrustInDirection(dirToTarget);
            }
            return successful;
        }
    }

    //////
}
