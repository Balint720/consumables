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
        PATROL,
        SEARCH,
        COMBAT
    };

    public bool useCrowdedPatrolRoute;
    BehaviourState b_state;
    PatrolScript patrolRoute;
    int currPatrolPoint;                                // 0 based, points start from 1 in editor
    int currCornerIndex;
    NavMeshPath currPath;
    SphereCollider detectionSphere;
    public float maxDistFromEntity;
    Transform targetEntity;
    Timer searchTimer;
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
        b_state = BehaviourState.PATROL;
        searchTimer = new Timer();
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

        for (int i = 0; i < DebugSpheres.Count(); i++)
        {
            if (currPath != null)
            {
                if (currPath.corners.Count() > i)
                {
                    DebugSpheres[i].transform.position = currPath.corners[i] + Vector3.up * (i / 2.0f) + Vector3.up*1.0f;
                    DebugSpheres[i].GetComponent<Renderer>().enabled = true;
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
            case BehaviourState.PATROL:
                if (currPath == null)
                {
                    PatrolRouteCalc();
                    currCornerIndex = 0;
                    currPatrolPoint++;
                }
                else
                {
                    if (MoveToNextCorner())
                    {
                        PatrolRouteCalc();
                        currCornerIndex = 0;
                        currPatrolPoint++;
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

                    if (searchTimer.IsDone() && (transform.position - targetEntity.position).sqrMagnitude > maxDistFromEntity*maxDistFromEntity) b_state = BehaviourState.PATROL;
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
    }

    bool PatrolRouteCalc()
    {
        if (patrolRoute != null)
        {
            if (patrolRoute.GetNumOfPoints() <= currPatrolPoint) { currPatrolPoint = 0; }
            if (!patrolRoute.GetPoint(currPatrolPoint, out Transform newLoc)) { Debug.Log(gameObject.name + ": Couldn't get patrol point"); return false; }
            if (!NavMesh.CalculatePath(rigBod.position, newLoc.position, NavMesh.AllAreas, currPath)) { Debug.Log(gameObject.name + ": Couldn't calculate path for point " + (currPatrolPoint + 1)); return false; }
            return true;
        }
        else
        {
            return false;
        }
    }

    bool MoveToNextCorner()
    {
        if (currPath != null)
        {
            if (currPath.corners.Count() <= currCornerIndex) return true;                    // Path is done, we reached the final corner
            else
            {
                float distanceFromCorner = (currPath.corners[currCornerIndex] - rigBod.position).sqrMagnitude;
                float stoppingDistance = (rigBod.linearVelocity.sqrMagnitude) / Mathf.Pow(HAccel * HAccelMultiplier, 2);


                if (distanceFromCorner <= Mathf.Pow(closeEnoughDistanceFromCorner, 2.0f)) { currCornerIndex++; return false; }
                //else if (distanceFromCorner <= stoppingDistance) { moveVect = Vector3.zero; return false; }
                else
                {
                    moveVect = (currPath.corners[currCornerIndex] - rigBod.position).normalized;
                    rotation.y = Vector3.SignedAngle(transform.forward, moveVect, Vector3.up);
                    return false;
                }
                
            }
        }
        else return false;
        
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
