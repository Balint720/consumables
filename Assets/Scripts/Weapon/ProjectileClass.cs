using Unity.VisualScripting;
using UnityEngine;
using System;


public class ProjectileClass : MonoBehaviour
{
    GameObject owner;           // Projectile is assigned to this GameObject
    MeshFilter mesh;
    BoxCollider hitbox;         // Projectile's hitbox
    Rigidbody body;
    private Vector3 dir = Vector3.zero;
    private Vector3 dirAccel = Vector3.zero;

    // Stats
    public bool cosmetic = false;
    public int dmg;
    public float speed;
    public float accel;
    public float range;

    private Vector3 spawnPos;
    private float spawnTime;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mesh = GetComponent<MeshFilter>();

        hitbox = GetComponent<BoxCollider>();
        hitbox.excludeLayers = LayerMask.GetMask("Player");

        body = GetComponent<Rigidbody>();

        spawnPos = transform.position;
        spawnTime = Time.time;
    }

    // Update is called once per frame
    void Update()
    {

    }

    void FixedUpdate()
    {
        body.Move(body.position + dir * speed * Time.deltaTime, body.rotation);

        if ((body.position - spawnPos).magnitude > range || Time.time - spawnTime > 30.0f)
        {
            Destroy(gameObject);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!cosmetic)
        {
            ApplyDamageToEnt(collision.collider);
        }
        Destroy(gameObject);
    }

    public void SetDirection(Vector3 newDir)
    {
        dir = newDir;
    }
    
    bool ApplyDamageToEnt(Collider col)
    {
        if (col.tag == "Entity")
        {
            try
            {
                EntityClass entityHit = col.gameObject.GetComponent<EntityClass>();
                entityHit.TakeDamageKnockback(dmg, Vector3.zero);
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
}
