using UnityEngine;

public class HitboxScript : MonoBehaviour
{
    EntityClass owner;
    public float dmgMultiplier;
    public bool critical;
    public int HP;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        owner = GetComponentInParent<EntityClass>();
        if (owner == null)
        {
            Debug.Log(gameObject.name + ": Couldn't find parent entity");
        }
        if (HP <= 0)
        {
            HP = owner.maxHP;
        }
    }

    public EntityClass GetOwnerEntity()
    {
        return owner;
    }

    public float GetDmgMultiplier()
    {
        return dmgMultiplier;
    }

    public bool IsCritical()
    {
        return critical;
    }

    public void ReduceHP(int dmg)
    {
        HP -= dmg;
        
        if (HP <= 0)
        {
            ZeroHP();
        }
    }

    void ZeroHP()
    {
        
    }
}
