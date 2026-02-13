using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct MinMaxDistance<T>
{
    public T minimum;
    public T maximum;
}

public enum AttackType
{
    MELEE_SWING,
    LEAP_ATTACK,
    PROJECTILE_SHOOT
}

[CreateAssetMenu(fileName = "AttackParams", menuName = "Scriptable Objects/AttackParams")]
[System.Serializable]
public class AttackParams : ScriptableObject
{
    // Base damage of the attack, the enemy should multiply it itself
    [SerializeField] int baseDamage;
    public int Damage => baseDamage;

    // Base knockback of the attack, the enemy should multiply it itself
    // It is a vector: the direction implies direction in which knockback should be applied more (high y value -> apply more knockback up, knocking up enemies)
    // Z is forward, X is sideways
    [SerializeField] Vector3 baseKnockback;
    public Vector3 Knockback => baseKnockback;

    // Distance from which attack can validly be used (or should be used)
    // There is a minimum and a maximum distance: closer than minimum -> not using attack, farther than maximum -> not using attack
    [SerializeField] MinMaxDistance<float> validDistance;
    public MinMaxDistance<float> ValidDistance => validDistance;

    // If attack type shoots a projectile, the projectile classes can be stored here
    // Whether damage and knockback affect projectile is dependent on implementation
    [SerializeField] List<ProjectileClass> projectile;
    public List<ProjectileClass> Projectile => projectile;

    // If attack type causes user to move some distance, this parameter contains how far they should go
    // Vector has to be rotated obviously
    // Minimum and maximum can be used to have a range instead of a set distance
    [SerializeField] MinMaxDistance<Vector3> leap;
    public MinMaxDistance<Vector3> Leap => leap;

    // How much the enemy must wait before he can use the attack again
    [SerializeField] float cooldownTime;
    public float CooldownTime => cooldownTime;

    // How much time the enemy must wait before he can use ANY attacks again
    // Could consider this "tiredness"
    [SerializeField] float globalAttackCooldownTime;
    public float GlobalAttackCooldownTime =>globalAttackCooldownTime;

}
