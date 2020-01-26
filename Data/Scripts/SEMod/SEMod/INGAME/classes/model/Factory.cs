using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI;

namespace SEMod.INGAME.classes.model
{
    //////
    public class Factory
    {
        Logger L;
        List<IMyShipWelder> welders = new List<IMyShipWelder>();
        List<IMyProjector> projectors = new List<IMyProjector>();
        List<IMyExtendedPistonBase> pistons = new List<IMyExtendedPistonBase>();
        List<IMyAirtightHangarDoor> doors = new List<IMyAirtightHangarDoor>();
        List<IMyShipMergeBlock> merges = new List<IMyShipMergeBlock>();
        public FactoryState currentState = FactoryState.Unknown;

        const float rotationVelocity = 3f;

        public Factory(IMyBlockGroup group, Logger log)
        {
            L = log;

            ConfigureComponents(group);
            FindCurrentState();
            L.Debug("Group Parts");
        }

        public void ConfigureComponents(IMyBlockGroup group)
        {
            welders.Clear();
            projectors.Clear();
            pistons.Clear();
            doors.Clear();
            merges.Clear();

            group.GetBlocksOfType(welders);
            group.GetBlocksOfType(projectors);
            group.GetBlocksOfType(pistons);
            group.GetBlocksOfType(doors);
            group.GetBlocksOfType(merges);
        }

        public void UpdateFactoryState()
        {
            var pistonSpeed = pistons.Average(x => x.Velocity);
            var pistonExtended = pistons.Any(x => x.CurrentPosition==x.MaxLimit);
            var pistonRetracted = pistons.All(x => x.CurrentPosition == 0);
            var constructionPartial = merges.Any(x => x.IsConnected);
            var weldersOnline = welders.Any(x => x.Enabled);
            var sevenSecondsPassed = (DateTime.Now - LaunchStartTime).TotalSeconds > 7;

            switch (currentState)
            {
                case FactoryState.Unknown:
                    FindCurrentState();
                    break;
                //triggers ready to build
                case FactoryState.Starting:
                    L.Debug(pistonExtended+" Extended");
                    if (pistonExtended)
                        currentState = FactoryState.ReadyToBuild;
                    break;
                //triggers building
                case FactoryState.ReadyToBuild:
                    Build();
                    currentState = FactoryState.Building;
                    break;
                //triggers readyToLaunch
                case FactoryState.Building:
                    if(pistonRetracted && constructionPartial)
                        currentState = FactoryState.ReadyToLaunch;
                    break;
                //calls launching
                case FactoryState.ReadyToLaunch:
                    //Launch Called Remotly
                    break;
                //triggers complete
                case FactoryState.Launching:
                    if (sevenSecondsPassed)
                        currentState = FactoryState.Complete;
                    break;
                case FactoryState.Complete:
                    //Start called remotly
                    break;
            }
        }
        private DateTime LaunchStartTime = DateTime.Now;
        String bPName = "#test#";
        // triggers starting
        public void Start(String bpName)
        {
            bPName = bpName;
            currentState = FactoryState.Starting;
            L.Debug("Extending pistons");
            Extend();
        }

        // triggers starting
        public void Launch()
        {
            currentState = FactoryState.Launching;
            LaunchStartTime = DateTime.Now;
            Release();
        }

        private void FindCurrentState()
        {
            var pistonSpeed = pistons.Average(x=>x.Velocity);
            var pistonExtended = pistons.Any(x=>x.CurrentPosition==x.MaxLimit);
            var pistonRetracted = pistons.All(x => x.CurrentPosition == 0);
            var constructionPartial = merges.Any(x=>x.IsConnected);
            var weldersOnline = welders.Any(x=>x.Enabled);
            var sevenSecondsPassed = (DateTime.Now - LaunchStartTime).TotalSeconds>7;

            if(pistonSpeed > 0)
            {
                if (pistonExtended)
                {
                    currentState = FactoryState.ReadyToBuild;
                }
                else
                currentState = FactoryState.Starting;
            }
            else
            {
                if (pistonRetracted)
                {
                    if (constructionPartial)
                        currentState = FactoryState.ReadyToLaunch;
                    else
                        currentState = FactoryState.Complete;
                }
                else
                    currentState = FactoryState.Building;
            }

        }

        public IMyProjector GetPrimaryProjector()
        {
            var w = TryGetProjectorWithNameContaining(bPName);
            if (w != null)
            {
                w.Enabled = true;
                return w;
            }
            return null;
        }

        private void Build()
        {
            var w = TryGetProjectorWithNameContaining(bPName);
            if (w != null)
            {
                w.Enabled = true;

                foreach (var merge in merges)
                    merge.GetActionWithName("OnOff_On").Apply(merge);

                foreach (var piston in pistons)
                    piston.SetValue<float>("Velocity", -1);

                foreach (var welder in welders)
                    welder.GetActionWithName("OnOff_On").Apply(welder);
            }
        }

        private IMyProjector TryGetProjectorWithNameContaining(String name)
        {
            foreach (var projector in projectors)
                if (projector.CustomName.Contains(name))
                    return projector;
            return null;
        }

        internal bool IsOperational()
        {
            return welders.Any() && projectors.Any() && pistons.Any() && merges.Any();
        }

        public string CompCounts()
        {
            return "W:"+welders.Count()+ " PR:" + projectors.Count() +" PI:" + pistons.Count() +" M:" + merges.Count(); ;
        }

        public void Extend()
        {
            foreach (var piston in pistons)
                piston.SetValue<float>("Velocity", 1);

            foreach (var welder in welders)
                welder.GetActionWithName("OnOff_Off").Apply(welder);

        }

        public void Release()
        {
            foreach (var welder in welders)
                welder.GetActionWithName("OnOff_Off").Apply(welder);

            foreach (var merge in merges)
            {
                merge.GetActionWithName("OnOff_Off").Apply(merge);
                merge.Enabled = false;
               
            }
        }
    }
    //////
}
