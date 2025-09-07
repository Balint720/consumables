using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using Unity.VisualScripting.ReorderableList.Element_Adder_Menu;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

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
    enum MoveStates { STOPPED, MOVING, SPRINTING, HOVERING, JUMPING };
    enum PatrolPoint { RESET, ADVANCE };
    enum RotateBeforeMove { WAITFORROTATE, MOVEANYWAY };
    enum LinkState { NONE, JUMPHOVER };

    public bool useCrowdedPatrolRoute;
    BehaviourState b_state;
    PathTraverseType p_patrolpoint_state;
    PathTraverseType p_pathcorner_state;
    MoveStates m_state;
    RotateBeforeMove r_state;
    LinkState l_state;
    PatrolScript patrolRoute;

    NavMeshAgent navMeshAgent;

    int currPatrolPoint;                                // 0 based, points start from 1 in editor
    int currCornerIndex;
    List<Vector3> offMeshLinkPoints;
    int currOffMeshLinkPoint;
    Vector3 dirToCorner;
    SphereCollider detectionSphere;
    public float maxDistFromEntity;
    Transform targetEntity;
    Timer searchTimer;
    Timer stopMoveTimer;
    public float searchTime;
    static float searchUpdateDestTime = 10.0f;
    static float combatUpdateDestTime = 0.25f;
    public float patrolPointPauseTime;
    public float pathCornerPauseTime;
    IEnumerator pathCalcSearch;
    bool pathCalcSearchRunning;
    IEnumerator pathCalcCombat;
    bool pathCalcCombatRunning;

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
        offMeshLinkPoints = new List<Vector3>();
        currOffMeshLinkPoint = 0;
        dirToCorner = Vector3.zero;
        b_state = BehaviourState.PATROL;
        p_patrolpoint_state = PathTraverseType.PAUSEATPOINTS;
        p_pathcorner_state = PathTraverseType.MOVEINSTANTLY;
        m_state = MoveStates.MOVING;
        r_state = RotateBeforeMove.WAITFORROTATE;
        l_state = LinkState.NONE;
        searchTimer = new Timer();
        stopMoveTimer = new Timer();
        pathCalcSearch = RecalculatePathToTarget(searchUpdateDestTime);
        pathCalcSearchRunning = false;
        pathCalcCombat = RecalculatePathToTarget(combatUpdateDestTime);
        pathCalcCombatRunning = false;
        detectionSphere.excludeLayers = ~(LayerMask.GetMask("EnvironmentBox") | (int)detectionSphere.includeLayers);

        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgent.updatePosition = false;
        navMeshAgent.updateRotation = false;
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
            if (navMeshAgent.path.corners.Count() > i)
            {
                DebugSpheres[i].transform.position = navMeshAgent.path.corners[i] + Vector3.up * 1.0f;
                DebugSpheres[i].GetComponent<Renderer>().enabled = true;
                if (navMeshAgent.steeringTarget == navMeshAgent.path.corners[i])
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
    }

    override protected void FixedUpdate()
    {
        CalcNewState();
        StateMachine();
        PathCoroutineManager();
        CalcMovementInput();

        base.FixedUpdate();
        navMeshAgent.nextPosition = rigBod.position;

        DebugText.text = moveVect.ToString();
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
        switch (l_state)
        {
            case LinkState.NONE:
                switch (b_state)
                {
                    case BehaviourState.STAND:
                        break;
                    case BehaviourState.PATROL:
                        if (!patrolRoute) { b_state = BehaviourState.STAND; break; }
                        if (navMeshAgent.hasPath)
                        {
                            CheckPathProgress(patrolPointPauseTime);
                        }
                        else
                        {
                            Debug.Log("Got here");
                            PatrolRouteCalc(PatrolPoint.ADVANCE);
                        }
                        break;
                    case BehaviourState.SEARCH:
                        if (!targetEntity)
                        {
                            navMeshAgent.ResetPath();
                            b_state = BehaviourState.PATROL;
                        }

                        break;
                    case BehaviourState.COMBAT:
                        if (!targetEntity)
                        {
                            navMeshAgent.ResetPath();
                            b_state = BehaviourState.PATROL;
                        }
                        break;
                }
                break;
            case LinkState.JUMPHOVER:
                float distanceFromCornerSqr = (offMeshLinkPoints[currOffMeshLinkPoint] - rigBod.position).sqrMagnitude;

                if (distanceFromCornerSqr <= Mathf.Pow(closeEnoughDistanceFromCorner, 2.0f))
                {
                    currOffMeshLinkPoint++;
                }

                if (currOffMeshLinkPoint < offMeshLinkPoints.Count())
                {
                    l_state = LinkState.NONE;
                    m_state = MoveStates.MOVING;
                }
                break;
        }
        
    }

    void CheckPathProgress(float pathEndPauseTime)
    {
        if (CheckIfReachedCurrCorner())
        {
            if (p_pathcorner_state == PathTraverseType.PAUSEATPOINTS)
            {
                m_state = MoveStates.STOPPED;
                stopMoveTimer.StopTimer();
                stopMoveTimer.StartTimer(pathEndPauseTime);
            }
            currCornerIndex++;
        }
    }

    void CalcNewState()
    {
        switch (b_state)
        {
            case BehaviourState.PATROL:
                if (targetEntity != null)
                {
                    if (CheckIfCanSeeTarget(rigBod.position)) b_state = BehaviourState.COMBAT;
                    else b_state = BehaviourState.SEARCH;
                }
                break;
            case BehaviourState.SEARCH:
                if (CheckIfCanSeeTarget(rigBod.position))
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
                if (!CheckIfCanSeeTarget(rigBod.position))
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

        if (m_state != MoveStates.STOPPED) MoveStateCalc();

        switch (m_state)
        {
            case MoveStates.STOPPED:
                if (stopMoveTimer.IsDone())
                {
                    MoveStateCalc();
                }
                break;
            case MoveStates.MOVING:
                moveType = MoveType.WALK;
                HSpeedCapMultiplier = 1.0f;
                break;
            case MoveStates.SPRINTING:
                moveType = MoveType.WALK;
                HSpeedCapMultiplier = 1.3f;
                break;
            case MoveStates.HOVERING:
                moveType = MoveType.FLY;
                break;
        }
    }

    void MoveStateCalc()
    {
        if (navMeshAgent.isOnOffMeshLink && l_state == LinkState.NONE)
        {
            try
            {
                int numOfChildren = navMeshAgent.currentOffMeshLinkData.owner.GetComponent<Transform>().childCount;
                Debug.Log(numOfChildren);
                for (int i = 0; i < numOfChildren; i++)
                {
                    Transform t = navMeshAgent.currentOffMeshLinkData.owner.GetComponent<Transform>().GetChild(i);
                    Debug.Log(t.position);
                    offMeshLinkPoints.Add(t.position);
                    Debug.Log("Point: " + t.position);
                }

                l_state = LinkState.JUMPHOVER;
                currOffMeshLinkPoint = 0;

                m_state = MoveStates.HOVERING;
                navMeshAgent.CompleteOffMeshLink();

                Debug.Log("Starting offmeshlink");
            }
            catch (Exception e)
            {
                Debug.Log(e);
                Debug.Log("Couldn't get transforms from offmeshlink");
                Destroy(gameObject);
            }
        }     
    }

    bool PatrolRouteCalc(PatrolPoint pp)
    {
        if (patrolRoute != null)
        {
            if (!patrolRoute.GetPoint(currPatrolPoint, out Transform newLoc))
            {
                Debug.Log(gameObject.name + ": Couldn't get patrol point");
                return false;
            }
            navMeshAgent.SetDestination(newLoc.position);
            currCornerIndex = 0;
            currPatrolPoint = ++currPatrolPoint % patrolRoute.GetNumOfPoints();
            return true;
        }
        else
        {
            return false;
        }
    }

    bool CheckIfReachedCurrCorner()
    {
        if (navMeshAgent.hasPath && currCornerIndex < navMeshAgent.path.corners.Count())
        {
            float distanceFromCornerSqr = (navMeshAgent.path.corners[currCornerIndex] - rigBod.position).sqrMagnitude;

            if (distanceFromCornerSqr <= Mathf.Pow(closeEnoughDistanceFromCorner, 2.0f)) return true;
            else return false;
        }
        else return false;
    }

    void CalcMovementInput()
    {
        switch (l_state)
        {
            case LinkState.NONE:
                switch (m_state)
                {
                    case MoveStates.STOPPED:
                        moveVect = Vector3.zero;
                        break;
                    case MoveStates.MOVING:
                    case MoveStates.SPRINTING:
                    case MoveStates.HOVERING:
                        switch (b_state)
                        {
                            case BehaviourState.STAND:
                                moveVect = Vector3.zero;
                                break;
                            case BehaviourState.PATROL:
                            case BehaviourState.SEARCH:
                            case BehaviourState.COMBAT:
                                moveVect = (navMeshAgent.steeringTarget - rigBod.position).normalized;
                                rotation.x = Vector3.SignedAngle(transform.forward, moveVect, transform.right);
                                rotation.y = Vector3.SignedAngle(transform.forward, moveVect, new Vector3(0.0f, 1.0f, 0.0f));
                                if (r_state == RotateBeforeMove.WAITFORROTATE)
                                {
                                    if (Mathf.Abs(((rotation.y < 0.0f) ? rotation.y + 360.0f : rotation.y) - modelTrans.eulerAngles.y) > maxRotationDegreesBeforeMove)
                                    {
                                        moveVect = Vector3.zero;
                                    }
                                }
                                else moveVect = Vector3.zero;
                                break;
                        }
                        break;
                }
                break;
            case LinkState.JUMPHOVER:
                moveVect = (offMeshLinkPoints[currOffMeshLinkPoint] - rigBod.position).normalized;
                rotation.x = Vector3.SignedAngle(transform.forward, moveVect, transform.right);
                rotation.y = Vector3.SignedAngle(transform.forward, moveVect, new Vector3(0.0f, 1.0f, 0.0f));
                if (r_state == RotateBeforeMove.WAITFORROTATE)
                {
                    if (Mathf.Abs(((rotation.y < 0.0f) ? rotation.y + 360.0f : rotation.y) - modelTrans.eulerAngles.y) > maxRotationDegreesBeforeMove)
                    {
                        moveVect = Vector3.zero;
                    }
                }
                else moveVect = Vector3.zero;
                break;
        }
        
    }

    bool CheckIfCanSeeTarget(Vector3 fromPos)
    {
        if (targetEntity != null)
        {
            Vector3 dir = targetEntity.position - fromPos;
            Debug.DrawRay(fromPos, dir);
            if (Physics.Raycast(fromPos, dir, out RaycastHit hit, maxDistFromEntity, LayerMask.GetMask("Obstacle", "EnvironmentBox")))
            {
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
            targetEntity = other.transform;
            if (CheckIfCanSeeTarget(rigBod.position)) b_state = BehaviourState.COMBAT;
            else b_state = BehaviourState.SEARCH;
        }
    }

    void PathCoroutineManager()
    {
        switch (b_state)
        {
            case BehaviourState.STAND:
            case BehaviourState.PATROL:
                if (pathCalcSearchRunning) { StopCoroutine(pathCalcSearch); pathCalcSearchRunning = false; }
                if (pathCalcCombatRunning) { StopCoroutine(pathCalcCombat); pathCalcCombatRunning = false; }
                break;
            case BehaviourState.SEARCH:
                if (!pathCalcSearchRunning) { StartCoroutine(pathCalcSearch); pathCalcSearchRunning = true; }
                if (pathCalcCombatRunning) { StopCoroutine(pathCalcCombat); pathCalcCombatRunning = false; }
                break;
            case BehaviourState.COMBAT:
                if (pathCalcSearchRunning) { StopCoroutine(pathCalcSearch); pathCalcSearchRunning = false; }
                if (!pathCalcCombatRunning) { StartCoroutine(pathCalcCombat); pathCalcCombatRunning = true; }
                break;
        }
    }

    IEnumerator RecalculatePathToTarget(float delayBetweenCalls)
    {
        while (targetEntity != null)
        {
            if (NavMesh.SamplePosition(targetEntity.position, out NavMeshHit nHit, maxDistFromEntity, NavMesh.AllAreas))
            {
                navMeshAgent.SetDestination(nHit.position);
                currCornerIndex = 0;
            }
            yield return new WaitForSeconds(delayBetweenCalls);
        }
    }
}
