using UnityEngine;

public class NoteHighway : MonoBehaviour
{
    public static Color[] StringColors = {
        new Color(0.5f, 0, 1),      // High E (Purple)
        Color.green,                // B
        new Color(1, 0.5f, 0),      // G (Orange)
        Color.blue,                 // D
        Color.yellow,               // A
        Color.red                   // Low E
    };

    void Awake()
    {
        GenerateStrings();
    }

    void GenerateStrings()
    {
        for (int i = 0; i < 6; i++)
        {
            // Create a long thin cube to act as a string
            GameObject stringLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stringLine.name = "String_" + i;
            
            // Position them side-by-side
            stringLine.transform.position = new Vector3(i * 1.5f - 3.75f, 0, 10);
            
            // Scale them to be long "tracks"
            stringLine.transform.localScale = new Vector3(0.1f, 0.01f, 30f);
            
            // Set the color
            stringLine.GetComponent<Renderer>().material.color = StringColors[i];
            stringLine.GetComponent<Renderer>().material.shader = Shader.Find("Unlit/Color");
        }
    }
}