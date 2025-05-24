using Unity.VisualScripting;
using UnityEngine;

public class ProjectileClass : MonoBehaviour
{
    GameObject owner;           // Projectile is assigned to this GameObject
    MeshFilter mesh;
    BoxCollider hitbox;         // Projectile's hitbox
    Rigidbody body;
    private Vector3 dir = Vector3.zero;
    private Vector3 dirAccel = Vector3.zero;
    public float speed = 10.0f;
    public float accel = 0.0f;
    public float range = 1000.0f;

    private Vector3 spawnPos;
    private float spawnTime;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log("Projectile created");
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
        Debug.Log("Projectile destroyed");
        Debug.Log("Collided with: " + collision.collider);
        Destroy(gameObject);
    }

    public void SetDirection(Vector3 newDir)
    {
        dir = newDir;
    }
}
