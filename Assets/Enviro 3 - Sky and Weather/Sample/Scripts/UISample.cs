using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Enviro
{
    public class UISample : MonoBehaviour
    {
        [Header("Time")]
        public Slider hourSlider;
        public Text hourText;
        public Text dateText;
        [Header("Weather")]
        public Text currentWeatherText;
        [Header("Environment")]
        public Text seasonText;
        public Text temperatureText;
        public Text wetnessText;
        public Text snowText;
        [Header("Quality")]
        public Text currentQualityText;

        void Start()
        {
            
        }

        void Update()
        {
           
        }

        void LateUpdate()
        {
            if(EnviroManager.instance.Time != null)
            {
               //hourSlider.value = EnviroManager.instance.Time.GetTimeOfDay() / 24f;
               hourText.text = EnviroManager.instance.Time.GetTimeStringWithSeconds();
               dateText.text = string.Format("{0:00}/{1:00}/{2:0000}", EnviroManager.instance.Time.days, EnviroManager.instance.Time.months, EnviroManager.instance.Time.years);
            }

            if(EnviroManager.instance.Weather != null)
            {
               currentWeatherText.text = "Current Weather: " + EnviroManager.instance.Weather.targetWeatherType.name;
            }

            if(EnviroManager.instance.Environment != null)
            {
                temperatureText.text = "Temperature: " + string.Format("{0:0.0} °C", EnviroManager.instance.Environment.Settings.temperature);
                wetnessText.text = "Wetness: " + string.Format("{0:0.00}", EnviroManager.instance.Environment.Settings.wetness);
                snowText.text = "Snow: " + string.Format("{0:0.00}", EnviroManager.instance.Environment.Settings.snow);

                string sText = "";

                switch (EnviroManager.instance.Environment.Settings.season)
                {
                case EnviroEnvironment.Seasons.Spring:
                    sText = "Current Season: Spring";
                    break;
                case EnviroEnvironment.Seasons.Summer:
                    sText = "Current Season: Summer";
                    break;
                case EnviroEnvironment.Seasons.Autumn:
                    sText = "Current Season: Autumn";
                    break;
                case EnviroEnvironment.Seasons.Winter:
                    sText = "Current Season: Winter";
                    break;
                }
                seasonText.text = sText;
            }
                   
            if(EnviroManager.instance.Quality != null)
            {
               currentQualityText.text = "Current Quality: " + EnviroManager.instance.Quality.Settings.defaultQuality.name;
            }
        }

        public void ChangeHourSlider () 
        {
            if(EnviroManager.instance.Time == null)
               return;

            if (hourSlider.value < 0f)
                hourSlider.value = 0f;

            EnviroManager.instance.Time.SetTimeOfDay (hourSlider.value * 24f);
        }

        public void ChangeQuality(int q)
        {
            if(EnviroManager.instance.Quality != null)
            {
                if(EnviroManager.instance.Quality.Settings.Qualities.Count >= q)
                   EnviroManager.instance.Quality.Settings.defaultQuality = EnviroManager.instance.Quality.Settings.Qualities[q];
            }
        }

        public void ChangeWeather(int w)
        {
            if(EnviroManager.instance.Weather != null)
            {
                if(EnviroManager.instance.Weather.Settings.weatherTypes.Count >= w)
                   EnviroManager.instance.Weather.ChangeWeather(EnviroManager.instance.Weather.Settings.weatherTypes[w]);
            }
        }
        public void ChangeTimeSimulation(bool t)
        {
            if(EnviroManager.instance.Time != null)
            {
                EnviroManager.instance.Time.Settings.simulate = t;
            }
        }
    }
}
