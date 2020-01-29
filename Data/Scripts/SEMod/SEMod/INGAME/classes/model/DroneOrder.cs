using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace SEMod.INGAME.classes.model
{
    //////
    public class DroneOrder
    {
        public OrderType Ordertype = OrderType.Unknown;
        public long DroneId;
        public double RequestId;
        public bool Initalized = false;
        public Vector3D PrimaryLocation;
        public Vector3D DirectionalVectorOne;
        public Vector3D ThirdLocation;
        public DateTime IssuedAt;
        public DateTime LastUpdated;
        public long TargetEntityID;
        public bool Confirmed = false;

        public PointOfInterest PointOfIntrest;
        public Vector3D Destination;
        internal IMyShipConnector Connector;
        Logger log;

        public DroneOrder(Logger l,  OrderType type, double requestID, long targetEntityId, long DroneID, Vector3D primaryLocation, Vector3D vectortwo, Vector3D thirdLocation)
        {
            log = l;
            TargetEntityID = targetEntityId;
            RequestId = requestID;
            IssuedAt = DateTime.Now;
            LastUpdated = IssuedAt;
            DroneId = DroneID;
            Ordertype = type;
            PrimaryLocation = primaryLocation;
            DirectionalVectorOne = vectortwo;
            ThirdLocation= thirdLocation;
            DirectionalVectorOne.Normalize();
            ThirdLocation.Normalize();
            Initalize();
            DockRouteIndex = dockroute.Count() - 1;
        }

        internal void Initalize()
        {
            switch (Ordertype)
            {
                case OrderType.Scan:
                    Destination = PrimaryLocation + (-DirectionalVectorOne * 500);
                    break;
                case OrderType.Attack:
                    Destination = PrimaryLocation;
                    break;
                case OrderType.Dock:
                    UpdateDockingCoords();
                    break;
                case OrderType.Mine:
                    UpdateMiningCoords();
                    break;
            }
        }

        
        int dockingDistance = 150;
        public int DockRouteIndex=39;
        public List<Vector3D> dockroute = new List<Vector3D>();
        internal void UpdateDockingCoords()
        {
            dockroute.Clear();
            //log.Debug("setting up dock routes");
            for (int i= 2; i < dockingDistance; i++)
            {
                //log.Debug("Point Added");
                dockroute.Add(PrimaryLocation + (DirectionalVectorOne * i));
            }
        }

        int miningDepth = 20;
        public int MiningIndex = 0;

        internal void UpdateMiningCoords()
        {
            dockroute.Clear();

            dockroute.Add(PrimaryLocation - (DirectionalVectorOne * 10));
            dockroute.Add(PrimaryLocation - (DirectionalVectorOne * 5));
            for (double i = 1; i < miningDepth; i+=.2)
            {
                //log.Debug("Point Added");
                dockroute.Add(PrimaryLocation + (DirectionalVectorOne * i));
            }
        }
    }
    //////
}
