using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using UnityEngine.VFX;



public class WeaponClass : MonoBehaviour
{
    public enum FiringMode
    {
        SEMI,
        AUTO,
        BOW,
        CHARGE
    }

    public enum WeaponType
    {
        HITSCAN_SINGLE,
        HITSCAN_SPREAD,
        PROJECTILE,
        PROJECTILE_MULTIPLE
    }

    public enum WeaponModifier
    {
        BASE,
        ELECTRIC
    }

    public enum TriggerState
    {
        RELEASED,
        HELD
    }

    bool _triggerShot;
    public bool TriggerShot
    {
        get
        {
            if (_triggerShot)
            {
                _triggerShot = false;
                return true;
            }
            else return false;
        }

        set
        {
            _triggerShot = value;
        }
    } 

    [Serializable]
    public struct WeaponStats
    {
        public int dmg;                                 // Damage will be calculated for every bullet
        public float criticalDmgMult;                   // Multiplier for damage if we hit critical hitbox
        public uint pelCount;                           // Pellet count for shotguns
        public List<Vector2> recoilPattern;             // List of recoilVal values for recoil pattern (can also be where bullets from a shotgun go)
        public float recoilRecoveryTime;                // How long until recoil resets in seconds
        public float bloom;                             // Random spread of weapon, deviation from where the bullet should go, value is maximum possible deviation
        public int rpm;                                 // Fire rate: rounds per minute
        public float weaponKnockback;                 // Strength of weaponKnockback applied by weapon
        public float range;                             // Range of weapon, for hitscan this is the max distance of the raycast, for projectiles the projectile gets destroyed after traveling this many units
        public float projSpeed;                         // Speed of projectile
        public float chargeMaxDur;                      // How long to fully charge weapon (in seconds)
        public WeaponType weaponType;                   // Hitscan, projectile etc
        public FiringMode fireMode;
        public ProjectileClass projectile;              // Projectile that is sent by weapon
    }

    public FiringMode FireMode
    {
        get => currStats.fireMode;
        private set => currStats.fireMode = value;
        }

    GameObject owner;
    public GameObject Owner
    {
        get
        {
            return owner;
        }
        set
        {
            owner = value;
        }
    }
    public LayerMask layerMaskOfHitscan;

    // State
    WeaponModifier w_mod;
    WeaponModifier prev_w_mod;
    TriggerState t_state;
    public TriggerState TriggerSt
    {
        get => t_state;
    }
    bool isActive;
    bool isPartPlaying;


    // Stats
    public WeaponStats baseStats;
    public List<WeaponStats> modifiedStats;
    WeaponStats currStats;
    float currModDur;
    float currChargeDur;
    public float ChargeMod {
        get
        {
            float tmp = currChargeDur / (currStats.chargeMaxDur * chargeMaxDurMod);
            return (tmp > 1.0f) ? 1.0f : tmp;
        }
}
    float dmgMod;                               // Damage will be multiplied by this value
    int recoilInd;                              // Index of where we are in the recoil pattern
    float recoilValMod;                         // Recoil will be multiplied by this value
    float bloomMod;                             // Maximum bloom will be multiplied by this value
    float rpmMod;                               // RPM will be multiplied by this value
    float weaponKnockbackMod;                         // weaponKnockback will be multiplied by this value
    float projSpeedMod;                         // Speed of projectile will be multiplied by this value
    float chargeMaxDurMod;                      // Duration for maximum charge will be multiplied by this value
    public Vector3 offset;                      // Visual fired bullets are offset from here (Maybe projectiles will just straight up be offset)

    // Time
    Cooldown canShootRPM;

    // Model
    public Vector3 modelOffset;
    public Quaternion modelOffsetRot;                // Weapon model offset rotation from character rotation (for fps perspective)
    public Vector3 modelScale;
    VisualEffect circlingVFX;
    VFXRenderer circlingVFXRenderer;
    MeshRenderer[] renderers;


    void Start()
    {
        if (layerMaskOfHitscan == LayerMask.GetMask())
        {
            layerMaskOfHitscan = LayerMask.GetMask("Hitbox", "Obstacle");
        }

        // Set intial values for multipliers
        dmgMod = 1.0f;
        recoilInd = 0;
        recoilValMod = 1.0f;
        bloomMod = 1.0f;
        rpmMod = 1.0f;
        weaponKnockbackMod = 1.0f;
        projSpeedMod = 1.0f;
        chargeMaxDurMod = 1.0f;
        currModDur = 0.0f;
        currChargeDur = 0.0f;

        // Cooldowns
        canShootRPM = new Cooldown();
        canShootRPM.timerMax = baseStats.rpm / 60.0f;
        canShootRPM.Init();

        // Set stats to base stats
        currStats = baseStats;
        w_mod = WeaponModifier.BASE;
        t_state = TriggerState.RELEASED;

        // On spawn be unequipped
        isActive = false;

        // Get renderer to hide and show weapon
        renderers = GetComponentsInChildren<MeshRenderer>();

        // Get vfx for consumable effect
        circlingVFX = GetComponent<VisualEffect>();
        if (circlingVFX != null)
        {
            circlingVFXRenderer = circlingVFX.GetComponent<VFXRenderer>();
        }
        isPartPlaying = true;

        //transform.localScale += modelScale;
    }

    void Update()
    {
        // Cooldowns
        canShootRPM.CallPerFrame(Time.deltaTime);
        currChargeDur += Time.deltaTime;

        if (isActive)
        {
            transform.position += Quaternion.LookRotation(transform.forward) * modelOffset;
            transform.rotation *= modelOffsetRot;
        }
    }

    void FixedUpdate()
    {
        canShootRPM.timerMax = 60.0f / currStats.rpm;
        canShootRPM.ModifyCooldown(rpmMod, false);

        ApplyModifier();

        // Timers
        if (currModDur > 0.0f && w_mod != WeaponModifier.BASE)
        {
            currModDur -= Time.fixedDeltaTime;
        }
        else
        {
            w_mod = WeaponModifier.BASE;
        }

        currChargeDur += Time.fixedDeltaTime;
    }

    /// <summary>
    /// Shoot a ray into direction, return hitInfo
    /// </summary>
    /// <param name="origin"></param>
    /// <param name="direction"></param>
    /// <param name="rotation"></param>
    /// <returns></returns>
    RaycastHit ShootHitScan(Vector3 origin, Vector3 direction)
    {
        // Shoot ray into direction from origin with a maximum range
        RaycastHit hitInfo;
        Physics.Raycast(origin, direction, out hitInfo, currStats.range, layerMaskOfHitscan);

        return hitInfo;
    }

    /// <summary>
    /// Shoot a projectile into direction, also sets projectile damage values
    /// </summary>
    /// <param name="origin"></param>
    /// <param name="direction"></param>
    /// <param name="rotation"></param>
    void ShootProjectile(Vector3 origin, Vector3 direction, Quaternion rotation, float chargeDurSpeedMod = 1.0f)
    {
        // Set the direction and offset IF there is a hitscanProjectile
        ProjectileClass p = Instantiate(currStats.projectile, origin, rotation);
        p.SetDirection(direction);

        // Set projectile values and owner
        p.SetValuesOfProj(Mathf.RoundToInt(currStats.dmg * dmgMod * chargeDurSpeedMod), currStats.criticalDmgMult, currStats.projSpeed * projSpeedMod * chargeDurSpeedMod, currStats.weaponKnockback * weaponKnockbackMod * chargeDurSpeedMod);
        p.Owner = owner;
    }

    public void Fire(CameraControl cam, ref Vector2 addRot, Vector3 speed = new Vector3())
    {
        Vector3 origin = cam.transform.position;
        Vector3 direction = cam.transform.forward;
        Quaternion rotation = cam.transform.rotation;

        Fire(origin, direction, rotation, ref addRot, speed);
    }

    public void Fire(Vector3 origin, Vector3 direction, Quaternion rotation, ref Vector2 addRot, Vector3 speed = new Vector3())
    {
        // Only shoot if rate of fire allows it
        if (canShootRPM.IsReady)
        {
            RaycastHit hitInfo;

            // Different methods for hitscan weapons, projectile weapons
            switch (currStats.weaponType)
            {
                case WeaponType.HITSCAN_SINGLE:
                    // Reset recoil if enough time has passed; Otherwise progress recoil pattern
                    if (Time.time - canShootRPM.LastTimeUsed >= currStats.recoilRecoveryTime)
                    {
                        recoilInd = 0;
                    }
                    else
                    {
                        if (++recoilInd >= currStats.recoilPattern.Count)
                        {
                            recoilInd = currStats.recoilPattern.Count - 1;
                        }
                    }

                    hitInfo = ShootHitScan(origin, direction);
                    CheckIfHit(hitInfo, direction);
                    ShootCosmeticProj(origin, rotation, direction, hitInfo, speed);

                    if (currStats.recoilPattern.Count != 0)
                    {
                        addRot += new Vector2(-currStats.recoilPattern[recoilInd].x * recoilValMod + UnityEngine.Random.Range(0.0f, currStats.bloom * bloomMod), currStats.recoilPattern[recoilInd].y * recoilValMod + UnityEngine.Random.Range(0.0f, currStats.bloom * bloomMod));
                    }

                    break;

                case WeaponType.HITSCAN_SPREAD:
                    // Shoot the pelCount amount of pellets
                    for (int i = 0; i < currStats.pelCount; i++)
                    {
                        // Calculate pellet direction and rotation
                        Vector3 pelDir = CalcRecoiledDir(direction, i);
                        Quaternion pelRot = CalcRecoiledRot(rotation, i);

                        // Shoot pellet's ray
                        hitInfo = ShootHitScan(origin, pelDir);
                        CheckIfHit(hitInfo, pelDir);
                        ShootCosmeticProj(origin, rotation, pelDir, hitInfo, speed);

                    }
                    break;

                case WeaponType.PROJECTILE:
                    Vector3 projOffsetOrigin = origin + rotation * new Vector3(offset.x, offset.y, offset.z);
                    float chargeMod = 1.0f;
                    if (currStats.fireMode == FiringMode.BOW || currStats.fireMode == FiringMode.CHARGE)
                    {
                        chargeMod = ChargeMod;
                    }
                    ShootProjectile(projOffsetOrigin, direction, rotation, chargeMod);
                    break;
            }

            canShootRPM.UseCooldown();
        }
    }

    void ShootCosmeticProj(Vector3 origin, Quaternion rotation, Vector3 dir, RaycastHit hitInfo, Vector3 speed)
    {
        // Cosmetic projectile
        if (currStats.projectile != null)
        {
            // Get point and direction from where the visual projectile should fire (gun barrel)
            // The direction should be towards the point that the ray hit, and if the ray hit nothing then just shoot it in the direction 50 units away
            Vector3 offsetOrigin = origin + rotation * new Vector3(offset.x, offset.y, offset.z);
            Vector3 offsetDir = Vector3.Normalize(origin + 50 * dir - offsetOrigin);
            if (hitInfo.collider != null)
            {
                offsetDir = Vector3.Normalize(hitInfo.point - offsetOrigin);
            }
            // Test: Adding the speed of the shooter for better cosmetic projectile
            offsetOrigin += speed;
            // Shoot the projectile with the calculated direction and point with offset
            ShootProjectile(offsetOrigin, offsetDir, rotation);
        }
    }

    void CheckIfHit(RaycastHit hitInfo, Vector3 dir)
    {
        // If we hit an entity, make it reduce its hp by the weapon's dmg
        if (hitInfo.collider != null)
        {
            if (hitInfo.collider.gameObject != Owner)
            {
                if (hitInfo.collider.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
                {
                    try
                    {
                        ApplyDamageToHBox(hitInfo.collider.gameObject.GetComponent<HitboxScript>(), dir);
                    }
                    catch (Exception e)
                    {
                        Debug.Log("Caught exception: " + e);
                        Debug.Log("Couldn't get HitboxScript from hitbox");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Calculates the new direction, modifying it with the recoil pattern value
    /// </summary>
    /// <param name="dir"></param>
    /// <param name="ind"></param>
    /// <returns></returns>
    Vector3 CalcRecoiledDir(Vector3 dir, int ind)
    {
        Vector3 newDir = Quaternion.Euler(-currStats.recoilPattern[ind].x, currStats.recoilPattern[ind].y, 0.0f) * Vector3.forward;
        Quaternion worldToLocalRotate = Quaternion.FromToRotation(Vector3.forward, dir);
        newDir = Quaternion.Euler(worldToLocalRotate.eulerAngles.x, worldToLocalRotate.eulerAngles.y, 0.0f) * newDir;

        return newDir;
    }

    /// <summary>
    /// Calculates the rotation of the projectile based on the recoil pattern
    /// </summary>
    /// <param name="rot"></param>
    /// <param name="ind"></param>
    /// <returns></returns>
    Quaternion CalcRecoiledRot(Quaternion rot, int ind)
    {
        Quaternion newRot = Quaternion.Euler(-currStats.recoilPattern[ind].x, currStats.recoilPattern[ind].y, 0.0f) * rot;

        return newRot;
    }

    bool ApplyDamageToHBox(HitboxScript hBox, Vector3 dir)
    {
        try
        {
            // Try to get entityclass from hit collider, then make it call its own takedamage function
            EntityClass entityHit = hBox.GetOwnerEntity();
            hBox.ReduceHP(Mathf.RoundToInt(currStats.dmg * dmgMod));

            float critMult = 1.0f;
            if (hBox.IsCritical()) critMult = currStats.criticalDmgMult;

            entityHit.TakeDamageKnockback(Mathf.RoundToInt(currStats.dmg * dmgMod * critMult * hBox.GetDmgMultiplier()), currStats.weaponKnockback * dir, Owner);
        }
        catch (Exception e)
        {
            Debug.Log("Caught exception: " + e);
            Debug.Log("Couldn't get owner of hitbox");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Modifies weapon behaviour based on consumable used
    /// </summary>
    /// <param name="pu">Type of consumable</param>
    public void ApplyModifier()
    {
        switch (w_mod)
        {
            case WeaponModifier.BASE:
                currStats = baseStats;
                if (circlingVFX != null)
                {
                    if (isPartPlaying)
                    {
                        circlingVFX.Stop();
                        isPartPlaying = false;
                    }
                }
                break;
            case WeaponModifier.ELECTRIC:
                if (modifiedStats.Count > (int)PickUpClass.PickUpType.ELECTRIC)
                {
                    currStats = modifiedStats[(int)PickUpClass.PickUpType.ELECTRIC];
                }
                if (circlingVFX != null && isActive)
                {
                    if (!isPartPlaying)
                    {
                        circlingVFX.Play();
                        isPartPlaying = true;
                    }
                }

                break;
            default:
                break;
        }
    }

    public void SetState(WeaponModifier newState, float duration)
    {
        w_mod = newState;
        switch (newState)
        {
            case WeaponModifier.ELECTRIC:
                break;
            default:
                break;
        }
        currModDur += duration;

    }

    public void SetTriggerState(TriggerState st, CameraControl cam, ref Vector2 addRot, Vector3 speed = new Vector3())
    {
        switch (FireMode)
        {
            case FiringMode.SEMI:
                if (t_state == TriggerState.RELEASED && st == TriggerState.HELD)
                {
                    Fire(cam, ref addRot, speed);
                }
                break;
            case FiringMode.AUTO:
                if (st == TriggerState.HELD)
                {
                    Fire(cam, ref addRot, speed);
                }
                break;
            case FiringMode.BOW:
                if (t_state == TriggerState.RELEASED && st == TriggerState.HELD)
                {
                    currChargeDur = 0.0f;
                }
                else if (t_state == TriggerState.HELD && st == TriggerState.RELEASED)
                {
                    TriggerShot = true;
                    Fire(cam, ref addRot, speed);
                }
                break;
        }

        t_state = st;
    }

    public bool Equip()
    {
        isActive = true;
        SetRender(true);
        return true;
    }

    public bool UnEquip()
    {
        isActive = false;
        SetRender(false);
        return true;
    }

    void SetRender(bool val)
    {
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Count(); i++)
            {
                renderers[i].enabled = val;
            }
        }

        if (circlingVFX != null && circlingVFXRenderer != null)
        {
            circlingVFX.pause = !val;
            circlingVFXRenderer.enabled = val;
        }
    }

    public void SendOnPlay()
    {
        circlingVFX.Play();
    }
    public void SendOnStop()
    {
        circlingVFX.Stop();
    }
}