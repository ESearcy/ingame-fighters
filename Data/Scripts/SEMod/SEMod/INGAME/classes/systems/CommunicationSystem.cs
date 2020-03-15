using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using SpaceEngineers.Game.ModAPI.Ingame;
using SEMod.INGAME.classes.systems;
using SEMod.INGAME.classes.model;

namespace SEMod.INGAME.classes
{
    //////
    public class CommunicationSystem
    {
        Dictionary<String,List<String>> PendingMessages = new Dictionary<String, List<String>>();
        private String lastmessageOnHold = null;
        ShipComponents components;
        

        int messagesSent = 0;
        int messagesRecieved = 0;

        public FleetMessage ParseMessage(string argument)
        {
            FleetMessage pm = new FleetMessage(argument, L);

            //L.AddRecievedMessage(argument);
            messagesRecieved++;

            return pm;
        }

        public void SendMessage(string key, string m)
        {
            if (PendingMessages.ContainsKey(key))
                PendingMessages[key].Add(m);
            else
                PendingMessages.Add(key, new List<string> { m });
        }

        public void TransmitOrder(DroneOrder m, long commandID)
        {
            SendMessage(m.TargetEntityID + "", FleetMessage.CreateOrder(m, commandID));
        }

        Logger L;
        IMyCubeGrid grid;

        public CommunicationSystem(Logger l, IMyCubeGrid _grid, ShipComponents componets)
        {
            L = l;
            grid = _grid;
            this.components = componets;
        }      

        public Dictionary<String, List<String>> RetrievePendingMessages()
        {
            return PendingMessages;
        }

        public void EmptyPendingMessages()
        {
            PendingMessages.Clear();
        }

        internal bool IsOperational()
        {
            return components.RadioAntennas.Any();
        }

        //internal void TransmitOrder(Order order, IMyCubeGrid grid)
        //{
        //    L.Debug("OrderType: " + order.Type+""+ grid.EntityId);
        //    var encryptedOrder = FleetMessage.CreateEncryptedOrder(order, grid.EntityId);
        //    SendMessage(encryptedOrder);
        //}

        int numberMessagesSent = 0;
        internal int GetMsgSntCount()
        {
            numberMessagesSent++;
            return numberMessagesSent;
        }
    }
    //////
}
