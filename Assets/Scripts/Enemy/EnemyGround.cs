using System;
using Unity.AI.Navigation;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

public class EnemyGround : EntityClass
{

    public GameObject DebugSphere;
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
        nav.stoppingDistance = 10.0f;
        nav.autoTraverseOffMeshLink = true;

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
        Vector3 dir = Vector3.zero;
        Quaternion rot = Quaternion.identity;


        if (entToChase != null)
        {
            dir = (entToChase.transform.position - transform.position).normalized;
            if (dir.x != 0.0f && dir.z != 0.0f) { rot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z)); }
        }
        else
        {
            dir = (nav.steeringTarget - transform.position).normalized;
            if (dir.x != 0.0f && dir.z != 0.0f) { rot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z)); }               // Rotation to the position being moved to
        }

        bool doJump = false;

        // Are we standing on a mesh link, if so, handle it
        if (nav.isOnOffMeshLink)
        {
            NavMeshLink link = nav.currentOffMeshLinkData.owner.GetComponent<NavMeshLink>();
            if ((link.endPoint - link.startPoint).sqrMagnitude < (nav.destination - transform.position).sqrMagnitude)
            {
                link.activated = true;
                switch (link.area)
                {
                    case 2:
                        doJump = true;
                        break;
                    default:
                        break;
                }
            }
            else
            {
                link.activated = false;
            }
        }

        CalcMovementGrounded(nav.nextPosition, rot, doJump);

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
        DebugSphere.transform.position = nav.nextPosition + new Vector3(0, 4.0f, 0);
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
