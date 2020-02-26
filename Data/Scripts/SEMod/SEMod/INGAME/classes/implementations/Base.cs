using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRageMath;
using SEMod.INGAME.classes.systems;
using SEMod.INGAME.classes.model;

namespace SEMod.INGAME.classes.implementations
{
    class Base : AIShipBase
    {
        IMyProgrammableBlock Me = null;
        IMyGridTerminalSystem GridTerminalSystem = null;
        IMyGridProgramRuntimeInfo Runtime;
        

        public Base()
        //////public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            shipComponents = new ShipComponents();
            LocateAllParts();
            SetupFleetListener();

            log = new Logger(Me.CubeGrid, shipComponents);

            //communicationSystems = new CommunicationSystem(log, Me.CubeGrid, shipComponents);
            //navigationSystems = new BasicNavigationSystem(log, Me.CubeGrid, shipComponents);
            //productionSystems = new ProductionSystem(log, Me.CubeGrid, shipComponents);
            //storageSystem = new StorageSystem(log, Me.CubeGrid, shipComponents);
            trackingSystems = new TrackingSystem(log, Me.CubeGrid, shipComponents, true);
            //weaponSystems = new WeaponSystem(log, Me.CubeGrid, shipComponents);

            operatingOrder.AddLast(new TaskInfo(LocateAllParts));
            operatingOrder.AddLast(new TaskInfo(InternalSystemCheck));
            operatingOrder.AddLast(new TaskInfo(ScanLocalArea));
            operatingOrder.AddLast(new TaskInfo(UpdateTrackedTargets));
            operatingOrder.AddLast(new TaskInfo(UpdateDisplays));
            

            maxCameraRange = 5000;
            maxCameraAngle = 80;
            //set new defaults
            hoverHeight = 400;
            InitialBlockCount = shipComponents.AllBlocks.Count();
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }


        protected void Main(String argument, UpdateType updateType)
        {
            try
            {
                if (argument.Length == 0)
                {
                    Update();
                }
                else
                {
                    //IntrepretMessage(argument);
                }
            }
            catch (Exception e)
            {
                log.Error(e.Message);
            }
        }

        protected void UpdateDisplays()
        {
            try
            {
                //UpdateInfoKey("Storage", " Mass: " + navigationSystems.RemoteControl.CalculateShipMass().PhysicalMass + " Max Mass: " + navigationSystems.GetMaxSupportedWeight());
                UpdateInfoKey("Power: ", "Current: " + CurPower + " Max: " + MaxPower);

                if (NearestPlanet != null)
                {
                    log.DisplayShipInfo(shipInfoKeys, "PlanetInfo:  altitude: " + (int)trackingSystems.GetAltitude() + "m");
                    log.UpdateRegionInfo(NearestPlanet.Regions, Me.CubeGrid);
                }
                else
                    log.DisplayShipInfo(shipInfoKeys, " No Planet ");

                //log.UpdateProductionInfo(factorySystems, Me.CubeGrid);
            }
            catch (Exception e) { log.Error("UpdateDisplays " + e.Message); }

            UpdateSystemScreens();

            UpdateAntenna();
        }

        protected void UpdateAntenna()
        {
            foreach (var antenna in shipComponents.RadioAntennas)
            {
                antenna.CustomName = "\nA: " + (int)trackingSystems.GetAltitude();
            }
        }
        //////
    }
}
