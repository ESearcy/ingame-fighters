using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRageMath;
using SEMod.INGAME.classes.systems;
using SEMod.INGAME.classes.model;

namespace SEMod.INGAME.classes.implementations
{
    class Drone : AIShipBase
    {
        IMyProgrammableBlock Me = null;
        IMyGridTerminalSystem GridTerminalSystem = null;
        IMyGridProgramRuntimeInfo Runtime;
        

        public Drone()
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
            operatingOrder.AddLast(new TaskInfo(UpdateTrackedTargets));
            operatingOrder.AddLast(new TaskInfo(SendPendingMessages));
            operatingOrder.AddLast(new TaskInfo(ProcessGridMessages));
            operatingOrder.AddLast(new TaskInfo(ProcessCurrentOrder));
            operatingOrder.AddLast(new TaskInfo(UpdateDisplays));

            maxCameraRange = 2000;
            maxCameraAngle = 80;
            //set new defaults
            hoverHeight = 400;
            InitialBlockCount = shipComponents.AllBlocks.Count();
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }
        protected NavigationSystem navigationSystems;


        public void ProcessGridMessages()
        {
            var messages = PullGridMessages();
            FleetMessage fm;
            foreach (var message in messages) {
                fm = new FleetMessage(message, log);
                log.Debug("recieved Grid Message: "+message);
                switch (fm.Get("type"))
                {
                    case "assignment":
                        CommandShipEntity  = fm.GetLong("cmd_id");
                        registered = true;
                        break;
                    case "order-standby":
                        break;
                    case "order-dock":
                        break;
                    case "order-scan":
                        break;
                }
            }
        }

        int combatAltitude = 800;
        bool Undocking = true;
        Vector3D undockPosition;
        DroneOrder CurrentOrder;
        DroneOrder NextOrder;
        bool MiningEngaged = false;
        bool Disengaging = false;
        private void ProcessCurrentOrder()
        {
            maxCameraRange = 3000;
            maxCameraAngle = 100;

            //weaponSystems.Disengage();

            var time = (DateTime.Now - lastUpdateSent).TotalSeconds;
            if (time > 5)
            {
                lastUpdateSent = DateTime.Now;
                if (!registered)
                {
                    log.Debug("sending update");
                    SendUpdate(true);
                }
                else
                {
                    log.Debug("waiting for order");
                    SendUpdate();
                }
            }

            if (NextOrder != null)
            {
                if (CurrentOrder != null && CurrentOrder.Ordertype == OrderType.Mine)
                {
                    Disengaging = true;
                    if (CurrentOrder.MiningIndex == 0)
                    {
                        CurrentOrder = NextOrder;
                        NextOrder = null;
                        Disengaging = false;
                    }
                }
                else
                {
                    CurrentOrder = NextOrder;
                    NextOrder = null;
                }
            }

            //log.Debug("processing");
            if (CurrentOrder != null)
            {

                //log.Debug("processing order");
                if (CurrentOrder.Ordertype == OrderType.Scan)
                {
                    if (Docked || Undocking)
                    {
                        //Undock();
                    }
                    else
                    {
                       // ScanLocation();
                    }
                }
                else if (CurrentOrder.Ordertype == OrderType.Dock)
                {
                    //DockToConnector();
                    //Hover();
                    //log.Debug("Position: " + CurrentOrder.PrimaryLocation + "\nforward: " + CurrentOrder.DirectionalVectorOne + "\nup: " + CurrentOrder.ThirdLocation);
                    //navigationSystems.MaintainAltitude(trackingSystems.GetAltitude(), hoverHeight);
                }
                else if (CurrentOrder.Ordertype == OrderType.Standby)
                {
                    if (Docked)
                    {
                        navigationSystems.EnableDockedMode();
                        return;
                    }
                    else
                    {
                        Hover();
                    }
                }
                else if (CurrentOrder.Ordertype == OrderType.Mine)
                {
                    if (Docked || Undocking)
                    {
                        //Undock();

                    }
                    //else if (Disengaging)
                        //ExitMiningPosition();
                    //else
                       // MinePosition();
                }
            }
            else if (Docked)
                navigationSystems.EnableDockedMode();
            else
            {
                //if no command ship

                //navigationSystems.AlignAcrossGravity();
                //navigationSystems.Roll(0);
                //navigationSystems.StopRoll();

                Hover();
            }

        }
        private void Hover()
        {
            navigationSystems.AlignAgainstGravity();
            navigationSystems.StopRoll();
            navigationSystems.StopSpin();
            if (navigationSystems.GetSpeed() > 20)
                navigationSystems.SlowDown();
            else
                navigationSystems.MaintainAltitude(trackingSystems.GetAltitude(), hoverHeight, Math.Abs(trackingSystems.GetAltitude() - hoverHeight) / 2);
        }

        bool registered = false;
        DateTime lastUpdateSent = DateTime.Now.AddMinutes(-5);


        bool Docked = false;
        int CurrentInvVolume = 0;
        int MaxInvVolume = 0;
        long CommandShipEntity = 0;
        public void SendUpdate(bool isRegistration = false)
        {
            Docked = shipComponents.Connectors.Any(x => x.Status == MyShipConnectorStatus.Connected);

            CurrentInvVolume = 0;
            MaxInvVolume = 0;
            foreach (var block in shipComponents.AllMyBlocks)
            {
                for (int i = 0; i < block.InventoryCount; i++)
                {
                    var inv = block.GetInventory(i);
                    CurrentInvVolume += (int)inv.CurrentVolume;
                    MaxInvVolume += (int)inv.MaxVolume;
                }
                //CurrentMass += (int)block.Mass;
                //CurrentMass += block.HasInventory ? (int)block.GetInventory().CurrentMass : 0;
            }

            String updateMessage = FleetMessage.CreateDroneUpdateMessage(
                //basic details
                Me.CubeGrid.CustomName,
                Me.CubeGrid.EntityId,
                CommandShipEntity,
                Docked,
                communicationSystems.GetMsgSntCount(),
                GetHealth(), //health
                             // speed/vector
                Vector3D.Zero,//navigationSystems.GetSpeedVector(),
                //grid info
                Me.CubeGrid.GetPosition(),
                Me.CubeGrid.GridSize,
                (int)CurrentInvVolume,
                (int)MaxInvVolume,
                //docking
                shipComponents.MergeBlocks.Count(),
                shipComponents.Connectors.Count(),
                //tools
                shipComponents.MiningDrills.Count(),
                //sensors
                shipComponents.Sensors.Count(),
                shipComponents.Cameras.Count(),
                //weapons
                shipComponents.GatlingGuns.Count(),
                shipComponents.RocketLaunchers.Count(),
                //reactor info
                shipComponents.Reactors.Count(),
                //battery info
                shipComponents.Batteries.Count(),
                CurPower,
                MaxPower,
                isRegistration);
            //log.Debug("Update Message: "+ updateMessage);
            communicationSystems.SendMessage(fleet_status_update, updateMessage);
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
                    log.DisplayShipInfo(shipInfoKeys, "PlanetInfo:  altitude: " + (int)trackingSystems.GetAltitude() + "m\n");
                    log.UpdateRegionInfo(NearestPlanet.Regions, Me.CubeGrid);
                }
                else
                    log.DisplayShipInfo(shipInfoKeys, " No Planet \n");

                //log.UpdateProductionInfo(factorySystems, Me.CubeGrid);
            }
            catch (Exception e) { log.Error("UpdateDisplays " + e.Message); }

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
        //////
    }
}
