using System;
using System.Collections.Generic;
using System.ComponentModel;
using Codice.CM.WorkspaceServer.Lock;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(AttackClass))]
public class AttackInspector : Editor
{
    public VisualTreeAsset m_AttackInspectorUXML;
    public override VisualElement CreateInspectorGUI()
    {
        AttackClass at = (AttackClass)target;

        // Create a new VisualElement to be the root of our Inspector UI.
        VisualElement myInspector = new VisualElement();

        // Add a simple label.
        myInspector.Add(new Label("This is a custom Inspector"));

        // Load the UXML file and clone its tree into the inspector
        if (m_AttackInspectorUXML != null)
        {
            VisualElement uxmlContent = m_AttackInspectorUXML.CloneTree();
            myInspector.Add(uxmlContent);
        }

        // Hurtbox field
        ObjectField hurtbox = new ObjectField();
        hurtbox.label = "Get hurtboxes from GameObject children";
        hurtbox.bindingPath = "";
        hurtbox.objectType = typeof(GameObject);

        // Hurtbox strings

        List<String> hurtboxNames = at.hurtboxes;

        ListView hurtboxNameList = new ListView();
        hurtboxNameList.makeItem = () =>  new TextField();
        hurtboxNameList.bindItem = (e, i) =>
        {
            ((TextField)e).value = hurtboxNames[i];
            ((TextField)e).style.paddingLeft = new StyleLength(100);
        };

        hurtboxNameList.itemsSource = hurtboxNames;
        hurtboxNameList.bindingPath = "hurtboxes";

        hurtbox.RegisterValueChangedCallback(
            (evt) =>
            {
                Transform[] cols = ((GameObject)hurtbox.value).GetComponentsInChildren<Transform>();
                
                foreach (Transform c in cols)
                {
                    if (c.name.Contains("Hurtbox") || c.name.Contains("HurtBox"))
                    {
                        hurtboxNames.Add(c.name);
                    }
                }

                hurtboxNameList.RefreshItems();
            }

        );

        hurtboxNameList.onAdd += (i) => 
        {
            hurtboxNames.Add("");

            hurtboxNameList.RefreshItems();
        };

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

        hurtboxNameList.showAddRemoveFooter = true;
        hurtboxNameList.allowAdd = true;
        hurtboxNameList.allowRemove = true;
        hurtboxNameList.reorderable = true;

        myInspector.Add(hurtbox);
        myInspector.Add(hurtboxNameList);

        // Return the finished Inspector UI.
        return myInspector;
    }
}
