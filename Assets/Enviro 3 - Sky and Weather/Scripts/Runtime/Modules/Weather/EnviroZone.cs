using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


namespace Enviro
{

    [Serializable]
    public class EnviroZoneWeather
    {
        public bool showEditor;
        public EnviroWeatherType weatherType;
        public float probability = 50f;
        public bool seasonalProbability = false;
        public float probabilitySpring = 50f;
        public float probabilitySummer = 50f;
        public float probabilityAutumn = 50f;
        public float probabilityWinter = 50f;

    }
    [AddComponentMenu("Enviro 3/Weather Zone")]
    [ExecuteInEditMode]
    public class EnviroZone : MonoBehaviour
    {

        public EnviroWeatherType currentWeatherType;
        public EnviroWeatherType nextWeatherType;

        public bool autoWeatherChanges = true;
        public float weatherChangeIntervall = 2f;
        public double nextWeatherUpdate;

        public List<EnviroZoneWeather> weatherTypeList = new List<EnviroZoneWeather>();
        public Vector3 zoneScale = Vector3.one;
        public Color zoneGizmoColor;
        private BoxCollider zoneCollider;

 
        void OnEnable()
        {
            if(zoneCollider == null)
            {
                zoneCollider = gameObject.GetComponent<BoxCollider>();
 
                if(zoneCollider == null)
                zoneCollider = gameObject.AddComponent<BoxCollider>();
            }
            zoneCollider.isTrigger = true;

            if(EnviroManager.instance != null && EnviroManager.instance.Weather != null)
            {   
                bool addedToMgr = false;

                for(int i = 0; i < EnviroManager.instance.zones.Count; i++)
                {
                    if(EnviroManager.instance.zones[i] == this)
                    {
                        addedToMgr = true;
                        break;
                    }
                } 

                if(!addedToMgr)
                   EnviroManager.instance.Weather.RegisterZone(this);  
            }

            nextWeatherUpdate = EnviroManager.instance.Time.GetDateInHours() + weatherChangeIntervall;     
        }

        void OnDisable()
        {
            if(EnviroManager.instance != null && EnviroManager.instance.Weather != null)
            {
                for(int i = 0; i < EnviroManager.instance.zones.Count; i++)
                {
                    if(EnviroManager.instance.zones[i] == this)
                       EnviroManager.instance.Weather.RemoveZone(this);
                }
            } 
        }

        public void UpdateZoneScale ()
        {
            zoneCollider.size = zoneScale;
        }

        // Adds a new weather type to the zone.
        public void AddWeatherType(EnviroWeatherType wType)
        {
            EnviroZoneWeather weatherTypeEntry = new EnviroZoneWeather();
            weatherTypeEntry.weatherType = wType;
            weatherTypeList.Add(weatherTypeEntry);
        }

        // Removes a weather type from the zone.
        public void RemoveWeatherZoneType(EnviroZoneWeather wType)
        {
            weatherTypeList.Remove(wType);
        }

        // Changes the weather of the zone instantly.
        public void ChangeZoneWeatherInstant (EnviroWeatherType type)
        {
            if(EnviroManager.instance != null && currentWeatherType != type)
            {
                EnviroManager.instance.NotifyZoneWeatherChanged(type,this);

                if(EnviroManager.instance.currentZone == this && EnviroManager.instance.Weather != null)
                   EnviroManager.instance.Weather.targetWeatherType = type;
            }
            
            currentWeatherType = type;
        }

        // Changes the weather of the zone to the type for next weather update.
        public void ChangeZoneWeather (EnviroWeatherType type)
        {
            nextWeatherType = type;
        }
 
        private void ChooseNextWeatherRandom ()
        {
            float rand = UnityEngine.Random.Range(0f,100f * weatherTypeList.Count);
            bool nextWeatherFound = false;

            for (int i = 0; i < weatherTypeList.Count; i++)
            {
                if(weatherTypeList[i].seasonalProbability == true && EnviroManager.instance != null && EnviroManager.instance.Environment != null)
                {
                    switch (EnviroManager.instance.Environment.Settings.season)
                    {
                        case Enviro.EnviroEnvironment.Seasons.Spring:
                            if(weatherTypeList[i].probabilitySpring > 0f && rand <= weatherTypeList[i].probabilitySpring * weatherTypeList.Count)
                            {
                                ChangeZoneWeather(weatherTypeList[i].weatherType);
                                nextWeatherFound = true;
                                return;
                            }
                        break;

                        case Enviro.EnviroEnvironment.Seasons.Summer:
                            if(weatherTypeList[i].probabilitySummer > 0f &&  rand <= weatherTypeList[i].probabilitySummer * weatherTypeList.Count)
                            {
                                ChangeZoneWeather(weatherTypeList[i].weatherType);
                                nextWeatherFound = true;
                                return;
                            } 
                        break;

                        case Enviro.EnviroEnvironment.Seasons.Autumn:
                            if(weatherTypeList[i].probabilityAutumn > 0f &&  rand <= weatherTypeList[i].probabilityAutumn * weatherTypeList.Count)
                            {
                                ChangeZoneWeather(weatherTypeList[i].weatherType);
                                nextWeatherFound = true;
                                return;
                            }
                        break;

                        case Enviro.EnviroEnvironment.Seasons.Winter:
                            if(weatherTypeList[i].probabilityWinter > 0f &&  rand <= weatherTypeList[i].probabilityWinter * weatherTypeList.Count)
                            {
                                ChangeZoneWeather(weatherTypeList[i].weatherType);
                                nextWeatherFound = true;
                                return;
                            }
                        break;
 
                    }
                }
                else 
                {
                    if(rand <= weatherTypeList[i].probability * weatherTypeList.Count)
                    {
                        ChangeZoneWeather(weatherTypeList[i].weatherType);
                        nextWeatherFound = true;
                        return;
                    }
                } 

                rand -= 100f;       
            }

            if(!nextWeatherFound)
               ChangeZoneWeather(currentWeatherType);
        }


        private void UpdateZoneWeather()
        {
            if(EnviroManager.instance.Time != null)
            {
               double currentDate = EnviroManager.instance.Time.GetDateInHours();

               if(currentDate >= nextWeatherUpdate)
               {

                if(nextWeatherType != null)
                  ChangeZoneWeatherInstant(nextWeatherType);
                 else
                  ChangeZoneWeatherInstant(currentWeatherType);
                 
                 //Get next weather
                 ChooseNextWeatherRandom ();
                 nextWeatherUpdate = currentDate + weatherChangeIntervall;
               }
            }
        }

        void Update()
        {
            UpdateZoneScale ();

            if(!Application.isPlaying)
               return;

            if (EnviroManager.instance == null || EnviroManager.instance.Weather == null)
                return;

            if(autoWeatherChanges && EnviroManager.instance.Weather.globalAutoWeatherChange)
                UpdateZoneWeather();

            //Forces the weather change in Enviro when this zone is currently the active one.
            if(EnviroManager.instance.currentZone == this && EnviroManager.instance.Weather.targetWeatherType != currentWeatherType)
               EnviroManager.instance.Weather.targetWeatherType = currentWeatherType;
        }
 
        void OnTriggerEnter (Collider col)
        {
            if (EnviroManager.instance == null || EnviroManager.instance.Weather == null)
                return;

            //Change Weather to Zone Weather:
            if(col.gameObject.GetComponent<EnviroManager>())
               EnviroManager.instance.currentZone = this;
        }

        void OnTriggerExit (Collider col)
        {
             if (EnviroManager.instance == null || EnviroManager.instance.Weather == null)
                 return;
        
             if(col.gameObject.GetComponent<EnviroManager>())
             {
                if(EnviroManager.instance.currentZone == this)
                {
                    if(EnviroManager.instance.defaultZone != null)
                       EnviroManager.instance.currentZone = EnviroManager.instance.defaultZone;
                    else
                       EnviroManager.instance.currentZone = null;
                }
                  
             }
        } 

        void OnDrawGizmos () 
        {
            Gizmos.color = zoneGizmoColor;
            
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.matrix = rotationMatrix;

            Gizmos.DrawCube(Vector3.zero, new Vector3(zoneScale.x, zoneScale.y, zoneScale.z));
        }
    }
}
