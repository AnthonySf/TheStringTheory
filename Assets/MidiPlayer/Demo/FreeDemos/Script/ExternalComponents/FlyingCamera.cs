using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DemoMPTK
{

    public class FlyingCamera : MonoBehaviour
    {
        /*
         * Based on Windex's flycam script found here: http://forum.unity3d.com/threads/fly-cam-simple-cam-script.67042/
         * C# conversion created by Ellandar
         * Improved camera made by LookForward
         * Modifications created by Angryboy
         * 1) Have to hold right-click to rotate
         * 2) Made variables public for testing/designer purposes
         * 3) Y-axis now locked (as if space was always being held)
         * 4) Q/E keys are used to raise/lower the camera
         *
         * Another Modification created by micah_3d
         * 1) adding an isColliding bool to allow camera to collide with world objects, terrain etc.
         */

        public float mainSpeed = 100.0f; //regular speed
        public float shiftAdd = 250.0f; //multiplied by how long shift is held.  Basically running
        public float maxShift = 1000.0f; //Maximum speed when holdin gshift
        public float camSens = 0.25f; //How sensitive it with mouse
        public bool Auto = true;
        //private Vector3 lastMouse = new Vector3(255, 255, 255); //kind of in the middle of the screen, rather than at the top (play)
        private float totalRun = 1.0f;

        private bool isRotating = false; // Angryboy: Can be called by other things (e.g. UI) to see if camera is rotating
        private float speedMultiplier; // Angryboy: Used by Y axis to match the velocity on X/Z axis

        public float mouseSensitivity = 5.0f;        // Mouse rotation sensitivity.
        private float rotationY = 0.0f;

        //micah_3d: added so camera will be able to collide with world objects if users chooses
        public bool isColliding = true;
        //physic material added to keep camera from spinning out of control if it hits a corner or multiple colliders at the same time.  
#if UNITY_6000_0_OR_NEWER
        PhysicsMaterial myMaterial;
#else
        PhysicMaterial myMaterial;
#endif
        void Start()
        {
            if (isColliding == true)
            {
#if UNITY_6000_0_OR_NEWER
                myMaterial = new PhysicsMaterial("ZeroFriction");
                myMaterial.frictionCombine = PhysicsMaterialCombine.Multiply;
                myMaterial.bounceCombine = PhysicsMaterialCombine.Average;
#else
                myMaterial = new PhysicMaterial("ZeroFriction");
                myMaterial.frictionCombine = PhysicMaterialCombine.Multiply;
                myMaterial.bounceCombine = PhysicMaterialCombine.Average;
#endif
                myMaterial.dynamicFriction = 0f;
                myMaterial.staticFriction = 0f;
                myMaterial.bounciness = 0f;
                
                gameObject.AddComponent<CapsuleCollider>();
                gameObject.GetComponent<CapsuleCollider>().radius = 1f;
                gameObject.GetComponent<CapsuleCollider>().height = 1.68f;
                gameObject.GetComponent<CapsuleCollider>().material = myMaterial;

                gameObject.AddComponent<Rigidbody>();
                gameObject.GetComponent<Rigidbody>().useGravity = false;
            }
        }
        void Update()
        {

            // Angryboy: Hold right-mouse button to rotate
            if (Input.GetMouseButtonDown(1))
            {
                isRotating = true;
            }
            if (Input.GetMouseButtonUp(1))
            {
                isRotating = false;
            }
            if (isRotating)
            {
                // Made by LookForward
                // Angryboy: Replaced min/max Y with numbers, not sure why we had variables in the first place
                float rotationX = transform.localEulerAngles.y + Input.GetAxis("Mouse X") * mouseSensitivity;
                rotationY += Input.GetAxis("Mouse Y") * mouseSensitivity;
                rotationY = Mathf.Clamp(rotationY, -90, 90);
                transform.localEulerAngles = new Vector3(-rotationY, rotationX, 0.0f);
            }

            //Keyboard commands
            //float f = 0.0f;
            Vector3 p = GetBaseInput();
            if (Input.GetKey(KeyCode.LeftShift))
            {
                totalRun += Time.unscaledDeltaTime;
                p = p * totalRun * shiftAdd;
                p.x = Mathf.Clamp(p.x, -maxShift, maxShift);
                p.y = Mathf.Clamp(p.y, -maxShift, maxShift);
                p.z = Mathf.Clamp(p.z, -maxShift, maxShift);
                // Angryboy: Use these to ensure that Y-plane is affected by the shift key as well
                speedMultiplier = totalRun * shiftAdd * Time.unscaledDeltaTime;
                speedMultiplier = Mathf.Clamp(speedMultiplier, -maxShift, maxShift);
            }
            else
            {
                totalRun = Mathf.Clamp(totalRun * 0.5f, 1f, 1000f);
                p = p * mainSpeed;
                speedMultiplier = mainSpeed * Time.unscaledDeltaTime; // Angryboy: More "correct" speed
            }

            p = p * Time.unscaledDeltaTime;

            // Angryboy: Removed key-press requirement, now perma-locked to the Y plane
            Vector3 newPosition = transform.position;//If player wants to move on X and Z axis only
            transform.Translate(p);
            newPosition.x = transform.position.x;
            newPosition.z = transform.position.z;

            // Angryboy: Manipulate Y plane by using Q/E keys
            if (Input.GetKey(KeyCode.Q))
            {
                newPosition.y += -speedMultiplier;
            }
            if (Input.GetKey(KeyCode.E))
            {
                newPosition.y += speedMultiplier;
            }

            transform.position = newPosition;
        }

        // Angryboy: Can be called by other code to see if camera is rotating
        // Might be useful in UI to stop accidental clicks while turning?
        public bool IsRotating()
        {
            return isRotating;
        }

        private Vector3 GetBaseInput()
        { //returns the basic values, if it's 0 than it's not active.
            Vector3 p_Velocity = new Vector3();
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                p_Velocity += new Vector3(0, 0, 1);
            }
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                p_Velocity += new Vector3(0, 0, -1);
            }
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                p_Velocity += new Vector3(-1, 0, 0);
            }
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                p_Velocity += new Vector3(1, 0, 0);
            }
            return p_Velocity;
        }
    }



    //    using UnityEngine;
    //    using System.Collections;

    //    public class CameraPath : MonoBehaviour
    //{

    //    /*
    //    Writen by Windexglow 11-13-10.  Use it, edit it, steal it I don't care.  
    //    Converted to C# 27-02-13 - no credit wanted.
    //    Simple flycam I made, since I couldn't find any others made public.  
    //    Made simple to use (drag and drop, done) for regular keyboard layout  
    //    wasd : basic movement
    //    shift : Makes camera accelerate
    //    space : Moves camera on X and Z axis only.  So camera doesn't gain any height*/


    //    float mainSpeed = 20.0f; //regular speed
    //    float shiftAdd = 250.0f; //multiplied by how long shift is held.  Basically running
    //    float maxShift = 100.0f; //Maximum speed when holdin gshift
    //    float camSens = 0.2f; //How sensitive it with mouse
    //    private Vector3 lastMouse = new Vector3(255, 255, 255); //kind of in the middle of the screen, rather than at the top (play)
    //    private float totalRun = 1.0f;

    //    private void Start()
    //    {

    //        lastMouse = Input.mousePosition;
    //    }
    //    void Update()
    //    {
    //        lastMouse = Input.mousePosition - lastMouse;
    //        lastMouse = new Vector3(-lastMouse.y * camSens, lastMouse.x * camSens, 0);
    //        lastMouse = new Vector3(transform.eulerAngles.x + lastMouse.x, transform.eulerAngles.y + lastMouse.y, 0);
    //        transform.eulerAngles = lastMouse;
    //        lastMouse = Input.mousePosition;
    //        //Mouse  camera angle done.  

    //        //Keyboard commands
    //        float f = 0.0f;
    //        Vector3 p = GetBaseInput();
    //        if (Input.GetKey(KeyCode.LeftShift))
    //        {
    //            totalRun += Time.deltaTime;
    //            p = p * totalRun * shiftAdd;
    //            p.x = Mathf.Clamp(p.x, -maxShift, maxShift);
    //            p.y = Mathf.Clamp(p.y, -maxShift, maxShift);
    //            p.z = Mathf.Clamp(p.z, -maxShift, maxShift);
    //        }
    //        else
    //        {
    //            totalRun = Mathf.Clamp(totalRun * 0.5f, 1f, 1000f);
    //            p = p * mainSpeed;
    //        }

    //        p = p * Time.deltaTime;
    //        Vector3 newPosition = transform.position;
    //        if (Input.GetKey(KeyCode.Space))
    //        { //If player wants to move on X and Z axis only
    //            transform.Translate(p);
    //            newPosition.x = transform.position.x;
    //            newPosition.z = transform.position.z;
    //            transform.position = newPosition;
    //        }
    //        else
    //        {
    //            transform.Translate(p);
    //        }

    //    }

    //    private Vector3 GetBaseInput()
    //    { //returns the basic values, if it's 0 than it's not active.
    //        Vector3 p_Velocity = new Vector3();
    //        if (Input.GetKey(KeyCode.W))
    //        {
    //            p_Velocity += new Vector3(0, 0, 1);
    //        }
    //        if (Input.GetKey(KeyCode.S))
    //        {
    //            p_Velocity += new Vector3(0, 0, -1);
    //        }
    //        if (Input.GetKey(KeyCode.A))
    //        {
    //            p_Velocity += new Vector3(-1, 0, 0);
    //        }
    //        if (Input.GetKey(KeyCode.D))
    //        {
    //            p_Velocity += new Vector3(1, 0, 0);
    //        }
    //        return p_Velocity;
    //    }
    //}
}
