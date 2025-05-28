using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Class to be inherited
public class EntityClass : MonoBehaviour
{
    public TextMeshProUGUI DebugText;

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
    public float turnSpeedRatio;
    protected float turnSpeedMod;
    // Current
    protected Vector3 speed = Vector3.zero;
    protected Vector2 rotation = Vector2.zero;

    protected List<String> extraTags;

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
        // Instantiate
        extraTags = new List<String>();

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

    protected void CalcMovementGrounded(Vector3 nextPos, Quaternion rot, bool doJump = false)
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
        Vector3 dirWhole = nextPos - transform.position;
        Vector3 dir = dirWhole.normalized;

        SpeedCalc(ref forwardsSpeed, Vector3.Dot(dir, transform.forward));
        SpeedCalc(ref sideSpeed, Vector3.Dot(dir, transform.right));

        // Calculate vertical speed based on state
        switch (movState)
        {
            case State.GROUNDED:
                // If we jump, set vertical speed to set value, otherwise, keep it negative
                if (doJump)
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

        /*
        Vector3 horizSpeed = new Vector3(speed.x, 0, speed.z);
        
        float brakeDist = (horizSpeed.magnitude * horizSpeed.magnitude) / (2 * HAccel);
        if (distance <= brakeDist)
        {
            Vector3 newHorizSpeed = horizSpeed - (horizSpeed.normalized * HAccel * Time.fixedDeltaTime);
            if (Vector3.Dot(horizSpeed, newHorizSpeed) < 0.0)
            {
                Debug.Log("Vector zeroed out because of sign switch");
                speed = Vector3.zero + new Vector3(0, speed.y, 0);
            }
            else
            {
                speed = newHorizSpeed + new Vector3(0, speed.y, 0);
            }
            
        }
        */

        // Add knockback then reset it
        speed += knockback;
        knockback = Vector3.zero;

        // Apply movement
        charCont.Move(speed * Time.fixedDeltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, turnSpeedRatio * Time.fixedDeltaTime);

        DebugText.text = speed.ToString();
    }

    protected void SpeedCalc(ref float speedVal, float inputVal)
    {
        // Calculate speed
        if (Mathf.Abs(inputVal) > 0.2)
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

    public virtual void OnGettingHit(GameObject hitBy)
    {

    }

    protected int AddExtraTag(String tagToAdd)
    {
        if (!extraTags.Contains(tagToAdd))
        {
            extraTags.Add(tagToAdd);
            return 0;
        }
        else
        {
            return -1;
        }
    }

    public bool HasExtraTag(String tagToCheck)
    {
        if (extraTags.Contains(tagToCheck))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

}
