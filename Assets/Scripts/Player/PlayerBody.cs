using System;
using TMPro;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class PlayerBody : MonoBehaviour
{
    // Debug
    public TextMeshProUGUI DebugText;

    // Body
    Rigidbody body;
    BoxCollider hitbox;
    // Camera
    public Camera cam;

    // Input Variables
    InputAction moveInput;          // Movement (forward backwards sideways)
    InputAction jumpInput;          // Jumping (vertical)
    InputAction lookInput;          // Looking
    InputAction[] invInput;         // Inventory

    Vector2 moveVect;               // Horizontal movement input vector
    Vector2 lookVect;               // Cursor vector

    // Movement Variables
    // Caps
    public float HSpeedCap = 5;
    public float VSpeedCap = 5;
    public float HAccel = 3;
    // Current
    private Vector3 speed = Vector3.zero;
    private Vector2 rotation = Vector2.zero;

    // State
    enum State
    {
        GROUNDED,
        AIRBORNE
    };
    private State state;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Assign body
        body = GetComponent<Rigidbody>();
        body.interpolation = RigidbodyInterpolation.Interpolate;
        hitbox = GetComponent<BoxCollider>();

        // Assign InputActions to the Actions made in InputSystem
        moveInput = InputSystem.actions.FindAction("Move");
        jumpInput = InputSystem.actions.FindAction("Jump");
        lookInput = InputSystem.actions.FindAction("Look");

        invInput = new InputAction[3];
        invInput[0] = InputSystem.actions.FindAction("Inv_1");
        invInput[1] = InputSystem.actions.FindAction("Inv_2");
        invInput[2] = InputSystem.actions.FindAction("Inv_3");

        // Set callback for inventory
        for (int i = 0; i < 3; i++)
        {
            invInput[i].started += InvCallBack;
        }

        // Default state is Airborne
        state = State.AIRBORNE;

        // Default rotation
        rotation = Vector2.zero;
    }

    // Update is called once per frame
    void Update()
    {
        //----Input polling------
        // Jump
        if (jumpInput.WasPressedThisFrame())
        {
            Debug.Log("Jump");
        }
        // Movement
        moveVect = moveInput.ReadValue<Vector2>();
        lookVect = lookInput.ReadValue<Vector2>();

        // Looking
        rotation += new Vector2(-lookVect.y, lookVect.x);                           // Calculate rotation from input as degrees in a 2D vector
    }

    void FixedUpdate()
    {
        CalcMovement(moveVect);
    }

    void CalcMovement(Vector2 moveVect)
    {
        // Current position of RigidBody
        Vector3 currPos = body.position;

        // Calculate speed
        // Get current forwards and sideways speed
        float forwardsSpeed = Vector3.Dot(speed, transform.forward);
        float sideSpeed = Vector3.Dot(speed, transform.right);
        float vertSpeed = Vector3.Dot(speed, transform.up);

        // Modify values based on input
        SpeedCalc(ref forwardsSpeed, moveVect.y);
        SpeedCalc(ref sideSpeed, moveVect.x);

        // Calculate new speed
        speed = forwardsSpeed * transform.forward + sideSpeed * transform.right + new Vector3(0, vertSpeed, 0);

        // Check collision in new location
        /*
        RaycastHit hitInfo;
        while (Physics.Linecast(currPos, currPos + speed * Time.fixedDeltaTime, out hitInfo, layerMask, QueryTriggerInteraction.UseGlobal))
        {
            Debug.Log("Collision in linecast");
            Debug.Log("Normal: " + hitInfo.normal.ToString());
            Debug.Log("Collided with: " + hitInfo.collider.ToString());

            float angle = Vector3.Dot(speed.normalized, hitInfo.normal);
            Debug.Log("Calculated angle: " + (angle * Mathf.Rad2Deg).ToString());
            Debug.Log("Old speed: " + speed.ToString());
            speed -= Vector3.Dot(speed, hitInfo.normal) * hitInfo.normal;
            Debug.Log("New speed: " + speed.ToString());
        }
        */
        RaycastHit hitInfo = new RaycastHit();

        Vector3[] castStartPos = {      currPos,
                                        new Vector3(currPos.x, currPos.y + (hitbox.size.y/2), currPos.z),
                                        new Vector3(currPos.x, currPos.y - (hitbox.size.y/2), currPos.z),
                                        new Vector3(currPos.x + (hitbox.size.x/2), currPos.y, currPos.z + (hitbox.size.z/2)),
                                        new Vector3(currPos.x - (hitbox.size.x/2), currPos.y, currPos.z + (hitbox.size.z/2)),
                                        new Vector3(currPos.x + (hitbox.size.x/2), currPos.y, currPos.z - (hitbox.size.z/2)),
                                        new Vector3(currPos.x - (hitbox.size.x/2), currPos.y, currPos.z - (hitbox.size.z/2))
        };

        bool collision = false;
        DebugText.text = "No collision";

        for (int i = 0; i < castStartPos.Length; i++)
        {
            if (CollisionCastCheck(castStartPos[i], speed * Time.fixedDeltaTime, ref hitInfo))
            {
                collision = true;
                DebugText.text = "Collision at " + i.ToString() + ". location!";
                break;
            }
        }

        if (collision)
        {
            Debug.Log("Collision in linecast");
            Debug.Log("Normal: " + hitInfo.normal.ToString());
            Debug.Log("Collided with: " + hitInfo.collider.ToString());

            float angle = Vector3.Dot(speed.normalized, hitInfo.normal);
            Debug.Log("Calculated angle: " + (angle * Mathf.Rad2Deg).ToString());
            Debug.Log("Old speed: " + speed.ToString());
            speed -= Vector3.Dot(speed, hitInfo.normal) * hitInfo.normal;
            Debug.Log("New speed: " + speed.ToString());
        }

        // Move the body
        body.MovePosition(currPos + speed * Time.fixedDeltaTime);

        // Rotate the body
        body.MoveRotation(Quaternion.Euler(0, rotation.y, 0));
        Debug.Log(hitbox.transform.rotation);
    }


    void InvCallBack(InputAction.CallbackContext context)
    {
        int slot = 0;
        for (int i = 0; i < 3; i++)
        {
            if (context.action == invInput[i])
            {
                slot = i + 1;
                break;
            }
        }

        Debug.Log("Pressed " + slot.ToString());
    }

    void OnCollisionEnter(Collision other)
    {
        Debug.Log("Collision!");
        Debug.Log(other.GetContact(0).normal);
    }

    public Vector2 GetRotation()
    {
        return new Vector2(rotation.x, rotation.y);
    }

    void SpeedCalc(ref float speedVal, float inputVal)
    {
        // Calculate speed
        if (Mathf.Abs(inputVal) > 0.1)
        {
            speedVal += inputVal * (HAccel * Time.fixedDeltaTime);
            if (Mathf.Abs(speedVal) > HSpeedCap) { speedVal = Mathf.Sign(speedVal) * HSpeedCap; }
        }
        else
        {
            if (Mathf.Abs(speedVal) > 0.1)
            {
                speedVal -= Mathf.Sign(speedVal) * (HAccel * Time.fixedDeltaTime);
            }
            else
            {
                speedVal = 0.0f;
            }
        }
    }
    
    bool CollisionCastCheck(Vector3 startPos, Vector3 speedMultTime, ref RaycastHit hitInfo)
    {
        int layerMask = ~(1 << 6);              // All layers except 6: Player
        return Physics.Linecast(startPos, startPos + speedMultTime, out hitInfo, layerMask, QueryTriggerInteraction.UseGlobal);
    }
}
