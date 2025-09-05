using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

class Consumable
{
    public static float consumableDuration = 30.0f;                // How long consumable items last
    int count;
    PickUpClass.PickUpType type;

    public Consumable()
    {
        count = 0;
        type = PickUpClass.PickUpType.ELECTRIC;
    }

    public Consumable(int i_count, PickUpClass.PickUpType i_type)
    {
        count = i_count;
        type = i_type;
    }

    public bool ReduceCount(int num)
    {
        if (count > 0)
        {
            count -= num;
            return true;
        }
        return false;
    }

    public bool IncreaseCount(int num)
    {
        if (count < 999)
        {
            count += num;
            return true;
        }
        return false;
    }

    public int GetCount()
    {
        return count;
    }

    public PickUpClass.PickUpType GetPUType()
    {
        return type;
    }

    public bool SetType(PickUpClass.PickUpType typeToSet)
    {
        type = typeToSet;
        return true;
    }
}

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

    List<Consumable> consumable;               // Number of a consumable the player has in their inventory

    int equippedItem;           // Currently equipped item (includes weapons)

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    override protected void Start()
    {
        base.Start();

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
            String invStr = "Inv_" + (i + 1).ToString();

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
            invInput[i].started += InvCallBack;
        }


        // Default values
        equippedItem = 0;                               // Equipped inventory slot
        // Instantiate each weapon
        weapon = new List<WeaponClass>();

        for (i = 0; i < weaponFabs.Count; i++)
        {
            weapon.Add(Instantiate<WeaponClass>(weaponFabs[i], transform.position, transform.rotation));
            weapon[i].SetOwner(gameObject);
            weapon[i].UnEquip();
        }

        equippedItem = 0;
        weapon[equippedItem].Equip();

        // Initialize consumables
        consumable = new List<Consumable>();
        for (i = 0; i < invInput.Count - weapon.Count; i++)
        {
            consumable.Add(new Consumable(0, (PickUpClass.PickUpType)i));
        }
    }

    // Update is called once per frame
    void Update()
    {
        //----Input polling------
        // Movement and look
        moveVect = new Vector3(moveInput.ReadValue<Vector2>().x, jumpInput.IsPressed() ? 1.0f : 0.0f, moveInput.ReadValue<Vector2>().y);
        lookVect = lookInput.ReadValue<Vector2>();

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

        weapon[equippedItem].transform.position = transform.position;
        weapon[equippedItem].transform.rotation = Quaternion.Euler(rotation.x, rotation.y, 0);
    }

    override protected void FixedUpdate()
    {
        CalcMovementAccelerationGrounded(true);
        RotateModel();
        DoAttack();
    }

    void InvCallBack(InputAction.CallbackContext context)
    {
        // Depending on which action it is, set the equipped item to that number
        for (int i = 0; i < invInput.Count; i++)
        {
            if (context.action == invInput[i])
            {
                if (i < weapon.Count)
                {
                    weapon[equippedItem].UnEquip();
                    equippedItem = i;
                    weapon[i].Equip();
                    break;
                }
                else
                {
                    UseConsumable(i - weapon.Count);
                    break;
                }

            }
        }
    }

    void DoAttack()
    {
        if (attackInput.IsPressed())
        {
            weapon[equippedItem].SetTriggerState(WeaponClass.TriggerState.HELD, cam, ref addedRotation, rigBod.linearVelocity * Time.fixedDeltaTime);
        }
        else
        {
            weapon[equippedItem].SetTriggerState(WeaponClass.TriggerState.RELEASED, cam, ref addedRotation, rigBod.linearVelocity * Time.fixedDeltaTime);
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
            consumable[(int)p.puType].IncreaseCount(p.num);

            // Destroy pickup
            Destroy(other.gameObject);
        }
    }

    void UseConsumable(int index)
    {
        if (equippedItem < weapon.Count)
        {
            if (consumable[index].GetCount() > 0)
            {
                switch (consumable[index].GetPUType())
                {
                    case PickUpClass.PickUpType.ELECTRIC:
                        weapon[equippedItem].SetState(WeaponClass.WeaponModifier.ELECTRIC, Consumable.consumableDuration);
                        break;
                }

                consumable[index].ReduceCount(1);
            }

        }
    }
}
