using System;
using System.Linq;
using UnityEngine;

public class RollerBodyWheel : MonoBehaviour
{
    Transform bodyTrans;
    Transform wheelTrans;
    EntityClass script;
    public float wheelTurnSpeed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Try to get body and wheel game objects
        Transform[] objects = GetComponentsInChildren<Transform>();

        for (int i = 0; i < objects.Count(); i++)
        {
            if (objects[i].name == "Body")
            {
                bodyTrans = objects[i];
            }
            if (objects[i].name == "Wheel")
            {
                wheelTrans = objects[i];
            }
        }

        script = GetComponent<EntityClass>();
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 dir = script.RigBodVel.normalized;
        dir = new Vector3(dir.x, 0.0f, dir.z);
        float angle = (float)Math.Acos(Vector3.Dot(new Vector3(0.0f, 0.0f, 1.0f), dir));

        if (dir == Vector3.zero)
        {
            wheelTrans.rotation = bodyTrans.rotation;
        }
        else
        {
            wheelTrans.rotation = Quaternion.Euler(0.0f, angle, 0.0f);
        }
    }
}
