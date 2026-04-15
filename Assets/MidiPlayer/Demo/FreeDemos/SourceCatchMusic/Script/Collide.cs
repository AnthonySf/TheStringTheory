using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MPTKDemoCatchMusic
{
    public class Collide : MonoBehaviour
    {

        public float accelerate;
        public float speed;

        // Use this for initialization
        void Start()
        {

        }

        void OnCollisionEnter(Collision col)
        {
            //Destroy(col.gameObject);
        }

        void FixedUpdate()
        {
            if (transform.position.y > 5f)
                speed += Time.fixedDeltaTime * accelerate;
            float translation = Time.fixedDeltaTime * speed;
            transform.Translate(0, translation, 0);
        }

        // Update is called once per frame
        void Update()
        {
            if (transform.position.y > 100f)
            {
                Destroy(this.gameObject);
            }
        }
    }
}
