using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Enviro
{
    

    [ExecuteInEditMode]
    public class EnviroManager : EnviroManagerBase
    {     
        private static EnviroManager _instance; // Creat a static instance for easy access!

        public static EnviroManager instance
        { 
            get 
            {
#if UNITY_6000_0_OR_NEWER
            if (_instance == null)
                _instance = GameObject.FindAnyObjectByType<EnviroManager>();
#else
            if (_instance == null)
                _instance = GameObject.FindObjectOfType<EnviroManager>();
#endif

                return _instance;
            }
        }
  
        //General
        public GeneralObjects Objects = new GeneralObjects();
        
        //Setup
        public bool dontDestroyOnLoad;
        public Camera Camera;
        public string CameraTag = "MainCamera";
        public List<EnviroCameras> Cameras = new List<EnviroCameras>();
        [Tooltip("'Optional': Assign a transform here to change what object Enviro and weather effects should follow. If not set it will use the camera transform.")]
        public Transform optionalFollowTransform;
  
        //Inspector
        public bool showSetup;
        public bool showModules;
        public bool showEvents;
        public bool showThirdParty;
 
        // Publics 
        [Range(0.2f,0.7f)]
        public float dayNightSwitch = 0.45f;
        public bool isNight;
        public float solarTime;
        public float lunarTime;
        public bool notFirstFrame = false;

        //Effect Removal Zones
        public List<EnviroEffectRemovalZone> removalZones = new List<EnviroEffectRemovalZone>();
   
        struct ZoneParams
        {
            public float type;
            public Vector3 pos;
            public float radius;
            public Vector3 size;
            public Vector3 axis;   
            public float stretch;
            public float density;
            public float feather;
            public Matrix4x4 transform;
            public float pad0;
            public float pad1;
        }

        public ComputeBuffer clearZoneCB;
        public ComputeBuffer removeZoneParamsCB;
        public ComputeBuffer clearCBPoint;
        public ComputeBuffer clearCBSpot;

        ZoneParams[] removalZoneParams;

        //Non time module controls
        [Range(0f,360f)]
        public float sunRotationX;
        [Range(0f,360f)]
        public float sunRotationY;
        [Range(0f,360f)]
        public float moonRotationX;
        [Range(0f,360f)]
        public float moonRotationY;
        public bool showNonTimeControls;
        /////// 
        //Events
        public Enviro.EnviroEvents Events;
        public delegate void HourPassed();
        public delegate void DayPassed();
        public delegate void YearPassed();
        public delegate void WeatherChanged(EnviroWeatherType weatherType);
        public delegate void ZoneWeatherChanged(EnviroWeatherType weatherType, Enviro.EnviroZone zone);
        public delegate void SeasonChanged(EnviroEnvironment.Seasons season);
        public delegate void isNightEvent();
        public delegate void isDayEvent();
 
        public event HourPassed OnHourPassed;
        public event DayPassed OnDayPassed;
        public event YearPassed OnYearPassed;
        public event WeatherChanged OnWeatherChanged;
        public event ZoneWeatherChanged OnZoneWeatherChanged;
        public event SeasonChanged OnSeasonChanged; 
        public event isNightEvent OnNightTime;
        public event isDayEvent OnDayTime;

        //Zones
        public EnviroZone currentZone;
        public EnviroZone defaultZone;
        public List<EnviroZone> zones = new List<EnviroZone>();

        public virtual void NotifyHourPassed()
        {
            if (OnHourPassed != null)
                OnHourPassed();
        }
        public virtual void NotifyDayPassed()
        {
            if (OnDayPassed != null)
                OnDayPassed();
        }
        public virtual void NotifyYearPassed()
        {
            if (OnYearPassed != null)
                OnYearPassed(); 
        }
        public virtual void NotifyWeatherChanged(EnviroWeatherType type)
        {
            if (OnWeatherChanged != null)
                OnWeatherChanged(type);
        }
        public virtual void NotifyZoneWeatherChanged(EnviroWeatherType type, EnviroZone zone)
        {
            if (OnZoneWeatherChanged != null)
                OnZoneWeatherChanged(type, zone);
        }
        public virtual void NotifySeasonChanged(EnviroEnvironment.Seasons season)
        {
            if (OnSeasonChanged != null)
                OnSeasonChanged(season);
        }
        public virtual void NotifyIsNight()
        {
            if (OnNightTime != null)
                OnNightTime();
        }
        public virtual void NotifyIsDay()
        {
            if (OnDayTime != null)
                OnDayTime();
        }

        //Event Invoke
        private void HourPassedInvoke()
        {
            Events.onHourPassedActions.Invoke();
        }
        private void DayPassedInvoke()
        {
            Events.onDayPassedActions.Invoke();
        }
        private void YearPassedInvoke()
        {
            Events.onYearPassedActions.Invoke();
        }
        private void WeatherChangedInvoke()
        {
            Events.onWeatherChangedActions.Invoke();
        }
         private void SeasonsChangedInvoke()
        {
            Events.onSeasonChangedActions.Invoke();
        }
        private void NightTimeInvoke()
        {
            Events.onNightActions.Invoke ();
        }
        private void DayTimeInvoke()
        {
            Events.onDayActions.Invoke ();
        }
        private void ZoneChangedInvoke()
        {
            Events.onZoneChangedActions.Invoke ();
        }

        //Lighting updates
        public bool updateSkyAndLighting = true;
        public bool updateSkyAndLightingHDRP = true;
 
        // HDRP
#if ENVIRO_HDRP 
        public UnityEngine.Rendering.Volume volumeHDRP;
        public UnityEngine.Rendering.VolumeProfile volumeProfileHDRP;
        public UnityEngine.Rendering.HighDefinition.CustomPassVolume customPassVolumeHDRP;
#endif
        //////



        void OnEnable()
        {
    #if UNITY_EDITOR
            if(UnityEditor.PrefabUtility.IsPartOfAnyPrefab(gameObject))
               UnityEditor.PrefabUtility.UnpackPrefabInstance(gameObject,UnityEditor.PrefabUnpackMode.OutermostRoot,UnityEditor.InteractionMode.UserAction);
    #endif

            if(configuration == null)
               Debug.Log("Please create or assign a configuration asset in your Enviro Manager!");

            CreateGeneralObjects ();
    #if ENVIRO_HDRP
            CreateHDRPVolume ();
    #endif
            UpdateManager();
            EnableModules();

    #if !ENVIRO_HDRP && !ENVIRO_URP
            //Add Enviro Render Component to main camera in builtin rp
           AddCameraComponents();
    #endif

            EventInit();
            SetSRPKeywords();



            if (isNight)
                NotifyIsNight();
            else
                NotifyIsDay();

        }  

        void OnDisable()
        {  
            //Disable shader integration code.
            Shader.SetGlobalFloat("_EnviroActive", 0f);
            
            if(Fog != null)
               Fog.Disable();

            ReleaseZoneBuffers();
        }
 
        private void AddCameraComponents()
        {
            if(Camera != null)
            {
                if(Camera.gameObject.GetComponent<EnviroRenderer>() == null)
                   Camera.gameObject.AddComponent<EnviroRenderer>();
            }

            for(int i = 0; i < Cameras.Count; i++)
            {
                if(Cameras[i].camera != null)
                {
                    if(Cameras[i].camera.gameObject.GetComponent<EnviroRenderer>() == null)
                        Cameras[i].camera.gameObject.AddComponent<EnviroRenderer>();
                }
            }
        }

        // Change the camera to a new one.
        public void ChangeCamera (Camera cam)
        {
            Camera = cam;

    #if !ENVIRO_HDRP && !ENVIRO_URP
            AddCameraComponents();
    #endif
        }
 
        public void AddAdditionalCamera (Camera cam, bool reset = false)
        {
            bool added = false;

            for(int i = 0; i < Cameras.Count; i++)
            {
                if(Cameras[i].camera != null && Cameras[i].camera == cam)
                added = true;
            }

            if(!added)
            {
                EnviroCameras newCam = new EnviroCameras();
                newCam.camera = cam;
                newCam.resetMatrix = reset;

                Cameras.Add(newCam);
            #if !ENVIRO_HDRP && !ENVIRO_URP
                AddCameraComponents();
            #endif
            }
        }

        void Start ()
        {   

            // Set dont destroy on load on start
            if(dontDestroyOnLoad && Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            //Set a first frame bool that will be used for events.
            notFirstFrame = false;
            StartCoroutine(FirstFrame());

            StartModules ();
        }
 
        //Update modules
        void Update()
        {     
            if(!Application.isPlaying)
                LoadConfiguration();

            UpdateManager ();

            //Update all modules
            UpdateModules ();

            //Update non time case
            if(Time == null)
               UpdateNonTime();

            Shader.SetGlobalFloat("_EnviroActive", 1f);
        }

        void LateUpdate() 
        { 
            if(Camera != null)
            {
                if(optionalFollowTransform != null)
                {
                    transform.position = optionalFollowTransform.position;
                }
                else
                {
                    transform.position = Camera.transform.position;
                }
            }    
        }

        void CreateGeneralObjects ()
        {
            if(Objects.sun == null)
            {
                Objects.sun = new GameObject();
                Objects.sun.name = "Sun";
                Objects.sun.transform.SetParent(transform);
                Objects.sun.transform.localPosition = Vector3.zero;
            }

            if(Objects.moon == null)
            {
                Objects.moon = new GameObject();
                Objects.moon.name = "Moon";
                Objects.moon.transform.SetParent(transform);
                Objects.moon.transform.localPosition = Vector3.zero;
            }
  
            if(Objects.stars == null)
            { 
                Objects.stars = new GameObject();
                Objects.stars.name = "Stars";
                Objects.stars.transform.SetParent(transform);
                Objects.stars.transform.localPosition = Vector3.zero;
            }
        }

        // Set the solar and lunar time based on sun rotation.
        public void UpdateNonTime()
        {
            if(Objects.sun != null)
            {
                Objects.sun.transform.eulerAngles = new Vector3(sunRotationX,sunRotationY,0f);

                if(sunRotationX > 0f && sunRotationX <= 90f)
                   solarTime = EnviroHelper.Remap(sunRotationX, 0f, 90f, 0.5f, 1f);
                else if (sunRotationX > 90f && sunRotationX <= 180f)
                   solarTime = EnviroHelper.Remap(sunRotationX, 90f, 180f, 1f, 0.5f);
                else if (sunRotationX > 180f && sunRotationX <= 270f)
                   solarTime = EnviroHelper.Remap(sunRotationX, 180f, 270f, 0.5f, 0.0f);
                else if (sunRotationX > 270f && sunRotationX <= 360f)
                   solarTime = EnviroHelper.Remap(sunRotationX, 270f, 360f, 0.0f, 0.5f);
                else solarTime = 0.5f;
            }
            if(Objects.moon != null)
            {
                Objects.moon.transform.eulerAngles = new Vector3(moonRotationX,moonRotationY,0f);
                 
                if(moonRotationX > 0f && moonRotationX <= 90f)
                   lunarTime = EnviroHelper.Remap(moonRotationX, 0f, 90f, 0.5f, 1f);
                else if (moonRotationX > 90f && moonRotationX <= 180f)
                   lunarTime = EnviroHelper.Remap(moonRotationX, 90f, 180f, 1f, 0.5f);
                else if (moonRotationX > 180f && moonRotationX <= 270f)
                   lunarTime = EnviroHelper.Remap(moonRotationX, 180f, 270f, 0.5f, 0.0f);
                else if (moonRotationX > 270f && moonRotationX <= 360f)
                   lunarTime = EnviroHelper.Remap(moonRotationX, 270f, 360f, 0.0f, 0.5f);
                else lunarTime = 0.5f; 
            }
        }

        //Effect Removal Zones
        public bool AddRemovalZone (EnviroEffectRemovalZone zone)
        {
            removalZones.Add(zone);
            return true;
        }
        public void RemoveRemovaleZone (EnviroEffectRemovalZone zone)
        {

         if(removalZones.Contains(zone))
            removalZones.Remove(zone);

        }
 
        private void SetupZoneBuffers() 
        { 
            int count = 0;

            for (int z = 0; z < removalZones.Count; z++)
            {
                if (removalZones[z] != null && removalZones[z].enabled && removalZones[z].gameObject.activeSelf)
                count++;
            }

            Shader.SetGlobalFloat("_EnviroRemovalZonesCount", count);

            if (count == 0)
            {
                // Can't not set the buffer
                Shader.SetGlobalBuffer("_EnviroRemovalZones", clearZoneCB);
                return;
            } 

            if (removalZoneParams == null || removalZoneParams.Length != count)
                removalZoneParams = new ZoneParams[count];

            int zoneID = 0;
            for (int i = 0; i < removalZones.Count; i++)
            {
                Enviro.EnviroEffectRemovalZone fz = removalZones[i];
                if (fz == null || !fz.enabled || !fz.gameObject.activeSelf)
                    continue;

                Transform t = fz.transform;

                removalZoneParams[zoneID].type = (int)fz.type;
                removalZoneParams[zoneID].pos = t.position;
                removalZoneParams[zoneID].radius = fz.radius * fz.radius;
                removalZoneParams[zoneID].size = fz.size;
                removalZoneParams[zoneID].axis = -t.up;
                removalZoneParams[zoneID].stretch = 1.0f/fz.stretch - 1.0f;
                removalZoneParams[zoneID].density = fz.density;
                removalZoneParams[zoneID].feather = 1.0f - fz.feather;
                removalZoneParams[zoneID].transform = t.transform.worldToLocalMatrix;
                removalZoneParams[zoneID].pad0 = 0f;
                removalZoneParams[zoneID].pad1 = 0f;

                zoneID++;
            }  
            removeZoneParamsCB.SetData(removalZoneParams);
            Shader.SetGlobalBuffer("_EnviroRemovalZones", removeZoneParamsCB);
        }

        private void CreateZoneBuffers()
        {
            EnviroHelper.CreateBuffer(ref removeZoneParamsCB, removalZones.Count, Marshal.SizeOf(typeof(ZoneParams)));
            EnviroHelper.CreateBuffer(ref clearZoneCB, 1, 128);
        }

        private void ReleaseZoneBuffers()
        {
            if(removeZoneParamsCB != null)
            EnviroHelper.ReleaseComputeBuffer(ref removeZoneParamsCB);
            if(clearZoneCB != null)
            EnviroHelper.ReleaseComputeBuffer(ref clearZoneCB);
        }
 
        IEnumerator FirstFrame ()
        {
            yield return 0;
            notFirstFrame = true;
        }

        ///HDRP Section
        public void CreateHDRPVolume ()
        {
#if ENVIRO_HDRP
            if(volumeProfileHDRP == null)
            {
               volumeProfileHDRP = EnviroHelper.GetDefaultSkyAndFogProfile("Enviro HDRP Sky and Fog Volume");
            }

            if(volumeHDRP == null)
            {
                GameObject volume = new GameObject();
                volume.name = "Enviro Sky and Fog Volume";
                volume.transform.SetParent(transform);
                volume.transform.localPosition = Vector3.zero;   
                volumeHDRP = volume.AddComponent<UnityEngine.Rendering.Volume>();     
                volumeHDRP.sharedProfile = volumeProfileHDRP;  
                volumeHDRP.priority = 1;
            }
            else
            {
                volumeHDRP.sharedProfile = volumeProfileHDRP;  
            }

            if(customPassVolumeHDRP == null)
            {
                GameObject volume = new GameObject();
                volume.name = "Enviro Custom Pass";
                volume.transform.SetParent(transform);
                volume.transform.localPosition = Vector3.zero;   
                customPassVolumeHDRP = volume.AddComponent<UnityEngine.Rendering.HighDefinition.CustomPassVolume>();
                customPassVolumeHDRP.isGlobal = true;
                customPassVolumeHDRP.injectionPoint = UnityEngine.Rendering.HighDefinition.CustomPassInjectionPoint.AfterOpaqueAndSky;
                customPassVolumeHDRP.AddPassOfType<EnviroHDRPCustomPass>(); 
            }
            else
            {
                 // Check if a pass of type EnviroHDRPCustomPass already exists
                bool hasPass = false;
                foreach (var pass in customPassVolumeHDRP.customPasses)
                {
                    if (pass is EnviroHDRPCustomPass)
                    {
                        hasPass = true;
                        break;
                    }
                }

                if(!hasPass)
                 customPassVolumeHDRP.AddPassOfType<EnviroHDRPCustomPass>(); 
            }
#endif
        }
 
        private void CheckCameraSetup ()
        {
            //Auto assign camera with the camera tag when camera not set.
            if(Camera == null)
            {
                for (int i = 0; i < Camera.allCameras.Length; i++)
                {
                    if (Camera.allCameras[i].tag == CameraTag)
                    {
                        Camera = Camera.allCameras[i];
                #if !ENVIRO_HDRP || !ENVIRO_URP
                        AddCameraComponents();
                #endif
                    }
                }
            }
        }

        private void SetSRPKeywords ()
        {
            #if ENVIRO_HDRP
            Shader.EnableKeyword("ENVIROHDRP");
            Shader.DisableKeyword("ENVIROURP");
            #elif ENVIRO_URP
            Shader.EnableKeyword("ENVIROURP");
            Shader.DisableKeyword("ENVIROHDRP");
            #else
            Shader.DisableKeyword("ENVIROURP");
            Shader.DisableKeyword("ENVIROHDRP");
            #endif
        }

        //Saves time and weather conditions
        public void Save()
        {
            if(Time != null)
            {
                PlayerPrefs.SetFloat("Time_Hours", Time.GetTimeOfDay());
                PlayerPrefs.SetInt("Time_Days", Time.days);
                PlayerPrefs.SetInt("Time_Months", Time.months);
                PlayerPrefs.SetInt("Time_Years", Time.years);
            }

            if(Weather != null)
            {
                for (int i = 0; i < Weather.Settings.weatherTypes.Count; i++)
                {
                    if (Weather.Settings.weatherTypes[i] == Weather.targetWeatherType)
                        PlayerPrefs.SetInt("currentWeather", i);
                }
            }
        }

        //Loads time and weather conditions
        public void Load()
        {
            if(Time != null)
            {
                if (PlayerPrefs.HasKey("Time_Hours"))
                    Time.SetTimeOfDay(PlayerPrefs.GetFloat("Time_Hours"));
                if (PlayerPrefs.HasKey("Time_Days"))
                    Time.days = PlayerPrefs.GetInt("Time_Days");
                if (PlayerPrefs.HasKey("Time_Months"))
                    Time.months = PlayerPrefs.GetInt("Time_Months");
                if (PlayerPrefs.HasKey("Time_Years"))
                    Time.years = PlayerPrefs.GetInt("Time_Years");
            }
            if(Weather != null)
            {
                if (PlayerPrefs.HasKey("currentWeather"))
                    Weather.ChangeWeatherInstant(PlayerPrefs.GetInt("currentWeather"));
            }
            
        }

        //Events
        private void EventInit()
        {
            if(Time != null)
            {
                OnHourPassed += () => HourPassedInvoke ();
                OnDayPassed += () => DayPassedInvoke ();
                OnYearPassed += () => YearPassedInvoke ();

                OnNightTime += () => NightTimeInvoke ();
                OnDayTime += () => DayTimeInvoke ();
            }
 
            if(Weather != null)
            { 
               OnWeatherChanged += (EnviroWeatherType type) =>  WeatherChangedInvoke ();
               OnZoneWeatherChanged += (EnviroWeatherType type, EnviroZone zone) =>  ZoneChangedInvoke ();
            }

            if(Environment != null)
            {
               OnSeasonChanged += (EnviroEnvironment.Seasons season) => SeasonsChangedInvoke ();
            } 
        }

        //Updates manager variables.
        private void UpdateManager ()
        {
            if(Application.isPlaying)
               CheckCameraSetup ();

            if(solarTime > dayNightSwitch)
            {
                if(isNight == true)
                    NotifyIsDay();

                isNight = false;
            }
            else
            {
                if(isNight == false)
                    NotifyIsNight();

                isNight = true;
            }

            //Effect Removal Zones:
            if(Fog != null || Effects != null)
            { 
                CreateZoneBuffers();
                SetupZoneBuffers();
            } 
        }
    }
}
