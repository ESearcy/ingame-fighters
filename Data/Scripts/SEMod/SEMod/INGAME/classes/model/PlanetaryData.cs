using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using SpaceEngineers.Game.ModAPI.Ingame;
using SEMod.INGAME.classes;
using SEMod.INGAME.classes.systems;
using SEMod.INGAME.classes.model;

namespace SEMod.INGAME.classes.model
{
    //////
    public class PlanetaryData
    {
        public Vector3D PlanetCenter;
        public List<Region> Regions = new List<Region>();
        public DateTime LastUpdated;
        private Logger log;

        public PlanetaryData(Logger log, Vector3D planetCenter, Region region, Vector3D detectorLocation)
        {
            this.log = log;
            PlanetCenter = planetCenter;
            LastUpdated = DateTime.Now;
            UpdatePlanetaryData(region, detectorLocation);
        }


        public bool UpdatePlanetaryData(Region region, Vector3D detectorLocation)
        {
            bool addedLocation = false;
            LastUpdated = DateTime.Now;
            var existingRegion = Regions.Where(x => x.EntityId == region.EntityId).FirstOrDefault();
            if (existingRegion != null)
            {
                //log.Debug(" ID's " + existingRegion.EntityId + "  :::  " + region.EntityId);
                //log.Debug("Updating existing Region");
                addedLocation = existingRegion.UpdateRegion(region, detectorLocation);
            }
            else
            {
                log.Debug("Logging New Region");
                addedLocation = true;
                Regions.Add(region);
            }
            return addedLocation;
        }

        public Vector3D GetNearestPoint(Vector3D gridLocation)
        {
            var closestRegions = Regions.OrderBy(x => (x.surfaceCenter - gridLocation).Length()).Take(5)
                .OrderBy(x => (x.GetNearestPoint(gridLocation) - gridLocation).Length());

            if (closestRegions.Any())
                return closestRegions.First().GetNearestPoint(gridLocation);
            // if near a planet... never happens, needed for build
            else return new Vector3D();
        }
    }

    public class Region
    {
        public long EntityId;
        public List<PointOfInterest> PointsOfInterest = new List<PointOfInterest>();
        public List<PointOfInterest> NearestPoints = new List<PointOfInterest>();
        public DateTime LastUpdated;
        public Vector3D PlanetCenter;
        public Vector3D surfaceCenter;
        long timesScanned = 0;
        long minDistBetweenPOI = 150;

        public double GetScanDensity()
        {
            var density = timesScanned / PointsOfInterest.Count();

            return density;
        }

        public bool UpdateRegion(Region region, Vector3D detectorLocation)
        {
            bool updatedLocation = false;
            //there will always be atleast one point in the region thanks to the constructor
            timesScanned++;
            LastUpdated = DateTime.Now;
            var location = region.NearestPoints[0];
            updatedLocation = UpdatePoints(location);
            UpdateNearestPoints(location, detectorLocation);

            return updatedLocation;
        }

        public Region(long EntityId, Vector3D planetLocation, PointOfInterest point, Vector3D detectorLocation)
        {
            this.EntityId = EntityId;
            LastUpdated = DateTime.Now;
            PlanetCenter = planetLocation;
            UpdatePoints(point);
            UpdateNearestPoints(point, detectorLocation);
        }

        public bool UpdatePoints(PointOfInterest pointOfInterest)
        {
            bool added = false;
            var isTooClose = PointsOfInterest.Any(x => Math.Abs((x.Location - pointOfInterest.Location).Length()) < minDistBetweenPOI);

            if (!isTooClose)
            {
                added = true;
                PointsOfInterest.Add(pointOfInterest);

                while (PointsOfInterest.Count > minDistBetweenPOI)
                    PointsOfInterest.RemoveAt(1);

                double x = 0, y = 0, z = 0;
                foreach (var point in PointsOfInterest)
                {
                    x = x + point.Location.X;
                    y = y + point.Location.Y;
                    z = z + point.Location.Z;
                }
                var poiCnt = PointsOfInterest.Count();
                surfaceCenter = new Vector3D(x / poiCnt, y / poiCnt, z / poiCnt);
            }
            return added;
        }

        internal void UpdateNearestPoints(PointOfInterest pointOfInterest, Vector3D droneLocation)
        {
            var furtherAway = NearestPoints.Where(x => Math.Abs((droneLocation - x.Location).Length()) > Math.Abs((droneLocation - pointOfInterest.Location).Length())).ToList();
            if (furtherAway.Count() > 0 || NearestPoints.Count() == 0)
            {
                NearestPoints.Add(pointOfInterest);
            }

            while (NearestPoints.Count > 10)
            {
                NearestPoints = NearestPoints.OrderByDescending(x => Math.Abs((droneLocation - x.Location).Length())).ToList();
                NearestPoints.RemoveAt(0);
            }
        }

        internal Vector3D GetNearestPoint(Vector3D vector3D)
        {
            return NearestPoints.OrderBy(x => Math.Abs((vector3D - x.Location).Length())).FirstOrDefault().Location;
        }

        internal PointOfInterest GetNearestSurveyPoint(Vector3D vector3D)
        {
            return NearestPoints.Where(x => !x.HasPendingOrder && !x.Reached && (DateTime.Now - x.Timestamp).TotalMinutes > 60).OrderBy(x => Math.Abs((vector3D - x.Location).Length())).FirstOrDefault();
        }

        internal double GetPercentReached()
        {
            return PointsOfInterest.Where(x => x.Reached).Count() / PointsOfInterest.Count() * 100;
        }
    }

    //////
}
