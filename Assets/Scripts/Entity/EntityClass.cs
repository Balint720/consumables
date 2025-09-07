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

    // Type of movement used by entity
    public enum MoveType
    {
        WALK,
        WALKWITHJUMP,
        FLY
    }

    public MoveType moveType;

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
    public float FlySpeedCap;
    protected float FlySpeedCapMultiplier;
    public float FlyAccel;
    protected float FlyAccelMultiplier;
    public float grav;                              // Gravity effect
    protected Vector3 knockback;                    // Knockback vector
    protected float knockbackMod;                   // Knockback multiplier
    protected Vector3 moveVect;                     // Movement input vector
    public float turnSpeedRatio;                    // Turn speed ratio: how fast the character turns
    protected float turnSpeedMod;                   // Turn speed modifier
    public float distanceOfGroundCheck;
    public float stepHeight;
    static float rampMaxAngle = 30.0f;

    // Current
    Vector3 speed;                        // Speed vector
    Vector3 accel;
    protected Vector2 rotation;                     // Rotation vector
    protected Quaternion rotationQuat;              // Rotation quaternion

    // Extra tags
    protected List<String> extraTags;

    // Unity components
    protected Rigidbody rigBod;
    RaycastHit groundHitInfo;
    GameObject groundObj;
    struct CollisionInfoStruct
    {
        public Vector3 point;
        public Vector3 normal;
        public CollisionInfoStruct(Vector3 i_point, Vector3 i_normal)
        {
            point = i_point;
            normal = i_normal;
        }
    }
    Dictionary<GameObject, CollisionInfoStruct> collisionInfo;

    protected BoxCollider envColl;                              // Rigid body environment hitbox
    protected Transform modelTrans;                             // Transform of model
    protected Dictionary<string, Transform> modelChildTrans;    // Transforms of parts of model
    protected Dictionary<string, Collider> hitboxes;

    // Static variables
    protected static float maxRotationDegreesBeforeMove = 10.0f;
    protected static float knockbackOnDeathMult = 10.0f;

    // Character movement state
    protected enum State
    {
        GROUNDED,
        AIRBORNE
    };

    protected State movState;

    // Debug
    Vector3 nextPhysicsFramePosition;

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
        accel = Vector3.zero;
        rotation = Vector2.zero;
        rotationQuat = Quaternion.identity;
        movState = State.AIRBORNE;
        HSpeedCapMultiplier = 1.0f;
        VSpeedCapMultiplier = 1.0f;
        HAccelMultiplier = 1.0f;
        VAccelMultiplier = 1.0f;
        FlySpeedCapMultiplier = 1.0f;
        FlyAccelMultiplier = 1.0f;
        groundObj = gameObject;
        collisionInfo = new Dictionary<GameObject, CollisionInfoStruct>();

        // Set up rigidbody in case it is set wrong in editor
        rigBod.isKinematic = false;
        rigBod.detectCollisions = true;
        rigBod.freezeRotation = true;
        rigBod.constraints = RigidbodyConstraints.FreezeRotation;
    }

    protected virtual void FixedUpdate()
    {
        switch (moveType)
        {
            case MoveType.WALK:
            case MoveType.WALKWITHJUMP:
                CalcMovementAccelerationGrounded();
                break;
            case MoveType.FLY:
                CalcMovementAccelerationFlying();
                break;
        }
        RotateModel();
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
        Vector3 pointOfGround = Vector3.zero;

        // Check if ground beneath
        if (collisionInfo.ContainsKey(groundObj))
        {
            movState = State.GROUNDED;

            normalOfGround = collisionInfo[groundObj].normal;
            pointOfGround = collisionInfo[groundObj].point;
            forward = Quaternion.AngleAxis(90.0f - Mathf.Rad2Deg * Mathf.Acos(Vector3.Dot(forward, normalOfGround)), right) * forward;
            right = Vector3.Cross(normalOfGround, forward);
        }
        else
        {
            if (rigBod.SweepTest(new Vector3(0.0f, -1.0f, 0.0f), out RaycastHit groundCheckHitInfo, distanceOfGroundCheck, QueryTriggerInteraction.Ignore))
            {
                movState = State.GROUNDED;

                normalOfGround = groundCheckHitInfo.normal;
                pointOfGround = groundCheckHitInfo.point;
                forward = Quaternion.AngleAxis(90.0f - Mathf.Rad2Deg * Mathf.Acos(Vector3.Dot(forward, normalOfGround)), right) * forward;
                right = Vector3.Cross(normalOfGround, forward);
            }
            else
            {
                movState = State.AIRBORNE;
            }
        }

        // Horizontal Movement
        Vector3 inputDir = moveVect.z * forward + moveVect.x * right;

        float forwardsSpeed = Vector3.Dot(rigBod.linearVelocity, forward);
        float sideSpeed = Vector3.Dot(rigBod.linearVelocity, right);

        Vector3 horSpeed = forwardsSpeed * forward + sideSpeed * right;

        Vector3 velChange = inputDir * HSpeedCap * HSpeedCapMultiplier - horSpeed;

        if (velChange.sqrMagnitude > Mathf.Pow(HAccel * HAccelMultiplier, 2))
        {
            accel = velChange.normalized * HAccel * HAccelMultiplier;
        }
        else
        {
            accel = velChange;
        }


        Vector3 currGravVec = new Vector3(0.0f, -grav, 0.0f);

        switch (movState)
        {
            case State.GROUNDED:
                if (moveVect.y > 0.2 && moveType == MoveType.WALKWITHJUMP)
                {
                    rigBod.linearVelocity = new Vector3(rigBod.linearVelocity.x, VSpeedCap * VSpeedCapMultiplier, rigBod.linearVelocity.z);
                }

                currGravVec = normalOfGround * (-1) * grav;
                break;
            case State.AIRBORNE:
                break;
        }

        // Step up on small steps
        if (movState == State.GROUNDED && collisionInfo.ContainsKey(groundObj))
        {
            nextPhysicsFramePosition = rigBod.position + (rigBod.linearVelocity + accel) * Time.fixedDeltaTime;
            if (Physics.BoxCast(nextPhysicsFramePosition + Vector3.up * 2.0f, envColl.size / 2.1f, Vector3.down, out RaycastHit rhit, transform.rotation, 30.0f, LayerMask.GetMask("Obstacle"), QueryTriggerInteraction.Ignore))
            {
                float angle = Vector3.SignedAngle(rhit.normal, new Vector3(0.0f, 1.0f, 0.0f), Vector3.Cross(rhit.normal, new Vector3(0.0f, 1.0f, 0.0f)));
                Vector3 d = rhit.point - pointOfGround;

                if (d.y <= stepHeight && d.y > 0.03f && (angle < rampMaxAngle))
                {
                    rigBod.MovePosition(rigBod.position + Vector3.up * (d.y * 1.1f));
                }
            }
        }

        // Remove forces that are just going into collided objects
        foreach (KeyValuePair<GameObject, CollisionInfoStruct> v in collisionInfo)
        {
            Vector3 accComponent = Mathf.Clamp(Vector3.Dot(accel, v.Value.normal), Mathf.NegativeInfinity, 0.0f) * v.Value.normal;
            Vector3 gravComponent = Mathf.Clamp(Vector3.Dot(currGravVec, v.Value.normal), Mathf.NegativeInfinity, 0.0f) * v.Value.normal;
            accel -= accComponent;
            currGravVec -= gravComponent;
        }

        // Apply movement
        rigBod.AddForce(accel, ForceMode.VelocityChange);
        rigBod.AddForce(currGravVec, ForceMode.Acceleration);
    }

    /// <summary>
    /// Grounded movement calculation with unity's Rigbody physics
    /// Use this one, the other movement calculations are just kept for memory :)
    /// </summary>
    /// <param name="rotateForwardAndRight">Should forward and right directions be rotated around Y axis with rotation value (use for players)</param>
    protected void CalcMovementAccelerationFlying(bool rotateForwardAndRight = false)
    {
        movState = State.AIRBORNE;

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        Vector3 up = transform.up;

        if (rotateForwardAndRight)
        {
            forward = Quaternion.Euler(0, rotation.y, 0) * transform.forward;
            right = Quaternion.Euler(0, rotation.y, 0) * transform.right;
        }

        // Movement
        Vector3 inputDir = moveVect.z * forward + moveVect.x * right + moveVect.y * up;

        Vector3 velChange = inputDir * FlySpeedCap * FlySpeedCapMultiplier - rigBod.linearVelocity;

        if (velChange.sqrMagnitude > Mathf.Pow(FlyAccel * FlyAccelMultiplier, 2))
        {
            accel = velChange.normalized * FlyAccel * FlyAccelMultiplier;
        }
        else
        {
            accel = velChange;
        }

        // Remove forces that are just going into collided objects
        foreach (KeyValuePair<GameObject, CollisionInfoStruct> v in collisionInfo)
        {
            Vector3 vComponent = Mathf.Clamp(Vector3.Dot(accel, v.Value.normal), Mathf.NegativeInfinity, 0.0f) * v.Value.normal;
            accel -= vComponent;
        }

        DebugText.text += "\n Inputdir: " + inputDir;
        DebugText.text += "\n VelChange: " + velChange;

        // Apply movement
        rigBod.AddForce(accel, ForceMode.VelocityChange);
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

    public Vector3 GetSpeed()
    {
        return speed;
    }

    protected virtual void OnCollisionEnter(Collision cInfo)
    {
        ContactPoint c = cInfo.GetContact(0);
        if (cInfo.collider.gameObject.layer == LayerMask.NameToLayer("Obstacle") && c.thisCollider == envColl)
        {
            if (!collisionInfo.ContainsKey(cInfo.gameObject))
            {
                Vector3 n = c.normal;
                collisionInfo.Add(cInfo.gameObject, new CollisionInfoStruct(c.point, c.normal));
                // Check if this collision is ground
                float angle = Vector3.SignedAngle(n, new Vector3(0.0f, 1.0f, 0.0f), Vector3.Cross(n, new Vector3(0.0f, 1.0f, 0.0f)));
                if (angle < rampMaxAngle)                                  // Max degree for it to still count as ground (can limit ramp angle this way)
                {
                    groundObj = cInfo.gameObject;
                }
            }
        }
    }

    protected virtual void OnCollisionExit(Collision cInfo)
    {
        if (collisionInfo.ContainsKey(cInfo.gameObject))
        {
            collisionInfo.Remove(cInfo.gameObject);
        }
    }

    protected virtual void OnCollisionStay(Collision cInfo)
    {
        ContactPoint c = cInfo.GetContact(0);
        if (collisionInfo.ContainsKey(cInfo.gameObject))
        {
            collisionInfo[cInfo.gameObject] = new CollisionInfoStruct(c.point, c.normal);
            float angle = Vector3.SignedAngle(c.normal, new Vector3(0.0f, 1.0f, 0.0f), Vector3.Cross(c.normal, new Vector3(0.0f, 1.0f, 0.0f)));
            if (angle < rampMaxAngle)                                  // Max degree for it to still count as ground (can limit ramp angle this way)
            {
                groundObj = cInfo.gameObject;
            }
        }
        else
        {
            if (cInfo.gameObject.layer == LayerMask.NameToLayer("Obstacle") && c.thisCollider == envColl)
            {
                collisionInfo.Add(cInfo.gameObject, new CollisionInfoStruct(c.point, c.normal));
            }
        }
    }

    protected void RotateModel()
    {
        modelTrans.rotation = Quaternion.RotateTowards(modelTrans.rotation, Quaternion.Euler(0, rotation.y, 0), turnSpeedRatio * Time.fixedDeltaTime);
    }

    // Getters and setters
    public Vector2 GetRotation()
    {
        return new Vector2(rotation.x, rotation.y);
    }

    public void AddRotation(Vector2 rotToAdd)
    {
        rotation += rotToAdd;
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
