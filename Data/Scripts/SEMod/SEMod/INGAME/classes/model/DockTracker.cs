using System;
using Sandbox.ModAPI.Ingame;

namespace SEMod.INGAME.classes
{
    //////
    public class Docktracker
    {
        public DateTime TimeConnected = DateTime.Now;
        public IMyShipConnector Connector;
        public DroneInfo DroneInfo;

        public Docktracker(IMyShipConnector connector, DroneInfo di)
        {
            DroneInfo = di;
            Connector = connector;
        }
    }
    //////
}
