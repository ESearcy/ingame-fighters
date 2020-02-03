using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game;
using VRageMath;

namespace SEMod.INGAME.classes
{
    //////
    public class TrackedEntity
    {
        public Vector3D Location;
        public Vector3D Velocity;
        public Vector3D AttackPoint;
        public List<PointOfInterest> PointsOfInterest = new List<PointOfInterest>();
        //public List<PointOfInterest> NearestPoints = new List<PointOfInterest>();
        public DateTime LastUpdated;
        public String DetailsString;
        public long EntityID;
        public String name;
        public int Radius;
        Vector3D nearestPoint;
        public MyRelationsBetweenPlayerAndBlock Relationship;
        Logger log;
        public String Type;

        public TrackedEntity(MyDetectedEntityInfo info, Logger log)
        {
            this.log = log;
            LastUpdated = DateTime.Now;
            Location = info.Position;
            Velocity = info.Velocity;
            EntityID = info.EntityId;
            name = info.Name;
            Radius = (int)Math.Abs((info.BoundingBox.Min - info.BoundingBox.Max).Length());
            Relationship = info.Relationship;
            nearestPoint = info.HitPosition.Value;
            Type = info.Type.ToString();
            UpdatePoints(new PointOfInterest(nearestPoint, EntityID));
            //UpdateNearestPoints(new PointOfInterest(pm.AttackPoint, pm.TargetEntityId), Vector3D.Zero);
        }

        public void UpdatePoints(PointOfInterest pointOfInterest)
        {
            PointsOfInterest.Add(pointOfInterest);

            while (PointsOfInterest.Count > 5)
                PointsOfInterest.RemoveAt(0);

        }

        internal Vector3D GetNearestPoint(Vector3D vector3D)
        {
            return PointsOfInterest.OrderBy(x => Math.Abs((vector3D - x.Location).Length())).FirstOrDefault().Location;
        }


    }

    public class PointOfInterest
    {
        public Vector3D Location;
        public DateTime Timestamp = DateTime.Now.AddMinutes(-61);
        public long regionEntityID = 0;
        public bool Reached = false;
        public bool HasPendingOrder = false;
        public bool Mined = false;

        public PointOfInterest(Vector3D Loc, long regionEntityID)
        {
            Location = Loc;
            this.regionEntityID = regionEntityID;
        }
    }

    //////
}
