using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class PlayerControl : EntityClass
{
    // Camera
    public CameraControl cam;
    const float lookVerticalDegLimit = 89.0f;

    // Input Variables
    InputAction moveInput;              // Movement (forward backwards sideways)
    InputAction jumpInput;              // Jumping (vertical)
    InputAction lookInput;              // Looking
    List<InputAction> invInput;         // Inventory
    InputAction attackInput;            // Shooting
    bool attackPressed;                 // Used for checking if attack button is held down or just pressed (probably an in engine way to check, this is cooked)
    float attackBuf;                    // How long attack has been held down

    Vector2 lookVect;                   // Cursor vector
    Vector2 addedRotation;              // Rotation from other factors than input

    // Player settings
    public float sens = 0.4f;           // Sensitivity of mouse movement
    public float inputBuffer = 0.2f;    // How many seconds are buffered inputs considered pressed

    // Inv
    public List<WeaponClass> weaponFabs;// List of WeaponClass objects that the player has
    List<WeaponClass> weapon;
    List<int> consumable;               // Number of a consumable the player has in their inventory

    private int equippedItem;           // Currently equipped item (includes weapons)

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        EntityStart();

        // Extra tags
        AddExtraTag("Player");

        // Set the cursor locked to game screen
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;

        // Assign InputActions to the Actions made in InputSystem
        moveInput = InputSystem.actions.FindAction("Move");
        jumpInput = InputSystem.actions.FindAction("Jump");
        lookInput = InputSystem.actions.FindAction("Look");
        attackInput = InputSystem.actions.FindAction("Attack");
        invInput = new List<InputAction>();

        int i = 0;
        while (true)
        {
            String invStr = "Inv_" + (i+1).ToString();
                
            InputAction curr = InputSystem.actions.FindAction(invStr);
            if (curr != null)
            {
                invInput.Add(curr);
            }
            else
            {
                break;
            }

            i++;
        }

        // Set callback for inventory
        for (i = 0; i < invInput.Count(); i++)
        {
            invInput[i].started += InvWeaponCallBack;
        }

        // Default values
        equippedItem = 0;                               // Equipped inventory slot
        // Instantiate each weapon
        weapon = new List<WeaponClass>();

        for (i = 0; i < weaponFabs.Count; i++)
        {
            weapon.Add(Instantiate<WeaponClass>(weaponFabs[i], transform.position, transform.rotation));
        }

        // Initialize consumables
        consumable = new List<int>();
        for (i = 0; i < Enum.GetNames(typeof(PickUpClass.PickUpType)).Length; i++)
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
        if (Math.Abs(rotation.x) > lookVerticalDegLimit)
        {
            rotation.x = lookVerticalDegLimit * Math.Sign(rotation.x);
        }

        if (addedRotation != Vector2.zero)                                                  // Added rotation is stuff like recoil, camera shake, etc
        {
            StartCoroutine(AddRotGradual(addedRotation, 50));
            addedRotation = Vector2.zero;
        }

        // Weapon update
        for (int i = 0; i < weapon.Count; i++)
        {
            if (i != equippedItem && weapon[i] != null)
            {
                weapon[i].gameObject.SetActive(false);
            }
        }

        weapon[equippedItem].transform.position = transform.position;
        weapon[equippedItem].transform.rotation = Quaternion.Euler(rotation.x, rotation.y, 0);
    }

    void FixedUpdate()
    {
        CalcMovementGrounded();
        DoAttack();
    }

    void InvWeaponCallBack(InputAction.CallbackContext context)
    {
        // Depending on which action it is, set the equipped item to that number
        for (int i = 0; i < 3; i++)
        {
            if (context.action == invInput[i])
            {
                equippedItem = i;
                weapon[i].gameObject.SetActive(true);
                break;
            }
        }
    }

    void InvItemCallBack(InputAction.CallbackContext context)
    {
        
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
