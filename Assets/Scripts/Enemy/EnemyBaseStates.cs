using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

using Vector3 = UnityEngine.Vector3;

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
                /*
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
                */

                Vector3 startPos = navMeshAgent.currentOffMeshLinkData.startPos;
                Vector3 endPos = navMeshAgent.currentOffMeshLinkData.endPos;

                int numOfPoints = (int)((endPos - startPos).sqrMagnitude / 1.25f);


                offMeshLinkPoints = new List<Vector3>(numOfPoints);
                for (int i = 0; i < numOfPoints; i++) offMeshLinkPoints.Add(Vector3.zero);
                offMeshLinkPoints[0] = startPos; offMeshLinkPoints[numOfPoints - 1] = endPos;


                float distanceX = Vector3.Dot(endPos - startPos, Vector3.right);
                float distanceZ = Vector3.Dot(endPos - startPos, Vector3.forward);
                float distanceY = Vector3.Dot(endPos - startPos, Vector3.up);
                float incrementAbsolute = 1.0f / numOfPoints;
                for (int i = 1; i < numOfPoints - 1; i++)
                {
                    // Function that starts slow then rapidly increases
                    // x^3
                    float x_xz = Mathf.Pow(incrementAbsolute * i, 3);
                    // Function mapping 0 to 1 values with start end multipliers 0.5 and middle peak of 1.25 on a curve, where the integral value of 0 to 1 is 1:
                    // -3 * (x - 0.5)^{2} + 1.25
                    float x_y = 1.0f - MathFunctions.DecayFunction(0.347f, 2.0f, 0.1f, 5.0f, incrementAbsolute * i);
                    Debug.Log("time: " + incrementAbsolute * i);
                    Debug.Log("x_xz: " + x_xz);
                    Debug.Log("x_y: " + x_y);
                    float newPointX = startPos.x + distanceX * x_xz;
                    float newPointZ = startPos.z + distanceZ * x_xz;
                    float newPointY = startPos.y + distanceY * x_y;

                    offMeshLinkPoints[i] = new Vector3(newPointX, newPointY, newPointZ);
                }

                // Debug
                foreach (Vector3 point in offMeshLinkPoints)
                {
                    GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphere.GetComponent<Collider>().enabled = false;
                    sphere.transform.position = point;
                }

                l_state = LinkState.JUMPHOVER;
                m_state = MoveStates.HOVERING;

                linkDir = LinkTraverseDir.FORWARD; currOffMeshLinkPoint = 0;

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
                if (moveVect != Vector3.zero) lookDir = moveVect;
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