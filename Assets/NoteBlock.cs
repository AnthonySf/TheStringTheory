using UnityEngine;

public class NoteBlock : MonoBehaviour
{
    public string targetNote;
    public int stringIndex;
    public float speed = 10f;
    public bool isHit = false;

    void Update()
    {
        // Move the note toward the camera (Z = 0)
        transform.Translate(Vector3.back * speed * Time.deltaTime);

        // Auto-destroy if it passes the player
        if (transform.position.z < -2f)
        {
            if (!isHit) Debug.Log("Missed: " + targetNote);
            Destroy(gameObject);
        }
    }
}