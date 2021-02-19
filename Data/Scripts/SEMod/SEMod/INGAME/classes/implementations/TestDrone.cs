using System;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRageMath;
using SEMod.INGAME.classes.systems;
using SEMod.INGAME.classes.model;
using System.Collections.Generic;

namespace SEMod.INGAME.classes.implementations
{
    class TestDrone : AIShipBase
    {

        
        public TestDrone()
        //////public Program()
        {
            shipComponents = new ShipComponents();
            LocateAllParts();
            log = new Logger(Me.CubeGrid, shipComponents);

            communicationSystems = new CommunicationSystem(log, Me.CubeGrid, shipComponents);
            navigationSystems = new NavigationSystem(log, Me.CubeGrid, shipComponents);

            trackingSystems = new TrackingSystem(log, Me.CubeGrid, shipComponents, false);
            weaponSystems = new WeaponSystem(log, Me.CubeGrid, shipComponents);

            
            operatingOrder.AddLast(new TaskInfo(InternalSystemCheck));
            operatingOrder.AddLast(new TaskInfo(NavigationCheck));
            //operatingOrder.AddLast(new TaskInfo(RecieveFleetMessages));
            operatingOrder.AddLast(new TaskInfo(SendPendingMessages));
            operatingOrder.AddLast(new TaskInfo(FollowOrders));
            operatingOrder.AddLast(new TaskInfo(ScanLocalArea));
            operatingOrder.AddLast(new TaskInfo(UpdateTrackedTargets));
            operatingOrder.AddLast(new TaskInfo(UpdateDisplays));
            operatingOrder.AddLast(new TaskInfo(FollowOrders));
            operatingOrder.AddLast(new TaskInfo(LocateAllParts));
            SetupFleetListener();

            maxCameraRange = 1000;
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
                antenna.CustomName = "\nTest Index: "+testPointIndex+"\nA: " + (int)trackingSystems.GetAltitude() + "\n" +
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

                RunTestRutine();
            }
        }

        public List<Vector3D> TestPoints3 = new List<Vector3D>() { new Vector3D(-38784.65, -38479.80, -27476.13), new Vector3D(-38643.30, -38407.75, -27756.96), new Vector3D(-38542.47, -38650.39, -27602.64) };
        public List<Vector3D> TestPoints2 = new List<Vector3D>() { new Vector3D(-38784.65, -38479.80, -27476.13), new Vector3D(-38643.30, -38407.75, -27756.96)};
        private int testPointIndex = 0;
        private int maxSpeed = 2;
        private void RunTestRutine()
        {
            if (testPointIndex >= TestPoints3.Count())
                testPointIndex = 0;

            var currentPoint = TestPoints3[testPointIndex];
            var distanceToPoint =(navigationSystems.RemoteControl.GetPosition() - currentPoint).Length();
            var align_angle = navigationSystems.AlignTo(currentPoint);
            navigationSystems.AlignUpWithGravity();

            if (distanceToPoint > 100 && align_angle < 2)
                maxSpeed = 20;
            else if (distanceToPoint < 15)
                maxSpeed = 1;
            else
                maxSpeed = 5;

            if (trackingSystems.GetAltitude() < 30)
                navigationSystems.MaintainAltitude(trackingSystems.GetAltitude(), trackingSystems.GetAltitude()+10, maxSpeed);
            else
            {
                navigationSystems.AlignTo(currentPoint);
                navigationSystems.HoverApproach(currentPoint, maxSpeed, hoverHeight, trackingSystems.GetAltitudeIncDir());
            }
            
            if (distanceToPoint <= 3 && navigationSystems.GetSpeed() <= 1)
                testPointIndex++;
        }

        private void AvoidAndApproach()
        {
            var rc = navigationSystems.RemoteControl;
            if (testPointIndex >= TestPoints2.Count())
                testPointIndex = 0;

            var mypos = rc.GetPosition();
            var currentPoint = TestPoints3[testPointIndex];
            var alignTarget = currentPoint;
            var distanceToPoint = (mypos - currentPoint).Length();
            var directionToPoint = (mypos - currentPoint);
            var distanceToSafty = 0.00;
            var distanceFromGround = 0.00;
            var directionToSafty = Vector3D.Zero;
            directionToPoint.Normalize();


            var lowAltitude = trackingSystems.GetAltitude() < 30;
            if (lowAltitude)
            {
                var avoidPos = trackingSystems.NearestSurfacePoint;
                distanceToSafty = 40 - trackingSystems.GetAltitude();
                distanceFromGround = trackingSystems.GetAltitude();
                // normal avoid aould be to add the avoid vector to 
                directionToSafty = (trackingSystems.NearestSurfacePoint - mypos);
                //if destination is same direction as ground, go up instead of away from the ground.

                var groundDirection = mypos - avoidPos;
                groundDirection.Normalize();

                var anglebetween = BasicNavigationSystem.AngleBetween(directionToPoint, groundDirection, true);
                if (anglebetween < 65)
                {
                    var gravityDir = rc.GetNaturalGravity();
                    gravityDir.Normalize();
                    var HoverLocation = rc.GetPosition() - (gravityDir * distanceToSafty);
                    directionToSafty = (trackingSystems.NearestSurfacePoint - mypos);
                    directionToPoint = Vector3D.Zero;
                    distanceToPoint = 0.00;
                    alignTarget = HoverLocation;
                }
            }

            //calculate max speeds for both
            var desiredavoidSpeed = distanceFromGround > 40 ? 20 : Convert.ToInt32(distanceFromGround);
            var desiredTravelSpeed = distanceToPoint > 100 ? 20 : distanceToPoint < 15 ? 1 : 5;
            maxSpeed = desiredTravelSpeed > desiredavoidSpeed ? desiredTravelSpeed : desiredavoidSpeed;
            navigationSystems.AlignTo(alignTarget);
            navigationSystems.EngageThrusters(directionToPoint, desiredTravelSpeed, directionToSafty, desiredavoidSpeed);
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
