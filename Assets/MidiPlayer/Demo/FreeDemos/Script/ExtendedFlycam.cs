using UnityEngine;
using System.Collections;

namespace DemoMPTK
{

    public class ExtendedFlycam : MonoBehaviour
    {

        /*
        EXTENDED FLYCAM
            Desi Quintans (CowfaceGames.com), 17 August 2012.
            Based on FlyThrough.js by Slin (http://wiki.unity3d.com/index.php/FlyThrough), 17 May 2011.

        LICENSE
            Free as in speech, and free as in beer.

        FEATURES
            WASD/Arrows:    Movement
                      Q:    Climb
                      E:    Drop
                          Shift:    Move faster
                        Control:    Move slower
                            End:    Toggle cursor locking to screen (you can also press Ctrl+P to toggle play mode on and off).
        */

        public float cameraSensitivity = 90;
        public float climbSpeed = 4;
        public float normalMoveSpeed = 10;
        public float slowMoveFactor = 0.25f;
        public float fastMoveFactor = 3;

        private float rotationX = 0.0f;
        private float rotationY = 0.0f;

        void Start()
        {
        }

        public void ResetPosition()
        {
            Debug.Log("Reset");
            transform.position = new Vector3(0f, 1.3f, -13f);
            //transform.eulerAngles = new Vector3(0, 0, 0);
            rotationX = rotationY = 0f;
            transform.localRotation = Quaternion.AngleAxis(rotationX, Vector3.up);
            transform.localRotation *= Quaternion.AngleAxis(rotationY, Vector3.left);


        }

        public void Quit()
        {
            Application.Quit();
        }

        void Update()
        {
            if (Time.realtimeSinceStartup > 3f)
            {
                float speedFactor = 1f;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    speedFactor = fastMoveFactor;
                else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    speedFactor = slowMoveFactor;

                rotationX += Input.GetAxis("Mouse X") * cameraSensitivity * speedFactor * Time.unscaledDeltaTime;
                rotationY += Input.GetAxis("Mouse Y") * cameraSensitivity * speedFactor * Time.unscaledDeltaTime;
                rotationY = Mathf.Clamp(rotationY, -90, 90);

                transform.localRotation = Quaternion.AngleAxis(rotationX, Vector3.up);
                transform.localRotation *= Quaternion.AngleAxis(rotationY, Vector3.left);


                transform.position += transform.forward * normalMoveSpeed * speedFactor * Input.GetAxis("Vertical") * Time.unscaledDeltaTime;
                transform.position += transform.right * normalMoveSpeed * speedFactor * Input.GetAxis("Horizontal") * Time.unscaledDeltaTime;

                if (Input.GetKey(KeyCode.Q)) { transform.position += transform.up * climbSpeed * speedFactor * Time.unscaledDeltaTime; }
                if (Input.GetKey(KeyCode.E)) { transform.position -= transform.up * climbSpeed * speedFactor * Time.unscaledDeltaTime; }

                if (Input.GetKeyDown(KeyCode.End))
                {
                    Debug.Log($"End {Cursor.lockState}");
                    Cursor.lockState = (Cursor.lockState == CursorLockMode.Confined) ? CursorLockMode.None : CursorLockMode.Confined;
                }
            }
        }
    }
}
