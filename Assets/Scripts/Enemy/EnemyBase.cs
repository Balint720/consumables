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

public partial class EnemyBase : EntityClass
{
    protected enum PathTraverseType { PAUSEATPOINTS, MOVEINSTANTLY };
    enum LinkTraverseDir { FORWARD = 1, BACKWARDS = -1 };
    protected enum RotateBeforeMove { WAITFORROTATE, MOVEANYWAY };
    protected PathTraverseType p_state;
    [SerializeField] bool useCrowdedPatrolRoute;
    PatrolScript patrolRoute;

    protected NavMeshAgent navMeshAgent;

    int currPatrolPoint;                                // 0 based, points start from 1 in editor
    List<Vector3> offMeshLinkPoints;
    int currOffMeshLinkPoint;
    LinkTraverseDir linkDir;
    SphereCollider detectionSphere;
    protected float DetectionSphereRadius => detectionSphere.radius;
    [SerializeField] protected float maxDistFromEntity;
    protected Transform targetEntity;
    protected Timer searchTimer;
    protected Timer stopMoveTimer;
    [SerializeField] protected float searchTime;
    static float searchUpdateDestTime = 10.0f;
    static float combatUpdateDestTime = 0.25f;
    [SerializeField] float patrolPointPauseTime;
    [SerializeField] float pathCornerPauseTime;
    IEnumerator pathCalcSearch;
    bool pathCalcSearchRunning;
    IEnumerator pathCalcCombat;
    bool pathCalcCombatRunning;
    protected float closeEnoughDistanceFromCorner = 1.1f;
    static float meleeDistance = 3.0f;
    static float distanceEpsilon = 1.0f;
    static int MaxNavMeshIterations = 20;

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
                    case BehaviourState.STAND:              break;
                    case BehaviourState.PATROL:             BasePatrolState(); break;
                    case BehaviourState.SEARCH:             BaseSearchState(); break;
                    case BehaviourState.COMBAT_CHASE:       BaseCombatChaseState(); break;
                }
                break;
            case LinkState.JUMPHOVER:           BaseLinkJumpHoverState(); break;
        }
    }

    protected void CalcNewState()
    {
        BehaviourStateCalc();

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
                hSpeedCapMultiplier = sprintSpeedMult;
                break;
            case MoveStates.HOVERING:
                moveType = MoveType.FLY;
                break;
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

    protected bool CheckIfCanSeeTarget(Vector3 fromPos)
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
        if (hitBy.CompareTag("Entity"))
        {
            targetEntity = hitBy.transform;
            if (CheckIfCanSeeTarget(RigBodPos)) b_state = BehaviourState.COMBAT_CHASE;
            else b_state = BehaviourState.SEARCH;
        }
    }

    virtual protected void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Entity"))
        {
            targetEntity = other.transform;
            if (CheckIfCanSeeTarget(RigBodPos)) b_state = BehaviourState.COMBAT_CHASE;
            else b_state = BehaviourState.SEARCH;
        }
    }

    void PathCoroutineManager()
    {
        switch (b_state)
        {
            case BehaviourState.SEARCH:
                if (!pathCalcSearchRunning) { StartCoroutine(pathCalcSearch); pathCalcSearchRunning = true; }
                if (pathCalcCombatRunning) { StopCoroutine(pathCalcCombat); pathCalcCombatRunning = false; }
                break;
            case BehaviourState.COMBAT_CHASE:
                if (pathCalcSearchRunning) { StopCoroutine(pathCalcSearch); pathCalcSearchRunning = false; }
                if (!pathCalcCombatRunning) { StartCoroutine(pathCalcCombat); pathCalcCombatRunning = true; }
                break;
            default:
                if (pathCalcSearchRunning) { StopCoroutine(pathCalcSearch); pathCalcSearchRunning = false; }
                if (pathCalcCombatRunning) { StopCoroutine(pathCalcCombat); pathCalcCombatRunning = false; }
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
                if ((nHit.position - RigBodPos).sqrMagnitude > distanceEpsilon * distanceEpsilon)
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

    // Navmesh position calculations

    /// <summary>
    /// Gets a navmesh position on the perimeter of a cylinder with radius "distance" and center "targetPos". Returns Vector3.zero if couldn't find a position within set iteration count
    /// </summary>
    /// <param name="targetPos">Center of the cylinder</param>
    /// <param name="distance">Radius of the cylinder's base circle</param>
    /// <param name="maxRandomDegree">Maximum angle that the vector to original position be rotated by</param>
    /// <param name="seeTargetFromPos">If true, position is recalculated if target cannot be seen from new position</param>
    /// <returns></returns>
    protected Vector3 GetNavmeshPosByDistanceFromTarget(Vector3 targetPos, float distance, float maxRandomDegree, bool seeTargetFromPos = false)
    {
        int i = 0;
        NavMeshHit nHit;
        bool done = false;
        do
        {
            Vector3 sampledPosition = targetPos + Quaternion.Euler(0.0f, UnityEngine.Random.value * 2 * maxRandomDegree - maxRandomDegree, 0.0f) * (RigBodPos - targetPos).normalized * distance;
            done = NavMesh.SamplePosition(sampledPosition, out nHit, Mathf.Infinity, NavMesh.AllAreas);
            if (seeTargetFromPos)
            {
                seeTargetFromPos = Physics.Raycast(nHit.position + Vector3.up * (WorldEnvSize.y / 2), (targetPos - nHit.position).normalized, (targetPos - nHit.position).magnitude, LayerMask.GetMask("Obstacle"));
            }
            i++;
            Debug.DrawLine(nHit.position, targetPos, Color.red, 2.0f);
            if (i > MaxNavMeshIterations) return Vector3.zero;
        }
        while (seeTargetFromPos || !done);
        return nHit.position;
    }
}
