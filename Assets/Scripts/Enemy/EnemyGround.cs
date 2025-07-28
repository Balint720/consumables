using System;
using System.ComponentModel;
using System.Linq;
using NUnit.Framework.Constraints;
using TMPro;
using Unity.AI.Navigation;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

public class EnemyGround : EntityClass
{
    public GameObject DebugSphere;                      // Sphere to show next nav position
    GameObject[] DebugPrimitives;
    // Movement variables                       
    NavMeshAgent nav;                                   // NavMeshAgent Unity component
    EntityClass entToChase;                             // Entity to chase in chase mode
    
    public float howCloseToNavPos;                      // How close is close enough for destination in nav mesh

    // Navigation settings
    public float chaseRadius;                           // If chased entity is this close, stop moving towards them
    public bool keepDistance;                           // If chased entity is closer than chase radius, should we back up or not care if we are closer

    // States
    public enum EnemyState
    {
        PATROL,
        CHASE,
        DASHSTART,
        DASH
    };

    EnemyState enState;                                 // State of enemy

    // Dash
    public float dashCooldown;
    public float dashDistance;
    public float dashSpeedCapMultiplier;
    public float dashAccelMultiplier;
    float currDashCooldown;
    bool dashReady;
    bool doDash;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        EntityStart();

        // Assign components
        nav = GetComponent<NavMeshAgent>();

        // Set up nav mesh agent component
        nav.updatePosition = false; nav.updateRotation = false;
        nav.autoTraverseOffMeshLink = true;

        // Assign state
        enState = EnemyState.PATROL;

        // Initialize values
        currDashCooldown = 0.0f;
        dashReady = true;

        // Debug primitives
        DebugPrimitives = new GameObject[10];
        for (int i = 0; i < 10; i++)
        {
            DebugPrimitives[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DebugPrimitives[i].GetComponent<SphereCollider>().enabled = false;
        }
        
    }

    // Update is called once per frame
    void Update()
    {

    }

    void FixedUpdate()
    {
        // Decrease cooldowns
        if (currDashCooldown > 0.0f)
        {
            currDashCooldown -= Time.fixedDeltaTime;
        }
        else
        {
            dashReady = true;
        }

        // Do these calculations if enemy is alive (HP larger than 0)
        if (HP > 0)
        {
            // Get direction to target for calculating rotation
            Vector3 dir = Vector3.zero;
            Quaternion rot = Quaternion.identity;

            switch (enState)
            {
                case EnemyState.CHASE:
                case EnemyState.DASH:
                    if (entToChase != null)
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
                    break;
                default:
                    dir = (nav.steeringTarget - transform.position).normalized;
                    if (dir.x != 0.0f && dir.z != 0.0f) { rot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z)); }               // Rotation to the position being moved to
                    break;
            }

            // Jumping variable
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
            CalcMovementGrounded(nav.steeringTarget, rot, doJump, howCloseToNavPos);
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
                        Vector3 posToMoveTo = entToChase.transform.position;
                        if ((posToMoveTo - transform.position).sqrMagnitude > chaseRadius * chaseRadius || keepDistance)
                        {
                            posToMoveTo -= chaseRadius * (posToMoveTo - transform.position).normalized;
                            nav.SetDestination(posToMoveTo);
                        }
                        else
                        {
                            nav.SetDestination(transform.position);
                        }
                    }
                    else
                    {
                        enState = EnemyState.PATROL;
                    }
                    break;
                case EnemyState.DASHSTART:
                    int sign = 1;
                    if (UnityEngine.Random.value >= 0.5f)
                    {
                        sign = -1;
                    }

                    HSpeedCapMultiplier = dashSpeedCapMultiplier;
                    HAccelMultiplier = dashAccelMultiplier;
                    nav.SetDestination(Quaternion.Euler(0.0f, sign * 90.0f, 0.0f) * transform.forward * dashDistance);


                    enState = EnemyState.DASH;
                    break;
                case EnemyState.DASH:
                    if ((nav.destination - transform.position).sqrMagnitude < howCloseToNavPos*howCloseToNavPos)
                    {
                        HSpeedCapMultiplier = 1.0f;
                        HAccelMultiplier = 1.0f;

                        if (entToChase != null) enState = EnemyState.CHASE;
                        else enState = EnemyState.PATROL;
                    }
                    break;
            }

            int i = 0;
            for (i = 0; i < DebugPrimitives.Count(); i++)
            {
                if (i < nav.path.corners.Count())
                {
                    DebugPrimitives[i].transform.position = nav.path.corners[i] + new Vector3(0.0f, 4.0f, 0.0f);
                    DebugPrimitives[i].GetComponent<Renderer>().material.color = new Color(0.0f, 0.2f * i, 0.0f);
                }
                else if (i == nav.path.corners.Count())
                {
                    DebugPrimitives[i + 1].transform.position = nav.steeringTarget + new Vector3(0.0f, 4.0f, 0.0f);
                    DebugPrimitives[i + 1].GetComponent<Renderer>().material.color = Color.red;
                }
                else
                {
                    DebugPrimitives[i].transform.position = new Vector3(0.0f, -10.0f, 0.0f);
                }
            }
        }

        DebugText.text = "Remaining distance: " + (nav.steeringTarget - transform.position).magnitude;
        DebugText.text += "\nPath status: " + nav.pathStatus;
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
                    if (enState != EnemyState.DASHSTART && enState != EnemyState.DASH) enState = EnemyState.CHASE;
                    entToChase = ent;
                }

                if (dashReady)
                {
                    dashReady = false;
                    currDashCooldown = dashCooldown;

                    enState = EnemyState.DASHSTART;
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
