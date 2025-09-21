using System.Linq;
using UnityEngine;

public class ShoulderAnimation : MonoBehaviour
{
    Animator animator;
    EnemyBase enemy;
    static float maxSpeed = 25.0f;
    static float maxSpeedMult = 1.5f;
    static float divisionValue = maxSpeed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        animator = GetComponent<Animator>();
        enemy = GetComponentInParent<EnemyBase>();

        if (animator == null) Debug.Log(gameObject.name + " has no animator");
        if (enemy == null) Debug.Log(gameObject.name + " does not have an EnemyBase component");
    }

    // Update is called once per frame
    void Update()
    {

    }

    void FixedUpdate()
    {
        float setSpeed = enemy.RigBodVel.sqrMagnitude;
        if (setSpeed > maxSpeed * maxSpeed) { setSpeed = maxSpeedMult; }
        else { setSpeed = Mathf.Sqrt(setSpeed); }
        animator.SetFloat("Speed", setSpeed);
    }
}
