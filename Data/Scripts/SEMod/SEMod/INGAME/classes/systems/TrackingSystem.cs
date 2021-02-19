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
        Region currentRegion = null;

        List<TrackedEntity> trackedEntities = new List<TrackedEntity>();
        List<PlanetaryData> KnownPlanets = new List<PlanetaryData>();
        string tracked_targets_logs = "";
        Dictionary<string, string> screen_texts;
        PlanetaryData nearestPlanet;
        Vector3D altitude = Vector3D.Zero;

        public TrackingSystem(Logger log, IMyCubeGrid cubeGrid, ShipComponents shipComponets, bool iscommand)
        {
            iscmd = iscommand;
            this.log = log;
            this.cubeGrid = cubeGrid;
            this.shipComponets = shipComponets;
            screen_texts = new Dictionary<string, string>();
        }

        internal bool IsOperational()
        {
            return (shipComponets.Sensors.Count() + shipComponets.Cameras.Count()) > 0;//trackedEntities.Count()>0 || KnownPlanets.Count()>0;
        }

        public void TrackEntity(TrackedEntity pm, bool selfcalled)
        {
            if (!pm.Name.ToLower().Contains("planet"))
                UpdateTrackedEntity(pm);
            if (pm.Name.ToLower().Contains("planet"))
                UpdateSurfaceLocation(pm);
        }
        public void UpdateTrackedEntity(ParsedMessage pm)
        {
            TrackedEntity target = new TrackedEntity(pm.Location, pm.Velocity, pm.AttackPoint, pm.EntityId, pm.Name, (int)pm.ShipSize, pm.Relationship, pm.AttackPoint, pm.Type, log);
            UpdateTrackedEntity(target);
        }
        public void UpdateTrackedEntity(TrackedEntity pm)
        {
            var id = pm.EntityID;
            TrackedEntity existing = trackedEntities.Where(x => x.EntityID == id).FirstOrDefault();

            if (existing == null)
            {
                existing = pm;
                trackedEntities.Add(pm);

            }

            existing.Location = pm.Location;
            existing.Velocity = pm.Velocity;
            existing.LastUpdated = DateTime.Now;
            existing.Radius = pm.Radius;
            existing.Relationship = pm.Relationship;

            if (pm.AttackPoint != Vector3D.Zero)
            {
                existing.UpdatePoints(new PointOfInterest(pm.AttackPoint, 0));
            }
        }


        public void UpdateSurfaceLocation(TrackedEntity pm)
        {
            //log.Debug("initial location to save: "+ pm.AttackPoint);
            var point = new PointOfInterest(pm.AttackPoint, pm.EntityID);

            //log.Debug(pm.AttackPoint+"");
            PlanetaryData planet = KnownPlanets.FirstOrDefault(x => x.PlanetCenter == pm.Location);
            Region region = null;
            
            region = new Region(pm.EntityID, pm.Location, point, iscmd, cubeGrid.GetPosition(), log);
            if(planet !=null)
                planet.UpdatePlanetaryData(region, cubeGrid.GetPosition());

            //if no planet
            if (planet == null) { 
                planet = CreateNewPlanet(pm.EntityID, pm.Location, region, point, cubeGrid.GetPosition());


                //if point is in current region, update region
                if (currentRegion.EntityId == pm.EntityID)
                {
                    currentRegion.UpdatePoints(point);
                }

                //if point in another region, increase its scan density
                //log.Debug("Number of regions: " + nearestPlanet.Regions.Count);
            }
            if (KnownPlanets.Count > 1)
                nearestPlanet = KnownPlanets.OrderBy(x => (x.PlanetCenter - pm.Location).Length()).FirstOrDefault();
        }

        public void UpdateSurfaceLocation(ParsedMessage pm)
        {
            TrackedEntity target = new TrackedEntity(pm.Location, pm.Velocity, pm.AttackPoint, pm.EntityId, pm.Name, (int)pm.ShipSize, pm.Relationship, pm.AttackPoint, pm.Type, log);
            UpdateSurfaceLocation(target);
        }

        private PlanetaryData CreateNewPlanet(long id, Vector3D loc, Region newregion, PointOfInterest point, Vector3D cubegridLoc)
        {
            PlanetaryData planet = null;
            
            //log.Debug("Logging new planet: "+ KnownPlanets.Count);

            planet = new PlanetaryData(log, loc, newregion, cubegridLoc);

            currentRegion = newregion;
            nearestPlanet = planet;
            KnownPlanets.Add(planet);
            

            return planet;
        }

        public PlanetaryData GetNearestPlanet()
        {
            return nearestPlanet;
        }

        public Vector3D NearestSurfacePoint;
        public void Update()
        {
            if (nearestPlanet != null)
            {
                NearestSurfacePoint = nearestPlanet.GetNearestPoint(cubeGrid.GetPosition());
                altitude = NearestSurfacePoint - cubeGrid.GetPosition();
            }

            long msStop = DateTime.Now.Ticks;
            long timeTaken = msStop - last_slow_update;

            if (timeTaken >= 3000)
            {
                SlowUpdate();
                last_slow_update = msStop;
            }
        }

        long last_slow_update = DateTime.Now.Ticks;

        public void SlowUpdate()
        {
            UpdateTrackedEntitiesScreens();
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
        
        internal Dictionary<string, string> GetScreenInfo()
        {
            //screens:
            screen_texts.Clear();
            screen_texts.Add("tracked_grids", tracked_targets_logs);
            return screen_texts;
        }

        
        private void UpdateTrackedEntitiesScreens()
        {
            //log.Debug("Entities "+ trackedEntities.Count());
            tracked_targets_logs = "ID, Type, Name, Relationship, Distance, Last Updated (s)\n\n";
            foreach (var ent in trackedEntities.OrderBy(x => x.Relationship))
            {
                var near = ent.GetNearestPoint(cubeGrid.GetPosition());
                var distance = (int)(near - cubeGrid.GetPosition()).Length();
                var lastfour = (ent.EntityID + "");
                lastfour = lastfour.Substring(lastfour.Length - 4);
                var record = lastfour + "," + ent.Type + "," + ent.Name + "," + ent.Relationship + "," + (int)distance + "m," + (int)(DateTime.Now - ent.LastUpdated).TotalSeconds;
                //log.Debug(record);
                tracked_targets_logs = tracked_targets_logs + record + "\n";
            }
        }

        public List<TrackedEntity> getCombatTargets(Vector3D point)
        {
            var targetsOfConcern = trackedEntities.Where(x => (x.GetNearestPoint(point) - point).Length() < 3000 && x.Radius > 50 && x.Relationship != MyRelationsBetweenPlayerAndBlock.Owner && (DateTime.Now - x.LastUpdated).TotalMinutes < 5);

            return targetsOfConcern.ToList();
        }

        internal PointOfInterest GetNextMiningSamplePoint(Vector3D point)
        {
            var nearestUncheckedRegions = GetNearestPlanet().Regions
                .OrderBy(x => (x.Value.surfaceCenter - point).Length())
                .Where(x => x.Value.PointsOfInterest.Count(y => y.Mined) < 5);

            if (nearestUncheckedRegions.Any())
            {
                var nearestUncheckedRegion = nearestUncheckedRegions.FirstOrDefault();

                var surveyPoints = nearestUncheckedRegion.Value.PointsOfInterest.Where(x => !x.Mined && !x.HasPendingOrder).OrderBy(x => (x.Location - point).Length());

                return surveyPoints.Any() ? surveyPoints.First() : null;
            }
            return null;
        }

        internal PointOfInterest GetNearestScanPoint(Vector3D point, int maxDistance)
        {
            var needToBeScanned = 
                GetNearestPlanet().Regions.OrderBy(x => (x.Value.surfaceCenter - point).Length()).Take(10)
                .Where(x => x.Value.PointsOfInterest.Any(y => (DateTime.Now - y.Timestamp).TotalMinutes > 20));

            //log.Debug("Checking for nuls " + point + "  " + needToBeScanned);

            PointOfInterest retrn = null;

            if (needToBeScanned.Any())
            {
                var nearestUncheckedRegion = needToBeScanned.First();

                var surveyPoints = nearestUncheckedRegion.Value.PointsOfInterest.Where(x => !x.HasPendingOrder && (x.Location - point).Length()<maxDistance);

                var weightedByImportance = surveyPoints.OrderBy(x => (x.Location - point).Length());
                retrn = surveyPoints.Any() ? surveyPoints.First() : null;
                //log.Debug((nearestUncheckedRegion.Value != null) + " region found " + needToBeScanned.Count()+"  "+ (retrn!=null));
                retrn.HasPendingOrder = true;
            }
            return retrn;
        }

        internal void UpdateScanPoint(PointOfInterest pointOfIntrest)
        {
            
            //log.Debug("region found: " + (region != null));
            if (currentRegion != null)
            {
                var pointOfIntrestToUpdate = currentRegion.PointsOfInterest.Where(x => x.Location == pointOfIntrest.Location).FirstOrDefault();
                //log.Debug("pointOfIntrest found: " + (pointOfIntrestToUpdate != null));
                currentRegion.PointsOfInterest.Remove(pointOfIntrestToUpdate);
                currentRegion.PointsOfInterest.Add(pointOfIntrest);
            }
        }
    }
    //////
}
