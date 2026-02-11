using System.Collections.Generic;
using UnityEngine;

public partial class EnemyBlade : EnemyBase
{
    // Stats
    float dmgMod;
    float projSpeedMod;
    float weaponKnockbackMod;

    [SerializeField] List<AttackClass> attacks;

    protected override void Update()
    {
        base.Update();

        // Animations
        
    }
}
