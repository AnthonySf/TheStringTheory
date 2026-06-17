using UnityEngine;
using System.Collections;

#if ENVIRO_PHOTON_SUPPORT
using Photon.Pun;
#endif

namespace Enviro
{
	#if ENVIRO_PHOTON_SUPPORT
	[RequireComponent(typeof (PhotonView))]
	[AddComponentMenu("Enviro 3/Integrations/Photon Integration")]
	public class EnviroPhotonIntegration : MonoBehaviourPunCallbacks, IPunObservable
	{ 
	#else
	public class EnviroPhotonIntegration : MonoBehaviour
	{

	#endif
	#if ENVIRO_PHOTON_SUPPORT
		public ViewSynchronization synchronizationType = ViewSynchronization.Unreliable;
		public float updateSmoothing = 15f;
		private float networkHours;

		void Start () 
		{
			if(EnviroManager.instance != null && EnviroManager.instance.Time != null)
			   networkHours = EnviroManager.instance.Time.GetTimeOfDay();
			
			photonView.ObservedComponents[0] = this;
			photonView.Synchronization = synchronizationType;
		}

		public override void OnJoinedRoom()
		{
			if (PhotonNetwork.IsMasterClient) 
			{
				EnviroManager.instance.OnZoneWeatherChanged += (EnviroWeatherType type, EnviroZone zone) => {
					SendWeatherToClient (type, zone);
				};

				EnviroManager.instance.OnSeasonChanged += (EnviroEnvironment.Seasons season) => {
					SendSeasonToClient (season);
				};
			} 
			else 
			{
				if(EnviroManager.instance.Weather != null)
				   EnviroManager.instance.Weather.globalAutoWeatherChange = false;

				if(EnviroManager.instance.Time != null)
				   EnviroManager.instance.Time.Settings.simulate = false;

				if(EnviroManager.instance.Environment != null)
				   EnviroManager.instance.Environment.Settings.changeSeason = false;

				StartCoroutine (GetWeather ());
			}
		}

		IEnumerator GetWeather ()
		{
			yield return 0;
			photonView.RPC("GetWeatherAndSeason", RpcTarget.MasterClient);
		}

		public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
		{
			if(EnviroManager.instance.Time == null)
			   return;
 
			if (stream.IsWriting) 
			{
				stream.SendNext(EnviroManager.instance.Time.GetTimeOfDay());
				stream.SendNext(EnviroManager.instance.Time.days);
				stream.SendNext(EnviroManager.instance.Time.months);
				stream.SendNext(EnviroManager.instance.Time.years);
			} 
			else 
			{
				networkHours = (float) stream.ReceiveNext();
				EnviroManager.instance.Time.days = (int) stream.ReceiveNext();
				EnviroManager.instance.Time.months = (int) stream.ReceiveNext(); 
				EnviroManager.instance.Time.years = (int) stream.ReceiveNext();
			}
		} 


		void SendWeatherToClient (EnviroWeatherType type, EnviroZone zone)
		{
			int weatherID = 0;
			int zoneID = -1;

			for(int i = 0; i < EnviroManager.instance.Weather.Settings.weatherTypes.Count; i++)
			{
				if (EnviroManager.instance.Weather.Settings.weatherTypes [i] == type)
					weatherID = i;
			}

			for (int i = 0; i < EnviroManager.instance.zones.Count; i++) 
			{
				if (EnviroManager.instance.zones [i] == zone)
					zoneID = i;
			}
	
			photonView.RPC("SendWeatherUpdate", RpcTarget.OthersBuffered,weatherID,zoneID);
		}

		void SendSeasonToClient (EnviroEnvironment.Seasons s)
		{
			photonView.RPC("SendSeasonUpdate",RpcTarget.OthersBuffered,(int)s);
		}

		[PunRPC]
		void GetWeatherAndSeason ()
		{
			if(EnviroManager.instance.Weather != null)
			{
				for (int i = 0; i < EnviroManager.instance.zones.Count; i++) 
				{
					SendWeatherToClient(EnviroManager.instance.zones[i].currentWeatherType, EnviroManager.instance.zones[i]);
				}

				SendWeatherToClient(EnviroManager.instance.Weather.targetWeatherType, null);
			}

			if(EnviroManager.instance.Environment != null)
			   SendSeasonToClient(EnviroManager.instance.Environment.Settings.season);
		}



		[PunRPC]
		void SendWeatherUpdate (int id, int zone) 
		{
			if(EnviroManager.instance.Weather != null)
			{
				if(zone == -1)
					EnviroManager.instance.Weather.ChangeWeather(id);
				else
					EnviroManager.instance.Weather.ChangeZoneWeather(id,zone);
			}
		}

		[PunRPC]
		void SendSeasonUpdate (int id) 
		{
			if(EnviroManager.instance.Environment != null)
			return;

			switch (id) 
			{
			case 0:
					EnviroManager.instance.Environment.Settings.season = EnviroEnvironment.Seasons.Spring;
			break;

			case 1:
					EnviroManager.instance.Environment.Settings.season = EnviroEnvironment.Seasons.Summer;
			break;

			case 2:
					EnviroManager.instance.Environment.Settings.season = EnviroEnvironment.Seasons.Autumn;
			break;

			case 3:
					EnviroManager.instance.Environment.Settings.season = EnviroEnvironment.Seasons.Winter;
			break;
			}
		}

		void Update ()
		{

			if (EnviroManager.instance == null || EnviroManager.instance.Time == null)
				return;

			if (!PhotonNetwork.IsMasterClient) 
			{
				if (networkHours < 0.5f && EnviroManager.instance.Time.GetTimeOfDay() > 23f)
					EnviroManager.instance.Time.SetTimeOfDay(networkHours);

				EnviroManager.instance.Time.SetTimeOfDay(Mathf.Lerp (EnviroManager.instance.Time.GetTimeOfDay(), (float)networkHours, Time.deltaTime * updateSmoothing));
			}

		}
	#endif
	}
}
