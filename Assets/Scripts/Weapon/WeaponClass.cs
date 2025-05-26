using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.UIElements;

public class WeaponClass : MonoBehaviour
{
    public enum FiringMode
    {
        SEMI,
        AUTO
    };

    public enum WeaponType
    {
        HITSCAN_SINGLE,
        HITSCAN_SPREAD,
        PROJECTILE
    }
    public WeaponType weaponType;
    public FiringMode fireMode;  // Basically, does holding down the mouse keep shooting or not

    // Stats
    public int dmg = 1;                    // Damage will be calculated for every bullet
    float dmgMod;                        // Damage will be multiplied by this value
    public uint pelCount;                       // Pellet count for shotguns
    int recoilInd;                             // Index of where we are in the recoil pattern
    float recoilValMod;                         // Recoil will be multiplied by this value
    public List<Vector2> recoilPattern;         // List of recoilVal values for recoil pattern (can also be where bullets from a shotgun go)
    public float recoilRecoveryTime;            // How long until recoil resets in seconds
    public float bloom;                         // Random spread of weapon, deviation from where the bullet should go, value is maximum possible deviation
    float bloomMod;                             // Maximum bloom will be multiplied by this value
    public int rpm;                             // Fire rate: rounds per minute
    float rpmMod;                               // RPM will be multiplied by this value
    public float knockbackStrength;
    float knockbackMod;
    public float range;                         // Range of weapon, for hitscan this is the max distance of the raycast, for projectiles the projectile gets destroyed after traveling this many units
    public Vector3 offset;

    // Time
    float lastFireTime;

    // Projectiles
    public ProjectileClass hitscanProjectile;

    WeaponClass()
    {
        weaponType = WeaponType.HITSCAN_SINGLE;
        fireMode = FiringMode.SEMI;
        dmg = 1;
        pelCount = 1;
        recoilPattern = new List<Vector2>(1);
        recoilRecoveryTime = 0.8f;
        bloom = 0.0f;
        rpm = 300;
        range = 200.0f;
        offset = Vector3.zero;
    }

    public void Init()
    {
        lastFireTime = 0.0f;
        dmgMod = 1.0f;
        recoilInd = 0;
        recoilValMod = 1.0f;
        bloomMod = 1.0f;
        rpmMod = 1.0f;
        knockbackMod = 1.0f;
    }

    RaycastHit ShootHitScan(Vector3 origin, Vector3 direction, Quaternion rotation)
    {
        // Shoot ray into direction from origin with a maximum range
        RaycastHit hitInfo;
        Physics.Raycast(origin, direction, out hitInfo, range);

        return hitInfo;
    }

    void ShootProjectile(Vector3 origin, Vector3 direction, Quaternion rotation)
    {
        // Set the direction and offset IF there is a hitscanProjectile
        ProjectileClass p = Instantiate(hitscanProjectile, origin, rotation);
        p.SetDirection(direction);
    }

    public FiringMode GetFiringMode()
    {
        return fireMode;
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
        if (sinceLastFire > 60.0f / rpm)
        {
            lastFireTime = Time.time;
            RaycastHit hitInfo;
            bool hitSomething = false;
            // Different methods for hitscan weapons, projectile weapons
            switch (weaponType)
            {
                case WeaponType.HITSCAN_SINGLE:
                    // Reset recoil if enough time has passed; Otherwise progress recoil pattern
                    if (sinceLastFire >= recoilRecoveryTime)
                    {
                        recoilInd = 0;
                    }
                    else
                    {
                        if (++recoilInd >= recoilPattern.Count)
                        {
                            recoilInd = recoilPattern.Count - 1;
                        }
                    }

                    hitInfo = ShootHitScan(origin, direction, rotation);
                    hitSomething = hitInfo.collider != null;

                    // If we hit an entity, make it reduce its hp by the weapon's dmg
                    if (hitSomething)
                    {
                        ApplyDamageToEnt(hitInfo, direction);
                    }


                    if (hitscanProjectile != null)
                    {
                        // Get point and direction from where the visual projectile should fire (gun barrel)
                        // The direction should be towards the point that the ray hit, and if the ray hit nothing then just shoot it in the direction 50 units away
                        Vector3 offsetOrigin = origin + rotation * new Vector3(offset.x, offset.y);
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

                    if (recoilPattern.Count != 0)
                    {
                        addRot += new Vector2(-recoilPattern[recoilInd].x, recoilPattern[recoilInd].y);
                    }

                    break;

                case WeaponType.HITSCAN_SPREAD:
                    // Shoot the pelCount amount of pellets
                    for (int i = 0; i < pelCount; i++)
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
                        if (hitscanProjectile != null)
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

    Vector3 CalcRecoiledDir(Vector3 dir, int ind)
    {
        Vector3 newDir = Quaternion.Euler(-recoilPattern[ind].x, recoilPattern[ind].y, 0.0f) * Vector3.forward;
        Quaternion worldToLocalRotate = Quaternion.FromToRotation(Vector3.forward, dir);
        newDir = Quaternion.Euler(worldToLocalRotate.eulerAngles.x, worldToLocalRotate.eulerAngles.y, 0.0f) * newDir;

        return newDir;
    }
    Quaternion CalcRecoiledRot(Quaternion rot, int ind)
    {
        Quaternion newRot = Quaternion.Euler(-recoilPattern[ind].x, recoilPattern[ind].y, 0.0f) * rot;

        return newRot;
    }

    bool ApplyDamageToEnt(RaycastHit hitInfo, Vector3 dir)
    {
        if (hitInfo.collider.tag == "Entity")
        {
            try
            {
                EntityClass entityHit = hitInfo.collider.gameObject.GetComponent<EntityClass>();
                entityHit.TakeDamageKnockback(dmg, knockbackStrength*dir);
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
}