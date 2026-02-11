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

public partial class EnemyBase
{
    // Enums
    protected enum PathTraverseType { PAUSEATPOINTS, MOVEINSTANTLY };
    enum LinkTraverseDir { FORWARD = 1, BACKWARDS = -1 };
    protected enum RotateBeforeMove { WAITFORROTATE, MOVEANYWAY };
    protected PathTraverseType p_state;
    LinkTraverseDir linkDir;

    // Optimization values
    static float distanceEpsilon = 1.0f;                // How close to point before we can consider entity to have "reached" it
    static int MaxNavMeshIterations = 20;               // How many times should nav mesh calculations be done before giving up

    // Patrol, search and state timers
    // Three basic states:
    // Patrol: Following set path on map
    // Search: Thinks there is an enemy nearby, is looking for it going to last seen positions
    // Combat: Sees enemy, is in combat with said enemy
    [SerializeField] float patrolPointPauseTime;
    [SerializeField] float pathCornerPauseTime;
    protected Timer searchTimer;
    [SerializeField] protected float searchTime;
    static float searchUpdateDestTime = 10.0f;
    static float combatUpdateDestTime = 0.25f;
    IEnumerator pathCalcSearch;
    bool pathCalcSearchRunning;
    IEnumerator pathCalcCombat;
    bool pathCalcCombatRunning;

    [SerializeField] bool useCrowdedPatrolRoute;
    PatrolScript patrolRoute;

    protected NavMeshAgent navMeshAgent;

    int currPatrolPoint;                                // 0 based, points start from 1 in editor
    List<Vector3> offMeshLinkPoints;
    int currOffMeshLinkPoint;

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
}