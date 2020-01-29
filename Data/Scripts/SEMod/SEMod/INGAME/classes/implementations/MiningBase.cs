using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRageMath;
using SEMod.INGAME.classes.systems;
using SEMod.INGAME.classes.model;

namespace SEMod.INGAME.classes.implementations
{
    class MiningBase : AIShipBase
    {
        IMyProgrammableBlock Me = null;
        IMyGridTerminalSystem GridTerminalSystem = null;
        IMyGridProgramRuntimeInfo Runtime;
        

        public MiningBase()
        //////public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            shipComponents = new ShipComponents();
            LocateAllParts();
            SetupFleetListener();

            log = new Logger(Me.CubeGrid, shipComponents);

            communicationSystems = new CommunicationSystem(log, Me.CubeGrid, shipComponents);
            navigationSystems = new BasicNavigationSystem(log, Me.CubeGrid, shipComponents);
            productionSystems = new ProductionSystem(log, Me.CubeGrid, shipComponents);
            storageSystem = new StorageSystem(log, Me.CubeGrid, shipComponents);
            trackingSystems = new TrackingSystem(log, Me.CubeGrid, shipComponents, true);
            weaponSystems = new WeaponSystem(log, Me.CubeGrid, shipComponents);

            operatingOrder.AddLast(new TaskInfo(LocateAllParts));
            operatingOrder.AddLast(new TaskInfo(InternalSystemScan));
            operatingOrder.AddLast(new TaskInfo(NavigationCheck));
            operatingOrder.AddLast(new TaskInfo(RecieveFleetMessages));
            operatingOrder.AddLast(new TaskInfo(SendPendingMessages));
            operatingOrder.AddLast(new TaskInfo(SensorScan));
            operatingOrder.AddLast(new TaskInfo(MaintainAltitude));
            operatingOrder.AddLast(new TaskInfo(UpdateTrackedTargets));
            operatingOrder.AddLast(new TaskInfo(UpdateDisplays));
            operatingOrder.AddLast(new TaskInfo(IssueOrders));
            operatingOrder.AddLast(new TaskInfo(RunProductionRoutine));
            

            maxCameraRange = 5000;
            maxCameraAngle = 80;
            //set new defaults
            hoverHeight = 400;
            InitialBlockCount = shipComponents.AllBlocks.Count();
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }
        protected BasicNavigationSystem navigationSystems;
        List<DroneContext> drones = new List<DroneContext>();
        DateTime startTime = DateTime.Now;


        public void RunProductionRoutine()
        {
            productionSystems.Update();
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
        
        private void RecieveFleetMessages()
        {
            var messages = RecieveMessages();
            foreach (var mes in messages) {
                var pm = communicationSystems.ParseMessage(mes);
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

            if (argument.StartsWith("FactoryShortcut:"))
            {
                productionSystems.ManualCommand(argument.Replace("FactoryShortcut:", ""));
                log.Debug("Engaged Manual Command "+argument);
                return;
            }

            
        }

        DateTime lastGlobalWakeupCall = DateTime.Now.AddMinutes(-10);
        int droneOrderIndex = 0;

        public void ResendOrder(DroneOrder order)
        {
            if (order != null)
            {
                var time = (DateTime.Now - order.LastUpdated).TotalSeconds;
                if (time >= 10)
                {
                    if (order.Ordertype == OrderType.Dock)
                    {
                        order.PrimaryLocation = order.Connector.GetPosition();
                    }
                    //send again
                    communicationSystems.TransmitOrder(order, Me.CubeGrid.EntityId);
                    log.Debug("Resending " + order.Ordertype + " order");
                    order.LastUpdated = DateTime.Now;
                }
            }
        }
        public void RunCommandBaseLogic(DroneContext droneInfo)
        {
            var drone = droneInfo.Info;
            var order = droneInfo.Order;
            var needOres = false;


            //log.Debug(drone.PercentCargo + " cargo & max " + drone.StorageMax);
            var droneCargo = (drone.PercentCargo / drone.StorageMax) * 100;
            var batteryPercent = (int)((drone.CurrentPower/drone.MaxPower)*100);

            if (order == null && drone.Docked)
            {
                log.Debug("docked and order null. Issuing standby order");
                IssueStandbyOrder(droneInfo);
            }
            else if (order == null && !drone.Docked)
            {

                if (!IssueDockOrder(droneInfo))
                {
                    log.Debug("undocked and order null. Dock Order failed, Issuing Standby Order.");
                    IssueStandbyOrder(droneInfo);
                }
                else
                    log.Debug("undocked and order null. Issuing Dock Order ");
            }
            else if (order != null && order.Ordertype == OrderType.Dock && drone.Docked)
            {
                log.Debug("Drone successfully docked - Issuing Standby Order");
                IssueStandbyOrder(droneInfo);
            }
            else if (droneCargo > 75 && !drone.Docked && (order == null || order.Ordertype != OrderType.Dock))
            {
                
                var dockOrderIssued = IssueDockOrder(droneInfo);

                if (!dockOrderIssued)
                {
                    log.Debug("Cargo Full: unable to issue dock Order, Standby order issued");
                    IssueStandbyOrder(droneInfo);
                }
                else
                    log.Debug("Cargo Full: dock Order Issued");

            }
            else if (batteryPercent < 30 && !drone.Docked && (order == null || order.Ordertype != OrderType.Dock))
            {
                var dockOrderIssued = IssueDockOrder(droneInfo);

                if (!dockOrderIssued)
                {
                    log.Debug("Low Battery: unable to issue dock Order, Standby order issued");
                    IssueStandbyOrder(droneInfo);
                }
                else
                    log.Debug("Low Battery: dock Order Issued");

            }
            else if ((batteryPercent < 90 || droneCargo > 75) && drone.Docked)
            {

                if (order != null && order.Ordertype != OrderType.Standby)
                {
                    log.Debug("Issuing Standby order while drone recharges/unloads");
                    IssueStandbyOrder(droneInfo);
                }
            }
            else if (drone.Type == DroneType.Miner && MiningDroneLogic(droneInfo, drone, order)) ;
            else if (drone.Type == DroneType.Combat && CombatDroneLogic(droneInfo, drone, order)) ;
            else if (drone.Type == DroneType.Scan && ScanDroneLogic(droneInfo, drone, order)) ;
            else if (order != null)
            {
                ResendOrder(order);
            }
            else
                log.Error("No order issued - maybe something wrong with command logic");

        }
        private bool CombatDroneLogic(DroneContext droneInfo, DroneInfo drone, DroneOrder order)
        {
            return false;
        }



        List<DroneOrder> OngoingScanOrders = new List<DroneOrder>();
        private bool ScanDroneLogic(DroneContext droneInfo, DroneInfo drone, DroneOrder order)
        {
            var awaitingOrders = (order != null && order.Ordertype == OrderType.Standby);
            if (awaitingOrders)
            {
                // log.Debug("Issuing First Scan Order");
                IssueScanOrder(droneInfo);
            }
            else if (order != null && order.Ordertype == OrderType.Scan)
            {
                var droneDistance = Math.Abs((order.PrimaryLocation - drone.lastKnownPosition).Length());
                if (droneDistance < 250 || (DateTime.Now - order.IssuedAt).TotalMinutes >= 2)
                {
                    //log.Debug("Scan Order Complete");
                    order.PointOfIntrest.HasPendingOrder = false;
                    order.PointOfIntrest.Timestamp = DateTime.Now;
                    order.PointOfIntrest.Reached = true;
                    OngoingScanOrders.Remove(order);
                    trackingSystems.UpdateScanPoint(order.PointOfIntrest);

                    var nextScanPoint = trackingSystems.GetNearestScanPoint(Me.GetPosition(), 600);
                    if (nextScanPoint != null)
                    {
                        //log.Debug("Issuing New Scan Order "+ nextScanPoint.Location);
                        droneInfo.Order = new DroneOrder(log, OrderType.Scan, communicationSystems.GetMsgSntCount(), 0, drone.EntityId, nextScanPoint.Location, Vector3D.Zero, Vector3D.Zero);
                        droneInfo.Order.PointOfIntrest = nextScanPoint;
                        communicationSystems.TransmitOrder(droneInfo.Order, Me.CubeGrid.EntityId);
                    }

                }
                else
                    return false;
            }
            else
                return false;

            return true;
        }

        private bool MiningDroneLogic(DroneContext droneInfo, DroneInfo drone, DroneOrder order)
        {
            var awaitingOrders = order != null && order.Ordertype == OrderType.Standby && drone.Docked;
            if (awaitingOrders)
            {
                IssueMiningOrder(droneInfo);
            }
            else if (order != null && order.Ordertype == OrderType.Standby && !drone.Docked)
            {
                IssueDockOrder(droneInfo);
            }
            else if (order != null && order.Ordertype == OrderType.Mine && !drone.Docked)
            {
                var directionV = navigationSystems.GetGravityDirection();
                directionV.Normalize();
                var target_endpoint = order.PrimaryLocation + (directionV * 35);
                var target_end_point_distance = (int)(drone.lastKnownPosition - target_endpoint).Length();
                var target_between_start_and_end = (int)(order.PrimaryLocation - target_endpoint).Length();
               // log.Debug("target endpoint: " + target_endpoint);
               // log.Debug("Distance to finish mining: "+ target_end_point_distance);
               // log.Debug("start - end distance: " + target_between_start_and_end);
                if (target_end_point_distance<=5)
                {
                    miningOrders.Remove(order);
                    order.PointOfIntrest.Mined = true;
                    IssueDockOrder(droneInfo);
                }
                else
                    return false;
            }
            else
                return false;
            //else if (order != null && order.Ordertype == OrderType.Mine)
            //{
            //    double dist = 1000000;

            //    for (int i = 0; i < droneInfo.Order.dockroute.Count(); i++)
            //    {
            //        var vect = droneInfo.Order.dockroute[i];
            //        var distance = Math.Abs((droneInfo.Info.lastKnownPosition - vect).Length());
            //        if (distance < dist && i > droneInfo.Order.MiningIndex)
            //        {
            //            droneInfo.Order.MiningIndex = i;
            //            dist = distance;
            //        }
            //    }
            //    log.Debug(droneInfo.Order.MiningIndex+" Mining Index");
            //    if(droneInfo.Order.MiningIndex == (droneInfo.Order.dockroute.Count()-1))
            //    {
            //        miningOrders.Remove(order);
            //        IssueStandbyOrder(droneInfo);
            //    }
            //    return false;
            //}


            return true;
        }

        public void IssueOrders()
        {
            try
            {
                if (droneOrderIndex >= drones.Count())
                    droneOrderIndex = 0;

                if ((DateTime.Now - lastGlobalWakeupCall).TotalMinutes >= 10)
                {
                    lastGlobalWakeupCall = DateTime.Now;
                    communicationSystems.SendAwakeningMessage();
                    log.Debug("sent awakening Message");
                }
                if (drones.Any())
                {
                    //log.Debug("attempting to order drone");
                    var drone = drones[droneOrderIndex];
                    if (drone != null)
                    {
                        RunCommandBaseLogic(drone);
                        //log.Debug("Drone Not null ");
                        //LOOK AT CURRENT ORDER
                        //var order = drone.Order;

                        //else if (order == null)
                        //{
                        //    //if (drone.Info.NumWeapons == 0)
                        //    //{
                        //    //    IssueSurveyOrder(order, drone);
                        //    //}
                        //    //else
                        //    //{
                        //    //if (drone.Info.NumWeapons > 0)
                        //    //    IssueAttackOrder(drone);
                        //    if (drone.Info.NumConnectors > 0 && !drone.Info.Docked)
                        //        IssueDockOrder(drone);
                        //    //}
                        //}
                    }
                }


                droneOrderIndex++;

            }
            catch (Exception e) { log.Error("IssueOrders " + e.Message + "\n" + e.StackTrace); }
            log.UpdateFleetInformationScreens(drones, Me.CubeGrid);
        }

        private bool IssueDockOrder(DroneContext drone)
        {
            //if (dockOrders.Count > 3)
              //  return false;

            log.Debug("Attampting Dock Order. miner? "+ (drone.Info.NumDrills > 0));
            var unused = (drone.Info.NumDrills > 0) ?
                    shipComponents.Connectors.Where(x => x.Status != MyShipConnectorStatus.Connectable && x.Status != MyShipConnectorStatus.Connected && x.CustomName.Contains("#miner#") && !x.CustomName.Contains("#trash#")) :
                    shipComponents.Connectors.Where(x => x.Status != MyShipConnectorStatus.Connectable && x.Status != MyShipConnectorStatus.Connected && !x.CustomName.Contains("#miner#") && !x.CustomName.Contains("#trash#"));

            var used = drones.Where(x => x.Order != null && x.Order.Connector != null).Select(x => x.Order.Connector);
            var available = unused.Where(x => !used.Contains(x));
            var usableConnector = available.FirstOrDefault();

            //log.Debug("unused: " + unused.Count() + " used: " + used.Count() + " available: " + available.Count() + "  : " + (usableConnector != null));

            if (usableConnector != null)
            {
                log.Debug("Issuing Dock Order");
                var order = new DroneOrder(log, OrderType.Dock, communicationSystems.GetMsgSntCount(), usableConnector.EntityId, drone.Info.EntityId, usableConnector.GetPosition(), usableConnector.WorldMatrix.Forward, usableConnector.WorldMatrix.Up);
                order.Connector = usableConnector;
                communicationSystems.TransmitOrder(order, Me.CubeGrid.EntityId);
                drone.Order = order;
                return true;
            }
            return false;
        }

        private void IssueStandbyOrder(DroneContext drone)
        {
            log.Debug("Attampting Standby Order");
            var order = new DroneOrder(log, OrderType.Standby, communicationSystems.GetMsgSntCount(), 0, drone.Info.EntityId, Vector3D.Zero, Vector3D.Zero, Vector3D.Zero);
            communicationSystems.TransmitOrder(order, Me.CubeGrid.EntityId);
            drone.Order = order;
        }

        public void IssueAttackOrder(DroneContext drone)
        {
            var closestTargets = trackingSystems.getCombatTargets(Me.GetPosition());
            if (closestTargets.Any())
            {
                var biggestTarget = closestTargets.OrderByDescending(x => x.Radius).FirstOrDefault();
                if (biggestTarget != null)
                {
                    log.Debug("Issuing Attack Order");
                    var order = new DroneOrder(log, OrderType.Attack, communicationSystems.GetMsgSntCount(), biggestTarget.EntityID, drone.Info.EntityId, biggestTarget.PointsOfInterest[0].Location, navigationSystems.GetGravityDirection(), biggestTarget.Location);
                    communicationSystems.TransmitOrder(order, Me.CubeGrid.EntityId);
                    drone.Order = order;
                }
            }
        }

        //public void IssueSurveyOrder(DroneOrder order,DroneContext drone)
        //{
        //    //get planets with a high scan density and few points
        //    var RegionsOfIntrest = trackingSystems.GetNearestPlanet().Regions.Where(x => (x.surfaceCenter - Me.CubeGrid.GetPosition()).Length() < 2000).Where(x => x.PointsOfInterest.Count() < 13 && x.GetScanDensity() >= 50).OrderBy(x => (x.surfaceCenter - Me.CubeGrid.GetPosition()).Length()).Take(5);
        //    if (RegionsOfIntrest.Any())
        //    {
        //        //log.Debug(RegionsOfIntrest.Count() + " Regions of Intrest Located");
        //        var regionsWithLowCoverage = RegionsOfIntrest.Where(x => x.GetPercentReached() < 50);
        //        if (regionsWithLowCoverage.Any())
        //        {
        //            //log.Debug(regionsWithLowCoverage.Count() + " Regions of Intrest With low coverage Located");
        //            var closiestRegion = regionsWithLowCoverage.First();
        //            var closestUnscannedPointToDrone = closiestRegion.GetNearestSurveyPoint(closiestRegion.surfaceCenter);
        //            if (closestUnscannedPointToDrone != null)
        //            {
        //                log.Debug(regionsWithLowCoverage.Count() + " Point of Intrest in low coverage region Located");
        //                order = new DroneOrder(log, OrderType.Scan, communicationSystems.GetMsgSntCount(), 0, drone.Info.EntityId, closestUnscannedPointToDrone.Location, navigationSystems.GetGravityDirection(), Vector3D.Zero);
        //                communicationSystems.TransmitOrder(order, Me.CubeGrid.EntityId);
        //                drone.Order = order;
        //            }
        //        }
        //    }
        //}

        List<DroneOrder> miningOrders = new List<DroneOrder>();
        protected void IssueMiningOrder(DroneContext droneinfo)
        {
            var existingOrder = miningOrders.Where(x => x.DroneId == droneinfo.Info.EntityId).ToList();
            if (existingOrder.Any())
            {
                droneinfo.Order = existingOrder.First();
            }
            else
            {
                var drone = droneinfo.Info;
                var miningTarget = trackingSystems.GetNextMiningSamplePoint(Me.GetPosition());
                if (miningTarget != null)
                {
                    miningTarget.HasPendingOrder = true;
                    var order = new DroneOrder(log, OrderType.Mine, communicationSystems.GetMsgSntCount(), 0, drone.EntityId, miningTarget.Location, navigationSystems.GetGravityDirection(), Vector3D.Zero);
                    order.PointOfIntrest = miningTarget;
                    droneinfo.Order = order;
                    miningOrders.Add(order);
                    communicationSystems.TransmitOrder(order, Me.CubeGrid.EntityId);
                }
            }
        }

        private void IssueScanOrder(DroneContext droneInfo)
        {
            var existingOrder = OngoingScanOrders.Where(x => x.DroneId == droneInfo.Info.EntityId).ToList();
            if (existingOrder.Any())
                droneInfo.Order = existingOrder.First();
            else
            {
                var drone = droneInfo.Info;
                var miningTarget = trackingSystems.GetNearestScanPoint(Me.GetPosition(), 600);
                if (miningTarget != null)
                {
                    miningTarget.HasPendingOrder = true;
                    var order = new DroneOrder(log, OrderType.Scan, communicationSystems.GetMsgSntCount(), 0, drone.EntityId, miningTarget.Location, navigationSystems.GetGravityDirection(), Vector3D.Zero);
                    OngoingScanOrders.Add(order);
                    order.PointOfIntrest = miningTarget;
                    droneInfo.Order = order;
                    communicationSystems.TransmitOrder(order, Me.CubeGrid.EntityId);
                    log.Debug("scan order sent");
                }
                else
                    log.Debug("failed to get scan location");
            }
        }

        protected void MaintainAltitude()
        {
            try
            {
                if (NearestPlanet != null)
                {
                    if (navigationSystems.GetSpeed() > 10)
                        navigationSystems.SlowDown();
                    if (drones.Any(x => x.Order !=null && ((DateTime.Now-x.Order.IssuedAt).TotalSeconds < 20 || !x.Info.Docked) && (x.Info.lastKnownPosition - Me.GetPosition()).Length() < 60))
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
            catch (Exception e) { log.Error("MaintainAltitude " + e.StackTrace); }
        }

        private void NavigationCheck()
        {
            var ns = navigationSystems.IsOperational();
            UpdateInfoKey("NavigationSystems", BoolToOnOff(ns) + "");
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

                UpdateInfoKey("Storage", " Mass: " + navigationSystems.RemoteControl.CalculateShipMass().PhysicalMass + " Max Mass: " + navigationSystems.GetMaxSupportedWeight());
                UpdateInfoKey("Power: ", "Current: " + CurPower + " Max: " + MaxPower);

                if (NearestPlanet != null)
                {
                    log.DisplayShipInfo(shipInfoKeys, "PlanetInfo:  altitude: " + (int)trackingSystems.GetAltitude() + "m" + "  Speed: " + (int)navigationSystems.GetSpeed() + "m/s");
                    log.UpdateRegionInfo(NearestPlanet.Regions, Me.CubeGrid);
                }
                else
                    log.DisplayShipInfo(shipInfoKeys, " No Planet ");

                log.UpdateProductionInfo(productionSystems, Me.CubeGrid);
            }
            catch (Exception e) { log.Error("UpdateDisplays " + e.Message); }

            log.DisplayLogScreens();

            UpdateAntenna();
        }

        //////
    }
}
