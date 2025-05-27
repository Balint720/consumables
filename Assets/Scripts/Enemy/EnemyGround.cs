using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

public class EnemyGround : EntityClass
{
    // Movement variables
    bool initJump = false;
    NavMeshAgent nav;
    int backAndForth;
    EntityClass entToChase;

    // States
    public enum EnemyState
    {
        PATROL,
        CHASE,

    };

    EnemyState enState;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        EntityStart();

        // Assign components
        nav = GetComponent<NavMeshAgent>();

        // Set up nav mesh agent component
        nav.updatePosition = false; nav.updateRotation = false;
        nav.speed = HSpeedCap;
        nav.acceleration = HAccel;

        backAndForth = 1;

        // Assign state
        enState = EnemyState.PATROL;

    }

    // Update is called once per frame
    void Update()
    {

    }

    void FixedUpdate()
    {
        // Get direction to target for calculating rotation
        Vector3 dir = (nav.steeringTarget - transform.position).normalized;
        Quaternion rot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z));

        CalcMovementGrounded(nav.velocity.normalized, rot);
        nav.nextPosition = transform.position;

        switch (enState)
        {
            case EnemyState.PATROL:
                break;
            case EnemyState.CHASE:
                if (entToChase != null)
                {
                    nav.SetDestination(entToChase.transform.position);
                }
                break;
        }

        Debug.Log("Rotation of enemy: " + rotation);
    }

    void OnCollisionEnter(Collision collision)
    {

    }

    override public void OnGettingHit(GameObject hitBy)
    {
        if (hitBy.tag == "Entity")
        {
            try
            {
                EntityClass ent = hitBy.GetComponent<EntityClass>();
                if (ent.HasExtraTag("Player"))
                {
                    enState = EnemyState.CHASE;
                    entToChase = ent;
                }
            }
            catch (Exception e)
            {
                Debug.Log(e);
                Debug.Log("Couldn't get EntityClass component from GameObject tagged as \"Entity\"");
            }
        }
    }
}
