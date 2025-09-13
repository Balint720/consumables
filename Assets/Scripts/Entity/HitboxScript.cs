using UnityEngine;

public class HitboxScript : MonoBehaviour
{
    EntityClass owner;
    Collider hitbox;
    public float dmgMultiplier;
    public bool critical;
    public int HP;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        owner = GetComponentInParent<EntityClass>();
        if (!TryGetComponent<Collider>(out hitbox))
        {
            Debug.Log(gameObject.name + ": Couldn't find hitbox collider");
            Destroy(gameObject);
        }
        if (owner == null)
        {
            Debug.Log(gameObject.name + ": Couldn't find parent entity");
            Destroy(gameObject);
        }
        if (HP <= 0)
        {
            HP = owner.MaxHP;
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
