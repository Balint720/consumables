using UnityEngine;

public class EnemyGround : EntityClass
{
    // Movement variables
    bool initJump = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        EntityStart();
    }

    // Update is called once per frame
    void Update()
    {

    }

    void FixedUpdate()
    {
        CalcMovementGrounded();
    }
}
