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
        public Dictionary<long, Region> Regions = new Dictionary<long, Region>();
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
            var existingRegion = Regions.Where(x => x.Key == region.EntityId).Select(x=> x.Value).FirstOrDefault();
            if (existingRegion!=null)
            {
               //log.Debug(" ID's " + existingRegion.EntityId + "  :::  " + region.EntityId);
               //log.Debug("Updating existing Region");
               addedLocation = existingRegion.UpdateRegion(region, detectorLocation);
            }
            else
            {
                log.Debug("Logging New Region");
                addedLocation = true;
                Regions.Add(region.EntityId, region);
            }
            return addedLocation;
        }

        public Vector3D GetNearestPoint(Vector3D gridLocation)
        {
            var closestRegions = Regions.OrderBy(x => (x.Value.surfaceCenter - gridLocation).Length()).Take(5)
                .OrderBy(x => (x.Value.GetNearestPoint(gridLocation) - gridLocation).Length());

            if (closestRegions.Any())
                return closestRegions.First().Value.GetNearestPoint(gridLocation);
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
        Logger log;

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

        public Region(long EntityId, Vector3D planetLocation, PointOfInterest point, bool iscommand, Vector3D myship_loc, Logger log)
        {
            this.log = log;
            iscmd = iscommand;
            minDistBetweenPOI = iscmd ? 150 : 30;
            maxSavedPoints = iscmd ? 50 : 15;
            this.EntityId = EntityId;
            LastUpdated = DateTime.Now;
            lastLocation = myship_loc;
            PlanetCenter = planetLocation;
            UpdatePoints(point);
        }

        public bool UpdatePoints(PointOfInterest newpoint)
        {
            bool added = false;
            var tooclosetootherpoints = PointsOfInterest.Any(eachpoint => Math.Abs((eachpoint.Location - newpoint.Location).Length()) < minDistBetweenPOI);
             if (!tooclosetootherpoints)
            {
                if (PointsOfInterest.Count < maxSavedPoints)
                {
                    //log.Debug("tracking new surface location: "+ PointsOfInterest.Count()+"  :  "+ newpoint.regionEntityID);
                    added = true;
                    PointsOfInterest.Add(newpoint);
                }
                else if (PointsOfInterest.Count + 1 >= maxSavedPoints)
                {
                    //log.Debug("tracking new surface location, removing 1");
                    PointsOfInterest.Add(newpoint);
                    PointsOfInterest.RemoveAt(0);
                    added = true;
                }
                else
                    log.Debug("Failed to save location ");

                if (added)
                {
                    if (PointsOfInterest.Count() == 1)
                        surfaceCenter = newpoint.Location;
                    else
                    {
                        var mtplr = PointsOfInterest.Count();
                        var loc = newpoint.Location;
                        var x = surfaceCenter.X + loc.X * mtplr;
                        var y = surfaceCenter.Y + loc.Y * mtplr;
                        var z = surfaceCenter.Z + loc.Z * mtplr;
                        surfaceCenter = new Vector3D(x / mtplr, y / mtplr, z / mtplr);
                    }
                    UpdateNearestPoints(newpoint);
                }
            }
            return added;
        }

        List<PointOfInterest> furtherPoints  = new List<PointOfInterest>();
        private void UpdateNearestPoints(PointOfInterest newpoint) {
            var dis = Math.Abs((newpoint.Location - lastLocation).Length());

            if (nearestPoints.Count == 0)
                nearestPoints.Add(newpoint);
            else if (nearestPoints.Count < maxSavedPoints / 2)
            {
                nearestPoints.Add(newpoint);
            }
            else if (nearestPoints.Count >= maxSavedPoints / 2)
            {
                furtherPoints = nearestPoints.Where(x => (x.Location - lastLocation).Length() > dis).ToList();
                if (furtherPoints.Any())
                {
                    nearestPoints.Add(newpoint);
                    nearestPoints.Remove(furtherPoints.First());
                }
                furtherPoints.Clear();
            }
        }

        Vector3D lastLocation;
        internal Vector3D GetNearestPoint(Vector3D shipLoc)
        {
            lastLocation = shipLoc;
            nearestPoints = nearestPoints.OrderBy(x => (shipLoc - x.Location).Length()).ToList();

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
