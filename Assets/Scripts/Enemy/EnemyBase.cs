using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.AI;

public class EnemyBase : EntityClass
{
    // States
    protected enum BehaviourState
    {
        STAND,
        PATROL,
        SEARCH,
        COMBAT
    };

    enum PathTraverseType { PAUSEATPOINTS, MOVEINSTANTLY };
    enum MoveStates { STOPPED, MOVING, SPRINTING };
    enum PatrolPoint { RESET, ADVANCE };

    public bool useCrowdedPatrolRoute;
    BehaviourState b_state;
    PathTraverseType p_patrolpoint_state;
    PathTraverseType p_pathcorner_state;
    MoveStates m_state;
    PatrolScript patrolRoute;
    
    int currPatrolPoint;                                // 0 based, points start from 1 in editor
    int currCornerIndex;
    NavMeshPath currPath;
    Vector3 dirToCorner;
    SphereCollider detectionSphere;
    public float maxDistFromEntity;
    Transform targetEntity;
    Timer searchTimer;
    Timer stopMoveTimer;
    public float searchTime;

    // Static variables
    static float closeEnoughDistanceFromCorner = 1.1f;

    // Debug
    GameObject[] DebugSpheres;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    override protected void Start()
    {
        base.Start();

        detectionSphere = GetComponentInChildren<SphereCollider>();
        if (detectionSphere != null)
        {
            if (detectionSphere.tag != "Detection")
            {
                detectionSphere = null;
                Debug.Log(gameObject.name + ": Couldn't find DetectionSphere");
                Destroy(gameObject);
            }
        }
        else
        {
            Debug.Log(gameObject.name + ": Couldn't find DetectionSphere");
            Destroy(gameObject);
        }


        // Initialization
        currPatrolPoint = 0;
        currCornerIndex = 0;
        patrolRoute = null;
        currPath = new NavMeshPath();
        dirToCorner = Vector3.zero;
        b_state = BehaviourState.PATROL;
        p_patrolpoint_state = PathTraverseType.PAUSEATPOINTS;
        p_pathcorner_state = PathTraverseType.MOVEINSTANTLY;
        m_state = MoveStates.MOVING;
        searchTimer = new Timer();
        stopMoveTimer = new Timer();
        detectionSphere.excludeLayers = ~(LayerMask.GetMask("EnvironmentBox") | (int)detectionSphere.includeLayers);

        if (!FindPatrolRoute(!useCrowdedPatrolRoute))
        {
            Debug.Log(gameObject.name + ": No patrol route found");
        }

        DebugSpheres = new GameObject[10];
        for (int i = 0; i < DebugSpheres.Count(); i++)
        {
            DebugSpheres[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DebugSpheres[i].GetComponent<Renderer>().enabled = false;
            if (DebugSpheres[i].TryGetComponent<Collider>(out Collider c)) c.enabled = false;
        }
    }

    protected virtual void Update()
    {
        // Cooldowns and timers
        searchTimer.CallPerFrame(Time.deltaTime);
        stopMoveTimer.CallPerFrame(Time.deltaTime);

        // Debug
        for (int i = 0; i < DebugSpheres.Count(); i++)
        {
            if (currPath.status != NavMeshPathStatus.PathInvalid)
            {
                if (currPath.corners.Count() > i)
                {
                    DebugSpheres[i].transform.position = currPath.corners[i] + Vector3.up * (i / 2.0f) + Vector3.up * 1.0f;
                    DebugSpheres[i].GetComponent<Renderer>().enabled = true;
                    if (i < currCornerIndex)
                    {
                        DebugSpheres[i].GetComponent<Renderer>().material.color = Color.green;
                    }
                    else
                    {
                        DebugSpheres[i].GetComponent<Renderer>().material.color = Color.red;
                    }
                }
                else
                {
                    DebugSpheres[i].GetComponent<Renderer>().enabled = false;
                }
            }
            else
            {
                DebugSpheres[i].GetComponent<Renderer>().enabled = false;
            }
        }
    }

    override protected void FixedUpdate()
    {
        CalcNewState();
        StateMachine();
        CalcMovementInput();

        base.FixedUpdate();
    }

    bool FindPatrolRoute(bool strict = true)
    {
        GameObject[] routeGos = GameObject.FindGameObjectsWithTag("Route");
        List<PatrolScript> routes = new List<PatrolScript>();
        foreach (GameObject go in routeGos)
        {
            if (go.TryGetComponent<PatrolScript>(out PatrolScript ps))
            {
                routes.Add(ps);
            }
        }

        float dist = Mathf.Infinity;
        PatrolScript selectedRoute;
        selectedRoute = null;

        foreach (PatrolScript ps in routes)
        {
            float currDist = (transform.position - ps.transform.position).sqrMagnitude;
            if (currDist < dist && (ps.IsUnused() || !strict))
            {
                dist = currDist;
                selectedRoute = ps;
            }
        }

        if (selectedRoute != null)
        {
            patrolRoute = selectedRoute;
            return true;
        }
        else
        {
            Debug.Log(gameObject.name + " func FindPatrolRoute(): Couldn't find patrol route");
            return false;
        }
    }

    void StateMachine()
    {
        switch (b_state)
        {
            case BehaviourState.STAND:
                break;
            case BehaviourState.PATROL:
                if (!patrolRoute) { b_state = BehaviourState.STAND; break; }
                if (currPath.status == NavMeshPathStatus.PathInvalid)
                {
                    PatrolRouteCalc(PatrolPoint.RESET);
                }
                else
                {
                    if (CheckIfReachedCurrCorner())
                    {
                        if (p_pathcorner_state == PathTraverseType.PAUSEATPOINTS)
                        {
                            m_state = MoveStates.STOPPED;
                            stopMoveTimer.StopTimer();
                            stopMoveTimer.StartTimer(0.5f);
                        }
                        currCornerIndex++;
                    }
                    if (currCornerIndex >= currPath.corners.Count())
                    {
                        if (p_patrolpoint_state == PathTraverseType.PAUSEATPOINTS)
                        {
                            m_state = MoveStates.STOPPED;
                            stopMoveTimer.StopTimer();
                            stopMoveTimer.StartTimer(5.0f);
                        }
                        PatrolRouteCalc(PatrolPoint.ADVANCE);
                    }
                }
                break;
            case BehaviourState.SEARCH:
                break;
            case BehaviourState.COMBAT:
                break;
        }
    }

    void CalcNewState()
    {
        switch (b_state)
        {
            case BehaviourState.PATROL:
                if (targetEntity != null)
                {
                    if (CheckIfCanSeeTarget()) b_state = BehaviourState.COMBAT;
                    else b_state = BehaviourState.SEARCH;
                }
                break;
            case BehaviourState.SEARCH:
                if (CheckIfCanSeeTarget())
                {
                    b_state = BehaviourState.COMBAT;
                    searchTimer.StopTimer();
                }
                else
                {
                    searchTimer.StartTimer(searchTime);

                    if (searchTimer.IsDone() && (transform.position - targetEntity.position).sqrMagnitude > maxDistFromEntity * maxDistFromEntity) b_state = BehaviourState.PATROL;
                }
                break;
            case BehaviourState.COMBAT:
                if (!CheckIfCanSeeTarget())
                {
                    searchTimer.StartTimer(searchTime / 2.0f);
                    if (searchTimer.IsDone())
                    {
                        b_state = BehaviourState.SEARCH;
                    }
                }
                else
                {
                    searchTimer.StopTimer();
                }
                break;
        }

        if (stopMoveTimer.IsDone())
        {
            m_state = MoveStates.MOVING;
        }
    }

    bool PatrolRouteCalc(PatrolPoint pp)
    {
        if (patrolRoute != null)
        {
            switch (pp)
            {
                case PatrolPoint.RESET:
                    int closestPoint = 0;
                    float d = Mathf.Infinity;
                    for (int i = 0; i < patrolRoute.GetNumOfPoints(); i++)
                    {
                        float currD = (patrolRoute.GetPoint(i).position - rigBod.position).sqrMagnitude;
                        Debug.Log("Current Point: " + patrolRoute.GetPoint(i).position);
                        Debug.Log("currD: " + currD);
                        Debug.Log("d" + d);
                        if (currD < d)
                        {
                            d = currD;
                            closestPoint = i;
                        }
                    }
                    currPatrolPoint = closestPoint;
                    break;
                case PatrolPoint.ADVANCE:
                    currPatrolPoint = (currPatrolPoint + 1) % patrolRoute.GetNumOfPoints();
                    break;
            }
            

            if (!patrolRoute.GetPoint(currPatrolPoint, out Transform newLoc))
            {
                Debug.Log(gameObject.name + ": Couldn't get patrol point");
                return false;
            }

            if (!NavMesh.CalculatePath(rigBod.position, newLoc.position, NavMesh.AllAreas, currPath))
            {
                Debug.Log(gameObject.name + ": Couldn't calculate path for point " + (currPatrolPoint + 1));
                return false;
            }

            currCornerIndex = 0;
            return true;
        }
        else
        {
            return false;
        }
    }

    bool CheckIfReachedCurrCorner()
    {
        if (currPath.status != NavMeshPathStatus.PathInvalid)
        {
            float distanceFromCornerSqr = (currPath.corners[currCornerIndex] - rigBod.position).sqrMagnitude;

            if (distanceFromCornerSqr <= Mathf.Pow(closeEnoughDistanceFromCorner, 2.0f)) return true;
            else return false;
        }
        else return false;
    }

    void CalcMovementInput()
    {
        if (m_state != MoveStates.STOPPED)
        {
            switch (b_state)
            {
                case BehaviourState.STAND:
                    moveVect = Vector3.zero;
                    break;
                case BehaviourState.PATROL:
                    if (currPath.status != NavMeshPathStatus.PathInvalid)
                    {
                        moveVect = (currPath.corners[currCornerIndex] - rigBod.position).normalized;
                    }
                    else moveVect = Vector3.zero;
                    break;
            }
        }
        else
        {
            moveVect = Vector3.zero;
        }
    }

    bool CheckIfCanSeeTarget()
    {
        if (targetEntity != null)
        {
            Vector3 dir = targetEntity.position - transform.position;
            Debug.DrawRay(transform.position, dir);
            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, maxDistFromEntity, LayerMask.GetMask("Obstacle", "EnvironmentBox")))
            {
                Debug.Log(hit.collider.gameObject.name);
                if (hit.transform == targetEntity)
                {
                    return true;
                }
            }
        }
        return false;
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (other.name.Contains("Player"))
        {
            //targetEntity = other.transform;
            //if (CheckIfCanSeeTarget()) b_state = BehaviourState.COMBAT;
            //else b_state = BehaviourState.SEARCH;
        }
    }
}
