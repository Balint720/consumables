using Unity.VisualScripting;
using UnityEngine;
using System;
using UnityEditor;
using System.Linq;
using System.Collections;
using UnityEngine.InputSystem.Controls;
using System.Runtime.CompilerServices;


public class ProjectileClass : MonoBehaviour
{
    int ownerID;

    // Unity components
    Collider hitbox;
    Rigidbody rigBod;

    private Vector3 dir;                    // Direction the projectile is going
    private Vector3 speedVec;
    public float grav;

    // Stats
    [SerializeField] bool cosmetic;                 // Is projectile cosmetic (for hitscan weapons for ex)
    [SerializeField] bool stickToTarget;            // Should the projectile destroy self on hit or should the model stay there for a brief period
    [SerializeField] int dmg;                       // Damage of projectile
    public int Dmg { get => dmg; }
    [SerializeField] float criticalDmgMult;         // Critical multiplier
    [SerializeField] float speed;                   // Speed of projectile
    [SerializeField] float accel;                   // Acceleration of projectile
    [SerializeField] float range;                   // Range at which projectile disappears
    [SerializeField] float knockbackStrength;       // Strength of knockback applied by weapon

    // Spawn
    private Vector3 spawnPos;               // Spawn position
    private float spawnTime;                // How long projectile has been alive
    static float deleteTime = 5.0f;
    private bool deleteBool;
    private bool isInitialized = false;

    // Static
    static float maxLifeTime = 30.0f;
    static float timeBeforeCanHitSelf = 0.5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (isInitialized) return;

        // Unity components
        rigBod = GetComponent<Rigidbody>();
        hitbox = GetComponent<Collider>();
        if (rigBod == null)
        {
            Debug.Log("Projectile " + gameObject.name + " doesn't have a Rigidbody");
            Destroy(gameObject);
            return;
        }
        if (hitbox == null)
        {
            Debug.Log("Projectile " + gameObject.name + " doesn't have a hitbox");
            Destroy(gameObject);
            return;
        }

        // Rigidbody setup
        rigBod.isKinematic = true;

        // Collider setup
        hitbox.excludeLayers = ~(LayerMask.GetMask("Hitbox", "Obstacle") | (int)hitbox.includeLayers);

        // Set default values
        spawnPos = rigBod.position;
        spawnTime = Time.time;
        speedVec = speed * dir;
        deleteBool = false;

        // Turn on collider
        //hitbox.enabled = true;
        isInitialized = true;
    }

    void FixedUpdate()
    {
        if (hitbox.enabled)
        {
            rigBod.MovePosition(rigBod.position + Time.fixedDeltaTime * speedVec);
            rigBod.MoveRotation(Quaternion.LookRotation(speedVec));

            speedVec += grav * Time.fixedDeltaTime * new Vector3(0.0f, -1.0f, 0.0f);
            speedVec += accel * Time.fixedDeltaTime * dir;

            // If projectile has travelled the defined range or has lived for over 30 seconds destroy projectile
            if ((rigBod.position - spawnPos).magnitude > range || Time.time - spawnTime > maxLifeTime)
            {
                Destroy(gameObject);
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        OnTriggerEnter(collision.collider);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isInitialized)
        {
            Start();
        }
        if (other.gameObject.layer == LayerMask.NameToLayer("Obstacle"))
        {
            DeleteSelf(other.transform);
        }
        else if (other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {
            // Exit if hit self before set time passed
            if (other.transform.IsChildOf(GetOwner().transform))
            {
                // Behaviour might be changed
                if (Time.time - spawnTime < timeBeforeCanHitSelf)
                {
                    return;
                }
            }

            if (!cosmetic)
            {
                try
                {
                    ApplyDamageToHBox(other.gameObject.GetComponent<HitboxScript>());
                }
                catch (Exception e)
                {
                    Debug.Log("Caught exception: " + e);
                    Debug.Log("Couldn't get HitboxScript from hitbox");
                }
            }

            DeleteSelf(other.transform);
        }
        else if (other.gameObject.layer == LayerMask.NameToLayer("Projectile"))
        {
            // For projectiles which can hit other projectiles
        }
    }

    bool ApplyDamageToHBox(HitboxScript hBox)
    {
        try
        {
            // Try to get entityclass from hit collider, then make it call its own takedamage function
            EntityClass entityHit = hBox.GetOwnerEntity();
            hBox.ReduceHP(dmg);

            float critMult = 1.0f;
            if (hBox.IsCritical()) critMult = criticalDmgMult;

            entityHit.TakeDamageKnockback(Mathf.RoundToInt(dmg * critMult * hBox.GetDmgMultiplier()), knockbackStrength * dir, GetOwner());
        }
        catch (Exception e)
        {
            Debug.Log("Caught exception: " + e);
            Debug.Log("Couldn't get owner of hitbox");
            return false;
        }
        return true;
    }

    // Getters and setters
    public void SetDirection(Vector3 newDir)
    {
        dir = newDir;
    }
    public void SetOwner(GameObject newOwner)
    {
        ownerID = newOwner.GetInstanceID();
    }
    public void SetOwner(int id)
    {
        ownerID = id;
    }

    public GameObject GetOwner()
    {
        if (ownerID == 0)
        {
            return null;
        }
        else
        {
            return (GameObject)EditorUtility.InstanceIDToObject(ownerID);
        }
    }

    public void SetValuesOfProj(int dmgIn, float criticalDmgMultIn, float speedIn = -1.0f, float knockbackStrengthIn = -1.0f)
    {
        dmg = dmgIn;
        if (criticalDmgMult >= 0.0f)
        {
            criticalDmgMult = criticalDmgMultIn;
        }
        if (speedIn >= 0.0f)
        {
            speed = speedIn;
        }
        if (knockbackStrengthIn >= 0.0f)
        {
            knockbackStrength = knockbackStrengthIn;
        }
    }

    void DeleteSelf(Transform t = null)
    {
        if (deleteTime <= 0.0f || !stickToTarget)
        {
            Destroy(gameObject);
        }
        else
        {
            hitbox.enabled = false;
            rigBod.detectCollisions = false;
            if (t != null)
            {
                transform.SetParent(t, true);
            }

            StartCoroutine(Deletion());
        }
    }

    IEnumerator Deletion()
    {
        while (true)
        {
            if (deleteBool)
            {
                Destroy(gameObject);
            }
            else
            {
                deleteBool = true;
            }
            yield return new WaitForSeconds(deleteTime);
        }
    }
}
