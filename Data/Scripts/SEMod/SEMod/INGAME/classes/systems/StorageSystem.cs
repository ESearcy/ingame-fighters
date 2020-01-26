using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI.Ingame;

namespace SEMod.INGAME.classes.systems
{
    //////
    public class StorageSystem
    {
        private Logger log;
        private IMyCubeGrid cubeGrid;
        private ShipComponents shipComponets;
        public double MaxCargoSpace = 0;
        public double CurrentCargoSpace = 0;
        public double CargoWeight = 0;
        public Dictionary<String, long> InventoryItems = new Dictionary<String, long>();

        public StorageSystem(Logger log, IMyCubeGrid cubeGrid, ShipComponents shipComponets)
        {
            this.log = log;
            this.cubeGrid = cubeGrid;
            this.shipComponets = shipComponets;
        }

        internal bool IsOperational()
        {
            return true;
        }

        internal double GetPercentFull()
        {
            return CurrentCargoSpace / MaxCargoSpace;
        }

        internal double GetInvMultiplier(IMyTerminalBlock theContainer)
        {
            //var inv = theContainer.GetInventory(0);

            //string subtype = theContainer.BlockDefinition.SubtypeId;

            //double capacity = (double)inv.MaxVolume;

            ////Echo("name=" + theContainer.DefinitionDisplayNameText + "\'"+ subtype +"'\n" + "maxvol="+capacity.ToString());
            ////log.Debug("capacity: "+capacity);
            //if (capacity < 999999999) return capacity;

            //// else creative; use default 1x capacity
            //if (theContainer is IMyCargoContainer)
            //{
            //    // Keen Large Block
            //    if (subtype.Contains("LargeBlockLargeContainer")) capacity = 421.875008;
            //    else if (subtype.Contains("LargeBlockSmallContainer")) capacity = 15.625;

            //    // Keen Small Block
            //    else if (subtype.Contains("SmallBlockLargeContainer")) capacity = 15.625;
            //    else if (subtype.Contains("SmallBlockMediumContainer")) capacity = 3.375;
            //    else if (subtype.Contains("SmallBlockSmallContainer")) capacity = 0.125;

            //    // Azimuth Large Grid
            //    else if (subtype.Contains("Azimuth_LargeContainer")) capacity = 7780.8;
            //    else if (subtype.Contains("Azimuth_MediumLargeContainer")) capacity = 1945.2;

            //    // Azimuth Small Grid
            //    else if (subtype.Contains("Azimuth_MediumContainer")) capacity = 1878.6;
            //    else if (subtype.Contains("Azimuth_SmallContainer")) capacity = 10.125;
            //}
            //else if (subtype.Contains("SmallBlockDrill")) capacity = 3.375;
            //else if (subtype.Contains("LargeBlockDrill")) capacity = 23.4375;
            //else if (subtype.Contains("ConnectorMedium")) capacity = 1.152; // sg connector
            //else if (subtype.Contains("ConnectorSmall")) capacity = 0.064; // sg ejector
            //else if (subtype.Contains("Connector")) capacity = 8.000; // lg connector
            //else if (subtype.Contains("LargeShipWelder")) capacity = 15.625;
            //else if (subtype.Contains("LargeShipGrinder")) capacity = 15.625;
            //else if (subtype.Contains("SmallShipWelder")) capacity = 3.375;
            //else if (subtype.Contains("SmallShipGrinder")) capacity = 3.375;
            //else
            //{
            //    log.Error("Unknown cargo for default Capacity:" + theContainer.DefinitionDisplayNameText + ":" + theContainer.BlockDefinition.SubtypeId);
            //    capacity = 12;
            //}
            return 0;//;(double)inv.MaxVolume / capacity;

        }
        double invMultiplier = 1;
        public void UpdateStats()
        {
            var invBlocks = shipComponets.AllBlocks.Where(x => x.HasInventory && x.CubeGrid == cubeGrid);
            MaxCargoSpace = 0;
            CurrentCargoSpace = 0;
            CargoWeight = 0;

            invMultiplier = 1d;

            if (shipComponets.Connectors.Any())
                invMultiplier = GetInvMultiplier(shipComponets.Connectors.First());
            else
                invMultiplier = GetInvMultiplier(invBlocks.First());

            InventoryItems.Clear();
            //foreach (var block in invBlocks)
            //{
            //    MaxCargoSpace += (double)block.GetInventory(0).MaxVolume;
            //    CurrentCargoSpace += (double)block.GetInventory(0).CurrentVolume;
            //    CargoWeight += (double)block.GetInventory(0).CurrentMass;

            //    foreach (var item in block.GetInventory(0).GetItems())
            //    {
            //        if (InventoryItems.ContainsKey(item.Content.SubtypeId.ToString()))
            //            InventoryItems[item.Content.SubtypeId.ToString()] += item.Amount.ToIntSafe();
            //        else
            //            InventoryItems.Add(item.Content.SubtypeId.ToString(), item.Amount.ToIntSafe());
            //    }
            //}
            //log.Debug(invMultiplier+": inv Multiplier");
        }

        internal double GetWeight()
        {
            return CargoWeight;// / invMultiplier;
        }
    }
    //////
}
