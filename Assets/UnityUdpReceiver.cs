using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class UnityUdpReceiver : MonoBehaviour
{
    Thread receiveThread;
    UdpClient client;
    
    [Header("Network Settings")]
    public int port = 9000;
    
    [Header("Debug Info")]
    public string lastReceived = "";

    void Start()
    {
        // Start a background thread to listen for Python packets
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log("Unity UDP Receiver: Listening on Port " + port);
    }

    private void ReceiveData()
    {
        client = new UdpClient(port);
        while (true)
        {
            try
            {
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = client.Receive(ref anyIP);
                string text = Encoding.UTF8.GetString(data);
                
                // Update the string so the Update() loop can see it
                lastReceived = text; 
            }
            catch (System.Exception e) { 
                Debug.LogWarning("UDP Error: " + e.Message); 
            }
        }
    }

    void Update()
    {
        if (!string.IsNullOrEmpty(lastReceived) && lastReceived != "--")
        {
            Debug.Log("<color=green>AI HEARD: </color>" + lastReceived);
            
            // NEXT STEP: Call your lighting function here
            // LightFretboard(lastReceived);
        }
    }

    void OnApplicationQuit()
    {
        // Cleanup when the game stops
        if (receiveThread != null) receiveThread.Abort();
        if (client != null) client.Close();
    }
}