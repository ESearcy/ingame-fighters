﻿using System;
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

        public TrackedEntity(ParsedMessage pm, Logger log)
        {
            this.log = log;
            LastUpdated = DateTime.Now;
            Location = pm.Location;
            Velocity = pm.Velocity;
            EntityID = pm.TargetEntityId;
            name = pm.Name;
            Radius = pm.TargetRadius;
            DetailsString = pm.ToString();
            Relationship = pm.Relationship;
            nearestPoint = pm.AttackPoint;
            Type = pm.Type;
            UpdatePoints(new PointOfInterest(pm.AttackPoint, pm.TargetEntityId));
            //UpdateNearestPoints(new PointOfInterest(pm.AttackPoint, pm.TargetEntityId), Vector3D.Zero);
        }

        public void UpdatePoints(PointOfInterest pointOfInterest)
        {
            PointsOfInterest.Add(pointOfInterest);

            while (PointsOfInterest.Count > 5)
                PointsOfInterest.RemoveAt(0);

        }

        //internal void UpdateNearestPoints(PointOfInterest pointOfInterest, Vector3D droneLocation)
        //{
        //    var furtherAway = NearestPoints.Where(x => Math.Abs((droneLocation - x.Location).Length()) > Math.Abs((droneLocation - pointOfInterest.Location).Length())).ToList();
        //    if (furtherAway.Count() < 1)
        //    {
        //        NearestPoints.Add(pointOfInterest);
        //    }
        //    else if (NearestPoints.Count() == 0)
        //    {
        //        NearestPoints.Add(pointOfInterest);
        //    }

        //    while (NearestPoints.Count > 2)
        //        NearestPoints.RemoveAt(1);
        //}

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
