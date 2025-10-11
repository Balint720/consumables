using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Unity.Collections;
using UnityEngine;

public class BlendMeshDriverConvert : MonoBehaviour
{
    public GameObject modelObject;
    public GameObject controlBone;
    public int indexOfBlendMesh;
    public enum Coord
    {
        X,
        Y,
        Z,
        RX,
        RY,
        RZ
    }
    public Coord coordOfBone;
    public bool multiply;
    public float multiplier;
    public bool degreeToWholeNumTransform;
    public bool radToDegTransform;
    SkinnedMeshRenderer skMeRenderer;
    Transform boneTrans;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    virtual protected void Start()
    {
        skMeRenderer = modelObject.GetComponent<SkinnedMeshRenderer>();
        boneTrans = controlBone.GetComponent<Transform>();
    }

    // Update is called once per frame
    virtual protected void Update()
    {
        float weight = 0.0f;
        switch (coordOfBone)
        {
            case Coord.X:
                weight = boneTrans.localPosition.x;
                break;
            case Coord.Y:
                weight = boneTrans.localPosition.y;
                break;
            case Coord.Z:
                weight = boneTrans.localPosition.z;
                break;
            case Coord.RX:
                weight = boneTrans.localRotation.x;
                break;
            case Coord.RY:
                weight = boneTrans.localRotation.y;
                break;
            case Coord.RZ:
                weight = boneTrans.localRotation.z;
                break;
        }

        if (radToDegTransform) weight *= Mathf.Rad2Deg;
        if (degreeToWholeNumTransform) weight /= 360.0f;

        if (multiply) weight *= multiplier;

        skMeRenderer.SetBlendShapeWeight(indexOfBlendMesh, weight);
    }
}
