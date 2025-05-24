using UnityEngine;

public class CameraControl : MonoBehaviour
{
    public GameObject Player;
    private PlayerControl script;
    public float offset;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        script = Player.GetComponent<PlayerControl>();
    }

    // Update is called once per frame
    void Update()
    {
        transform.localRotation = Quaternion.Euler(script.GetRotation().x, script.GetRotation().y, 0);
        transform.position = Player.transform.position;
    }
}
