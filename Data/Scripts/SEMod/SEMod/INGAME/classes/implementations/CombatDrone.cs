using System;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRageMath;
using SEMod.INGAME.classes.systems;
using SEMod.INGAME.classes.model;

namespace SEMod.INGAME.classes.implementations
{
    class CombatDrone : AIShipBase
    {

        public CombatDrone()
        //////public Program()
        {
            shipComponents = new ShipComponents();
            LocateAllParts();
            log = new Logger(Me.CubeGrid, shipComponents);

            communicationSystems = new CommunicationSystem(log, Me.CubeGrid, shipComponents);
            navigationSystems = new NavigationSystem(log, Me.CubeGrid, shipComponents);

            trackingSystems = new TrackingSystem(log, Me.CubeGrid, shipComponents, false);
            weaponSystems = new WeaponSystem(log, Me.CubeGrid, shipComponents);

            operatingOrder.AddLast(new TaskInfo(LocateAllParts));
            operatingOrder.AddLast(new TaskInfo(InternalSystemCheck));
            operatingOrder.AddLast(new TaskInfo(NavigationCheck));
            operatingOrder.AddLast(new TaskInfo(RecieveFleetMessages));
            operatingOrder.AddLast(new TaskInfo(SendPendingMessages));
            operatingOrder.AddLast(new TaskInfo(FollowOrders));
            operatingOrder.AddLast(new TaskInfo(ScanLocalArea));
            operatingOrder.AddLast(new TaskInfo(UpdateTrackedTargets));
            operatingOrder.AddLast(new TaskInfo(UpdateDisplays));
            operatingOrder.AddLast(new TaskInfo(FollowOrders));
            SetupFleetListener();

            maxCameraRange = 5000;
            maxCameraAngle = 80;

            //set new defaults
            hoverHeight = 150;
            InitialBlockCount = shipComponents.AllBlocks.Count();
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }
        protected NavigationSystem navigationSystems;
        protected void Main(String argument, UpdateType updateType)
        {
            try
            {
                if (argument.Length == 0)
                {
                    Update();
                }
            }
            catch (Exception e)
            {
                log.Error(e.Message);
            }

        }

        private void NavigationCheck()
        {
            var ns = navigationSystems.IsOperational();
            UpdateInfoKey("NavigationSystems", BoolToOnOff(ns) + "");
        }

        protected void UpdateAntenna()
        {
            foreach (var antenna in shipComponents.RadioAntennas)
            {
                antenna.CustomName = "\nA: " + (int)trackingSystems.GetAltitude() + "\n" +
                    "S: " + (int)navigationSystems.GetSpeed();
            }
        }

        private void RecieveFleetMessages()
        {
            var messages = RecieveMessages();
            //log.Debug("recieved Message: " + messages);
            foreach (var mes in messages)
            {
                var pm = communicationSystems.ParseMessage(mes);

                if (!registered && pm.TargetEntityId == Me.CubeGrid.EntityId && pm.MessageType == MessageCode.Confirmation)
                {
                    registered = true;
                    CommandShipEntity = pm.EntityId;
                    //log.Debug("Registered!!");
                }

                if (ParsedMessage.MaxNumBounces < pm.NumBounces && pm.MessageType != MessageCode.PingEntity)
                {
                    pm.NumBounces++;
                    //log.Debug("Bounced Message");
                    communicationSystems.SendMessage(pm.ToString());
                }

                if (registered)
                {
                    switch (pm.MessageType)
                    {
                        case MessageCode.Order:
                            if (CommandShipEntity == pm.CommanderId && pm.EntityId == Me.CubeGrid.EntityId)
                            {
                                log.Debug(pm.OrderType + " order recieved");
                                if (pm.OrderType == OrderType.Dock && CurrentOrder != null && CurrentOrder.Ordertype == OrderType.Dock)
                                {
                                    try
                                    {
                                        CurrentOrder.PrimaryLocation = pm.Location;
                                        CurrentOrder.UpdateDockingCoords();
                                    }
                                    catch (Exception e) { log.Error(e.StackTrace); }

                                }
                                else
                                {
                                    NextOrder = new DroneOrder(log, pm.OrderType, pm.RequestID, pm.TargetEntityId, pm.EntityId, pm.Location, pm.AlignForward, pm.AlignUp);
                                }
                            }
                            break;
                    }
                }
            }

        }

        //Order related variables
        DateTime LastUpdateTime = DateTime.Now.AddMinutes(-5);
        long CommandShipEntity = 0;
        bool registered = false;
        DroneOrder CurrentOrder;
        DroneOrder NextOrder;
        public void FollowOrders()
        {
            try
            {
                //send update or register with any command ship
                if ((DateTime.Now - LastUpdateTime).TotalSeconds >= 1)
                {
                    if (!registered)
                    {
                        SendUpdate(true);
                    }
                    else
                    {
                        SendUpdate();
                    }
                    LastUpdateTime = DateTime.Now;
                }

                ProcessCurrentOrder();
            }
            catch (Exception e) { log.Error("FollowOrders " + e.Message + " " + e.StackTrace); }
        }

        int CurrentInvVolume = 0;
        int MaxInvVolume = 0;
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

            String updateMessage = ParsedMessage.CreateUpdateMessage(
                //basic details
                Me.CubeGrid.EntityId,
                CommandShipEntity,
                Docked,
                communicationSystems.GetMsgSntCount(),
                GetHealth(), //health
                             // speed/vector
                navigationSystems.GetSpeedVector(),
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
            communicationSystems.SendMessage(updateMessage);
        }

        int combatAltitude = 800;
        bool Undocking = true;
        Vector3D undockPosition;
        private void ProcessCurrentOrder()
        {
            maxCameraRange = 3000;
            maxCameraAngle = 100;

            //weaponSystems.Disengage();

            if (NextOrder != null)
            {
                CurrentOrder = NextOrder;
                NextOrder = null;
            }

            //log.Debug("processing");
            if (CurrentOrder != null)
            {
                //log.Debug("processing order");
                if (CurrentOrder.Ordertype == OrderType.Scan)
                {
                    if (Docked || Undocking)
                    {
                        Undock();
                    }
                    else
                    {
                        ScanLocation();
                    }
                }
                else if (CurrentOrder.Ordertype == OrderType.Dock)
                {
                    DockToConnector();
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

            if (navigationSystems.GetSpeed() > 20)
                navigationSystems.SlowDown();
            else
                navigationSystems.MaintainAltitude(trackingSystems.GetAltitude(), hoverHeight, Math.Abs(trackingSystems.GetAltitude() - hoverHeight) / 2);
        }

        private void ScanLocation()
        {
            var grav = navigationSystems.RemoteControl.GetNaturalGravity();
            grav.Normalize();
            var targetLoc = CurrentOrder.PrimaryLocation - (grav * hoverHeight);
            navigationSystems.HoverApproach(targetLoc, 20, hoverHeight, trackingSystems.GetAltitudeIncDir());

            navigationSystems.AlignUpWithGravity();
            //navigationSystems.StopRoll();
            navigationSystems.AlignTo(targetLoc);
        }

        private void Undock()
        {
            if (Docked)
            {
                Undocking = true;
                foreach (var connector in shipComponents.Connectors)
                {
                    connector.Disconnect();
                    connector.GetActionWithName("OnOff_On").Apply(connector);
                }
                undockPosition = Me.GetPosition() + (navigationSystems.RemoteControl.WorldMatrix.Forward * 30);
                navigationSystems.EnableFlightMode();
            }
            if (Undocking)
            {
                navigationSystems.Approach(undockPosition, 100);
                navigationSystems.AlignTo(undockPosition);
                navigationSystems.AlignUpWithGravity();
                var distance = (navigationSystems.RemoteControl.GetPosition() - undockPosition).Length();
                if (distance < 5)
                    Undocking = false;
            }
        }

        private void DockToConnector()
        {
            try
            {
                //log.Debug("Processing Dock Order");
                //log.Debug(CurrentOrder.dockroute.Count()+" Number of dock Orders");
                var preDockLocation = CurrentOrder.dockroute[CurrentOrder.DockRouteIndex];
                if (preDockLocation != null)
                {
                    var remoteControl = shipComponents.ControlUnits.FirstOrDefault();
                    var connector = shipComponents.Connectors.First();

                    
                    var connectorAdjustrange = (connector.GetPosition() - remoteControl.GetPosition());
                    var myloc = remoteControl.GetPosition() + connectorAdjustrange;


                    if (connector.Status != MyShipConnectorStatus.Connected)
                    {
                        var distanceFromConnector = (myloc - CurrentOrder.PrimaryLocation).Length();
                        var distanceFromCPK1 = (myloc - preDockLocation).Length();

                        log.Debug("Dock cp2 " + distanceFromCPK1);
                        if (distanceFromCPK1 <= 1 && CurrentOrder.DockRouteIndex >= 1)
                        {
                            CurrentOrder.DockRouteIndex--;
                        }

                        

                        if (distanceFromConnector < 10)
                        {
                            log.Debug("Dock cp3: " + distanceFromConnector + " " + connector.Status);
                            connector.GetActionWithName("OnOff_On").Apply(connector);

                            log.Debug("Connecter Status: " + connector.Status);

                            connector.Connect();

                        }

                        //log.Debug("from dock " + distanceFromConnector + " from point: " + distanceFromCPK1 + " index: " + CurrentOrder.dockroute.Count);
                        navigationSystems.DockApproach(myloc, preDockLocation);
                        if(distanceFromConnector>180)
                            navigationSystems.AlignTo(preDockLocation);
                        else
                            navigationSystems.AlignTo(Me.CubeGrid.GetPosition() + (CurrentOrder.DirectionalVectorOne * 200));

                        navigationSystems.Roll(.12f);
                        //navigationSystems.AlignUp(navigationSystems.RemoteControl.GetPosition() + (CurrentOrder.ThirdLocation * 400));
                    }
                    else
                    {
                        navigationSystems.EnableDockedMode();
                    }
                }
                else
                {
                    log.Error("No Predock Route");
                    navigationSystems.SlowDown();
                }

            }
            catch (Exception e)
            {
                log.Error("In Dock\n" + e.Message + "\n" + e.StackTrace);
            }
        }

        bool Docked = false;
        protected void UpdateDisplays()
        {
            try
            {
                UpdateInfoKey("Storage", " Mass: " + navigationSystems.RemoteControl.CalculateShipMass().PhysicalMass + " Max Mass: " + navigationSystems.GetMaxSupportedWeight());
                UpdateInfoKey("Power: ", "Current: " + CurPower + " Max: " + MaxPower);

                if (NearestPlanet != null)
                {
                    log.DisplayShipInfo(shipInfoKeys, "PlanetInfo:  altitude: " + (int)trackingSystems.GetAltitude() + "m" + "  Speed: " + (int)navigationSystems.GetSpeed() + "m/s");
                    log.UpdateRegionInfo(NearestPlanet.Regions, Me.CubeGrid);
                }
                else
                    log.DisplayShipInfo(shipInfoKeys, " No Planet ");

                //log.UpdateProductionInfo(factorySystems, Me.CubeGrid);
            }
            catch (Exception e) { log.Error("UpdateDisplays " + e.Message); }

            UpdateSystemScreens();

            UpdateAntenna();
        }
        //////
    }
}
