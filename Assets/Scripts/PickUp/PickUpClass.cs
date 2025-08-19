using UnityEngine;

public class PickUpClass : MonoBehaviour
{
    public enum PickUpType : int
    {
        ELECTRIC,
        BLOODBAG,
        GUNPOWDER
    };

    public PickUpType puType;
    public int num;
}
