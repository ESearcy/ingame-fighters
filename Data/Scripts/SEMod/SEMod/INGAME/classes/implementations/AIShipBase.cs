using System;
using System.Collections.Generic;
using System.Linq;
using SEMod.INGAME.classes.systems;
using SEMod.INGAME.classes.model;
using Sandbox.ModAPI.Ingame;

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
        protected ProductionSystem productionSystems;
        protected ShipComponents shipComponents;
        protected StorageSystem storageSystem;
        protected TrackingSystem trackingSystems;
        protected WeaponSystem weaponSystems;

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
                TransmitMessage(message);
        }

        protected void RunNextOperation()
        {
            if (lastOperationIndex == operatingOrder.Count())
                lastOperationIndex = 0;

            long msStart = DateTime.Now.Ticks;
            TaskInfo info = operatingOrder.ElementAt(lastOperationIndex);
            info.CallMethod();

            long msStop = DateTime.Now.Ticks;
            long timeTaken = msStop - msStart;

            info.AddResult(new TaskResult(timeTaken, 0, Runtime.CurrentInstructionCount / 40000, Runtime.CurrentCallChainDepth / 40000));
            lastOperationIndex++;
        }

        protected void InternalSystemScan()
        {
            try
            {
                var cs = communicationSystems.IsOperational();
                //
                var ps = productionSystems.IsOperational();
                var ss = storageSystem.IsOperational();
                var ts = trackingSystems.IsOperational();
                var ws = weaponSystems.IsOperational();

                UpdateInfoKey("WeaponSystems", BoolToOnOff(ws) + "");
                UpdateInfoKey("CommunicationSystems", BoolToOnOff(cs) + "");
                // 
                UpdateInfoKey("TrackingSystems", BoolToOnOff(ts) + "");
                UpdateInfoKey("ProductionSystems", BoolToOnOff(ps) + "");
                UpdateInfoKey("StorageSystem", BoolToOnOff(ss) + "");
                CalculatePower();
                storageSystem.UpdateStats();
            }
            catch (Exception e) { log.Error("InternalSystemScan " + e.Message); }
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

        protected int InitialBlockCount;

        protected double GetHealth()
        {
            return shipComponents.AllBlocks.Count() / InitialBlockCount;
        }

        protected string BoolToOnOff(bool conv)
        {
            return conv ? "Online" : "Offline";
        }

        protected void SensorScan()
        {
            try
            {
                ScanWithSensors();
                ScanWithCameras();
            }
            catch (Exception e) { log.Error("SensorScan " + e.Message); }
        }


        protected void ScanWithSensors()
        {
            var miliseconds = (DateTime.Now - lastReportTime).TotalMilliseconds;
            if (miliseconds >= 1000 / sensorScansPerSecond)
            {
                lastReportTime = DateTime.Now;
                var foundentities = new Dictionary<long, String>();
                foreach (var sensor in shipComponents.Sensors)
                {
                    sensor.DetectEnemy = true;
                    sensor.DetectPlayers = true;
                    sensor.DetectLargeShips = true;
                    sensor.DetectSmallShips = true;
                    sensor.DetectOwner = false;
                    sensor.DetectStations = true;
                    sensor.DetectAsteroids = true;

                    var ent = sensor.LastDetectedEntity;//LastDetectedEntity; 

                    if (ent.EntityId != 0)
                    {
                        String EntityInformation = ParsedMessage.BuildPingEntityMessage(ent, Me.CubeGrid.EntityId, communicationSystems.GetMsgSntCount());
                        //communicationSystems.SendMessage(EntityInformation);
                        if (!foundentities.Keys.Contains(ent.EntityId))
                            foundentities.Add(ent.EntityId, EntityInformation);

                        ParseMessage(EntityInformation, true);
                    }
                }
                foreach (var entity in foundentities)
                    communicationSystems.SendMessage(entity.Value);
            }
        }

        protected int pitch = 0;
        protected int yaw = 0;
        protected int range = 0;
        protected int maxCameraRange = 2000;
        protected int maxCameraAngle = 90;

        protected void ScanWithCameras()
        {
            var foundentities = new Dictionary<long, String>();
            foreach (var camera in shipComponents.Cameras)
            {
                var maxAngle = maxCameraAngle;
                //== 0 ? camera.RaycastConeLimit : maxCameraAngle;
                var maxRange = maxCameraRange;
                //== 0? camera.RaycastDistanceLimit: maxCameraRange;
                if (!camera.EnableRaycast)
                    camera.EnableRaycast = true;

                var timeToScan = camera.TimeUntilScan(range);

                if (timeToScan <= 0)
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
                        range -= 500;
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
                        String EntityInformation = ParsedMessage.BuildPingEntityMessage(ent, Me.CubeGrid.EntityId, communicationSystems.GetMsgSntCount());
                        ParseMessage(EntityInformation, true);
                        //log.Debug("Entity Found: "+ ent.Type);
                        if (!foundentities.Keys.Contains(ent.EntityId))
                            foundentities.Add(ent.EntityId, EntityInformation);
                    }
                }
            }
            foreach (var entity in foundentities)
                communicationSystems.SendMessage(entity.Value);
        }

        private void TransmitMessage(String message)
        {
                IGC.SendBroadcastMessage("fleet", message, TransmissionDistance.TransmissionDistanceMax);
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

                switch (pm.MessageType)
                {
                    case MessageCode.PingEntity:
                        //if its a new point of intrest, sync the position with all ships.
                        if (pm.Type.Trim().ToLower().Contains("planet") && trackingSystems.UpdatePlanetData(pm, selfCalled))
                            communicationSystems.SendMessage(pm.ToString());
                        else
                            trackingSystems.UpdateTrackedEntity(pm, selfCalled);

                        break;
                }
            }
            catch (Exception e) { log.Error(e.Message); }
        }

        internal PlanetaryData NearestPlanet = null;

        protected void UpdateTrackedTargets()
        {
            try
            {
                log.DisplayTargets(trackingSystems.getTargets());
                trackingSystems.Update();
                NearestPlanet = trackingSystems.GetNearestPlanet();
                //log.Debug("local Planet region count: "+NearestPlanet.Regions.Count());
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
