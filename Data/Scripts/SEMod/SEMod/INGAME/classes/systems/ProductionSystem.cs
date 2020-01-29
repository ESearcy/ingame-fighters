using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI.Ingame;
using SEMod.INGAME.classes.systems;
using SEMod.INGAME.classes.model;

namespace SEMod.INGAME.classes
{
    //////
    public class ProductionSystem
    {
        Logger L;
        public Dictionary<String, Factory> Factories = new Dictionary<String,Factory>();
        ShipComponents shipComponents;
        IMyCubeGrid primaryGrid;
        const float rotationVelocity = 3f;

        public ProductionSystem(Logger log, IMyCubeGrid grid, ShipComponents components)
        {
            L = log;
            shipComponents = components;
            primaryGrid = grid;
        }
        
        public bool IsOperational()
        {
            return Factories.Any();
        }
        


        public void Update()
        {
            foreach (var group in shipComponents.Groups.Where(x=>x.Name.ToLower().Contains("#factory#")))
            {
                if (!Factories.Keys.Contains(group.Name))
                {
                    Factories.Add(group.Name, new Factory(group, L));
                }
                else
                {
                    Factories[group.Name].ConfigureComponents(group);
                }
            }
            UpdateFactoryStates();
        }

        DateTime lastUpdate = DateTime.Now;
        public void UpdateFactoryStates()
        {
            if ((DateTime.Now - lastUpdate).TotalSeconds > 1)
            {
                foreach (var factory in Factories)
                    factory.Value.UpdateFactoryState();

                lastUpdate = DateTime.Now;
            }
        }
        private void Launch(String factoryName)
        {
            if (Factories.Keys.Contains(factoryName))
            {
                Factories[factoryName].Launch();
                L.Debug("Launch successful");
            }
        }

        public void Build(String factoryName, String bpName)
        {
            L.Debug(Factories.Keys.First());
            if (Factories.Keys.Contains(factoryName))
            {
                Factories[factoryName].Start(bpName);
                L.Debug("build successful");
            }

        }

        internal void ManualCommand(string v)
        {
            if (v.Contains("Launch:"))
            {
                L.Debug("launching");
                Launch(v.Replace("Launch:", ""));
            }

            if (v.Contains("Build:"))
            {
                
                var factoryName = v.Replace("Build:", "").Split(':')[0];
                var bpName = v.Replace("Build:", "").Split(':')[1];
                L.Debug("building " +factoryName+""+bpName);

                Build(factoryName, bpName);
            }
        }
    }
    //////
}
