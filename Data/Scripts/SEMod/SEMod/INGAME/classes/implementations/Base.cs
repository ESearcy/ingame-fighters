using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRageMath;
using SEMod.INGAME.classes.systems;
using SEMod.INGAME.classes.model;

namespace SEMod.INGAME.classes.implementations
{
    class Base : AIShipBase
    {
        IMyProgrammableBlock Me = null;
        IMyGridTerminalSystem GridTerminalSystem = null;
        IMyGridProgramRuntimeInfo Runtime;
        

        public Base()
        //////public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            shipComponents = new ShipComponents();
            LocateAllParts();
            SetupFleetListener();

            log = new Logger(Me.CubeGrid, shipComponents);

            communicationSystems = new CommunicationSystem(log, Me.CubeGrid, shipComponents);
            navigationSystems = new NavigationSystem(log, Me.CubeGrid, shipComponents);
            //productionSystems = new ProductionSystem(log, Me.CubeGrid, shipComponents);
            //storageSystem = new StorageSystem(log, Me.CubeGrid, shipComponents);
            trackingSystems = new TrackingSystem(log, Me.CubeGrid, shipComponents, true);
            //weaponSystems = new WeaponSystem(log, Me.CubeGrid, shipComponents);

            
            operatingOrder.AddLast(new TaskInfo(LocateAllParts));
            operatingOrder.AddLast(new TaskInfo(InternalSystemCheck));
            operatingOrder.AddLast(new TaskInfo(ScanLocalArea));
            operatingOrder.AddLast(new TaskInfo(MaintainAltitude));
            operatingOrder.AddLast(new TaskInfo(UpdateTrackedTargets));
            operatingOrder.AddLast(new TaskInfo(SendPendingMessages));
            operatingOrder.AddLast(new TaskInfo(ProcessFleetUpdateMessages));
            operatingOrder.AddLast(new TaskInfo(UpdateDisplays));
            
            maxCameraRange = 2000;
            maxCameraAngle = 80;
            //set new defaults
            hoverHeight = 400;
            InitialBlockCount = shipComponents.AllBlocks.Count();
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }
        
        NavigationSystem navigationSystems;
        public void ProcessFleetUpdateMessages()
        {
            var messages = RecieveMessages(x => x.Tag.Contains(fleet_status_update));
            FleetMessage fm;
            foreach (var message in messages)
            {
                log.Debug("Recieved Update message: "+message);
                fm = new FleetMessage(message, log);
                var d_info = UpdateDrone(fm);

                if (fm.GetLong("cmd_id")==0 || fm.GetBool("is_registration"))
                {
                    var assignmentMessage = FleetMessage.CreateDroneAssignmentMessage(Me.CubeGrid.EntityId);
                    communicationSystems.SendMessage(d_info.EntityId+"", assignmentMessage);
                }
            }
        }


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
                    //IntrepretMessage(argument);
                }
            }
            catch (Exception e)
            {
                log.Error(e.Message);
            }
        }

        protected void UpdateDisplays()
        {
            try
            {
                //UpdateInfoKey("Storage", " Mass: " + navigationSystems.RemoteControl.CalculateShipMass().PhysicalMass + " Max Mass: " + navigationSystems.GetMaxSupportedWeight());
                UpdateInfoKey("Power: ", "Current: " + CurPower + " Max: " + MaxPower);

                if (NearestPlanet != null)
                {
                    log.DisplayShipInfo(shipInfoKeys, "PlanetInfo:  altitude: " + (int)trackingSystems.GetAltitude() + "m");
                    log.UpdateRegionInfo(NearestPlanet.Regions, Me.CubeGrid);
                }
                else
                    log.DisplayShipInfo(shipInfoKeys, " No Planet ");

                //log.UpdateProductionInfo(factorySystems, Me.CubeGrid);
            }
            catch (Exception e) { log.Error("UpdateDisplays " + e.Message); }
            log.UpdateFleetInformationScreens(GetDroneContexts(), Me.CubeGrid);
            UpdateSystemScreens();

            UpdateAntenna();
        }

        protected void UpdateAntenna()
        {
            foreach (var antenna in shipComponents.RadioAntennas)
            {
                antenna.CustomName = "\nA: " + (int)trackingSystems.GetAltitude();
            }
        }

        //Fleet Related commands and stuff
        public Dictionary<long, DroneContext> DroneDetails = new Dictionary<long, DroneContext>();

        public List<DroneContext> GetDroneContexts()
        {
            return DroneDetails.Select(x => x.Value).ToList();
        }

        //    message.Set("is_registration", isRegistration);

        public DroneInfo UpdateDrone(FleetMessage message)
        {
            var entid = message.GetLong("s_id");
            var loc = message.GetVector("loc");
            var vel = message.GetVector("vel");

            var dronedeets = DroneDetails.ContainsKey(entid) ? DroneDetails[entid] : null;
            if (dronedeets == null)
            {
                log.Debug("adding new drone to fleet " + DroneDetails.Count);
                var name = message.Get("name");
                dronedeets = new DroneContext(new DroneInfo(entid, name, loc, vel), null);
                DroneDetails.Add(entid, dronedeets);
            }
            else
            {
                //log.Debug("updating existing drone");
                var info = dronedeets.Info;
                //ship info
                info.StorageMax = message.GetFloat("m_inv");
                info.MaxPower = message.GetDouble("m_pow");
                info.CurrentPower = message.GetFloat("c_pow");
                info.StorageCurrent = message.GetDouble("c_inv");

                info.ShipSize = message.GetFloat("size");
                info.Health = message.GetDouble("hp");
                info.LastKnownVector = message.GetVector("vel");
                info.lastKnownPosition = message.GetVector("loc");
                info.NumConnectors = message.GetInt("connect");
                info.numSensors = message.GetInt("sensors");
                info.NumDrills = message.GetInt("drills");
                info.CameraCount = message.GetInt("cameras");
                info.Rockets = message.GetInt("rockets");
                info.Reactors = message.GetInt("reactors");
                info.Batteries = message.GetInt("batteries");
                info.Merge = message.GetInt("merge");
                info.Docked = message.GetBool("docked");
                info.Name = message.Get("name");
                //commandship & registration imfo
                info.CommanderId = message.GetLong("cmd_id");
            }
            return dronedeets.Info;
        }

        protected void MaintainAltitude()
        {
            try
            {
                if (NearestPlanet != null)
                {
                    if (navigationSystems.GetSpeed() > 10)
                        navigationSystems.SlowDown();
                    else if (DroneDetails.Any(x => x.Value.Order != null && ((DateTime.Now - x.Value.Order.IssuedAt).TotalSeconds < 20 || !x.Value.Info.Docked) && (x.Value.Info.lastKnownPosition - Me.GetPosition()).Length() < 60))
                        navigationSystems.SlowDown();
                    else if (Math.Abs(hoverHeight - trackingSystems.GetAltitude()) > 5)
                        navigationSystems.MaintainAltitude(trackingSystems.GetAltitude(), hoverHeight, Math.Abs(trackingSystems.GetAltitude() - hoverHeight) / 2);
                    else
                        navigationSystems.SlowDown();

                    //navigationSystems.AlignAgainstGravity();
                    log.Debug("Aligning to planet at: " + NearestPlanet.PlanetCenter);
                    navigationSystems.AlignTo(NearestPlanet.PlanetCenter);
                }
                else
                    log.Debug("No Planet");
            }
            catch (Exception e) { log.Error("MaintainAltitude " + e.StackTrace); }
        }
        //////
    }
}
