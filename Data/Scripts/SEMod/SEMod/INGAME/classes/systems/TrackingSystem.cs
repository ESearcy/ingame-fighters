using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using SEMod.INGAME.classes.model;

namespace SEMod.INGAME.classes.systems
{
    //////
    public class TrackingSystem
    {
        private Logger log;
        private IMyCubeGrid cubeGrid;
        private ShipComponents shipComponets;
        bool iscmd;

        List<TrackedEntity> trackedEntities = new List<TrackedEntity>();
        List<PlanetaryData> KnownPlanets = new List<PlanetaryData>();

        public TrackingSystem(Logger log, IMyCubeGrid cubeGrid, ShipComponents shipComponets, bool iscommand)
        {
            iscmd = iscommand;
            this.log = log;
            this.cubeGrid = cubeGrid;
            this.shipComponets = shipComponets;
        }

        internal bool IsOperational()
        {
            return (shipComponets.Sensors.Count() + shipComponets.Cameras.Count()) > 0;//trackedEntities.Count()>0 || KnownPlanets.Count()>0;
        }

        public void UpdateTrackedEntity(ParsedMessage pm, bool selfcalled)
        {
            TrackedEntity te = trackedEntities.Where(x => x.EntityID == pm.TargetEntityId).FirstOrDefault();

            if (te == null)
            {
                te = new TrackedEntity(pm, log);
                if(!te.name.ToLower().Contains("planet"))
                    trackedEntities.Add(te);
            }

            te.Location = pm.Location;
            te.Velocity = pm.Velocity;
            te.LastUpdated = DateTime.Now;
            te.Radius = pm.TargetRadius;
            te.DetailsString = pm.ToString();
            te.Relationship = pm.Relationship;

            if (pm.AttackPoint != Vector3D.Zero)
            {
                te.UpdatePoints(new PointOfInterest(pm.AttackPoint, 0));
            }
        }

        List<String> idsFound = new List<string>();
        public bool UpdatePlanetData(ParsedMessage pm, bool selfcalled)
        {
            //log.Debug("attempting to update planet data");
            bool locationAdded = false;
            var lastfour = (pm.EntityId + "");
            lastfour = lastfour.Substring(lastfour.Length - 4);
            if (!idsFound.Contains(lastfour + ""))
            {
                idsFound.Add(lastfour + "");
                //log.Debug(lastfour + " Processed" );
            }

            var existingPlanet = KnownPlanets.Where(x => x.PlanetCenter == pm.Location).FirstOrDefault();
            if (existingPlanet != null)
            {
                locationAdded = existingPlanet.UpdatePlanetaryData(new Region(pm.TargetEntityId, pm.Location, new PointOfInterest(pm.AttackPoint, pm.TargetEntityId), cubeGrid.GetPosition(), iscmd), cubeGrid.GetPosition());
                //log.Debug("updated planet data");
            }
            else
            {
                KnownPlanets.Add(new PlanetaryData(log, pm.Location, new Region(pm.TargetEntityId, pm.Location, new PointOfInterest(pm.AttackPoint, pm.TargetEntityId), cubeGrid.GetPosition(), iscmd), cubeGrid.GetPosition()));
                locationAdded = true;
                //log.Debug("Logged New planet discovery: "+ pm.Location);
            }
            return locationAdded;

        }

        PlanetaryData nearestPlanet;
        Vector3D altitude = Vector3D.Zero;

        public PlanetaryData GetNearestPlanet()
        {
            var np = KnownPlanets.OrderBy(y => (y.PlanetCenter - cubeGrid.GetPosition()).Length());
            return np.FirstOrDefault();
        }

        public void Update()
        {
            nearestPlanet = GetNearestPlanet();
            if (nearestPlanet != null)
                altitude = (nearestPlanet.GetNearestPoint(cubeGrid.GetPosition()) - cubeGrid.GetPosition());
        }

        internal double GetAltitude()
        {
            return Math.Abs(altitude.Length());
        }

        internal Vector3D GetAltitudeIncDir()
        {
            return altitude;
        }

        internal List<TrackedEntity> getTargets()
        {
            return trackedEntities;
        }

        internal TrackedEntity GetEntity(long targetEntityID)
        {
            return trackedEntities.Where(x => x.EntityID == targetEntityID).FirstOrDefault();
        }

        public List<TrackedEntity> getCombatTargets(Vector3D point)
        {
            var targetsOfConcern = trackedEntities.Where(x => (x.GetNearestPoint(point) - point).Length() < 3000 && x.Radius > 50 && x.Relationship != MyRelationsBetweenPlayerAndBlock.Owner && (DateTime.Now - x.LastUpdated).TotalMinutes < 5);

            return targetsOfConcern.ToList();
        }

        internal PointOfInterest GetNextMiningSamplePoint(Vector3D point)
        {
            var nearestUncheckedRegions = GetNearestPlanet().Regions
                .OrderBy(x => (x.surfaceCenter - point).Length())
                .Where(x => x.PointsOfInterest.Count(y => y.Mined) < 5);
            if (nearestUncheckedRegions.Any())
            {
                var nearestUncheckedRegion = nearestUncheckedRegions.FirstOrDefault();

                var surveyPoints = nearestUncheckedRegion.PointsOfInterest.Where(x => !x.Mined && !x.HasPendingOrder).OrderBy(x => (x.Location - point).Length());

                return surveyPoints.Any() ? surveyPoints.First() : null;
            }
            return null;
        }

        internal PointOfInterest GetNearestScanPoint(Vector3D point, int maxDistance)
        {
            var needToBeScanned = 
                GetNearestPlanet().Regions.OrderBy(x => (x.surfaceCenter - point).Length()).Take(10)
                .Where(x => x.PointsOfInterest.Any(y => (DateTime.Now - y.Timestamp).TotalMinutes > 20));
            log.Debug("Checking for nuls " + point + "  " + needToBeScanned);
            PointOfInterest retrn = null;
            if (needToBeScanned.Any())
            {
                var nearestUncheckedRegion = needToBeScanned.First();

                var surveyPoints = nearestUncheckedRegion.PointsOfInterest.Where(x => !x.HasPendingOrder && (x.Location - point).Length()<maxDistance);

                var weightedByImportance = surveyPoints.OrderBy(x => (x.Location - point).Length());
                retrn = surveyPoints.Any() ? surveyPoints.First() : null;
                log.Debug((nearestUncheckedRegion != null) + " region found " + needToBeScanned.Count()+"  "+ (retrn!=null));
                retrn.HasPendingOrder = true;
            }
            return retrn;
        }

        internal void UpdateScanPoint(PointOfInterest pointOfIntrest)
        {
            var regions = GetNearestPlanet().Regions.Where(x => x.EntityId == pointOfIntrest.regionEntityID);
            var region = regions.FirstOrDefault();
            //log.Debug("region found: " + (region != null));
            if (region != null)
            {
                var pointOfIntrestToUpdate = region.PointsOfInterest.Where(x => x.Location == pointOfIntrest.Location).FirstOrDefault();
                //log.Debug("pointOfIntrest found: " + (pointOfIntrestToUpdate != null));
                region.PointsOfInterest.Remove(pointOfIntrestToUpdate);
                region.PointsOfInterest.Add(pointOfIntrest);
            }
        }
    }
    //////
}
