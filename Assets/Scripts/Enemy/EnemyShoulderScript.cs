using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class EnemyShoulderScript : EnemyBase
{
    [SerializeField] WeaponClass.WeaponStats currStats;
    float dmgMod;
    float projSpeedMod;
    float weaponKnockbackMod;
    [SerializeField] float chargeAttackTime;
    float chargeAttackTimeMultiplier;
    [SerializeField] float chargeAttackDelay;
    enum ChargeAttack
    {
        CHARGING,
        RIGHT_READY,
        DELAY,
        LEFT_READY
    }
    ChargeAttack a_state;
    Timer chargeAttackTimer;
    [SerializeField] Cooldown chargeAttackCooldown;
    [SerializeField] Transform leftFireLoc;
    [SerializeField] Transform rightFireLoc;
    Vector3 direction;

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
        base.StateMachine();

        switch (l_state)
        {
            case LinkState.NONE:
                switch (b_state)
                {
                    case BehaviourState.COMBAT:
                        if (navMeshAgent.remainingDistance < DetectionSphereRadius && chargeAttackCooldown.IsReady)
                        {
                            chargeAttackCooldown.UseCooldown();
                            b_state = BehaviourState.ATTACK;
                        }
                        break;
                    case BehaviourState.ATTACK:
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
                                    b_state = BehaviourState.COMBAT;
                                }
                                break;
                        }
                        break;
                }
                break;
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

    protected override void MoveStateCalc()
    {
        base.MoveStateCalc();
        if (l_state == LinkState.NONE)
        {
            switch (b_state)
            {
                case BehaviourState.ATTACK:
                    m_state = MoveStates.STOPPED;
                    break;
            }
        }
    }
}
