using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.SearchService;
using UnityEngine;

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
    SphereCollider detectionSphere;
    public float maxDistFromEntity;
    Transform targetEntity;
    Timer searchTimer;
    public float searchTime;

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
            }
        }
        else
        {
            Debug.Log(gameObject.name + ": Couldn't find DetectionSphere");
        }


        // Initialization
        patrolRoute = null;
        b_state = BehaviourState.PATROL;
        searchTimer = new Timer();
        detectionSphere.excludeLayers = ~(LayerMask.GetMask("EnvironmentBox") | (int)detectionSphere.includeLayers);

        if (!FindPatrolRoute(!useCrowdedPatrolRoute))
        {
            Debug.Log(gameObject.name + ": No patrol route found");
        }
    }

    protected virtual void Update()
    {
        // Cooldowns and timers
        searchTimer.CallPerFrame(Time.deltaTime);

        //Debug.Log(b_state);
    }

    override protected void FixedUpdate()
    {
        base.FixedUpdate();

        CalcNewState();
        DebugText.text = b_state.ToString();
        DebugText.text += "\nTimer: " + searchTimer.GetTimeLeft().ToString();
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

    void PatrolMovement()
    {

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
            targetEntity = other.transform;
            if (CheckIfCanSeeTarget()) b_state = BehaviourState.COMBAT;
            else b_state = BehaviourState.SEARCH;
        }
    }
}
