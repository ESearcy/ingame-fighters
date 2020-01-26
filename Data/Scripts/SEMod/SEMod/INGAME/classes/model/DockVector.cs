using VRageMath;


namespace SEMod.INGAME.classes.model
{
    //////
    public class DockVector
    {
        public Vector3D Location;
        public bool Reached = false;

        public DockVector(Vector3D pos)
        {
            Location = pos;
        }
    }
    //////
}
