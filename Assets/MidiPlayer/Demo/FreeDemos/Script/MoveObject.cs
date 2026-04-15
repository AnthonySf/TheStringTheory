using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DemoMPTK
{
    public class MoveObject : MonoBehaviour
    {
        public enum MoveAxe { X, Y, Z };
        public float From;
        public float To;
        public float Speed;
        public bool RandomPosition;
        public MoveAxe Axe;

        private bool toTo;
        private float target;
        private float velocity;

        // Use this for initialization
        void Start()
        {
            target = To;
            toTo = true;
        }

        // Update is called once per frame
        void Update()
        {
            float position = 0;
            float angle = Time.unscaledDeltaTime * 50f;
            switch (Axe)
            {
                case MoveAxe.X: transform.Rotate(new Vector3(1, 0, 0), angle); ; break;
                case MoveAxe.Y: transform.Rotate(new Vector3(0, 1, 0), angle); break;
                case MoveAxe.Z: transform.Rotate(new Vector3(0, 0, 1), angle); break;
            }

            switch (Axe)
            {
                case MoveAxe.X: position = Mathf.SmoothDamp(transform.position.x, target, ref velocity, Speed, 100, Time.unscaledDeltaTime); break;
                case MoveAxe.Y: position = Mathf.SmoothDamp(transform.position.y, target, ref velocity, Speed, 100, Time.unscaledDeltaTime); break;
                case MoveAxe.Z: position = Mathf.SmoothDamp(transform.position.z, target, ref velocity, Speed, 100, Time.unscaledDeltaTime); break;
            }

            if (Mathf.Abs(velocity) < 0.1f)
            {
                if (RandomPosition)
                {
                    target = Random.Range(From, To);
                }
                else
                {
                    target = toTo ? From : To;
                    toTo = !toTo;
                }
            }

            switch (Axe)
            {
                case MoveAxe.X: transform.position = new Vector3(position, transform.position.y, transform.position.z); break;
                case MoveAxe.Y: transform.position = new Vector3(transform.position.x, position, transform.position.z); break;
                case MoveAxe.Z: transform.position = new Vector3(transform.position.x, transform.position.y, position); break;
            }
        }
    }
}
