using Unity.VisualScripting;
using UnityEngine;

public class BladeAnimatorScript : MonoBehaviour
{
    EntityClass ent;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ent = GetComponentInParent<EntityClass>();
        if (ent == null)
        {
            Debug.Log(gameObject.name + "couldn't find parent entity");
            Destroy(this);
        }
        
    }

    void ToggleHurtboxOnEntity(string hurtboxName)
    {
        ent.ToggleHurtboxState(hurtboxName);
    }
}
