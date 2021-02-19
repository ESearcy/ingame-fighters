using System;
using System.Collections.Generic;
using System.Linq;
using SEMod.INGAME.classes.systems;
using SEMod.INGAME.classes.model;
using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace SEMod.INGAME.classes.implementations
{
    class AIShipBase
    {
        protected IMyGridProgramRuntimeInfo Runtime;
        protected IMyProgrammableBlock Me = null;
        protected IMyGridTerminalSystem GridTerminalSystem = null;
        IMyIntergridCommunicationSystem IGC;

        //////
        protected LinkedList<TaskInfo> operatingOrder = new LinkedList<TaskInfo>();

        protected Logger log;

        protected CommunicationSystem communicationSystems;
        
        protected ShipComponents shipComponents;
        protected TrackingSystem trackingSystems;
        protected WeaponSystem weaponSystems;
        protected DateTime startTime = DateTime.Now;


        protected Dictionary<String, object> shipInfoKeys = new Dictionary<string, object>();

        //configuration variables
        protected int sensorScansPerSecond = 2;
        protected int hoverHeight = 900;

        //changing variables
        protected int lastOperationIndex = 0;
        protected DateTime lastReportTime = DateTime.Now;
        protected long messagesRecieved = 0;


        public void SetupFleetListener()
        {

            IGC.RegisterBroadcastListener("fleet");
        }

        protected void LocateAllParts()
        {
            shipComponents.Sync(GridTerminalSystem, Me.CubeGrid);
        }

        protected void Update()
        {
            RunNextOperation();
        }

        public void SendPendingMessages()
        {
            var messages = communicationSystems.RetrievePendingMessages();
            foreach (var message in messages)
               TransmitMessage("fleet", message);
        }

        TaskResult lastTask = null;
        TaskInfo lastTaskInfo = null;
        protected void RunNextOperation()
        {
            if (lastTask != null)
            {
                lastTask.trueRuntme = Runtime.LastRunTimeMs;
                lastTaskInfo.AddResult(lastTask);
            }

            if (lastOperationIndex == operatingOrder.Count())
                lastOperationIndex = 0;

            TaskInfo info = operatingOrder.ElementAt(lastOperationIndex);
            info.CallMethod();

            lastTask = new TaskResult(Runtime.CurrentInstructionCount, Runtime.CurrentCallChainDepth);
            lastTaskInfo = info;

            lastOperationIndex++;
        }
        
        protected void InternalSystemCheck()
        {
            try
            {
                if(communicationSystems !=null)
                    UpdateInfoKey("communicationSystems", BoolToOnOff(communicationSystems.IsOperational()) + "");
                if (trackingSystems != null)
                    UpdateInfoKey("trackingSystems", BoolToOnOff(trackingSystems.IsOperational()) + "");
                if (weaponSystems != null)
                    UpdateInfoKey("weaponSystems", BoolToOnOff(weaponSystems.IsOperational()) + "");

                CalculatePower();
            }
            catch (Exception e) { log.Error("InternalSystemScan " + e.StackTrace); }
        }

        protected double CurPower = 0;
        protected double MaxPower = 0;
        protected void CalculatePower()
        {
            CurPower = 0;
            MaxPower = 0;

            foreach (var battery in shipComponents.Batteries)
            {
                CurPower += (double)battery.CurrentStoredPower;
                MaxPower += (double)battery.MaxStoredPower;
            }
        }

        //references only. lists exist in specific systems, just sharing to save memory & allocations.
        Dictionary<String, String> all_refs = new Dictionary<string, string>();
        public void UpdateSystemScreens()
        {
            try
            {
                all_refs.Clear();
                if (trackingSystems != null)
                    foreach (var screen in trackingSystems.GetScreenInfo())
                        all_refs.Add(screen.Key, screen.Value);

                Mass = (int)(GetCargoMass() + shipComponents.AllBlocks.Sum(x => x.Mass));

                var controlBlock = shipComponents.ControlUnits.FirstOrDefault();

                //if your ship has a remote controll, it will tell you the supported mass & current mass
                if (controlBlock != null)
                {
                    var maxMass = (int)shipComponents.Thrusters.Where(x => x.WorldMatrix.Forward == controlBlock.WorldMatrix.Forward).Sum(x => x.MaxThrust) / (controlBlock.GetNaturalGravity().Length());
                    UpdateInfoKey("Weight Information", " Mass: " + Mass + "kg  MaxMass: " + (int)maxMass + "kg");
                }

                foreach (var op in operatingOrder)
                    UpdateInfoKey(op.CallMethod.Method.Name + "", (
                        " true-runtime: " + op.GetTrueAverageExecutionTime() +
                        " CallCount: " + op.GetAverageCallCount() +
                        " CallDepth: " + op.GetAverageCallCount() + ""));

                log.DisplayLogs(shipComponents.TextPanels, all_refs);
            }
            catch (Exception e)
            {
                log.Error("UpdateSystemScreens " + e.StackTrace);
            }

            log.DisplayLogScreens();

        }

        protected int InitialBlockCount;

        protected double GetHealth()
        {
            return shipComponents.AllBlocks.Count() / InitialBlockCount;
        }

        protected string BoolToOnOff(bool conv)
        {
            return conv ? "Online" : "Offline";
        }

        Dictionary<long, TrackedEntity> foundentities = new Dictionary<long, TrackedEntity>();
        protected void ScanLocalArea()
        {
            try
            {
                foundentities.Clear();
                ScanWithSensors(foundentities);
                ScanWithCameras(foundentities);

                if(trackingSystems != null)
                {
                    foreach(var t in foundentities)
                        TrackTarget(t.Value);
                }
            }
            catch (Exception e) { log.Error("SensorScan " + e.StackTrace); }
        }

        public void TrackTarget(TrackedEntity ent)
        {
            trackingSystems.TrackEntity(ent, true);
            //var messagesToSend = trackingSystems.UpdateTrackedEntity(ent);
        }

        int sensorindex = 0;

        protected void ScanWithSensors(Dictionary<long, TrackedEntity> foundentities)
        {
            if (sensorindex == shipComponents.Sensors.Count())
                sensorindex = 0;

            var miliseconds = (DateTime.Now - lastReportTime).TotalMilliseconds;
            if (miliseconds >= 1000 / sensorScansPerSecond && sensorindex < shipComponents.Sensors.Count())
            {
                lastReportTime = DateTime.Now;

                var sensor = shipComponents.Sensors[sensorindex];
                sensor.DetectEnemy = true;
                sensor.DetectPlayers = true;
                sensor.DetectLargeShips = true;
                sensor.DetectSmallShips = true;
                sensor.DetectOwner = false;
                sensor.DetectStations = true;
                sensor.DetectAsteroids = true;

                var ent = sensor.LastDetectedEntity;

                if (ent.EntityId != 0)
                {
                    //communicationSystems.SendMessage(EntityInformation);
                    if (!foundentities.Keys.Contains(ent.EntityId))
                    {
                        //var t = new TrackedEntity(ent.Position, ent.Velocity, ent.HitPosition.Value, ent.EntityId, ent.Name, 0, ent.Relationship, ent.HitPosition.Value, ent.Type.ToString(), log);
                        var t = new TrackedEntity(
                            ent.Position
                            , ent.Velocity
                            , ent.Position
                            , ent.EntityId
                            , ent.Name
                            , 0
                            , ent.Relationship
                            , ent.Position
                            , ent.Type.ToString()
                            , log); ;
                        foundentities.Add(ent.EntityId, t);
                    }
                }
            }

            sensorindex++;
        }

        protected int pitch = 0;
        protected int yaw = 0;
        protected int range = 0;
        protected int maxCameraRange = 2000;
        protected int maxCameraAngle = 90;

        int cameraIndex = 0;
        protected void ScanWithCameras(Dictionary<long, TrackedEntity> foundentities)
        {
            if (cameraIndex == shipComponents.Cameras.Count())
                cameraIndex = 0;

            var camera = shipComponents.Cameras[cameraIndex];
            
            var maxAngle = maxCameraAngle;
            //== 0 ? camera.RaycastConeLimit : maxCameraAngle;
            var maxRange = maxCameraRange;
            //== 0? camera.RaycastDistanceLimit: maxCameraRange;
            if (!camera.EnableRaycast)
                camera.EnableRaycast = true;

            var timeToScan = camera.TimeUntilScan(range);

            if (timeToScan <= 0 && cameraIndex < shipComponents.Cameras.Count())
            {
                pitch -= 5;

                if (pitch <= -maxAngle)
                {
                    pitch = pitch * -1;
                    yaw -= 5;
                    //log.Debug("flipping pitch");
                }
                if (yaw <= -maxAngle)
                {
                    yaw = yaw * -1;
                    //range -= 200;
                    //log.Debug("flipping yaw");
                }
                if (range <= 1)
                {
                    range = maxCameraRange;
                    // log.Debug("flipping range");
                }


                //var ent = camera.Raycast(range, pitch, yaw); 
                var ent = camera.Raycast(range, pitch, yaw);
                //log.Debug("Scanning Raycast: \nrange:pitch:yaw " + range + ":" + pitch + ":" + yaw);

                if (ent.EntityId != 0)
                {
                    if (!foundentities.Keys.Contains(ent.EntityId))
                    {
                        var t = new TrackedEntity(ent.Position, ent.Velocity, ent.HitPosition.Value, ent.EntityId, ent.Name, (int)Math.Abs((ent.BoundingBox.Min - ent.BoundingBox.Max).Length()), ent.Relationship, ent.HitPosition.Value, ent.Type.ToString(), log);
                        //log.Debug("initial camera points: "+ ent.HitPosition.Value +":    "+ ent.Position);
                        foundentities.Add(ent.EntityId, t);
                    }
                }
                
            }
            cameraIndex++;
        }

        private void TransmitMessage(String destination, String message)
        {
                IGC.SendBroadcastMessage(destination, message, TransmissionDistance.TransmissionDistanceMax);
                //L.Debug("Transmiting: " + message);
        }
        protected void ParseMessage(string argument, bool selfCalled = false)
        {
            try
            {
                if (argument == null)
                    return;

                var pm = communicationSystems.ParseMessage(argument);

                if (ParsedMessage.MaxNumBounces < pm.NumBounces && !selfCalled && pm.MessageType != MessageCode.PingEntity)
                {
                    pm.NumBounces++;
                    //LOG.Debug("Bounced Message");
                    communicationSystems.SendMessage(pm.ToString());
                }

                //switch (pm.MessageType)
                //{
                //   // case MessageCode.PingEntity:
                //        //if its a new point of intrest, sync the position with all ships.

                //    //    if (!pm.Type.Trim().ToLower().Contains("planet"))// && trackingSystems.UpdatePlanetData(pm, selfCalled))
                //    //    {
                //    //        var ent = new TrackedEntity(pm, log);
                //    //        trackingSystems.UpdateTrackedEntity(pm, selfCalled);
                //    //    }
                //        //communicationSystems.SendMessage(pm.ToString());


                //            break;
                //}
            }
            catch (Exception e) { log.Error(e.Message); }
        }

        internal PlanetaryData NearestPlanet = null;

        protected void UpdateTrackedTargets()
        {
            try
            {
                trackingSystems.Update();
                NearestPlanet = trackingSystems.GetNearestPlanet();
            }
            catch (Exception e) { log.Error("UpdateTrackedTargets " + e.Message); }
        }

        protected void UpdateInfoKey(string name, string value)
        {
            if (shipInfoKeys.Keys.Contains(name))
                shipInfoKeys.Remove(name);

            shipInfoKeys.Add(name, value);
        }

        protected double GetCargoMass()
        {
            //double mass = 0;
            //List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            //GridTerminalSystem.GetBlocks(blocks);
            //for (int i = 0; i < blocks.Count; i++)
            //{
            //    var count = blocks[i].GetInventoryCount(); // Multiple inventories in Refineriers, Assemblers, Arc Furnances.
            //    for (var inv = 0; inv < count; inv++)
            //    {
            //        var inventory = blocks[i].GetInventory(inv);
            //        //if (inventory != null) // null means, no items in inventory.
            //           // mass += (double)inventory.CurrentMass;
            //    }
            //}
            return 0;
        }

        protected int Mass = 0;


        List<String> incomingMessages = new List<String>();
        public List<String> RecieveMessages()
        {
            List<IMyBroadcastListener> listeners = new List<IMyBroadcastListener>();

            // The method argument below is the list we wish IGC to populate with all Listeners we've made.
            // Our Listener will be at index 0, since it's the only one we've made so far.
            IGC.GetBroadcastListeners(listeners);
            incomingMessages.Clear();

            if (listeners[0].HasPendingMessage)
            {
                // Let's create a variable for our new message. 
                // Remember, messages have the type MyIGCMessage.
                MyIGCMessage message = new MyIGCMessage();

                // Time to get our message from our Listener (at index 0 of our Listener list). 
                // We do this with the following method:
                message = listeners[0].AcceptMessage();

                if (message.Data != null)
                {
                    // A message is a struct of 3 variables. To read the actual data, 
                    // we access the Data field, convert it to type string (unboxing),
                    // and store it in the variable messagetext.
                    string messagetext = message.Data.ToString();

                    // We can also access the tag that the message was sent with.
                    string messagetag = message.Tag;

                    //Here we store the "address" to the Programmable Block (our friend's) that sent the message.
                    long sender = message.Source;

                    //Do something with the information!
                    incomingMessages.Add(messagetext);
                }
            }
            return incomingMessages;
        }

        //////
    }
}
