using System;
using VRageMath;

namespace SEMod.INGAME.classes
{
    //////
    public enum DroneType
    {
        Miner, Scan, Combat, Unknown
    }
    public class DroneInfo
    {
        public long EntityId;
        public DroneType Type = DroneType.Unknown;
        public Vector3D lastKnownPosition = Vector3D.Zero;
        public Vector3D LastKnownVector = Vector3D.Zero;
        public DateTime lastUpdated = DateTime.Now;
        public String Status = "none";
        public String Name;
        public bool Docked = false;
        public int NumConnectors = 0;
        public int NumDrills = 0;
        public int NumWeapons = 0;
        public int numSensors = 0;
        public double ShipSize = 0;
        public double StorageCurrent = 0;
        public int CameraCount = 0;
        public bool Unloaded = false;
        public double Health = 0;
        public double StorageMax = 0;
        public int Merge = 0;
        public int Guns = 0;
        public int Rockets = 0;
        public int Reactors = 0;
        public int Batteries = 0;
        public double CurrentPower = 0;
        public double MaxPower = 0;
        public long CommanderId;

        public DroneInfo(long id, String name, Vector3D location, Vector3D velocity)
        {
            EntityId = id;
            Name = name;
            lastKnownPosition = location;
            LastKnownVector = velocity;
        }

        public void Update(String name, Vector3D location, Vector3D velocity, bool docked, int cameraCount, double shipsize, int drillcount, int weaponCount, int sensorCount, int connectorCount, double storagecurrent
            , double health, double storagemax, int merge, int guns, int rockets, int reactors, int batteries, double currentpower, double maxpower)
        {
            CameraCount = cameraCount;
            StorageCurrent = storagecurrent;
            Name = name;
            lastKnownPosition = location;
            LastKnownVector = velocity;
            Docked = docked;
            ShipSize = shipsize;
            NumWeapons = weaponCount;
            NumDrills = drillcount;
            numSensors = sensorCount;
            NumConnectors = connectorCount;
            lastUpdated = DateTime.Now;
            Health = health;
            StorageMax = storagemax;
            Merge = merge;
            Guns = guns;
            Rockets = rockets;
            Reactors = reactors;
            Batteries = batteries;
            CurrentPower = currentpower;
            MaxPower = maxpower;
        }
    }
    //////
}
