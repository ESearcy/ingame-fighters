using System;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRageMath;
using SEMod.INGAME.classes.systems;
using SEMod.INGAME.classes.model;

namespace SEMod.INGAME.classes.implementations
{
    class MiningDrone : AIShipBase
    {

        public MiningDrone()
        //////public Program()
        {
            shipComponents = new ShipComponents();
            LocateAllParts();
            log = new Logger(Me.CubeGrid, shipComponents);

            communicationSystems = new CommunicationSystem(log, Me.CubeGrid, shipComponents);
            navigationSystems = new NavigationSystem(log, Me.CubeGrid, shipComponents);
            productionSystems = new ProductionSystem(log, Me.CubeGrid, shipComponents);
            storageSystem = new StorageSystem(log, Me.CubeGrid, shipComponents);
            trackingSystems = new TrackingSystem(log, Me.CubeGrid, shipComponents, false);
            weaponSystems = new WeaponSystem(log, Me.CubeGrid, shipComponents);

            operatingOrder.AddLast(new TaskInfo(LocateAllParts));
            operatingOrder.AddLast(new TaskInfo(InternalSystemScan));
            operatingOrder.AddLast(new TaskInfo(NavigationCheck));
            operatingOrder.AddLast(new TaskInfo(RecieveFleetMessages));
            operatingOrder.AddLast(new TaskInfo(SendPendingMessages));
            operatingOrder.AddLast(new TaskInfo(FollowOrders));
            operatingOrder.AddLast(new TaskInfo(SensorScan));
            operatingOrder.AddLast(new TaskInfo(UpdateTrackedTargets));
            operatingOrder.AddLast(new TaskInfo(FollowOrders));
            operatingOrder.AddLast(new TaskInfo(UpdateDisplays));
            SetupFleetListener();

            maxCameraRange = 5000;
            maxCameraAngle = 80;

            //set new defaults
            hoverHeight = 75;
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
            foreach (var mes in messages)
            {
                var pm = communicationSystems.ParseMessage(mes);

                if (!registered && pm.TargetEntityId == Me.CubeGrid.EntityId && pm.MessageType == MessageCode.Confirmation)
                {
                    registered = true;
                    CommandShipEntity = pm.EntityId;
                    log.Debug("Registered!!");
                }

                if (ParsedMessage.MaxNumBounces < pm.NumBounces && pm.MessageType != MessageCode.PingEntity)
                {
                    pm.NumBounces++;
                    //LOG.Debug("Bounced Message");
                    communicationSystems.SendMessage(pm.ToString());
                }

                if (registered)
                {
                    switch (pm.MessageType)
                    {
                        case MessageCode.Order:
                            //log.Debug((CommandShipEntity == pm.CommanderId) + " command ship set | meant for me: "+  Me.CubeGrid.EntityId + "  " + pm.EntityId +"  "+ pm.TargetEntityId);
                            if (CommandShipEntity == pm.CommanderId && pm.EntityId == Me.CubeGrid.EntityId)
                            {
                                log.Debug(pm.OrderType + " order recieved");
                                if (pm.OrderType == OrderType.Dock && CurrentOrder != null && CurrentOrder.Ordertype == OrderType.Dock)
                                {
                                    try
                                    {
                                        CurrentOrder.PrimaryLocation = pm.Location;
                                        CurrentOrder.DirectionalVectorOne = pm.AlignForward;
                                        CurrentOrder.ThirdLocation = pm.AlignUp;
                                        CurrentOrder.UpdateDockingCoords();
                                    }
                                    catch (Exception e) { log.Error(e.StackTrace); }

                                }
                                else if (pm.OrderType == OrderType.Mine && CurrentOrder != null && CurrentOrder.Ordertype == OrderType.Mine)
                                {
                                    if (pm.Location != CurrentOrder.PrimaryLocation)
                                    {
                                        Disengaging = true;

                                        NextOrder = new DroneOrder(log, pm.OrderType, pm.RequestID, pm.TargetEntityId, pm.EntityId, pm.Location, pm.AlignForward, pm.AlignUp);
                                        //log.Error("Mining Order changed unexpectedly");
                                    }
                                }
                                else if (pm.OrderType == OrderType.Mine && CurrentOrder == null)
                                {
                                    NextOrder = new DroneOrder(log, pm.OrderType, pm.RequestID, pm.TargetEntityId, pm.EntityId, pm.Location, pm.AlignForward, pm.AlignUp);
                                    log.Error("New Mining Order -checking for reboot");
                                    NextOrder.DockRouteIndex = NextOrder.dockroute.IndexOf(NextOrder.dockroute.OrderBy(x => (x - navigationSystems.RemoteControl.GetPosition()).Length()).First());
                                }
                                else if (CurrentOrder == null) {
                                    CurrentOrder = new DroneOrder(log, pm.OrderType, pm.RequestID, pm.TargetEntityId, pm.EntityId, pm.Location, pm.AlignForward, pm.AlignUp);
                                }
                                else
                                {
                                    if (CurrentOrder != null && CurrentOrder.Ordertype == OrderType.Mine)
                                        Disengaging = true;

                                    if (CurrentOrder.Ordertype == OrderType.Mine && pm.OrderType == OrderType.Dock)
                                        Disengaging = true;

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
                        //log.Debug("sending registration request");
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
        
        public void SendUpdate(bool isRegistration = false)
        {
            Docked = shipComponents.Connectors.Any(x => x.Status == MyShipConnectorStatus.Connected);
            int CurrentInvVolume = 0;
            int MaxInvVolume = 0;
            foreach (var block in shipComponents.AllMyBlocks)
            {
                for (int i=0; i< block.InventoryCount;i++)
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
                //cargo info -- Not Implemented
                CurrentInvVolume,
                MaxInvVolume,
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
            //health
            //cargo m3 total, volume used aswell
            //

            communicationSystems.SendMessage(updateMessage);
            //ParsedMessage.CreateUpdateMessage(
            //    Me.CubeGrid.EntityId,
            //    CommandShipEntity,
            //    navigationSystems.GetSpeedVector(),
            //    Me.CubeGrid.GetPosition(),
            //    communicationSystems.GetMsgSntCount(),
            //    Docked,
            //    shipComponents.Cameras.Count(),
            //    shipComponents.Connectors.Count(),
            //    shipComponents.MiningDrills.Count(),
            //    shipComponents.Sensors.Count(),
            //    shipComponents.GatlingGuns.Count(),
            //    Me.CubeGrid.GridSize,
            //    storageSystem.GetPercentFull()));
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
                        Undock();
                        
                    }
                    else if (Disengaging)
                        ExitMiningPosition();
                    else
                        MinePosition();
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

        private void ScanLocation()
        {
            var grav = navigationSystems.RemoteControl.GetNaturalGravity();
            grav.Normalize();
            var targetLoc = CurrentOrder.PrimaryLocation - (grav * hoverHeight);
            navigationSystems.HoverApproach(targetLoc, 30, hoverHeight, trackingSystems.GetAltitudeIncDir());
            navigationSystems.AlignUp(targetLoc);
            navigationSystems.AlignAgainstGravity();
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
                navigationSystems.Approach(undockPosition, 5);
                navigationSystems.AlignTo(undockPosition);
                var distance = (navigationSystems.RemoteControl.GetPosition() - undockPosition).Length();
                if (distance < 5)
                    Undocking = false;
            }
        }

        bool MiningEngaged = false;
        bool Disengaging = false;

        private void MinePosition()
        {

            var navPoint = CurrentOrder.dockroute[CurrentOrder.MiningIndex];

            navigationSystems.AlignAgainstGravity();
            //navigationSystems.AlignTo(navPoint);
            foreach (var drill in shipComponents.MiningDrills)
            {
                drill.GetActionWithName("OnOff_On").Apply(drill);
            }

            var distance = (navigationSystems.RemoteControl.GetPosition() - navPoint).Length();

            if (distance > 50)
            {
                var difference = 50 - trackingSystems.GetAltitude();
                if (difference > 10)
                    navigationSystems.MaintainAltitude(trackingSystems.GetAltitude(), 50, 10);

                navigationSystems.Approach(navPoint, 10);

            }
            else
                navigationSystems.Approach(navPoint, 1);

            if (distance <= 1 && CurrentOrder.MiningIndex == 0)
            {
                MiningEngaged = true;
            }

            if (distance < 10)
            {
                navigationSystems.Roll(2);

                if (navigationSystems.GetSpeed() > .15)
                    navigationSystems.SlowDown();
            }
            else
                navigationSystems.AlignUp(navPoint);

            if (distance < .5 && CurrentOrder.MiningIndex + 1 < CurrentOrder.dockroute.Count())
                CurrentOrder.MiningIndex++;

            log.Debug(CurrentOrder.dockroute.Count() + " :distance: " + distance + " index: " + CurrentOrder.MiningIndex);

        }

        private void ExitMiningPosition()
        {
            if (Docked)
            {
                foreach (var connector in shipComponents.Connectors)
                {
                    connector.Disconnect();
                    connector.GetActionWithName("OnOff_On").Apply(connector);
                }

                navigationSystems.EnableFlightMode();
            }

            var navPoint = CurrentOrder.dockroute[CurrentOrder.MiningIndex];
            navigationSystems.Approach(navPoint, 1);
            navigationSystems.AlignAgainstGravity();

            foreach (var drill in shipComponents.MiningDrills)
            {
                drill.GetActionWithName("OnOff_Off").Apply(drill);
            }

            var distance = (navigationSystems.RemoteControl.GetPosition() - navPoint).Length();

            if (distance <= 1 && CurrentOrder.MiningIndex == 0)
            {
                MiningEngaged = false;
            }

            if (distance < 1)
            {
                navigationSystems.Roll(0);

                if (navigationSystems.GetSpeed() > .1)
                    navigationSystems.SlowDown();
            }
            else
                navigationSystems.AlignUp(navPoint);

            if (distance < 1.5 && CurrentOrder.MiningIndex >= 1)
                CurrentOrder.MiningIndex--;
            log.Debug("Exiting Mine distance: " + distance + " index: " + CurrentOrder.MiningIndex);
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

                    var myloc = remoteControl.GetPosition();
                    var connectorAdjustrange = (connector.GetPosition() - remoteControl.GetPosition()).Length();

                    if (connector.Status != MyShipConnectorStatus.Connected)
                    {
                        log.Debug("Dock cp2");
                        var distanceFromCPK1 = (myloc - preDockLocation).Length();

                        if (distanceFromCPK1 <= .5 && CurrentOrder.DockRouteIndex > (int)connectorAdjustrange)
                        {
                            CurrentOrder.DockRouteIndex--;
                        }

                        var distanceFromConnector = (myloc - CurrentOrder.PrimaryLocation).Length();

                        if (distanceFromConnector < 10)
                        {
                            log.Debug("Dock cp3: "+ distanceFromConnector + " "+ connector.Status);
                            connector.GetActionWithName("OnOff_On").Apply(connector);

                            log.Debug("Connecter Status: " + connector.Status);

                            connector.Connect();

                        }
                        
                        log.Debug("dockpos: "+preDockLocation);
                        log.Debug("endpos: " + CurrentOrder.PrimaryLocation);

                        navigationSystems.DockApproach(myloc, preDockLocation);
                        navigationSystems.AlignAgainstGravity();
                        navigationSystems.AlignUp(navigationSystems.RemoteControl.GetPosition() + (CurrentOrder.ThirdLocation * 400));


                    }
                    else
                    {
                        navigationSystems.EnableDockedMode();
                    }
                }
                else
                {
                    log.Error("No Predock Location");
                    navigationSystems.SlowDown();
                }

            }
            catch (Exception e)
            {
                log.Error("In Dock\n" + e.Message + "\n" + e.StackTrace);
            }
        }

        private void NavigationCheck()
        {
            
            var ns = navigationSystems.IsOperational();
            UpdateInfoKey("NavigationSystems", BoolToOnOff(ns) + "");
        }

        bool Docked = false;
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
        //////
    }
}
