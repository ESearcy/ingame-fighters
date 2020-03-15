using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRageMath;
using SEMod.INGAME.classes.model;
using System.Text;

namespace SEMod.INGAME.classes
{
    //////
    public class FleetMessage
    {
        Dictionary<String, String> messageElements = new Dictionary<string, string>();


        public FleetMessage(String message, Logger log)
        {
            ParseMessage(message);
        }

        public FleetMessage()
        {
        }

        public void Set(string key, string value)
        {
            messageElements.Add(key, value);
        }

        public void Set(string key, Vector3D value)
        {
            var str = "" + value.X + ":" + value.Y + ":" + value.Z;
            messageElements.Add(key, str);
        }

        public Vector3D GetVector(string key)
        {
            if (messageElements.ContainsKey(key))
            {
                var xyz = messageElements[key].Split(':');
                return new Vector3D(double.Parse(xyz[0]), double.Parse(xyz[1]), double.Parse(xyz[2]));
            }
            return Vector3D.Zero;
        }

        internal static string CreateOrder(DroneOrder m, long commandID)
        {
            //throw new NotImplementedException();
            return "create order mock";
        }

        public void Set(string key, int value)
        {
            messageElements.Add(key, ""+value);
        }
        public int GetInt(string key)
        {
            return int.Parse(messageElements[key]);
        }

        public void Set(string key, long value)
        {
            messageElements.Add(key, "" + value);
        }

        public void Set(string key, double value)
        {
            messageElements.Add(key, "" + value);
        }
        public void Set(string key, bool value)
        {
            if(value)
                messageElements.Add(key, "" + 1);
            else
                messageElements.Add(key, "" + 0);
        }

        public double GetDouble(string key)
        {
            return double.Parse(messageElements[key]);
        }

        public void Set(string key, float value)
        {

                messageElements.Add(key, "" + value);
        }

        public float GetFloat(string key)
        {
            return float.Parse(messageElements[key]);
        }

        public long GetLong(string key)
        {
            return long.Parse(messageElements[key]);
        }

        public bool GetBool(string key)
        {
            var val = int.Parse(messageElements[key]);
            return val == 0 ? false : true;
        }

        public string Get(string key)
        {
            return messageElements[key];
        }

        public override String ToString()
        {
            System.Text.StringBuilder mapAsString = new System.Text.StringBuilder("");
            foreach (string key in messageElements.Keys)
            {
                mapAsString.Append(key + "=" + messageElements[key] + ",");
            }
            mapAsString.Remove(mapAsString.Length - 1, 1);
            return mapAsString.ToString();
        }

        public void ParseMessage(String message)
        {
            var kvs = message.Split(',');
            System.Text.StringBuilder mapAsString = new System.Text.StringBuilder("{");
            foreach (string line in kvs)
            {
                var kv = line.Split('=');
                messageElements.Add(kv[0], kv[1]);
            }
        }

        internal static string CreateDroneAssignmentMessage(long entityId)
        {
            var message = new FleetMessage();
            message.Set("cmd_id", entityId);
            message.Set("type", "assignment");

            return message.ToString();
        }


        internal static string CreateDroneUpdateMessage(String name, long entityId,
            long commandShipEntity,
            bool docked,
            int message_id,
            double health,
            Vector3D speed,
            Vector3D position,
            float gridSize,
            int currentInvVolume,
            int maxInvVolume,
            int merge,
            int connect,
            int drills,
            int sensors,
            int cameras,
            int gat,
            int rockets,
            int reactors,
            int batteries,
            double curPower,
            double maxPower,
            bool isRegistration){

            var message = new FleetMessage();
            message.Set("name", name);
            message.Set("s_id", entityId);
            message.Set("cmd_id", commandShipEntity);
            message.Set("docked", docked);
            message.Set("m_id", message_id);
            message.Set("hp", health);
            message.Set("vel", speed);
            message.Set("loc", position);
            message.Set("size", gridSize);
            message.Set("c_inv", currentInvVolume);
            message.Set("m_inv", maxInvVolume);
            message.Set("merge", merge);
            message.Set("connect", connect);
            message.Set("sensors", sensors);
            message.Set("drills", drills);
            message.Set("cameras", cameras);
            message.Set("gun", gat);
            message.Set("rockets", rockets);
            message.Set("reactors", reactors);
            message.Set("batteries", batteries);
            message.Set("c_pow", curPower);
            message.Set("m_pow", maxPower);
            message.Set("is_registration", isRegistration);

            return message.ToString();
        }
    }
    //////
}
