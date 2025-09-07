using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class NavMeshScript : MonoBehaviour
{
    NavMeshSurface nav;
    NavMeshLink[] navLinks;
    NavMeshLinkData[] navLinkDatas;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
        nav = GetComponent<NavMeshSurface>();
        navLinks = GetComponentsInChildren<NavMeshLink>();

        /*
        navLinkDatas = new NavMeshLinkData[navLinks.Count()];

        for (int i = 0; i < navLinks.Count(); i++)
        {
            navLinkDatas[i].agentTypeID = navLinks[i].agentTypeID;
            navLinkDatas[i].area = navLinks[i].area;
            navLinkDatas[i].bidirectional = navLinks[i].bidirectional;
            navLinkDatas[i].costModifier = navLinks[i].costModifier;
            navLinkDatas[i].endPosition = navLinks[i].endPoint;
            navLinkDatas[i].startPosition = navLinks[i].startPoint;
            navLinkDatas[i].width = navLinks[i].width;

            NavMesh.AddLink(navLinkDatas[i]);
        }
        */
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
