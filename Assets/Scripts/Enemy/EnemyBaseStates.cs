using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public partial class EnemyBase
{
    // States
    protected enum BehaviourState
    {
        STAND,
        PATROL,
        SEARCH,
        COMBAT_CHASE,
        COMBAT_REPOSITION,
        ATTACK,
    };
    protected enum MoveStates
    {
        STOPPED,
        WALKING,
        SPRINTING,
        HOVERING,
        JUMPING
    };
    protected enum LinkState
    {
        NONE,
        JUMPHOVER
    };
    protected BehaviourState b_state;
    protected MoveStates m_state;
    protected RotateBeforeMove r_state;
    protected LinkState l_state;
    protected void BasePatrolState()
    {
        if (!patrolRoute) { b_state = BehaviourState.STAND; return; }
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
    }
    protected void BaseSearchState()
    {
        if (!targetEntity)
        {
            navMeshAgent.ResetPath();
            b_state = BehaviourState.PATROL;
        }
    }

    protected void BaseCombatChaseState()
    {
        if (!targetEntity)
        {
            navMeshAgent.ResetPath();
            b_state = BehaviourState.PATROL;
        }
    }

    protected void BaseLinkJumpHoverState()
    {
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
    }

    virtual protected void BehaviourStateCalc()
    {
        switch (b_state)
        {
            case BehaviourState.PATROL:
                if (targetEntity != null)
                {
                    if (CheckIfCanSeeTarget(RigBodPos)) b_state = BehaviourState.COMBAT_CHASE;
                    else b_state = BehaviourState.SEARCH;
                }
                break;
            case BehaviourState.SEARCH:
                if (CheckIfCanSeeTarget(RigBodPos))
                {
                    b_state = BehaviourState.COMBAT_CHASE;
                    searchTimer.StopTimer();
                }
                else
                {
                    searchTimer.StartTimer(searchTime);

                    if (searchTimer.IsDone && (transform.position - targetEntity.position).sqrMagnitude > maxDistFromEntity * maxDistFromEntity) b_state = BehaviourState.PATROL;
                }
                break;
            case BehaviourState.COMBAT_CHASE:
            case BehaviourState.COMBAT_REPOSITION:
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
            default:
                searchTimer.StopTimer();
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
            if (stopMoveTimer.IsOn)
            {
                if (stopMoveTimer.IsDone) stopMoveTimer.StopTimer();
                else m_state = MoveStates.STOPPED;
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
                    case BehaviourState.COMBAT_CHASE:
                        if (navMeshAgent.remainingDistance > closeEnoughDistanceFromCorner)
                        {
                            if (b_state == BehaviourState.COMBAT_CHASE && navMeshAgent.remainingDistance > meleeDistance * 1.1f)
                                m_state = MoveStates.SPRINTING;
                            else
                                m_state = MoveStates.WALKING;
                        }
                        else
                            m_state = MoveStates.STOPPED;
                        break;
                    case BehaviourState.COMBAT_REPOSITION:
                        m_state = MoveStates.SPRINTING;
                        break;
                    case BehaviourState.ATTACK:
                        m_state = MoveStates.STOPPED;
                        break;
                }
            }
        }
    }

    protected void BaseMoveInputNoLink()
    {
        switch (m_state)
        {
            case MoveStates.STOPPED:
                moveVect = Vector3.zero;
                break;
            case MoveStates.WALKING:
            case MoveStates.SPRINTING:
                moveVect = navMeshAgent.steeringTarget - CenterPos;
                moveVect = new Vector3(moveVect.x, 0.0f, moveVect.z).normalized;
                break;
            case MoveStates.HOVERING:
                moveVect = (navMeshAgent.steeringTarget - CenterPos).normalized;
                break;
        }
    }
    protected void BaseLookDirNoLink()
    {
        switch (b_state)
        {
            case BehaviourState.STAND:
            case BehaviourState.ATTACK:
                break;
            case BehaviourState.PATROL:
            case BehaviourState.SEARCH:
                lookDir = moveVect;
                break;
            case BehaviourState.COMBAT_CHASE:
                lookDir = CheckIfCanSeeTarget(RigBodPos) ? targetEntity.position - RigBodPos : moveVect;
                break;
            case BehaviourState.COMBAT_REPOSITION:
                if (m_state == MoveStates.STOPPED) lookDir = CheckIfCanSeeTarget(RigBodPos) ? targetEntity.position - RigBodPos : lookDir;
                else lookDir = moveVect;
                break;
        }
        if (r_state == RotateBeforeMove.WAITFORROTATE)
        {
            if (Mathf.Abs(AngleFromLookDir[1]) > maxRotationDegreesBeforeMove)
            {
                moveVect = Vector3.zero;
            }
        }
    }
    protected void BaseMoveLookInputJumpHoverLink()
    {
        moveVect = (offMeshLinkPoints[currOffMeshLinkPoint] - BottomPos).normalized;
        lookDir = moveVect;
        if (r_state == RotateBeforeMove.WAITFORROTATE)
        {
            if (Mathf.Abs(AngleFromLookDir[1]) > maxRotationDegreesBeforeMove)
            {
                moveVect = Vector3.zero;
            }
        }
    }
}