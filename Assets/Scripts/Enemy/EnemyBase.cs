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
        COMBAT,
        ATTACK
    };

    protected enum PathTraverseType { PAUSEATPOINTS, MOVEINSTANTLY };
    protected enum MoveStates { STOPPED, WALKING, SPRINTING, HOVERING, JUMPING };
    enum LinkTraverseDir { FORWARD = 1, BACKWARDS = -1 };
    protected enum RotateBeforeMove { WAITFORROTATE, MOVEANYWAY };
    protected enum LinkState { NONE, JUMPHOVER };

    [SerializeField] bool useCrowdedPatrolRoute;
    protected BehaviourState b_state;
    protected PathTraverseType p_state;
    protected MoveStates m_state;
    protected RotateBeforeMove r_state;
    protected LinkState l_state;
    PatrolScript patrolRoute;

    protected NavMeshAgent navMeshAgent;

    int currPatrolPoint;                                // 0 based, points start from 1 in editor
    List<Vector3> offMeshLinkPoints;
    int currOffMeshLinkPoint;
    LinkTraverseDir linkDir;
    SphereCollider detectionSphere;
    protected float DetectionSphereRadius => detectionSphere.radius;
    [SerializeField] float maxDistFromEntity;
    protected Transform targetEntity;
    Timer searchTimer;
    Timer stopMoveTimer;
    [SerializeField] float searchTime;
    static float searchUpdateDestTime = 10.0f;
    static float combatUpdateDestTime = 0.25f;
    [SerializeField] float patrolPointPauseTime;
    [SerializeField] float pathCornerPauseTime;
    IEnumerator pathCalcSearch;
    bool pathCalcSearchRunning;
    IEnumerator pathCalcCombat;
    bool pathCalcCombatRunning;
    float closeEnoughDistanceFromCorner = 1.1f;
    static float meleeDistance = 3.0f;
    static float distanceEpsilon = 1.0f;

    // Animation
    Animator animator;
    float par_Speed;

    // Debug
    GameObject[] DebugSpheres;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    override protected void Start()
    {
        base.Start();

        // Detection collider init
        detectionSphere = GetComponentInChildren<SphereCollider>();
        if (detectionSphere != null)
        {
            if (detectionSphere.tag != "Detection")
            {
                detectionSphere = null;
                Debug.Log(gameObject.name + ": Couldn't find DetectionSphere");
            }
            else
            {
                detectionSphere.excludeLayers = ~(LayerMask.GetMask("EnvironmentBox") | (int)detectionSphere.includeLayers);
            }
        }
        else
        {
            Debug.Log(gameObject.name + ": Couldn't find DetectionSphere");
        }


        // Initialization
        // Patrol and off mesh traverse variables
        currPatrolPoint = 0;
        patrolRoute = null;
        offMeshLinkPoints = new List<Vector3>();
        currOffMeshLinkPoint = 0;

        // States
        b_state = BehaviourState.PATROL;
        p_state = PathTraverseType.PAUSEATPOINTS;
        m_state = MoveStates.WALKING;
        r_state = RotateBeforeMove.WAITFORROTATE;
        l_state = LinkState.NONE;

        // Timers
        searchTimer = new Timer();
        stopMoveTimer = new Timer();

        // Search times and their coroutines
        pathCalcSearch = RecalculatePathToTarget(searchUpdateDestTime);
        pathCalcSearchRunning = false;
        pathCalcCombat = RecalculatePathToTarget(combatUpdateDestTime);
        pathCalcCombatRunning = false;

        // NavMeshAgent component
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            Debug.Log(gameObject.name + " doesn't have a NavMeshAgent");
        }
        navMeshAgent.updatePosition = false;
        navMeshAgent.updateRotation = false;
        navMeshAgent.stoppingDistance = 0.0f;
        navMeshAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

        // Patrol route
        if (!FindPatrolRoute(!useCrowdedPatrolRoute))
        {
            Debug.Log(gameObject.name + ": No patrol route found");
        }

        // Animation
        animator = GetComponentInChildren<Animator>();

        if (animator == null) Debug.Log(gameObject.name + " has no animator");
        // Parameters
        par_Speed = 0.0f;

        StartCoroutine(ParameterCalc());

        // Debug
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

        // Animation parameters
        // Speed (between 0 and 1)
        animator.SetFloat("Speed", par_Speed);

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

        // Debug
        DebugText.text = "MoveVect: " + moveVect.ToString();
    }

    override protected void FixedUpdate()
    {
        CalcNewState();
        StateMachine();
        PathCoroutineManager();
        CalcMovementInput();

        base.FixedUpdate();
        navMeshAgent.nextPosition = RigBodPos;
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

    virtual protected void StateMachine()
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
                            if (navMeshAgent.remainingDistance < closeEnoughDistanceFromCorner)
                            {
                                navMeshAgent.ResetPath();
                            }
                        }
                        if (!navMeshAgent.hasPath)
                        {
                            if (p_state == PathTraverseType.PAUSEATPOINTS)
                            {
                                m_state = MoveStates.STOPPED;
                                stopMoveTimer.StopTimer();
                                stopMoveTimer.StartTimer(patrolPointPauseTime);
                            }
                            PatrolRouteCalc();
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
                int closestPoint = currOffMeshLinkPoint;
                float distanceFromCornerSqr = Mathf.Infinity;
                switch (linkDir)
                {
                    case LinkTraverseDir.FORWARD:
                        for (int i = currOffMeshLinkPoint; i < offMeshLinkPoints.Count(); i++)
                        {
                            float d = (offMeshLinkPoints[currOffMeshLinkPoint] - BottomPos).sqrMagnitude;
                            if (distanceFromCornerSqr > d)
                            {
                                distanceFromCornerSqr = d;
                                closestPoint = i;
                            }
                        }
                        break;
                    case LinkTraverseDir.BACKWARDS:
                        for (int i = currOffMeshLinkPoint; i >= 0; i--)
                        {
                            float d = (offMeshLinkPoints[currOffMeshLinkPoint] - BottomPos).sqrMagnitude;
                            if (distanceFromCornerSqr > d)
                            {
                                distanceFromCornerSqr = d;
                                closestPoint = i;
                            }
                        }
                        break;
                }

                currOffMeshLinkPoint = closestPoint;

                if (distanceFromCornerSqr <= Mathf.Pow(closeEnoughDistanceFromCorner, 2.0f))
                {
                    currOffMeshLinkPoint += (int)linkDir;
                }

                if (currOffMeshLinkPoint >= offMeshLinkPoints.Count() || currOffMeshLinkPoint < 0)
                {
                    l_state = LinkState.NONE;
                }
                break;
        }
    }

    virtual protected void CalcNewState()
    {
        switch (b_state)
        {
            case BehaviourState.PATROL:
                if (targetEntity != null)
                {
                    if (CheckIfCanSeeTarget(RigBodPos)) b_state = BehaviourState.COMBAT;
                    else b_state = BehaviourState.SEARCH;
                }
                break;
            case BehaviourState.SEARCH:
                if (CheckIfCanSeeTarget(RigBodPos))
                {
                    b_state = BehaviourState.COMBAT;
                    searchTimer.StopTimer();
                }
                else
                {
                    searchTimer.StartTimer(searchTime);

                    if (searchTimer.IsDone && (transform.position - targetEntity.position).sqrMagnitude > maxDistFromEntity * maxDistFromEntity) b_state = BehaviourState.PATROL;
                }
                break;
            case BehaviourState.COMBAT:
                if (!CheckIfCanSeeTarget(RigBodPos))
                {
                    searchTimer.StartTimer(searchTime / 2.0f);
                    if (searchTimer.IsDone)
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

        MoveStateCalc();

        switch (m_state)
        {
            case MoveStates.STOPPED:
                if (stopMoveTimer.IsDone)
                {
                    MoveStateCalc();
                }
                break;
            case MoveStates.WALKING:
                moveType = MoveType.WALK;
                hSpeedCapMultiplier = 1.0f;
                break;
            case MoveStates.SPRINTING:
                moveType = MoveType.WALK;
                hSpeedCapMultiplier = 1.3f;
                break;
            case MoveStates.HOVERING:
                moveType = MoveType.FLY;
                break;
        }
    }

    virtual protected void MoveStateCalc()
    {
        if (navMeshAgent.isOnOffMeshLink && l_state == LinkState.NONE)
        {
            try
            {
                int numOfChildren = navMeshAgent.currentOffMeshLinkData.owner.GetComponent<Transform>().childCount;
                offMeshLinkPoints.Clear();

                for (int i = 0; i < numOfChildren; i++)
                {
                    Transform t = navMeshAgent.currentOffMeshLinkData.owner.GetComponent<Transform>().GetChild(i);
                    offMeshLinkPoints.Add(t.position);
                }

                float startPosDist = (offMeshLinkPoints[0] - BottomPos).sqrMagnitude;
                float endPosDist = (offMeshLinkPoints[offMeshLinkPoints.Count() - 1] - BottomPos).sqrMagnitude;

                if (startPosDist <= endPosDist) { linkDir = LinkTraverseDir.FORWARD; currOffMeshLinkPoint = 0; }
                else { linkDir = LinkTraverseDir.BACKWARDS; currOffMeshLinkPoint = offMeshLinkPoints.Count() - 1; }

                l_state = LinkState.JUMPHOVER;


                m_state = MoveStates.HOVERING;
                navMeshAgent.CompleteOffMeshLink();

                Debug.Log("Starting NavMeshLink");
            }
            catch (Exception e)
            {
                Debug.Log(e);
                Debug.Log("Couldn't get transforms from offmeshlink");
                Destroy(gameObject);
            }
        }
        else if (l_state == LinkState.NONE)
        {
            if (m_state == MoveStates.STOPPED && stopMoveTimer.IsOn)
            {
                if (stopMoveTimer.IsDone) stopMoveTimer.StopTimer();
            }
            else
            {
                switch (b_state)
                {
                    case BehaviourState.STAND:
                        m_state = MoveStates.STOPPED;
                        break;
                    case BehaviourState.PATROL:
                        m_state = MoveStates.WALKING;
                        break;
                    case BehaviourState.SEARCH:
                        m_state = MoveStates.WALKING;
                        break;
                    case BehaviourState.COMBAT:
                        if (navMeshAgent.remainingDistance > closeEnoughDistanceFromCorner)
                        {
                            if (b_state == BehaviourState.COMBAT && navMeshAgent.remainingDistance > meleeDistance * 1.1f)
                                m_state = MoveStates.SPRINTING;
                            else
                                m_state = MoveStates.WALKING;
                        }
                        else
                            m_state = MoveStates.STOPPED;
                        break;
                }
            }
        }
    }

    bool PatrolRouteCalc()
    {
        if (patrolRoute != null)
        {
            if (!patrolRoute.GetPoint(currPatrolPoint, out Transform newLoc))
            {
                Debug.Log(gameObject.name + ": Couldn't get patrol point");
                return false;
            }
            navMeshAgent.SetDestination(newLoc.position);
            currPatrolPoint = ++currPatrolPoint % patrolRoute.GetNumOfPoints();
            return true;
        }
        else
        {
            return false;
        }
    }

    virtual protected void CalcMovementInput()
    {
        switch (l_state)
        {
            case LinkState.NONE:
                switch (m_state)
                {
                    case MoveStates.STOPPED:
                        moveVect = Vector3.zero;
                        break;
                    case MoveStates.WALKING:
                    case MoveStates.SPRINTING:
                        moveVect = navMeshAgent.steeringTarget - CenterPos;
                        moveVect = new Vector3(moveVect.x, 0.0f, moveVect.z).normalized;
                        PitchX = Vector3.SignedAngle(transform.forward, moveVect, transform.right);
                        YawY = Vector3.SignedAngle(transform.forward, moveVect, Vector3.up);
                        break;
                    case MoveStates.HOVERING:
                        moveVect = (navMeshAgent.steeringTarget - CenterPos).normalized;
                        PitchX = Vector3.SignedAngle(transform.forward, moveVect, transform.right);
                        YawY = Vector3.SignedAngle(transform.forward, moveVect, Vector3.up);
                        break;
                }
                if (r_state == RotateBeforeMove.WAITFORROTATE)
                {
                    if (Mathf.Abs(((YawY < 0.0f) ? YawY + 360.0f : YawY) - modelTrans.eulerAngles.y) > maxRotationDegreesBeforeMove)
                    {
                        moveVect = Vector3.zero;
                    }
                }
                else moveVect = Vector3.zero;
                break;
            case LinkState.JUMPHOVER:
                moveVect = (offMeshLinkPoints[currOffMeshLinkPoint] - BottomPos).normalized;
                PitchX = Vector3.SignedAngle(transform.forward, moveVect, transform.right);
                YawY = Vector3.SignedAngle(transform.forward, moveVect, Vector3.up);
                if (r_state == RotateBeforeMove.WAITFORROTATE)
                {
                    if (Mathf.Abs(((YawY < 0.0f) ? YawY + 360.0f : YawY) - modelTrans.eulerAngles.y) > maxRotationDegreesBeforeMove)
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

    protected override void OnGettingHit(GameObject hitBy)
    {
        base.OnGettingHit(hitBy);
        if (hitBy.CompareTag("Entity"))
        {
            targetEntity = hitBy.transform;
            if (CheckIfCanSeeTarget(RigBodPos)) b_state = BehaviourState.COMBAT;
            else b_state = BehaviourState.SEARCH;
        }
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (other.name.Contains("Player"))
        {
            targetEntity = other.transform;
            if (CheckIfCanSeeTarget(RigBodPos)) b_state = BehaviourState.COMBAT;
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

    virtual protected IEnumerator RecalculatePathToTarget(float delayBetweenCalls)
    {
        while (targetEntity != null)
        {
            Vector3 positionCloseToEntity = targetEntity.position + (RigBodPos - targetEntity.position).normalized * meleeDistance;
            if (NavMesh.SamplePosition(positionCloseToEntity, out NavMeshHit nHit, maxDistFromEntity, NavMesh.AllAreas))
            {
                if ((nHit.position - RigBodPos).sqrMagnitude > distanceEpsilon*distanceEpsilon)
                    navMeshAgent.SetDestination(nHit.position);
            }
            yield return new WaitForSeconds(delayBetweenCalls);
        }
    }

    virtual protected IEnumerator ParameterCalc()
    {
        while (true)
        {
            float val = RigBodVel.sqrMagnitude;
            if (val > 1.0f)
            {
                val = ((RigBodVel.sqrMagnitude / 10.0f) + 1.2f) / hSpeedCap;
            }
            if (Mathf.Abs(val - par_Speed) > 0.01f)
            {
                par_Speed += Mathf.Sign(val - par_Speed) * 0.01f;
            }
            yield return null;
        }
    }
}
