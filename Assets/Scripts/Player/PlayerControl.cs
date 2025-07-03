using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControl : EntityClass
{
    // Camera
    public CameraControl cam;

    // Input Variables
    InputAction moveInput;              // Movement (forward backwards sideways)
    InputAction jumpInput;              // Jumping (vertical)
    InputAction lookInput;              // Looking
    InputAction[] invInput;             // Inventory
    InputAction attackInput;            // Shooting
    bool attackPressed;                 // Used for checking if attack button is held down or just pressed (probably an in engine way to check, this is cooked)
    float attackBuf;                    // How long attack has been held down

    Vector2 lookVect;                   // Cursor vector
    Vector2 addedRotation;              // Rotation from other factors than input

    // Player settings
    public float sens = 0.4f;           // Sensitivity of mouse movement
    public float inputBuffer = 0.2f;    // How many seconds are buffered inputs considered pressed

    // Inv
    public List<WeaponClass> weapon;    // List of WeaponClass objects that the player has
    List<int> consumable;               // Number of a consumable the player has in their inventory

    private int equippedItem;           // Currently equipped item (includes weapons)

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        EntityStart();

        // Extra tags
        AddExtraTag("Player");

        // Set the cursor locked to game screen
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
        // Initialize each weapon
        for (int i = 0; i < weapon.Count; i++)
        {
            weapon[i].Init();
            weapon[i].SetOwner(gameObject);
        }

        // Initialize consumables
        consumable = new List<int>();
        for (int i = 0; i < Enum.GetNames(typeof(PickUpClass.PickUpType)).Length; i++)
        {
            consumable.Add(0);
        }
    }

    // Update is called once per frame
    void Update()
    {
        //----Input polling------
        // Movement and look
        moveVect = new Vector3(moveInput.ReadValue<Vector2>().x, jumpInput.IsPressed() ? 1.0f : 0.0f, moveInput.ReadValue<Vector2>().y);
        lookVect = lookInput.ReadValue<Vector2>();

        // Attack input checked, check if held or pressed
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

        // Weapon update
        weapon[0].transform.position = transform.position;
        weapon[0].transform.rotation = transform.rotation;
        weapon[0].Upd();
    }

    void FixedUpdate()
    {
        CalcMovementGrounded();
        DoAttack();
    }

    void InvCallBack(InputAction.CallbackContext context)
    {
        // Depending on which action it is, set the equipped item to that number
        for (int i = 0; i < 3; i++)
        {
            if (context.action == invInput[i])
            {
                equippedItem = i;
                break;
            }
        }
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
            weapon[equippedItem].Fire(cam, ref addedRotation, charCont.velocity * 2 * Time.fixedDeltaTime);
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

    void OnTriggerEnter(Collider other)
    {
        // If we hit a pickup
        if (other.CompareTag("PickUp"))
        {
            // Get Pickup type
            PickUpClass p;
            try
            {
                p = other.gameObject.GetComponent<PickUpClass>();
            }
            catch (Exception e)
            {
                Debug.Log(e);
                Debug.Log("Couldn't get PickUpClass component from object tagged as \"PickUp\"");
                return;
            }
            // Add to inventory
            consumable[(int)p.puType] += 1;
        }

        // Destroy pickup
        Destroy(other.gameObject);
    }

    // Getters and setters
    public Vector2 GetRotation()
    {
        return new Vector2(rotation.x, rotation.y);
    }

    public void AddRotation(Vector2 rotToAdd)
    {
        rotation += rotToAdd;
    }
}
