using UnityEngine;

public class CameraControl : MonoBehaviour
{
    public GameObject Player;
    private EntityClass script;
    public Vector3 offset;
    public Camera cameraComp;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Get the player character's script, which is assigned to the camera
        script = Player.GetComponent<EntityClass>();
        cameraComp = gameObject.GetComponent<Camera>();
    }

    // Update is called once per frame
    void Update()
    {
        // The camera rotates based on player script's orientation variable, and the camera is at the position of the player
        transform.localRotation = Quaternion.Euler(script.PitchX, script.YawY, 0);
        transform.position = Player.transform.position + offset;
    }
}
