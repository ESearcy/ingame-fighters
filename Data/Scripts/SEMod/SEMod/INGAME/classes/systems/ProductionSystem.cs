using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI.Ingame;
using SEMod.INGAME.classes.systems;
using SEMod.INGAME.classes.model;

namespace SEMod.INGAME.classes
{
    //////
    public class ProductionSystem : SubSystem
    {
        Logger L;
        ShipComponents shipComponents;
        IMyCubeGrid primaryGrid;
        const float rotationVelocity = 3f;

        public ProductionSystem(Logger log, IMyCubeGrid grid, ShipComponents components)
        {
            L = log;
            shipComponents = components;
            primaryGrid = grid;
            // set primary inventory
        }
    }
    //////
}
