using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControl : EntityClass
{
    // Debug text
    public TextMeshProUGUI DebugText;

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

    Vector2 lookVect;               // Cursor vector
    Vector2 addedRotation;          // Rotation from other factors than input

    // Player settings
    public float sens = 0.4f;
    public float inputBuffer = 0.2f;
    // Constants

    // Weapons
    public List<WeaponClass> weapon;

    private int equippedItem;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        EntityStart();

        Cursor.lockState = CursorLockMode.Locked;

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

        // Default values
        equippedItem = 0;                               // Equipped inventory slot
        for (int i = 0; i < weapon.Count; i++)
        {
            weapon[i].Init();
        }
    }

    // Update is called once per frame
    void Update()
    {
        //----Input polling------
        // Movement
        moveVect = new Vector3(moveInput.ReadValue<Vector2>().x,jumpInput.IsPressed() ? 1.0f : 0.0f, moveInput.ReadValue<Vector2>().y);
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
        CalcMovementGrounded();
        DoAttack();
    }

    void InvCallBack(InputAction.CallbackContext context)
    {
        for (int i = 0; i < 3; i++)
        {
            if (context.action == invInput[i])
            {
                equippedItem = i;
                break;
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
            weapon[equippedItem].Fire(cam, ref addedRotation, charCont.velocity*2*Time.fixedDeltaTime);
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
