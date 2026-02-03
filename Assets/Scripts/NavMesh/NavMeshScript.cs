using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class NavMeshScript : MonoBehaviour
{
    NavMeshSurface nav;
    List<NavMeshLink> navLinks;
    NavMeshLinkData[] navLinkDatas;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        nav = GetComponent<NavMeshSurface>();
        nav.BuildNavMesh();

        NavMeshData mesh = nav.navMeshData;

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
