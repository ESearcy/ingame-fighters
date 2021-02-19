using VRage.Game.ModAPI.Ingame;
using VRageMath;


namespace SEMod.INGAME.classes.model
{
    //////
    public class SubSystem
    {
        public IMyInventory primary_inventory;
        public bool isConnected(IMyInventory inv)
        {
            if(inv!=null && primary_inventory!=null)
                return inv.IsConnectedTo(primary_inventory);

            return false;
        }
    }
    //////
}
