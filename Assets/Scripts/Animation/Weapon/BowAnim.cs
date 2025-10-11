using UnityEngine;

public class BowAnimation : BlendMeshDriverConvert
{
    Animator animator;
    WeaponClass bow;
    AnimatorTransitionInfo transToShoot;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    override protected void Start()
    {
        base.Start();
        animator = GetComponent<Animator>();
        if (animator == null) Debug.Log(gameObject.name + "has no animator component");
        bow = GetComponentInParent<WeaponClass>();
        if (bow == null) Debug.Log(gameObject.name + " has no WeaponClass component");
    }

    // Update is called once per frame
    override protected void Update()
    {
        base.Update();
    }

    void FixedUpdate()
    {
        animator.SetFloat("ChargePercent", bow.ChargeMod);
        if (bow.TriggerShot) animator.SetTrigger("ReleaseArrow");
        animator.SetBool("Charging", bow.TriggerSt == WeaponClass.TriggerState.HELD);

        
    }
}
