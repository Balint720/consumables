using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(AttackClass))]
public class AttackInspector : Editor
{
    public VisualTreeAsset m_AttackInspectorUXML;
    public override VisualElement CreateInspectorGUI()
    {
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

        // Return the finished Inspector UI.
        return myInspector;
    }
}
