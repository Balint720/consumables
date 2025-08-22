using System.Linq;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;

public class BowAnimation : MonoBehaviour
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
    SkinnedMeshRenderer skMeRenderer;
    Transform boneTrans;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        skMeRenderer = modelObject.GetComponent<SkinnedMeshRenderer>();
        boneTrans = controlBone.GetComponent<Transform>();
    }

    // Update is called once per frame
    void Update()
    {
        float weight = 0.0f;
        switch (coordOfBone)
        {
            case Coord.X:
                weight = boneTrans.position.x;
                break;
            case Coord.Y:
                weight = boneTrans.position.y;
                break;
            case Coord.Z:
                weight = boneTrans.position.z;
                break;
            case Coord.RX:
                weight = boneTrans.rotation.x;
                break;
            case Coord.RY:
                weight = boneTrans.rotation.y;
                break;
            case Coord.RZ:
                weight = boneTrans.rotation.z;
                break;
        }

        if (degreeToWholeNumTransform) weight /= 90;

        if (multiply) weight *= multiplier;

        skMeRenderer.SetBlendShapeWeight(indexOfBlendMesh, weight);
    }
}
