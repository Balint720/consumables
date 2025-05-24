using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControl : EntityClass
{
    // Debug text
    public TextMeshProUGUI DebugText;

    // Hitbox
    CapsuleCollider movHitBox;
    // Camera
    public CameraControl cam;

    // Input Variables
    InputAction moveInput;          // Movement (forward backwards sideways)
    InputAction jumpInput;          // Jumping (vertical)
    InputAction lookInput;          // Looking
    InputAction[] invInput;         // Inventory
    InputAction attackInput;         // Shooting
    bool attackPressed = false;
    float attackBuf = 0;

    Vector2 moveVect;               // Horizontal movement input vector
    Vector2 lookVect;               // Cursor vector
    Vector2 addedRotation;          // Rotation from other factors than input

    // Player settings
    public float sens = 0.4f;
    public float inputBuffer = 0.2f;
    // Constants

    // Character Control
    CharacterController charCont;

    // Character movement state
    enum State
    {
        GROUNDED,
        AIRBORNE
    };

    State movState = State.AIRBORNE;

    // Weapons
    public List<WeaponClass> weapon;

    private int equippedItem;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Assign InputActions to the Actions made in InputSystem
        moveInput = InputSystem.actions.FindAction("Move");
        jumpInput = InputSystem.actions.FindAction("Jump");
        lookInput = InputSystem.actions.FindAction("Look");
        attackInput = InputSystem.actions.FindAction("Attack");

        invInput = new InputAction[3];
        invInput[0] = InputSystem.actions.FindAction("Inv_1");
        invInput[1] = InputSystem.actions.FindAction("Inv_2");
        invInput[2] = InputSystem.actions.FindAction("Inv_3");

        // Set callback for inventory
        for (int i = 0; i < 3; i++)
        {
            invInput[i].started += InvCallBack;
        }

        // Assign Components
        charCont = GetComponent<CharacterController>();
        movHitBox = GetComponent<CapsuleCollider>();

        // Default values
        equippedItem = 0;
        for (int i = 0; i < weapon.Count; i++)
        {
            weapon[i].SetLastFireTime(0.0f);
        }
    }

    // Update is called once per frame
    void Update()
    {
        //----Input polling------
        // Movement
        moveVect = moveInput.ReadValue<Vector2>();
        lookVect = lookInput.ReadValue<Vector2>();
        if (attackInput.WasPressedThisFrame())
        {
            attackPressed = true;
            attackBuf = 0.0f;
        }
        if (attackPressed)
        {
            attackBuf += Time.deltaTime;
            if (attackBuf > inputBuffer)
            {
                attackPressed = false;
            }
        }

        // Looking
        rotation += new Vector2(-lookVect.y, lookVect.x) * sens;                            // Calculate rotation from input as degrees in a 2D vector
        if (addedRotation != Vector2.zero)
        {
            StartCoroutine(AddRotGradual(addedRotation, 50));
            addedRotation = Vector2.zero;
        }
    }

    void FixedUpdate()
    {
        CalcMovement(moveVect);
        DoAttack();
    }

    void InvCallBack(InputAction.CallbackContext context)
    {
        int slot = 0;
        for (int i = 0; i < 3; i++)
        {
            if (context.action == invInput[i])
            {
                equippedItem = i;
                slot = i + 1;
                break;
            }
        }
    }

    void CalcMovement(Vector2 moveVect)
    {
        // Calculate speed
        // Get current forwards and sideways speed
        float forwardsSpeed = Vector3.Dot(charCont.velocity, transform.forward);
        float sideSpeed = Vector3.Dot(charCont.velocity, transform.right);                      // We get these from the real speed of the character because sliding off of walls would make us shoot off them with max speed as soon as we are no longer colliding with them
        float vertSpeed = Vector3.Dot(speed, new Vector3(0, 1, 0));                             // We get this from the "theoretical" speed vector because we want to keep negative speed while on the ground (shitty collision solution for walking down ramps)

        // State on previous frame
        State prevFrame = movState;

        // Set state based on if ground beneath
        if (charCont.isGrounded)
        {
            movState = State.GROUNDED;
        }
        else
        {
            movState = State.AIRBORNE;
        }

        // Modify values based on input
        SpeedCalc(ref forwardsSpeed, moveVect.y);
        SpeedCalc(ref sideSpeed, moveVect.x);

        // Calculate vertical speed based on state
        switch (movState)
        {
            case State.GROUNDED:
                // If we jump, set vertical speed to set value, otherwise, keep it negative
                if (jumpInput.IsPressed())
                {
                    vertSpeed = VSpeedCap;
                }
                else
                {
                    vertSpeed = -10 * grav * Time.fixedDeltaTime;
                }
                break;
            case State.AIRBORNE:
                if (prevFrame == State.GROUNDED && vertSpeed < 0.0f)
                {
                    vertSpeed = 0.0f;
                }
                vertSpeed -= grav * Time.fixedDeltaTime;
                break;
        }

        speed = forwardsSpeed * transform.forward + sideSpeed * transform.right + new Vector3(0, vertSpeed, 0);

        // Handle gravity and jumping IF the character is touching the ground

        // Apply movement
        charCont.Move(speed * Time.fixedDeltaTime);
        transform.rotation = Quaternion.Euler(0, rotation.y, 0);
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
            if (Mathf.Abs(speedVal) > 0.5)
            {
                speedVal -= Mathf.Sign(speedVal) * (HAccel * Time.fixedDeltaTime);
            }
            else
            {
                speedVal = 0.0f;
            }
        }
    }

    public Vector2 GetRotation()
    {
        return new Vector2(rotation.x, rotation.y);
    }

    public void AddRotation(Vector2 rotToAdd)
    {
        rotation += rotToAdd;
    }

    void DoAttack()
    {
        bool canShoot;
        switch (weapon[equippedItem].GetFiringMode())
        {
            case WeaponClass.FiringMode.SEMI:
                canShoot = attackPressed;
                break;
            case WeaponClass.FiringMode.AUTO:
                canShoot = attackInput.IsPressed();
                break;
            default:
                canShoot = true;
                break;
        }

        if (canShoot)
        {
            weapon[equippedItem].Fire(cam, ref addedRotation);
        }

        attackPressed = false;
    }

    IEnumerator AddRotGradual(Vector2 rotToAdd, int increments)
    {
        Vector2 incVec = rotToAdd / increments;
        for (int i = 0; i < increments; i++)
        {
            rotation += incVec;
            yield return null;
        }
    }
}
