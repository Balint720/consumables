using Unity.VisualScripting;
using UnityEngine;
using System;
using UnityEditor;


public class ProjectileClass : MonoBehaviour
{
    int ownerID;

    // Unity components
    BoxCollider hitbox;
    Rigidbody body;

    private Vector3 dir;                    // Direction the projectile is going
    private Vector3 dirAccel;               // Acceleration vector (gravity, slowing down, etc)

    // Stats
    public bool cosmetic;                   // Is projectile cosmetic (for hitscan weapons for ex)
    public int dmg;                         // Damage of projectile
    public float speed;                     // Speed of projectile
    public float accel;                     // Acceleration of projectile
    public float range;                     // Range at which projectile disappears
    public float knockbackStrength;             // Strength of knockback applied by weapon

    private Vector3 spawnPos;               // Spawn position
    private float spawnTime;                // How long projectile has been alive

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Unity components
        hitbox = GetComponent<BoxCollider>();
        hitbox.excludeLayers = LayerMask.GetMask("Player");             // Scuffed solution for projectiles hitting the player
        body = GetComponent<Rigidbody>();

        // Set default values
        spawnPos = transform.position;
        spawnTime = Time.time;
    }

    // Update is called once per frame
    void Update()
    {

    }

    void FixedUpdate()
    {
        // Move rigidbody
        body.Move(body.position + dir * speed * Time.deltaTime, body.rotation);

        // If projectile has travelled the defined range or has lived for over 30 seconds destroy projectile
        if ((body.position - spawnPos).magnitude > range || Time.time - spawnTime > 30.0f)
        {
            Destroy(gameObject);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // If not cosmetic apply damage to hit entity
        if (!cosmetic)
        {
            ApplyDamageToEnt(collision.collider);
        }

        // Destroy projectile (should check what we hit first though...)
        Destroy(gameObject);
    }

    bool ApplyDamageToEnt(Collider col)
    {
        if (col.tag == "Entity")
        {
            try
            {
                // Try to get entityclass from hit collider, then make it call its own takedamage function
                EntityClass entityHit = col.gameObject.GetComponent<EntityClass>();
                entityHit.TakeDamageKnockback(dmg, knockbackStrength * dir);
                entityHit.OnGettingHit(GetOwner());
            }
            catch (Exception e)
            {
                Debug.Log("Caught exception: " + e);
                Debug.Log("Couldn't convert GameObject tagged as \"Entity\"");
                return false;
            }
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

    public void SetValuesOfProj(int dmgIn, float speedIn = -1.0f, float knockbackStrengthIn = -1.0f)
    {
        dmg = dmgIn;
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
