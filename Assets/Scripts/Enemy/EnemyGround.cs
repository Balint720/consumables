using System;
using Unity.AI.Navigation;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

public class EnemyGround : EntityClass
{
    public GameObject DebugSphere;                      // Sphere to show next nav position
    // Movement variables                       
    NavMeshAgent nav;                                   // NavMeshAgent Unity component
    EntityClass entToChase;                             // Entity to chase in chase mode

    // Navigation settings
    public float chaseRadius;                           // If chased entity is this close, stop moving towards them

    // States
    public enum EnemyState
    {
        PATROL,
        CHASE,
    };

    EnemyState enState;                                 // State of enemy

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
        nav.stoppingDistance = chaseRadius;
        nav.autoTraverseOffMeshLink = true;

        // Assign state
        enState = EnemyState.PATROL;
        
    }

    // Update is called once per frame
    void Update()
    {

    }

    void FixedUpdate()
    {
        // Do these calculations if enemy is alive (HP larger than 0)
        if (HP > 0)
        {
            // Get direction to target for calculating rotation
            Vector3 dir = Vector3.zero;
            Quaternion rot = Quaternion.identity;

            if (enState == EnemyState.CHASE)
            {
                // Look towards the entity that is being chased if it can see it
                dir = (entToChase.transform.position - transform.position).normalized;

                // Cast a ray towards the entity, which if not blocked by any colliders means we look at them, otherwise we look towards the direction we are going
                RaycastHit rayHit;
                Physics.Raycast(transform.position, dir, out rayHit);
                if (rayHit.collider.gameObject.name == entToChase.gameObject.name)
                {
                    if (dir.x != 0.0f && dir.z != 0.0f) { rot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z)); }
                }
                else
                {
                    dir = (nav.steeringTarget - transform.position).normalized;
                    if (dir.x != 0.0f && dir.z != 0.0f) { rot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z)); }               // Rotation to the position being moved to
                }
            }
            // If we are not chasing anyone, then just look towards where it is going
            else
            {
                dir = (nav.steeringTarget - transform.position).normalized;
                if (dir.x != 0.0f && dir.z != 0.0f) { rot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z)); }               // Rotation to the position being moved to
            }
            
            // Reset jumping variable
            bool doJump = false;

            // Are we standing on a mesh link, if so, handle it
            if (nav.isOnOffMeshLink)
            {
                // Get current link that we are on
                NavMeshLink link = nav.currentOffMeshLinkData.owner.GetComponent<NavMeshLink>();
                
                // Check if the distance between the link's start- and endpoint is smaller than the distance of our current position and the point we want to go to
                if ((link.endPoint - link.startPoint).sqrMagnitude < (nav.destination - transform.position).sqrMagnitude)
                {
                    // We activate the link (?) (dont know if this does anything)
                    link.activated = true;

                    // Based on link area type, we handle the path
                    switch (link.area)
                    {
                        // 2: Jump area
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

            // Calculate the movement based on navmesh next position using entity movement
            CalcMovementGrounded(nav.nextPosition, rot, doJump);

            // Set the position of nav to the actual position of gameobject
            nav.nextPosition = transform.position;

            // Based on enemy state:
            switch (enState)
            {
                case EnemyState.PATROL:
                    break;
                case EnemyState.CHASE:
                    // The navmesh should try to find a path towards the entity being chased
                    if (entToChase != null)
                    {
                        nav.SetDestination(entToChase.transform.position);
                    }
                    else
                    {
                        enState = EnemyState.PATROL;
                    }
                    break;
            }
            DebugSphere.transform.position = nav.nextPosition + new Vector3(0, 4.0f, 0);
        }
    }

    override public void OnGettingHit(GameObject hitBy)
    {
        // If we get hit by an Entity, then the enemy is going to chase it
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
