using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Codice.CM.WorkspaceServer.Lock;
using NUnit.Framework.Internal;
using Unity.Properties;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(AttackClass))]
public class AttackInspector : Editor
{
    const float paddingLeftLength = 50.0f;
    const float h2 = 16.0f;

    SerializedObject sObj;

    SerializedProperty hurtboxesProperty;
    SerializedProperty animationSizeProperty;
    SerializedProperty animationProperty;


    public override VisualElement CreateInspectorGUI()
    {
        // Get the target object
        AttackClass at = (AttackClass)target;

        sObj = new SerializedObject(at);
        
        // Properties
        hurtboxesProperty = sObj.FindProperty("hurtboxes");

        // Create a new VisualElement to be the root of our Inspector UI.
        VisualElement myInspector = new VisualElement();

        // Add a simple label.
        myInspector.Add(new Label("This is a custom Inspector"));

        /*
        // Load the UXML file and clone its tree into the inspector
        if (m_AttackInspectorUXML != null)
        {
            VisualElement uxmlContent = m_AttackInspectorUXML.CloneTree();
            myInspector.Add(uxmlContent);
        }
        */

        // Box for base stats of attack
        Box statBox = new Box();

        Label statBoxLabel = new Label("Base stats");
        statBoxLabel.style.fontSize = h2;

        // Damage and knockback fields
        IntegerField damage = new IntegerField()
        {
            label = "Damage of attack",
            bindingPath = "damage",
        };
        FloatField knockback = new FloatField()
        {
            label = "Knockback value of attack",
            bindingPath = "knockback"
        };

        damage.style.paddingLeft = new StyleLength(paddingLeftLength);
        knockback.style.paddingLeft = new StyleLength(paddingLeftLength);

        // Adding fields to box
        statBox.Add(statBoxLabel);
        statBox.Add(damage);
        statBox.Add(knockback);

        // Hurtbox box
        Box hurtboxBox = new Box();

        Label hurtboxLabel = new Label("Hurtboxes");
        hurtboxLabel.style.fontSize = h2;

        // Field for gathering hurtboxes from GameObject
        // This is a convenience field so we do not have to write every single hurtbox, we can load them up
        ObjectField hurtboxObject = new ObjectField()
        {
            label = "Get hurtboxes from GameObject and its children",
            bindingPath = "",
            objectType = typeof(GameObject)
        };

        // Item count for listing index of items
        int indexCountHurtbox = 0;
        //int indexCountAnim = 0;

        // Hurtbox strings which will be saved to AttackClass object
        if (at.hurtboxes == null) at.hurtboxes = new List<String>();
        List<String> hurtboxNames = at.hurtboxes;
        ListView hurtboxNameList = new ListView()
        {
            // Every item is a new textfield
            makeItem = () =>  new TextField() 
                {
                    label = (indexCountHurtbox++).ToString()
                },

            // Binding items
            bindItem = (e, i) =>
            {
                while (hurtboxesProperty.arraySize <= i)
                {
                    hurtboxesProperty.arraySize++;
                }
                ((TextField)e).BindProperty(hurtboxesProperty.GetArrayElementAtIndex(i));
                ((TextField)e).style.paddingLeft = new StyleLength(paddingLeftLength);
            },
            itemsSource = hurtboxNames,
            bindingPath = "hurtboxes",

            // Show + and - icon, allow them to be pressed
            showAddRemoveFooter = true,
            allowAdd = true,
            allowRemove = true,

            // Allow reordering of items in list
            reorderable = true,
        };

        // On object loaded into hurtboxObject, gather all hurtboxes from object
        hurtboxObject.RegisterValueChangedCallback(
            (evt) =>
            {
                Transform[] cols = ((GameObject)hurtboxObject.value).GetComponentsInChildren<Transform>();
                
                foreach (Transform c in cols)
                {
                    if (c.name.Contains("Hurtbox") || c.name.Contains("HurtBox") || c.name.Contains("hurtbox"))
                    {
                        hurtboxNames.Add(c.name);
                    }
                }

                // Refresh items, because we added new ones
                hurtboxNameList.RefreshItems();
            }

        );
        
        // Event on pressing the "+" icon
        // Adds an empty string to end of list
        hurtboxNameList.onAdd += (i) => 
        {
            hurtboxNames.Add("");

            hurtboxNameList.RefreshItems();
        };

        // Event on pressing the "-" icon
        // Removes either selected item OR the last item (if selection is incorrect)
        hurtboxNameList.onRemove += (i) => 
        {
            if (i.selectedIndex >= 0 && i.selectedIndex < hurtboxNames.Count)
            {
                hurtboxNames.RemoveAt(i.selectedIndex);
            }
            else if (hurtboxNames.Count > 0)
            {
                hurtboxNames.RemoveAt(hurtboxNames.Count - 1);
            }
        };

        // Adding fields to box
        hurtboxBox.Add(hurtboxLabel);
        hurtboxBox.Add(hurtboxObject);
        hurtboxBox.Add(hurtboxNameList);

        
        // Box for animations
        Box animBox = new Box();
        Label animLabel = new Label("Animations");
        animLabel.style.fontSize = h2;

        ListView animList = new ListView()
        {
            bindingPath = "animations",
            showAddRemoveFooter = true,
            allowAdd = true,
            allowRemove = true,

            virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
            fixedItemHeight = 250.0f
        };



        

        // Add fields to box
        animBox.Add(animLabel);
        animBox.Add(animList);

        // Add the elements to inspector object
        myInspector.Add(statBox);
        myInspector.Add(hurtboxBox);
        myInspector.Add(animBox);

        // Return the finished Inspector UI.
        return myInspector;
    }

    public ListView CreateListView()
    {
        var listView = new ListView()
        {
            virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
            showFoldoutHeader = true,
            headerTitle = "Elements",
            makeItem = () => {
                var propertyField = new PropertyField();
                propertyField.bindingPath = "myField";

                var container = new BindableElement(); // This MUST be a bindable element!
                container.Add(propertyField);
                return container;
            }
        };
        
        return listView;
        // NOTE: Don't forget to add the list view to your tree!
    }
}
