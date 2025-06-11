using UnityEngine;

public class CameraControl : MonoBehaviour
{
    public GameObject Player;
    private PlayerControl script;
    public float offset;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Get the player character's script, which is assigned to the camera
        script = Player.GetComponent<PlayerControl>();
    }

    // Update is called once per frame
    void Update()
    {
        // The camera rotates based on player script's orientation variable, and the camera is at the position of the player
        transform.localRotation = Quaternion.Euler(script.GetRotation().x, script.GetRotation().y, 0);
        transform.position = Player.transform.position;
    }
}
