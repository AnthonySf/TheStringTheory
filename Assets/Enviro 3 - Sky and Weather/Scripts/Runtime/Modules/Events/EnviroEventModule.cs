using System.Collections;
using System.Collections.Generic; 
using UnityEngine;
using System;

namespace Enviro
{


    [Serializable]
    public class EnviroEvents
    {
        [Serializable]
        public class EnviroActionEvent : UnityEngine.Events.UnityEvent
        {
        
        }

        public EnviroActionEvent onHourPassedActions = new EnviroActionEvent();
        public EnviroActionEvent onDayPassedActions = new EnviroActionEvent();
        public EnviroActionEvent onYearPassedActions = new EnviroActionEvent();
        public EnviroActionEvent onWeatherChangedActions = new EnviroActionEvent();
        public EnviroActionEvent onSeasonChangedActions = new EnviroActionEvent();
        public EnviroActionEvent onNightActions = new EnviroActionEvent();
        public EnviroActionEvent onDayActions = new EnviroActionEvent();
        public EnviroActionEvent onZoneChangedActions = new EnviroActionEvent();
    } 
}