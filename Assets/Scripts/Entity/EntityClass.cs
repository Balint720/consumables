using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using NUnit.Framework.Internal;
using TMPro;
using Unity.Properties;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.UIElements;

public interface Damageable
{
    public int MaxHP
    {
        get;
    }
    public int CurrHP
    {
        get;
    }
    public void TakeDamageKnockback(int dmg, Vector3 knock, GameObject hitBy);
}

// Entity
// Class to be inherited
public abstract class EntityClass : MonoBehaviour, Damageable
{
    public TextMeshProUGUI DebugText;

    // Type of movement used by entity
    protected enum MoveType
    {
        WALK,
        WALKWITHJUMP,
        FLY
    }

    protected MoveType moveType;

    // Stats
    [SerializeField] int maxHP;
    public int MaxHP
    {
        get => maxHP;
        protected set => maxHP = value > 0 ? value : 1;
    }
    int currHP;
    public int CurrHP
    {
        get => currHP;
        protected set => currHP = value > 0 ? value : 0;
    }

    // Movement Variables
    [SerializeField] protected float hSpeedCap;
    [SerializeField] protected float sprintSpeedMult;
    protected float hSpeedCapMultiplier;
    [SerializeField] protected float vSpeedCap;
    protected float vSpeedCapMultiplier;
    [SerializeField] protected float fallSpeedCap;
    [SerializeField] protected float hAccel;
    protected float hAccelMultiplier;
    [SerializeField] protected float vAccel;
    protected float vAccelMultiplier;
    [SerializeField] protected float flySpeedCap;
    protected float flySpeedCapMultiplier;
    [SerializeField] protected float flyAccel;
    protected float flyAccelMultiplier;
    [SerializeField] protected float grav;
    protected Vector3 knockback;
    protected float knockbackMod;
    protected Vector3 moveVect;
    [SerializeField] protected float turnSpeedRatio;
    protected float turnSpeedMod;
    float distanceOfGroundCheck;
    [SerializeField] float stepHeight = 1.0f;
    static float rampMaxAngle { get => 30.0f; }

    // Current
    Vector3 accel;
    Vector3 rotation;
    static float LookVerticalDegLimit { get => 89.0f; }
    public float PitchX
    {
        get => rotation.x;
        protected set => rotation.x = Mathf.Abs(value) < LookVerticalDegLimit ? value : Mathf.Sign(value) * LookVerticalDegLimit;
    }
    public float YawY
    {
        get => rotation.y;
        protected set => rotation.y = value;
    }
    public float RollZ
    {
        get => rotation.z;
        protected set => rotation.z = value;
    }

    // Extra tags
    protected List<String> extraTags;

    // Unity components
    Rigidbody rigBod;
    public Vector3 RigBodVel
    {
        get => rigBod.linearVelocity;
    }
    protected Vector3 RigBodPos
    {
        get => rigBod.position;
    }
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

    BoxCollider envColl;                                        // Rigid body environment hitbox
    public Vector3 BottomPos
    {
        get
        {
            return envColl.bounds.min;
        }
    }
    public Vector3 CenterPos
    {
        get
        {
            return envColl.bounds.center;
        }
    }

    public Vector3 WorldEnvSize
    {
        get
        {
            return new Vector3(envColl.size.x * transform.localScale.x, envColl.size.y * transform.localScale.y, envColl.size.z * transform.localScale.z);
        }
    }
    protected Transform modelTrans;                             // Transform of model
    protected Dictionary<string, Transform> modelChildTrans;    // Transforms of parts of model
    protected Dictionary<string, Collider> hitboxes;

    // Static variables
    protected static float maxRotationDegreesBeforeMove = 20.0f;
    protected static float knockbackOnDeathMult = 10.0f;

    // Character movement state
    protected enum State
    {
        GROUNDED,
        AIRBORNE
    };

    protected State movState;

    // Debug
    Vector3 nextPhysicsFramePositionCenter;

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
            if (transes[i].CompareTag("Model"))
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
        }

        for (int i = 0; i < cols.Count(); i++)
        {
            if (cols[i].name != name)
            {
                hitboxes.Add(cols[i].name, cols[i]);
            }
        }

        // Set variables
        currHP = maxHP;
        knockback = Vector3.zero;
        knockbackMod = 1.0f;
        moveVect = Vector3.zero;
        turnSpeedMod = 1.0f;
        accel = Vector3.zero;
        rotation = Vector2.zero;
        movState = State.AIRBORNE;
        hSpeedCapMultiplier = 1.0f;
        vSpeedCapMultiplier = 1.0f;
        hAccelMultiplier = 1.0f;
        vAccelMultiplier = 1.0f;
        flySpeedCapMultiplier = 1.0f;
        flyAccelMultiplier = 1.0f;
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

        Vector3 velChange = inputDir * hSpeedCap * hSpeedCapMultiplier - horSpeed;

        if (velChange.sqrMagnitude > Mathf.Pow(hAccel * hAccelMultiplier, 2))
        {
            accel = velChange.normalized * hAccel * hAccelMultiplier;
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
                    rigBod.linearVelocity = new Vector3(rigBod.linearVelocity.x, vSpeedCap * vSpeedCapMultiplier, rigBod.linearVelocity.z);
                }

                currGravVec = normalOfGround * (-1) * grav;
                break;
            case State.AIRBORNE:
                break;
        }
        
        // Step up on small steps
        if (movState == State.GROUNDED && collisionInfo.ContainsKey(groundObj))
        {
            nextPhysicsFramePositionCenter = CenterPos + (rigBod.linearVelocity + accel) * Time.fixedDeltaTime;

            if (Physics.BoxCast(nextPhysicsFramePositionCenter + Vector3.up * 2.0f, WorldEnvSize / 2.0f, Vector3.down, out RaycastHit rhit, transform.rotation, 30.0f, LayerMask.GetMask("Obstacle"), QueryTriggerInteraction.Ignore))
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

        DebugText.text = "Acceleration vector: " + accel.ToString();
        DebugText.text += "\nGravity vector: " + currGravVec.ToString();

        // Apply movement
        rigBod.AddForce(accel, ForceMode.VelocityChange);
        rigBod.AddForce(currGravVec, ForceMode.Acceleration);

        // Apply knockback
        if (knockback.sqrMagnitude > 0)
        {
            Debug.Log(knockback * knockbackMod);
        }
        rigBod.AddForce(knockback * knockbackMod, ForceMode.VelocityChange);
        knockback = Vector3.zero;
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

        Vector3 velChange = inputDir * flySpeedCap * flySpeedCapMultiplier - rigBod.linearVelocity;

        if (velChange.sqrMagnitude > Mathf.Pow(flyAccel * flyAccelMultiplier, 2))
        {
            accel = velChange.normalized * flyAccel * flyAccelMultiplier;
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

        // Apply movement
        rigBod.AddForce(accel, ForceMode.VelocityChange);
    }

    /// <summary>
    /// Apply damage and knockback to entity, check if HP is zero
    /// </summary>
    /// <param name="dmg"></param>
    /// <param name="knock"></param>
    public void TakeDamageKnockback(int dmg, Vector3 knock, GameObject hitBy)
    {
        currHP -= dmg;
        knockback = knock;
        Debug.Log("Curr HP: " + currHP + " Damage taken: " + dmg);

        if (currHP <= 0)
        {
            Die();
        }
        else
        {
            OnGettingHit(hitBy);
        }
    }

    /// <summary>
    /// If entity hits 0 hp, run this function
    /// </summary>
    protected virtual void Die()
    {
        rigBod.AddForce(knockback * knockbackOnDeathMult, ForceMode.Force);
    }

    protected virtual void OnGettingHit(GameObject hitBy)
    {

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

    protected IEnumerator AddRotGradual(Vector3 rotToAdd, int increments)
    {
        Vector3 incVec = rotToAdd / increments;
        for (int i = 0; i < increments; i++)
        {
            rotation += incVec;
            yield return null;
        }
    }
}
