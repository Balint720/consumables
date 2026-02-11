using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

public partial class EnemyBlade
{
    // Check if they correspond correctly with the animatorcontroller in the editor
    // Could find a better solution that cannot cause redundancy errors
    enum Attacks_Blade
    {
        QUICK_SLASH_L = 0,
        QUICK_SLASH_R = 1,
        SHOOT_SAW_L = 2,
        SHOOT_SAW_R = 3,
        SLAM = 4
    };
    int Par_AttackID
    {
        get
        {
            return animator.GetInteger("AttackID");
        }
        set
        {
            animator.SetInteger("AttackID", value);
        }
    }

    bool Par_Combo
    {
        get
        {
            return animator.GetBool("Combo");
        }
        set
        {
            animator.SetBool("Combo", value);
        }
    }

    bool Trigger_Attack
    {
        set
        {
            if (value) animator.SetTrigger("Attack");
            else animator.ResetTrigger("Attack");
        }
    }


}