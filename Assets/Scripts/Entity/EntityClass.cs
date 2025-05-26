using UnityEngine;

// Class to be inherited
public class EntityClass : MonoBehaviour
{
    // Stats
    public int maxHP;
    protected int HP;

    // Movement Variables
    public float HSpeedCap = 10;
    public float VSpeedCap = 10;
    public float HAccel = 40;
    public float grav = 20.0f;
    protected Vector3 knockback = Vector3.zero;
    protected float knockbackMod = 1.0f;
    protected Vector3 moveVect;               // Movement input vector
    // Current
    protected Vector3 speed = Vector3.zero;
    protected Vector2 rotation = Vector2.zero;

    // Character Control
    protected CharacterController charCont;

    // Character movement state
    protected enum State
    {
        GROUNDED,
        AIRBORNE
    };

    protected State movState = State.AIRBORNE;

    protected void EntityStart()
    {
        HP = maxHP;

        // Assign components
        charCont = GetComponent<CharacterController>();
    }

    protected void CalcMovementGrounded()
    {
        // Calculate speed
        // Get current forwards and sideways speed
        float forwardsSpeed = Vector3.Dot(charCont.velocity, transform.forward);
        float sideSpeed = Vector3.Dot(charCont.velocity, transform.right);                      // We get these from the real speed of the character because sliding off of walls would make us shoot off them with max speed as soon as we are no longer colliding with them
        float vertSpeed = Vector3.Dot(speed, new Vector3(0, 1, 0));                             // We get this from the "theoretical" speed vector because we want to keep negative speed while on the ground (shitty collision solution for walking down ramps)

        // State on previous frame
        State prevFrame = movState;

        // Set state based on if ground beneath
        if (charCont.isGrounded)
        {
            movState = State.GROUNDED;
        }
        else
        {
            movState = State.AIRBORNE;
        }

        // Modify values based on input
        SpeedCalc(ref forwardsSpeed, moveVect.z);
        SpeedCalc(ref sideSpeed, moveVect.x);

        // Calculate vertical speed based on state
        switch (movState)
        {
            case State.GROUNDED:
                // If we jump, set vertical speed to set value, otherwise, keep it negative
                if (moveVect.y > 0.5)
                {
                    vertSpeed = VSpeedCap;
                }
                else
                {
                    vertSpeed = -10 * grav * Time.fixedDeltaTime;
                }
                break;
            case State.AIRBORNE:
                if (prevFrame == State.GROUNDED && vertSpeed < 0.0f)
                {
                    vertSpeed = 0.0f;
                }
                vertSpeed -= grav * Time.fixedDeltaTime;
                break;
        }

        // Add speeds together
        speed = forwardsSpeed * transform.forward + sideSpeed * transform.right + new Vector3(0, vertSpeed, 0);
        // Add knockback then reset it
        speed += knockback;
        knockback = Vector3.zero;

        // Apply movement
        charCont.Move(speed * Time.fixedDeltaTime);
        transform.rotation = Quaternion.Euler(0, rotation.y, 0);
    }

    protected void SpeedCalc(ref float speedVal, float inputVal)
    {
        // Calculate speed
        if (Mathf.Abs(inputVal) > 0.1)
        {
            speedVal += inputVal * (HAccel * Time.fixedDeltaTime);
            if (Mathf.Abs(speedVal) > HSpeedCap) { speedVal = Mathf.Sign(speedVal) * HSpeedCap; }
        }
        else
        {
            if (Mathf.Abs(speedVal) > 0.5)
            {
                speedVal -= Mathf.Sign(speedVal) * (HAccel * Time.fixedDeltaTime);
            }
            else
            {
                speedVal = 0.0f;
            }
        }
    }

    public void TakeDamageKnockback(int dmg, Vector3 knock)
    {
        Debug.Log("Old HP: " + HP);
        HP -= dmg;
        knockback = knock;

        if (HP <= 0)
        {
            ZeroHP();
        }

        Debug.Log("New HP: " + HP);
    }

    protected void ZeroHP()
    {
        
    }

}
