using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using Unity.VisualScripting.ReorderableList.Element_Adder_Menu;
using UnityEditor.Animations;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

public partial class EnemyBase : EntityClass
{
    SphereCollider detectionSphere;
    protected float DetectionSphereRadius => detectionSphere.radius;
    [SerializeField] protected float maxDistFromEntity;
    protected Transform targetEntity;
    protected float closeEnoughDistanceFromCorner = 1.1f;
    static float meleeDistance = 3.0f;

    // Timers
    protected Timer stopMoveTimer;                  // How long to stop moving for


    // Animation
    protected Animator animator;
    protected AnimatorController animatorController;
    protected float par_Speed;

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
        else
        {
            animatorController = (AnimatorController)animator.runtimeAnimatorController;
            if (animatorController == null) Debug.Log(animator.name + " has no controller");
        }
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

    virtual protected void CalcMovementInput()
    {
        switch (l_state)
        {
            case LinkState.NONE:
                BaseMoveInputNoLink();
                BaseLookDirNoLink();
                break;
            case LinkState.JUMPHOVER:
                BaseMoveLookInputJumpHoverLink();
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
