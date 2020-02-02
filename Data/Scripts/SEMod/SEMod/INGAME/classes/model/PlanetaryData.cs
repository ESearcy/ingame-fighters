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
               // log.Debug("Logging New Region");
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
        public List<PointOfInterest> nearestPoints = new List<PointOfInterest>();
        public DateTime LastUpdated;
        public Vector3D PlanetCenter;
        public Vector3D surfaceCenter;
        long timesScanned = 0;
        long minDistBetweenPOI;
        int maxSavedPoints;
        bool iscmd;

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
            var location = region.PointsOfInterest[0];
            updatedLocation = UpdatePoints(location);

            return updatedLocation;
        }

        public Region(long EntityId, Vector3D planetLocation, PointOfInterest point, Vector3D detectorLocation, bool iscommand)
        {
            iscmd = iscommand;
            minDistBetweenPOI = iscmd ? 150 : 30;
            maxSavedPoints = iscmd ? 50 : 15;
            this.EntityId = EntityId;
            LastUpdated = DateTime.Now;
            PlanetCenter = planetLocation;
            UpdatePoints(point);
        }

        public bool UpdatePoints(PointOfInterest pointOfInterest)
        {
            bool added = false;
            var isTooClose = PointsOfInterest.Any(x => Math.Abs((x.Location - pointOfInterest.Location).Length()) < minDistBetweenPOI);

            if (!isTooClose)
            {
                if (PointsOfInterest.Count < maxSavedPoints && iscmd)
                {
                    added = true;
                    PointsOfInterest.Add(pointOfInterest);
                }
                else if (PointsOfInterest.Count + 1 >= maxSavedPoints && !iscmd)
                {
                    PointsOfInterest.Add(pointOfInterest);
                    PointsOfInterest.RemoveAt(0);
                }
                else if (PointsOfInterest.Count + 1 < maxSavedPoints && !iscmd)
                {
                    PointsOfInterest.Add(pointOfInterest);
                }

                if (PointsOfInterest.Count() == 1)
                    surfaceCenter = pointOfInterest.Location;
                else
                {
                    var mtplr = PointsOfInterest.Count();
                    var loc = pointOfInterest.Location;
                    var x = surfaceCenter.X + loc.X * mtplr;
                    var y = surfaceCenter.Y + loc.Y * mtplr;
                    var z = surfaceCenter.Z + loc.Z * mtplr;
                    surfaceCenter = new Vector3D(x / mtplr, y / mtplr, z / mtplr);
                }
                var dis = Math.Abs((pointOfInterest.Location - lastLocation).Length());
                if (nearestPoints.Count == 0 || (lastLocation != null && nearestPoints.Any(x => (x.Location - lastLocation).Length() < dis)))
                    nearestPoints.Add(pointOfInterest);
            }
            return added;
        }

        Vector3D lastLocation;
        internal Vector3D GetNearestPoint(Vector3D vector3D)
        {
            lastLocation = vector3D;
            nearestPoints = nearestPoints.OrderBy(x => (vector3D - x.Location).Length()).ToList();
            if(nearestPoints.Count >5)
                nearestPoints.Remove(PointsOfInterest.LastOrDefault());
            return nearestPoints.FirstOrDefault().Location;
        }

        internal PointOfInterest GetNearestSurveyPoint(Vector3D vector3D)
        {
            return PointsOfInterest.Where(x => !x.HasPendingOrder && (DateTime.Now - x.Timestamp).TotalMinutes > 30).OrderBy(x => Math.Abs((vector3D - x.Location).Length())).FirstOrDefault();
        }

        internal double GetPercentReached()
        {
            return PointsOfInterest.Where(x => x.Reached).Count() / PointsOfInterest.Count() * 100;
        }
    }

    //////
}
