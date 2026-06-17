using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Enviro
{
    [ExecuteInEditMode]
    [AddComponentMenu("Enviro 3/Effect Removal Zone")]
    public class EnviroEffectRemovalZone : MonoBehaviour
    {
        public enum Mode
        {
            Spherical,
            Cubical
        }
        public Mode type;

        [Range(-10f, 0f)]
        public float density = -10.0f;

        public float radius = 1.0f;
        public float stretch = 2.0f;
        [Range(0, 1)]
        public float feather = 0.7f;

        public Vector3 size = Vector3.one * 10;

        void OnEnable()
        {
            if(EnviroManager.instance != null)
               AddToZoneToManager();
        }
 
        void OnDisable() 
        {
            if(EnviroManager.instance != null)
               RemoveZoneFromManager();
        }

        
        void OnDestroy()
        {
            if(EnviroManager.instance != null)
               RemoveZoneFromManager();
        }

  
        void AddToZoneToManager()
        {
           bool addedToMgr = false;

           for(int i = 0; i <  EnviroManager.instance.removalZones.Count; i++)
           {
                if(EnviroManager.instance.removalZones[i] == this)
                {
                   addedToMgr = true;
                   break;
                }
           }

           if(!addedToMgr)
              EnviroManager.instance.AddRemovalZone(this);
        }
         
        void RemoveZoneFromManager()
        {
           for(int i = 0; i <  EnviroManager.instance.removalZones.Count; i++)
           {
                if(EnviroManager.instance.removalZones[i] == this)
                   EnviroManager.instance.RemoveRemovaleZone(EnviroManager.instance.removalZones[i]);
           }
        }

        void Update()
        {
            transform.localScale = size;
        }

        void OnDrawGizmosSelected()
        {
            if(type == Mode.Spherical)
            {
                Matrix4x4 m = Matrix4x4.identity;
                Transform t = transform;
                m.SetTRS(t.position, t.rotation, new Vector3(1.0f, stretch, 1.0f));
                Gizmos.matrix =  m;
                Gizmos.DrawWireSphere(Vector3.zero, radius);
            }
            else
            {
                Matrix4x4 m = Matrix4x4.identity;
                Transform t = transform; 
                m.SetTRS(t.position, t.rotation, new Vector3(1.0f, 1.0f, 1.0f));
                Gizmos.matrix =  m;
                Gizmos.DrawWireCube(Vector3.zero,t.localScale);
            }
        }
    }
}
