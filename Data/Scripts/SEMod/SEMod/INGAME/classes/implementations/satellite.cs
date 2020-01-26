using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using SEMod.INGAME.classes.systems;
using SEMod.INGAME.classes.model;

namespace SEMod.INGAME.classes.implementations
{
    class Satellite : AIShipBase
    {
        public Satellite()
        //////public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            shipComponents = new ShipComponents();
            LocateAllParts();
            log = new Logger(Me.CubeGrid, shipComponents);

            communicationSystems = new CommunicationSystem(log, Me.CubeGrid, shipComponents);
            navigationSystems = new NavigationSystem(log, Me.CubeGrid, shipComponents);
            productionSystems = new ProductionSystem(log, Me.CubeGrid, shipComponents);
            storageSystem = new StorageSystem(log, Me.CubeGrid, shipComponents);
            trackingSystems = new TrackingSystem(log, Me.CubeGrid, shipComponents);
            weaponSystems = new WeaponSystem(log, Me.CubeGrid, shipComponents);

            operatingOrder.AddLast(new TaskInfo(LocateAllParts));
            operatingOrder.AddLast(new TaskInfo(InternalSystemScan));
            operatingOrder.AddLast(new TaskInfo(SensorScan));
            operatingOrder.AddLast(new TaskInfo(MaintainAltitude));
            operatingOrder.AddLast(new TaskInfo(UpdateTrackedTargets));
            operatingOrder.AddLast(new TaskInfo(UpdateDisplays));
            maxCameraRange = 30000;
            maxCameraAngle = 5;
            //set new defaults
            hoverHeight = 20000;
            InitialBlockCount = shipComponents.AllBlocks.Count();
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        protected NavigationSystem navigationSystems;
        List<DroneContext> drones = new List<DroneContext>();
        protected void Main(String argument, UpdateType updateType)
        {
            try
            {
                if (argument.Length == 0)
                {
                    Update();
                }

                else
                {
                    IntrepretMessage(argument);
                }
            }
            catch (Exception e)
            {
                log.Error(e.Message);
            }
        }

        protected void UpdateAntenna()
        {
            foreach (var antenna in shipComponents.RadioAntennas)
            {
                antenna.CustomName = "\nA: " + (int)trackingSystems.GetAltitude() + "\n" +
                    "S: " + (int)navigationSystems.GetSpeed();
            }
        }

        private void RegisterDrone(ParsedMessage pm)
        {
            var drone = drones.Where(x => x.Info.EntityId == pm.EntityId).FirstOrDefault();
            if (drone == null)
            {
                drones.Add(new DroneContext(new DroneInfo(pm.EntityId, pm.Name, pm.Location, pm.Velocity), null));

                UpdateDrone(pm);
            }

            communicationSystems.SendMessage(ParsedMessage.CreateConfirmationMessage(Me.CubeGrid.EntityId, pm.EntityId, pm.RequestID));
            log.Debug("registered Drone");
            UpdateDrone(pm);
        }

        private void IdentifyDroneType(DroneContext context)
        {
            bool scan = false, miner = false, combat = false;

            if (context.Info.CameraCount > 0)
                scan = true;
            if ((context.Info.Guns + context.Info.Rockets) > 0)
                combat = true;
            if (context.Info.NumDrills > 0)
                miner = true;

            if (miner)
                context.Info.Type = DroneType.Miner;
            else if (combat)
                context.Info.Type = DroneType.Combat;
            else if (scan)
                context.Info.Type = DroneType.Scan;

        }

        private DroneContext UpdateDrone(ParsedMessage pm)
        {
            //log.Debug("processing update for drone");
            var drone = drones.Where(x => x.Info.EntityId == pm.EntityId).FirstOrDefault();
            if (drone == null)
            {
                drone = new DroneContext(new DroneInfo(pm.EntityId, pm.Name, pm.Location, pm.Velocity), null);
                drones.Add(drone);
            }
            else
            {
                drone.Info.Update(pm.Name, pm.Location, pm.Velocity, pm.Docked, pm.CameraCount, pm.ShipSize, pm.DrillCount, pm.WeaponCount, pm.SensorCount, pm.ConnectorCount, pm.PercentCargo, pm.HP, pm.MaxStorage, pm.MergeCount, pm.GuneCount, pm.RocketCount, pm.ReactorCount, pm.BatteryCount, pm.CurrentPower, pm.MaxPower);
            }
            IdentifyDroneType(drone);
            return drone;
        }

        public void IntrepretMessage(String argument)
        {
            if (argument == null)
                return;

            var pm = communicationSystems.ParseMessage(argument);

            if (ParsedMessage.MaxNumBounces < pm.NumBounces && pm.MessageType != MessageCode.PingEntity)
            {
                pm.NumBounces++;
                //LOG.Debug("Bounced Message");
                communicationSystems.SendMessage(pm.ToString());
            }


            switch (pm.MessageType)
            {
                case MessageCode.Register:
                    RegisterDrone(pm);
                    break;
                case MessageCode.Update:
                    UpdateDrone(pm);
                    break;
                case MessageCode.PingEntity:
                    if (pm.Type.Trim().ToLower().Contains("planet"))
                        trackingSystems.UpdatePlanetData(pm, false);
                    else
                        trackingSystems.UpdateTrackedEntity(pm, false);

                    break;
            }
        }

        protected void UpdateDisplays()
        {
            try
            {
                Mass = (int)(GetCargoMass() + shipComponents.AllBlocks.Sum(x => x.Mass));
                var controlBlock = shipComponents.ControlUnits.FirstOrDefault();
                if (controlBlock != null)
                {
                    var maxMass = (int)shipComponents.Thrusters.Where(x => x.WorldMatrix.Forward == controlBlock.WorldMatrix.Forward).Sum(x => x.MaxThrust) / (controlBlock.GetNaturalGravity().Length());
                    UpdateInfoKey("Weight Information", " Mass: " + Mass + "kg  MaxMass: " + (int)maxMass + "kg");
                }

                //display operation details
                foreach (var op in operatingOrder)
                    UpdateInfoKey(op.CallMethod.Method.Name + "", ((int)op.GetAverageExecutionTime() + "ms" + " CallCountPerc: " + op.GetAverageCallCount() + "% CallDepthPer: " + op.GetAverageCallCount() + "%"));

                UpdateInfoKey("Storage", " Mass: " + navigationSystems.RemoteControl.CalculateShipMass().PhysicalMass + " Max Mass: " + navigationSystems.MaxSupportedWeight / navigationSystems.RemoteControl.GetNaturalGravity().Length());
                UpdateInfoKey("Power: ", "Current: " + CurPower + " Max: " + MaxPower);

                if (NearestPlanet != null)
                {
                    log.DisplayShipInfo(shipInfoKeys, "PlanetInfo:  altitude: " + (int)trackingSystems.GetAltitude() + "m" + "  Speed: " + (int)navigationSystems.GetSpeed() + "m/s");
                    log.UpdateRegionInfo(NearestPlanet.Regions, Me.CubeGrid);
                }
                else
                    log.DisplayShipInfo(shipInfoKeys, " No Planet ");
            }
            catch (Exception e) { log.Error("UpdateDisplays " + e.Message); }

            log.DisplayLogScreens();

            UpdateAntenna();
        }

        protected void MaintainAltitude()
        {
            try
            {
                if (NearestPlanet != null)
                {
                    if (navigationSystems.GetSpeed() > 10 || (drones.Any(x => x.Order.Ordertype == OrderType.Dock && (x.Info.lastKnownPosition - x.Order.Connector.GetPosition()).Length() < 60)))
                        navigationSystems.SlowDown();
                    else if (Math.Abs(hoverHeight - trackingSystems.GetAltitude()) > 5)
                        navigationSystems.MaintainAltitude(trackingSystems.GetAltitude(), hoverHeight, Math.Abs(trackingSystems.GetAltitude() - hoverHeight) / 2);
                    else
                        navigationSystems.SlowDown();

                    navigationSystems.AlignAgainstGravity();
                }
                else
                    log.Debug("No Planet");
            }
            catch (Exception e) { log.Error("MaintainAltitude " + e.Message); }
        }

        //////
    }
}
