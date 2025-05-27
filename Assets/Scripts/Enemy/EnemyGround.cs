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
        nav.updatePosition = true; nav.updateRotation = true;
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
        nav.velocity += knockback;
        knockback = Vector3.zero;

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
