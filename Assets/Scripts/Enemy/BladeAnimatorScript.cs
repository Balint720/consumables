using UnityEngine;

public class BladeAnimatorScript : MonoBehaviour
{
    EntityClass ent;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ent = GetComponentInParent<EntityClass>();
        Debug.Log("Found entity");
    }

    void ToggleHurtboxOnEntity(string hurtboxName)
    {
        ent.ToggleHurtboxState(hurtboxName);
    }
}
