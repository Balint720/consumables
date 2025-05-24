using UnityEngine;

// Class to be inherited
public class EntityClass : MonoBehaviour
{
    // Movement Variables
    public float HSpeedCap = 10;
    public float VSpeedCap = 10;
    public float HAccel = 40;
    public float grav = 20.0f;
    protected float knockback = 0.0f;
    protected float knockbackMod = 1.0f;
    // Current
    protected Vector3 speed = Vector3.zero;
    protected Vector2 rotation = Vector2.zero;

}
