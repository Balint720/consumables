using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public partial class EnemyShoulder
{
    enum ChargeAttack
    {
        CHARGING,
        RIGHT_READY,
        DELAY,
        LEFT_READY
    }
    ChargeAttack a_state;
    protected void ShoulderCombatRepositionState()
    {
        if (!targetEntity)
        {
            navMeshAgent.ResetPath();
            b_state = BehaviourState.PATROL;
        }
        else
        {
            if (navMeshAgent.remainingDistance < closeEnoughDistanceFromCorner)
            {
                navMeshAgent.destination = GetNavmeshPosByDistanceFromTarget(targetEntity.position, distanceFromTarget, 60.0f, false);

                stopMoveTimer.StartTimer(chargeAttackCooldown.Timer);

                if (chargeAttackCooldown.IsReady)
                {
                    chargeAttackCooldown.UseCooldown();
                    b_state = BehaviourState.ATTACK;
                    stopMoveTimer.StopTimer();
                }
            }
        }
    }

    protected void ShoulderAttackState()
    {
        switch (a_state)
        {
            case ChargeAttack.CHARGING:
                chargeAttackTimer.StartTimer(chargeAttackTime * chargeAttackTimeMultiplier);
                if (chargeAttackTimer.IsDone)
                {
                    chargeAttackTimer.StopTimer();
                    Vector3 d = (targetEntity.position - rightFireLoc.position).normalized;
                    direction = new Vector3(d.x, d.y, d.z);
                    a_state = ChargeAttack.RIGHT_READY;
                }
                break;
            case ChargeAttack.RIGHT_READY:
                chargeAttackTimer.StartTimer(smallDelayRightBeforeFire);
                if (chargeAttackTimer.IsDone)
                {
                    chargeAttackTimer.StopTimer();
                    FireCannon(rightFireLoc);
                    a_state = ChargeAttack.DELAY;
                }
                break;
            case ChargeAttack.DELAY:
                chargeAttackTimer.StartTimer(chargeAttackDelay * chargeAttackTimeMultiplier);
                if (chargeAttackTimer.IsDone)
                {
                    chargeAttackTimer.StopTimer();
                    Vector3 d = (targetEntity.position - leftFireLoc.position).normalized;
                    direction = new Vector3(d.x, d.y, d.z);
                    a_state = ChargeAttack.LEFT_READY;
                }
                break;
            case ChargeAttack.LEFT_READY:
                chargeAttackTimer.StartTimer(smallDelayRightBeforeFire);
                if (chargeAttackTimer.IsDone)
                {
                    chargeAttackTimer.StopTimer();
                    FireCannon(leftFireLoc);
                    a_state = ChargeAttack.CHARGING;
                    b_state = BehaviourState.COMBAT_REPOSITION;
                }
                break;
        }
    }

    protected override void BehaviourStateCalc()
    {
        switch (b_state)
        {
            case BehaviourState.PATROL:
                if (targetEntity != null)
                {
                    if (CheckIfCanSeeTarget(RigBodPos)) b_state = BehaviourState.COMBAT_REPOSITION;
                    else b_state = BehaviourState.SEARCH;
                }
                break;
            case BehaviourState.SEARCH:
                if (CheckIfCanSeeTarget(RigBodPos))
                {
                    b_state = BehaviourState.COMBAT_REPOSITION;
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

    protected override void MoveStateCalc()
    {
        base.MoveStateCalc();

    }
}