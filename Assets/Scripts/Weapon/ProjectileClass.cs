using Unity.VisualScripting;
using UnityEngine;
using System;
using UnityEditor;


public class ProjectileClass : MonoBehaviour
{
    int ownerID;

    // Unity components
    Collider hitbox;
    Rigidbody rigBod;

    private Vector3 dir;                    // Direction the projectile is going
    private Vector3 dirAccel;               // Acceleration vector (gravity, slowing down, etc)

    // Stats
    public bool cosmetic;                   // Is projectile cosmetic (for hitscan weapons for ex)
    public int dmg;                         // Damage of projectile
    public float criticalDmgMult;           // Critical multiplier
    public float speed;                     // Speed of projectile
    public float accel;                     // Acceleration of projectile
    public float range;                     // Range at which projectile disappears
    public float knockbackStrength;             // Strength of knockback applied by weapon

    // Spawn
    private Vector3 spawnPos;               // Spawn position
    private float spawnTime;                // How long projectile has been alive

    // Static
    static float maxLifeTime = 30.0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Unity components
        rigBod = GetComponent<Rigidbody>();
        hitbox = GetComponent<Collider>();
        if (rigBod == null)
        {
            Debug.Log("Projectile " + gameObject.name + " doesn't have a Rigidbody");
            Destroy(this);
            return;
        }
        if (hitbox == null)
        {
            Debug.Log("Projectile " + gameObject.name + " doesn't have a hitbox");
            Destroy(this);
            return;
        }

        // Rigidbody setup
        rigBod.isKinematic = true;

        // Collider setup
        hitbox.excludeLayers = ~(LayerMask.GetMask("Hitbox", "Obstacle") | (int)hitbox.includeLayers);

        // Set default values
        spawnPos = rigBod.position;
        spawnTime = Time.time;
    }

    // Update is called once per frame
    void Update()
    {

    }

    void FixedUpdate()
    {
        // Move rigidbody
        rigBod.MovePosition(rigBod.position + dir * speed * Time.deltaTime);

        // If projectile has travelled the defined range or has lived for over 30 seconds destroy projectile
        if ((rigBod.position - spawnPos).magnitude > range || Time.time - spawnTime > maxLifeTime)
        {
            Destroy(gameObject);
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log(LayerMask.LayerToName(collision.gameObject.layer));
        if (collision.gameObject.layer == LayerMask.NameToLayer("Obstacle"))
        {
            Destroy(gameObject);
        }
        else if (collision.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {

            if (!cosmetic)
            {
                try
                {
                    ApplyDamageToHBox(collision.gameObject.GetComponent<HitboxScript>());
                }
                catch (Exception e)
                {
                    Debug.Log("Caught exception: " + e);
                    Debug.Log("Couldn't get HitboxScript from hitbox");                    
                }
            }
            Destroy(gameObject);
        }
        else if (collision.gameObject.layer == LayerMask.NameToLayer("Projectile"))
        {
            // For projectiles which can hit other projectiles
        }
        
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Obstacle"))
        {
            Destroy(gameObject);
        }
        else if (other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {
            Debug.Log(other.name);
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
            Destroy(gameObject);
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
            if (hBox.GetIsCritical()) critMult = criticalDmgMult;

            entityHit.TakeDamageKnockback(Mathf.RoundToInt(dmg * hBox.GetDmgMultiplier()), knockbackStrength * dir);
            entityHit.OnGettingHit(GetOwner());
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
}
