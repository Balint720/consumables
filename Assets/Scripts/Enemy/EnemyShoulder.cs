using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public partial class EnemyShoulder : EnemyBase
{
    [SerializeField] WeaponClass.WeaponStats currStats;
    float dmgMod;
    float projSpeedMod;
    float weaponKnockbackMod;
    [SerializeField] float repositionPointPauseTime;
    [SerializeField] float chargeAttackTime;
    float chargeAttackTimeMultiplier;
    [SerializeField] float chargeAttackDelay;
    Timer chargeAttackTimer;
    [SerializeField] Cooldown chargeAttackCooldown;
    [SerializeField] Transform leftFireLoc;
    [SerializeField] Transform rightFireLoc;
    Vector3 direction;
    [SerializeField] float distanceFromTarget;

    // Animation
    static float smallDelayRightBeforeFire = 0.2f;
    override protected void Start()
    {
        base.Start();

        // Timers and cooldowns
        chargeAttackTimer = new Timer();
        chargeAttackCooldown.Init();

        // Multipliers
        chargeAttackTimeMultiplier = 1.0f;
        dmgMod = 1.0f;
        projSpeedMod = 1.0f;
        weaponKnockbackMod = 1.0f;

        // States
        a_state = ChargeAttack.CHARGING;

        // Shoulder fire transforms
        if (leftFireLoc == null || rightFireLoc == null)
        {
            Debug.Log(gameObject + " does not have firing cannon locations set: Set it in the editor");
            Destroy(this);
        }
    }
    override protected void StateMachine()
    {
        switch (l_state)
        {
            case LinkState.NONE:
                switch (b_state)
                {
                    case BehaviourState.STAND: break;
                    case BehaviourState.PATROL: BasePatrolState(); break;
                    case BehaviourState.SEARCH: BaseSearchState(); break;
                    case BehaviourState.COMBAT_CHASE: BaseCombatChaseState(); break;
                    case BehaviourState.COMBAT_REPOSITION: ShoulderCombatRepositionState(); break;
                    case BehaviourState.ATTACK: ShoulderAttackState(); break;
                }
                break;
            case LinkState.JUMPHOVER: BaseLinkJumpHoverState(); break;
        }
    }

    override protected void Update()
    {
        base.Update();

        // Cooldowns and timers
        chargeAttackTimer.CallPerFrame(Time.deltaTime);
        chargeAttackCooldown.CallPerFrame(Time.deltaTime);
    }

    bool FireCannon(Transform shoulder)
    {
        ProjectileClass p = Instantiate(currStats.projectile, shoulder.position, shoulder.rotation);
        p.SetDirection(direction);

        // Set projectile values and owner
        p.SetValuesOfProj(Mathf.RoundToInt(currStats.dmg * dmgMod), currStats.criticalDmgMult, currStats.projSpeed * projSpeedMod, currStats.weaponKnockback * weaponKnockbackMod);
        p.Owner = gameObject;
        return true;
    }

    override protected void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Entity"))
        {
            targetEntity = other.transform;
            if (CheckIfCanSeeTarget(RigBodPos))
            {
                navMeshAgent.destination = GetNavmeshPosByDistanceFromTarget(targetEntity.position, distanceFromTarget, 60.0f, false);
                b_state = BehaviourState.COMBAT_REPOSITION;
            }
            else b_state = BehaviourState.SEARCH;
        }
    }
    
    override protected void OnGettingHit(GameObject hitBy)
    {
        if (hitBy.CompareTag("Entity"))
        {
            targetEntity = hitBy.transform;
            if (CheckIfCanSeeTarget(RigBodPos))
            {
                navMeshAgent.destination = GetNavmeshPosByDistanceFromTarget(targetEntity.position, distanceFromTarget, 60.0f, false);
                b_state = BehaviourState.COMBAT_REPOSITION;
            }
            else b_state = BehaviourState.SEARCH;
        }
    }
}
