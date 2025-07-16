using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        AUTO
    }

    public enum WeaponType
    {
        HITSCAN_SINGLE,
        HITSCAN_SPREAD,
        PROJECTILE
    }

    public enum WeaponState
    {
        BASE,
        ELECTRIC
    }

    [Serializable]
    public struct WeaponStats
    {
        public int dmg;                                // Damage will be calculated for every bullet
        public uint pelCount;                          // Pellet count for shotguns
        public List<Vector2> recoilPattern;            // List of recoilVal values for recoil pattern (can also be where bullets from a shotgun go)
        public float recoilRecoveryTime;               // How long until recoil resets in seconds
        public float bloom;                            // Random spread of weapon, deviation from where the bullet should go, value is maximum possible deviation
        public int rpm;                                // Fire rate: rounds per minute
        public float knockbackStrength;                // Strength of knockback applied by weapon
        public float range;                            // Range of weapon, for hitscan this is the max distance of the raycast, for projectiles the projectile gets destroyed after traveling this many units
        public float projSpeed;                        // Speed of projectile
        public WeaponType weaponType;                  // Hitscan, projectile etc
        public FiringMode fireMode;                    // Basically, does holding down the mouse keep shooting or not
        public ProjectileClass projectile;             // Projectile that is sent by weapon
    }


    int ownerID;                                // Get ID of weapon's owner

    // State
    WeaponState w_state;
    WeaponState prev_w_state;
    bool isActive;
    bool isPartPlaying;

    // Stats
    public WeaponStats baseStats;
    public List<WeaponStats> modifiedStats;
    WeaponStats currStats;
    float currModDur;
    float dmgMod;                               // Damage will be multiplied by this value
    int recoilInd;                              // Index of where we are in the recoil pattern
    float recoilValMod;                         // Recoil will be multiplied by this value
    float bloomMod;                             // Maximum bloom will be multiplied by this value
    float rpmMod;                               // RPM will be multiplied by this value
    float knockbackMod;                         // KnockbackStrength will be multiplied by this value
    float projSpeedMod;                         // Speed of projectile will be multiplied by this value
    public Vector3 offset;                      // Visual fired bullets are offset from here (Maybe projectiles will just straight up be offset)

    // Time
    float lastFireTime;                         // Seconds that have passed since last time weapon fired

    // Model
    public Vector3 modelOffset;
    public Quaternion modelOffsetRot;                // Weapon model offset rotation from character rotation (for fps perspective)
    public Vector3 modelScale;
    VisualEffect circlingVFX;
    VFXRenderer circlingVFXRenderer;
    MeshRenderer[] renderers;


    void Start()
    {
        // Set intial values for multipliers
        lastFireTime = 0.0f;
        dmgMod = 1.0f;
        recoilInd = 0;
        recoilValMod = 1.0f;
        bloomMod = 1.0f;
        rpmMod = 1.0f;
        knockbackMod = 1.0f;
        projSpeedMod = 1.0f;
        currModDur = 0.0f;

        // Set stats to base stats
        currStats = baseStats;
        w_state = WeaponState.BASE;

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

        transform.localScale += modelScale;
    }

    void Update()
    {
        if (isActive)
        {
            transform.position += Quaternion.LookRotation(transform.forward) * modelOffset;
            transform.rotation *= modelOffsetRot;
        }
    }

    void FixedUpdate()
    {
        ApplyModifier();

        if (currModDur > 0.0f && w_state != WeaponState.BASE)
        {
            currModDur -= Time.fixedDeltaTime;
        }
        else
        {
            w_state = WeaponState.BASE;
        }
    }

    /// <summary>
    /// Shoot a ray into direction, return hitInfo
    /// </summary>
    /// <param name="origin"></param>
    /// <param name="direction"></param>
    /// <param name="rotation"></param>
    /// <returns></returns>
    RaycastHit ShootHitScan(Vector3 origin, Vector3 direction, Quaternion rotation)
    {
        // Shoot ray into direction from origin with a maximum range
        RaycastHit hitInfo;
        Physics.Raycast(origin, direction, out hitInfo, currStats.range);

        return hitInfo;
    }

    /// <summary>
    /// Shoot a projectile into direction, also sets projectile damage values
    /// </summary>
    /// <param name="origin"></param>
    /// <param name="direction"></param>
    /// <param name="rotation"></param>
    void ShootProjectile(Vector3 origin, Vector3 direction, Quaternion rotation)
    {
        // Set the direction and offset IF there is a hitscanProjectile
        ProjectileClass p = Instantiate(currStats.projectile, origin, rotation);
        p.SetDirection(direction);

        // Set projectile values and owner
        p.SetValuesOfProj((int)(currStats.dmg * dmgMod), currStats.projSpeed * projSpeedMod, currStats.knockbackStrength * knockbackMod);
        p.SetOwner(ownerID);
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
        // Get time in seconds since the last time the weapon fired
        float sinceLastFire = Time.time - lastFireTime;

        // Only shoot if rate of fire allows it
        if (sinceLastFire > 60.0f / (currStats.rpm * rpmMod))
        {
            lastFireTime = Time.time;
            RaycastHit hitInfo;
            bool hitSomething = false;
            // Different methods for hitscan weapons, projectile weapons
            switch (currStats.weaponType)
            {
                case WeaponType.HITSCAN_SINGLE:
                    // Reset recoil if enough time has passed; Otherwise progress recoil pattern
                    if (sinceLastFire >= currStats.recoilRecoveryTime)
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

                    hitInfo = ShootHitScan(origin, direction, rotation);
                    hitSomething = hitInfo.collider != null;

                    // If we hit an entity, make it reduce its hp by the weapon's dmg
                    if (hitSomething)
                    {
                        ApplyDamageToEnt(hitInfo, direction);
                    }


                    if (currStats.projectile != null)
                    {
                        // Get point and direction from where the visual projectile should fire (gun barrel)
                        // The direction should be towards the point that the ray hit, and if the ray hit nothing then just shoot it in the direction 50 units away
                        Vector3 offsetOrigin = origin + rotation * new Vector3(offset.x, offset.y, offset.z);
                        Vector3 offsetDir = Vector3.Normalize(origin + 50 * direction - offsetOrigin);
                        if (hitSomething)
                        {
                            offsetDir = Vector3.Normalize(hitInfo.point - offsetOrigin);
                        }
                        // Test: Adding the speed of the shooter for better cosmetic projectile
                        offsetOrigin += speed;
                        // Shoot the projectile with the calculated direction and point with offset
                        ShootProjectile(offsetOrigin, offsetDir, rotation);
                    }

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
                        hitInfo = ShootHitScan(origin, pelDir, pelRot);
                        hitSomething = hitInfo.collider != null;

                        // If we hit an entity, make it reduce its hp by the weapon's dmg
                        if (hitSomething)
                        {
                            ApplyDamageToEnt(hitInfo, pelDir);
                        }

                        // Cosmetic projectile
                        if (currStats.projectile != null)
                        {
                            // Get point and direction from where the visual projectile should fire (gun barrel)
                            // The direction should be towards the point that the ray hit, and if the ray hit nothing then just shoot it in the direction 50 units away
                            Vector3 offsetOrigin = origin + pelRot * new Vector3(offset.x, offset.y);
                            Vector3 offsetDir = Vector3.Normalize(origin + 50 * pelDir - offsetOrigin);
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
                    break;
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

    bool ApplyDamageToEnt(RaycastHit hitInfo, Vector3 dir)
    {
        if (hitInfo.collider.tag == "Entity")
        {
            try
            {
                // Try to get entityclass from hit collider, then make it call its own takedamage function
                EntityClass entityHit = hitInfo.collider.gameObject.GetComponent<EntityClass>();
                entityHit.TakeDamageKnockback((int)(currStats.dmg * dmgMod), currStats.knockbackStrength * knockbackMod * dir);
                entityHit.OnGettingHit(GetOwner());
            }
            catch (Exception e)
            {
                Debug.Log("Caught exception: " + e);
                Debug.Log("Couldn't convert GameObject tagged as \"Entity\"");
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Modifies weapon behaviour based on consumable used
    /// </summary>
    /// <param name="pu">Type of consumable</param>
    public void ApplyModifier()
    {
        switch (w_state)
        {
            case WeaponState.BASE:
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
            case WeaponState.ELECTRIC:
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

    // Setters and getters
    public void SetOwner(GameObject newOwner)
    {
        ownerID = newOwner.GetInstanceID();
    }
    public GameObject GetOwner()
    {
        if (ownerID == 0)
        {
            return null;
        }
        else
        {
            return (GameObject)EditorUtility.InstanceIDToObject(ownerID);
        }
    }

    public FiringMode GetFiringMode()
    {
        return currStats.fireMode;
    }

    public void SetState(WeaponState newState, float duration)
    {
        w_state = newState;
        switch (newState)
        {
            case WeaponState.ELECTRIC:
                break;
            default:
                break;
        }
        currModDur += duration;

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