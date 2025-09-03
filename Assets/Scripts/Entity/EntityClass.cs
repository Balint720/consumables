using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework.Internal;
using TMPro;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.UIElements;

// Entity
// Class to be inherited
public class EntityClass : MonoBehaviour
{
    public TextMeshProUGUI DebugText;

    // Stats
    public int maxHP;                               // Maximum HP
    protected int HP;                               // Current HP

    // Movement Variables
    public float HSpeedCap;                         // Horizontal speed cap
    protected float HSpeedCapMultiplier;
    public float VSpeedCap;                         // Vertical speed cap (Jump height if grounded)
    protected float VSpeedCapMultiplier;
    public float fallSpeedCap;
    public float HAccel;                            // Horizontal acceleration
    protected float HAccelMultiplier;
    public float VAccel;
    protected float VAccelMultiplier;
    public float grav;                              // Gravity effect
    protected Vector3 knockback;                    // Knockback vector
    protected float knockbackMod;                   // Knockback multiplier
    protected Vector3 moveVect;                     // Movement input vector
    public float turnSpeedRatio;                    // Turn speed ratio: how fast the character turns
    protected float turnSpeedMod;                   // Turn speed modifier
    public float distanceOfGroundCheck;

    // Current
    protected Vector3 speed;                        // Speed vector
    protected Vector2 rotation;                     // Rotation vector
    protected Quaternion rotationQuat;              // Rotation quaternion

    // Extra tags
    protected List<String> extraTags;

    // Unity components
    protected Rigidbody rigBod;
    protected float heightOfEnvColl;
    protected float radiusOfEnvColl;
    RaycastHit groundHitInfo;
    GameObject groundObj;
    Dictionary<GameObject, Vector3> normalOfCollision;

    protected BoxCollider envColl;                              // Rigid body environment hitbox
    protected Transform modelTrans;                             // Transform of model
    protected Dictionary<string, Transform> modelChildTrans;    // Transforms of parts of model
    protected Dictionary<string, Collider> hitboxes;

    // Static variables
    protected static float maxRotationDegreesBeforeMove = 20.0f;
    static float knockbackOnDeathMult = 10.0f;

    // Character movement state
    protected enum State
    {
        GROUNDED,
        AIRBORNE
    };

    protected State movState;

    GameObject SphereDebug;

    protected virtual void Start()
    {
        // Instantiate, assign components
        extraTags = new List<String>();
        rigBod = GetComponent<Rigidbody>();
        envColl = GetComponent<BoxCollider>();
        if (rigBod == null)
        {
            Debug.Log("Entity " + gameObject.name + " doesn't have a Rigidbody");
            gameObject.SetActive(false);
            return;
        }
        if (envColl == null)
        {
            Debug.Log("Entity " + gameObject.name + " doesn't have an environment collider");
            gameObject.SetActive(false);
            return;
        }

        Transform[] transes = GetComponentsInChildren<Transform>();
        Collider[] cols = GetComponentsInChildren<Collider>();

        modelChildTrans = new Dictionary<string, Transform>();
        hitboxes = new Dictionary<string, Collider>();

        for (int i = 0; i < transes.Count(); i++)
        {
            if (transes[i].name != name)
            {
                if (transes[i].name == "Model")
                {
                    modelTrans = transes[i];
                }
                else if (transes[i].IsChildOf(modelTrans))
                {
                    modelChildTrans.Add(transes[i].name, transes[i]);
                }
            }
        }

        for (int i = 0; i < cols.Count(); i++)
        {
            if (cols[i].name != name)
            {
                hitboxes.Add(cols[i].name, cols[i]);
            }
        }

        // Set variables
        HP = maxHP;
        knockback = Vector3.zero;
        knockbackMod = 1.0f;
        moveVect = Vector3.zero;
        turnSpeedMod = 1.0f;
        speed = Vector3.zero;
        rotation = Vector2.zero;
        rotationQuat = Quaternion.identity;
        movState = State.AIRBORNE;
        HSpeedCapMultiplier = 1.0f;
        VSpeedCapMultiplier = 1.0f;
        HAccelMultiplier = 1.0f;
        VAccelMultiplier = 1.0f;
        groundObj = gameObject;
        normalOfCollision = new Dictionary<GameObject, Vector3>();

        // Set up rigidbody in case it is set wrong in editor
        rigBod.isKinematic = false;
        rigBod.detectCollisions = true;
        rigBod.freezeRotation = true;
        rigBod.constraints = RigidbodyConstraints.FreezeRotation;

        SphereDebug = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        SphereDebug.GetComponent<Collider>().enabled = false;
        SphereDebug.GetComponent<Renderer>().enabled = false;
    }

    protected virtual void FixedUpdate()
    {
        CalcMovementAccelerationGrounded();
        RotateModel();
    }

    /// <summary>
    /// Grounded movement calculation based on MoveInput vector
    /// </summary>
    protected void CalcMovementGrounded(bool applyMovRot = true)
    {
        // Calculate speed
        // Get current forwards and sideways speed
        float forwardsSpeed = Vector3.Dot(rigBod.linearVelocity, transform.forward);
        float sideSpeed = Vector3.Dot(rigBod.linearVelocity, transform.right);                      // We get these from the real speed of the character because sliding off of walls would make us shoot off them with max speed as soon as we are no longer colliding with them
        float vertSpeed = Vector3.Dot(rigBod.linearVelocity, new Vector3(0, 1, 0));

        // State on previous frame
        State prevFrame = movState;

        // Set state based on if ground beneath
        if (Physics.Raycast(transform.position - new Vector3(0, envColl.size.y / 2, 0), transform.up * (-1), out groundHitInfo, distanceOfGroundCheck))
        {
            movState = State.GROUNDED;
            SphereDebug.transform.position = groundHitInfo.point;
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
                // If we jump, set vertical speed to set value, otherwise, keep it negative to stick to the ground (ramps, slopes)
                if (moveVect.y > 0.5)
                {
                    vertSpeed = VSpeedCap;
                }
                else
                {
                    vertSpeed = 0.0f;
                    if (groundHitInfo.distance > distanceOfGroundCheck / 2.0f)
                    {
                        rigBod.position = new Vector3(rigBod.position.x, groundHitInfo.point.y + ((envColl.size.y + distanceOfGroundCheck) / 2.0f), rigBod.position.z);
                    }
                }
                break;
            case State.AIRBORNE:
                /*
                // If we were grounded the last frame, then reset vertSpeed if it was negative
                if (prevFrame == State.GROUNDED && vertSpeed < 0.0f)
                {
                    vertSpeed = 0.0f;
                }
                */
                // Apply gravity
                vertSpeed -= grav * Time.fixedDeltaTime;
                break;
        }

        // Add speeds together
        speed = forwardsSpeed * transform.forward + sideSpeed * transform.right;
        speed += new Vector3(0, vertSpeed, 0);

        // Add knockback then reset it
        speed += knockback * knockbackMod;
        knockback = Vector3.zero;

        if (applyMovRot)
        {
            ApplyMoveRot();
        }

    }

    /// <summary>
    /// Grounded movement calculation using the next position and rotation value
    /// </summary>
    /// <param name="nextPos">Position in world space to move to</param>
    /// <param name="rot">Rotation of character</param>
    /// <param name="doJump">Does the character jump?</param>
    protected void CalcMovementGrounded(Vector3 nextPos, Quaternion rot, bool doJump = false, float distanceCap = 0.0f)
    {
        // Get direction from next position and current position
        Vector3 dirWhole = nextPos - transform.position;
        Vector3 dir = dirWhole.normalized;

        // Get input values from direction and doJump variable, set rotation as well
        moveVect.z = Vector3.Dot(dir, transform.forward);
        moveVect.x = Vector3.Dot(dir, transform.right);
        moveVect.y = doJump ? 1.0f : 0.0f;
        rotationQuat = rot;

        float HSpeed = new Vector2(speed.x, speed.z).magnitude;
        float timeToStop = HSpeed / (HAccel * HAccelMultiplier);

        if (dirWhole.sqrMagnitude < Math.Pow(HSpeed * timeToStop - (1 / 2) * HAccel * HAccelMultiplier * timeToStop * timeToStop, 2)
            || dirWhole.sqrMagnitude < distanceCap * distanceCap)
        {
            moveVect.x = 0.0f; moveVect.z = 0.0f;
            //GetComponent<Renderer>().material.color = Color.red;
        }
        else
        {
            //GetComponent<Renderer>().material.color = Color.blue;
        }

        // Calc movement but do not apply it, apply it with different setting
        CalcMovementGrounded();

    }

    /// <summary>
    /// Grounded movement calculation with unity's Rigbody physics
    /// Use this one, the other movement calculations are just kept for memory :)
    /// </summary>
    /// <param name="rotateForwardAndRight">Should forward and right directions be rotated around Y axis with rotation value (use for players)</param>
    protected void CalcMovementAccelerationGrounded(bool rotateForwardAndRight = false)
    {
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        if (rotateForwardAndRight)
        {
            forward = Quaternion.Euler(0, rotation.y, 0) * transform.forward;
            right = Quaternion.Euler(0, rotation.y, 0) * transform.right;
        }

        Vector3 normalOfGround = Vector3.zero;

        // Check if ground beneath
        if (normalOfCollision.ContainsKey(groundObj))
        {
            movState = State.GROUNDED;

            normalOfGround = normalOfCollision[groundObj];
            forward = Quaternion.AngleAxis(90.0f - Mathf.Rad2Deg * Mathf.Acos(Vector3.Dot(forward, normalOfGround)), right) * forward;
            right = Vector3.Cross(normalOfGround, forward);
        }
        else
        {
            if (rigBod.SweepTest(new Vector3(0.0f, -1.0f, 0.0f), out RaycastHit groundCheckHitInfo, distanceOfGroundCheck, QueryTriggerInteraction.Ignore))
            {
                movState = State.GROUNDED;

                normalOfGround = groundCheckHitInfo.normal;
                forward = Quaternion.AngleAxis(90.0f - Mathf.Rad2Deg * Mathf.Acos(Vector3.Dot(forward, normalOfGround)), right) * forward;
                right = Vector3.Cross(normalOfGround, forward);
            }
            else
            {
                movState = State.AIRBORNE;
            }
        }

        // Horizontal Movement
        Vector3 a = Vector3.zero;
        Vector3 inputDir = moveVect.z * forward + moveVect.x * right;

        float forwardsSpeed = Vector3.Dot(rigBod.linearVelocity, forward);
        float sideSpeed = Vector3.Dot(rigBod.linearVelocity, right);

        Vector3 horSpeed = forwardsSpeed * forward + sideSpeed * right;

        Vector3 velChange = inputDir * HSpeedCap * HSpeedCapMultiplier - horSpeed;

        if (velChange.sqrMagnitude > Mathf.Pow(HAccel * HAccelMultiplier, 2))
        {
            a = velChange.normalized * HAccel * HAccelMultiplier;
        }
        else
        {
            a = velChange;
        }


        Vector3 currGravVec = new Vector3(0.0f, -grav, 0.0f);

        switch (movState)
        {
            case State.GROUNDED:

                if (moveVect.y > 0.2)
                {
                    rigBod.linearVelocity = new Vector3(rigBod.linearVelocity.x, VSpeedCap * VSpeedCapMultiplier, rigBod.linearVelocity.z);
                }

                currGravVec = normalOfGround * (-1) * grav;
                break;
            case State.AIRBORNE:
                break;
        }

        // Remove forces that are just going into collided objects
        foreach (KeyValuePair<GameObject, Vector3> v in normalOfCollision)
        {
            Vector3 accComponent = Mathf.Clamp(Vector3.Dot(a, v.Value),Mathf.NegativeInfinity, 0.0f) * v.Value;
            //Vector3 gravComponent = Mathf.Clamp(Vector3.Dot(currGravVec, v.Value),Mathf.NegativeInfinity, 0.0f) * v.Value;
            a -= accComponent;
            //currGravVec -= gravComponent;
        }

        // Apply movement
        rigBod.AddForce(a, ForceMode.VelocityChange);
        rigBod.AddForce(currGravVec, ForceMode.Acceleration);

    }

    /// <summary>
    /// Calculate value to add to speed based on input value
    /// </summary>
    /// <param name="speedVal"></param>
    /// <param name="inputVal"></param>
    protected void SpeedCalc(ref float speedVal, float inputVal)
    {
        // If inputVal is on, then add speed using acceleration, capping it at the speed cap
        if (Mathf.Abs(inputVal) > 0.2)
        {
            speedVal += inputVal * (HAccel * HAccelMultiplier * Time.fixedDeltaTime);
            if (Mathf.Abs(speedVal) > HSpeedCap * HSpeedCapMultiplier) { speedVal = Mathf.Sign(speedVal) * HSpeedCap * HSpeedCapMultiplier; }
        }
        // Otherwise, reduce it until it is below 0.5, at which point set it to 0
        else
        {
            if (Mathf.Abs(speedVal) > 0.5)
            {
                speedVal -= Mathf.Sign(speedVal) * (HAccel * HAccelMultiplier * Time.fixedDeltaTime);
            }
            else
            {
                speedVal = 0.0f;
            }
        }
    }

    /// <summary>
    /// Applies movement based on speed vector and rotation vector/quaternion
    /// </summary>
    /// <param name="quatSlerpRot">If true, will use rotationQuat variable and Slerp between current rotation, if false, only sets rotation to yaw of rotation vector</param>
    protected void ApplyMoveRot(bool isPlayer = false)
    {
        /*
        // Collision check
        RaycastHit[] hitInfo;
        hitInfo = rigBod.SweepTestAll(speed, speed.magnitude * Time.fixedDeltaTime, QueryTriggerInteraction.Ignore);
        if (hitInfo != null)
        {
            for (int i = 0; i < hitInfo.Length; i++)
            {
                Vector3 removeVec = hitInfo[i].normal * (-1) * Vector3.Dot(speed, hitInfo[i].normal * (-1));
                speed -= removeVec;
            }
        }
        */

        rigBod.linearVelocity = speed;

        if (isPlayer)
        {
            rigBod.MoveRotation(Quaternion.Euler(0, rotation.y, 0));
        }
        else
        {
            //transform.rotation = Quaternion.Slerp(transform.rotation, rotationQuat, turnSpeedRatio * Time.fixedDeltaTime);
            rigBod.MoveRotation(rotationQuat);
        }
    }

    /// <summary>
    /// Apply damage and knockback to entity, check if HP is zero
    /// </summary>
    /// <param name="dmg"></param>
    /// <param name="knock"></param>
    public void TakeDamageKnockback(int dmg, Vector3 knock)
    {
        HP -= dmg;
        knockback = knock;

        if (HP <= 0)
        {
            ZeroHP();
        }
    }

    /// <summary>
    /// If entity hits 0 hp, run this function
    /// </summary>
    protected virtual void ZeroHP()
    {
        rigBod.AddForce(knockback * knockbackOnDeathMult, ForceMode.Force);
    }

    public virtual void OnGettingHit(GameObject hitBy)
    {
        
    }

    /// <summary>
    /// Add extra tags to entity
    /// </summary>
    /// <param name="tagToAdd">Tag to add as string</param>
    /// <returns></returns>
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

    /// <summary>
    /// Check if entity has tag
    /// </summary>
    /// <param name="tagToCheck">Tag string to check</param>
    /// <returns></returns>
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

    public Vector3 GetSpeed()
    {
        return speed;
    }

    protected virtual void OnCollisionEnter(Collision cInfo)
    {
        if (!normalOfCollision.ContainsKey(cInfo.gameObject))
        {
            if (cInfo.collider.gameObject.layer == LayerMask.NameToLayer("Obstacle") && cInfo.GetContact(0).thisCollider == envColl)
            {
                Vector3 n = cInfo.GetContact(0).normal;
                normalOfCollision.Add(cInfo.gameObject, cInfo.GetContact(0).normal);
                float angle = Vector3.SignedAngle(n, new Vector3(0.0f, 1.0f, 0.0f), Vector3.Cross(n, new Vector3(0.0f, 1.0f, 0.0f)));
                if (angle < 30.0f)                                  // Max degree for it to still count as ground (can limit ramp angle this way)
                {
                    groundObj = cInfo.gameObject;
                }
            }
        }
    }

    protected virtual void OnCollisionExit(Collision cInfo)
    {
        if (normalOfCollision.ContainsKey(cInfo.gameObject))
        {
            normalOfCollision.Remove(cInfo.gameObject);
        }
    }

    protected virtual void OnCollisionStay(Collision cInfo)
    {
        if (normalOfCollision.ContainsKey(cInfo.gameObject))
        {
            Vector3 n = cInfo.GetContact(0).normal;
            normalOfCollision[cInfo.gameObject] = n;
            float angle = Vector3.SignedAngle(n, new Vector3(0.0f, 1.0f, 0.0f), Vector3.Cross(n, new Vector3(0.0f, 1.0f, 0.0f)));
            if (angle < 30.0f)                                  // Max degree for it to still count as ground (can limit ramp angle this way)
            {
                groundObj = cInfo.gameObject;
            }
        }
        else
        {
            if (cInfo.gameObject.layer == LayerMask.NameToLayer("Obstacle") && cInfo.GetContact(0).thisCollider == envColl)
            {
                normalOfCollision.Add(cInfo.gameObject, cInfo.GetContact(0).normal);
            }
        }
    }

    protected void RotateModel()
    {
        modelTrans.rotation = Quaternion.RotateTowards(modelTrans.rotation, Quaternion.Euler(0, rotation.y, 0), turnSpeedRatio * Time.fixedDeltaTime);
    }

    protected IEnumerator AddRotGradual(Vector2 rotToAdd, int increments)
    {
        Vector2 incVec = rotToAdd / increments;
        for (int i = 0; i < increments; i++)
        {
            rotation += incVec;
            yield return null;
        }
    }
}
