using Sandbox.ModAPI.Interfaces;
using System;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace SEMod.INGAME.classes.systems
{
    //////
    public class NavigationSystem : BasicNavigationSystem
    {
        bool docking = false;
        bool station = false;

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
            return Math.Abs(angoff);
        }

        Vector3D ProjectVector(Vector3D vectorToProject, Vector3D vectorProjectedOn)
        {
            return vectorToProject.Dot(vectorProjectedOn) / vectorProjectedOn.LengthSquared() * vectorProjectedOn;
        }

        // usage
        //void Main()
        //{
        //    var gravity = RemoteControl.GetNaturalGravity();
        //    var velocity = RemoteControl.GetShipVelocities().LinearVelocity;

        //    var downwardVelocity = ProjectVector(velocity, gravity);
        //}

        double lastUpAngle = 0;
        int thrusterMaxPower = 12;

        internal bool EngageThrusters(Vector3D travelVector, double travelSpeed, Vector3D avoidVector, int avoidSpeed, Vector3D currentSpeedVector)
        {
            var maxSpeed = avoidSpeed>travelSpeed? travelSpeed: avoidSpeed;
            var successful = false;
            if (GetSpeed() > maxSpeed)
            {
                SlowDown();
            }
            else
            {
                var thrusted = false;

                //antidrift handled by Dampeners
                if (!RemoteControl.DampenersOverride)
                    RemoteControl.DampenersOverride = true;

                var udAndle = Math.Abs(AngleBetween(-gravity, thrusterVector, true));

                foreach (var thruster in components.Thrusters)
                {
                    //get current thrust (Dampeners)
                    var currentThrust = thruster.CurrentThrust;
                    var maxPossibleThrust = thruster.MaxThrust;

                    thruster.GetActionWithName("OnOff_On").Apply(thruster);
                    var thrusterVector = thruster.WorldMatrix.Forward;
                    var joinedVector = travelVector + avoidVector;
                    joinedVector.Normalize();

                    double joinedAngle = Math.Abs(AngleBetween(thrusterVector, joinedVector, true));
                    double travelAngle = Math.Abs(AngleBetween(thrusterVector, travelVector, true));
                    double avoidAngle = Math.Abs(AngleBetween(thrusterVector, avoidVector, true));

                    //60 negative and pos, technically 120* cone
                    var gravity = RemoteControl.GetNaturalGravity();

                    var angleDown = Math.Abs(AngleBetween(-gravity, thrusterVector, true));
                    var angleUp = Math.Abs(AngleBetween(gravity, thrusterVector, true));
                    //going up needs 200% anti gravity thrust
                    //going straight down requires 0 anti gravity thrust
                    //going horizontal needs 100% anti gravity thrust to nullify gravity
                    var applicableUpDownPercent = (angleUp < 90 ? ((90 - angleUp) / 90): angleDown < 90 ? ((90 - angleDown) / 90) * -1 : 0) + 1;
                    maxPossibleThrust = maxPossibleThrust * (float)applicableUpDownPercent;
                    //if (Upward)


                    if (angle <= 85)
                    { 
                        if (blockUpDownGravityMovement && (!Downward && !Upward))
                        {
                            thruster.SetValueFloat("Override", (float)(desiredThrust));
                        }
                        else if (!Downward)
                        {
                            thruster.SetValueFloat("Override", (float)(desiredThrust));
                        }
                        else if (Downward && !blockUpDownGravityMovement)
                        {
                            thruster.SetValueFloat("Override", desiredThrust / 4);
                        }

                        successful = true;
                    }
                    else if (disableIsAlreadyRunning)
                        thruster.SetValueFloat("Override", 0);

                }
                return successful;

            }
            return successful;
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

        //internal bool Approach(Vector3D vector3D, double speed)
        //{
        //    RemoteControl.DampenersOverride = true;
        //    var successful = false;

        //    var upAng = AngleBetween(RemoteControl.WorldMatrix.Forward, _grid.GetPosition() - vector3D, true);
        //    var downAng = AngleBetween(RemoteControl.WorldMatrix.Backward, _grid.GetPosition() - vector3D, true);

        //    var dirToTarget = RemoteControl.GetPosition() - vector3D;
        //    var dist = dirToTarget.Length();
        //    dirToTarget.Normalize();

        //    if (GetSpeed() > speed)
        //    {
        //        SlowDown();
        //    }
        //    else
        //    {
        //        successful = ThrustInDirection(dirToTarget);
        //          //MaintainAltitude
        //          successful = ThrustInDirection(dirToTarget, false, false);
        //    }
        //    return successful;
        //}

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
