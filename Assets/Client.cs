using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System;

public class Client : MonoBehaviour
{
    //singleton
    public static Client instance;

    public string ipAddress = "127.0.0.1";
    bool isStarted = false;

    private Socket client;
    private IPEndPoint localEP;
    private IPAddress thisPlayerIp;

    private byte[] sendBuffer = new byte[512];
    private byte[] recieveBuffer = new byte[512];
    private int clientId; //asigned by the server

    private float timeBetweenConnectionChecks = 1f, elapsedTime = 0f;

    public static Action onConnect;
    public static Action onDisconnect;

    /*
     * Comm Codes
     * Seperate using $
     * Messages starting with 0 are commands to the server
     * Messages starting with 1 are things the server should be forwarding to other clients
     * Next part of the message should be the clientId so they can be identified cleanly
     * Following that the type of information being communicated
     * Finally the acutal content of that data
     */

    // Start is called before the first frame update
    void Start()
    {
        if(instance != null)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (isStarted)
        {
            //check the connection at a regular interval
            elapsedTime += Time.deltaTime;
            if (elapsedTime >= timeBetweenConnectionChecks)
            {
                if(!IsConnected(client))
                {
                    //if the connection has been lost, release the socket and set it to not started   
                    Disconnect();
                    return;
                }
            }
            //all other sending and recieving should probably happen here
        }
    }

    private void StartClient()
    {
        IPAddress serverIP = IPAddress.Parse(ipAddress);
        localEP = new IPEndPoint(serverIP, 11111);
        client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        //every second check the client is still active, if it fails check again in half a second, if both fail then register the socket as disconnected
        SetKeepAliveValues(client, 1000, 500); 
        //it needs to be blocking when first connecting or it might not connect properly, once the connection is established it will be made non-blocking
        client.Blocking = true;

        //attempt a connection
        try
        {
            IPHostEntry hostInfo = Dns.GetHostEntry(Dns.GetHostName());
            thisPlayerIp = null;

            for (int i = 0; i < hostInfo.AddressList.Length; i++)
            {
                //check for IPv4 address
                if (hostInfo.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                    thisPlayerIp = hostInfo.AddressList[i];
            }

            //acutally connect
            client.Connect(localEP);

            //once the connection has been complete we now want the socket to be nonblocking
            client.Blocking = false;
            isStarted = true;
            onConnect?.Invoke();
        }
        catch (SocketException e)
        {
            if (e.SocketErrorCode != SocketError.WouldBlock) Debug.Log("SocketException: " + e.ToString());
        }
    }

    //check if a socket is connected
    //taken from here https://stackoverflow.com/questions/2661764/how-to-check-if-a-socket-is-connected-disconnected-in-c/2661876#2661876
    private bool IsConnected(Socket s)
    {
        try
        {
            return !((s.Poll(1000, SelectMode.SelectRead) && (s.Available == 0)) || !s.Connected);
        }
        catch (SocketException e)
        {
            if (e.SocketErrorCode == SocketError.WouldBlock) return true;
            else return false;
        }
    }

    //control how long a socket can stay alive
    //based on code from here https://stackoverflow.com/questions/2661764/how-to-check-if-a-socket-is-connected-disconnected-in-c/2661876#2661876
    private void SetKeepAliveValues(Socket s, int keepAliveTime, int keepAliveInterval)
    {
        int size = sizeof(uint);
        byte[] values = new byte[size * 3];

        BitConverter.GetBytes((uint)(1)).CopyTo(values, 0);
        BitConverter.GetBytes((uint)keepAliveTime).CopyTo(values, size);
        BitConverter.GetBytes((uint)keepAliveInterval).CopyTo(values, size * 2);

        byte[] outvalues = BitConverter.GetBytes(0);

        s.IOControl(IOControlCode.KeepAliveValues, values, outvalues);
    }

    public void Disconnect()
    {
        if(IsConnected(client))
        {
            string toSend = "0$" + clientId.ToString() + "$quit";
            sendBuffer = Encoding.ASCII.GetBytes(toSend);
            client.Send(sendBuffer);
        }
        client.Shutdown(SocketShutdown.Both);
        client.Close();
        isStarted = false;
        onDisconnect?.Invoke();
    }

    public void SendMessageToOtherPlayers(string msg)
    {
        string toSend =  "1$" + clientId.ToString() + "$msg$" + msg; //will probably want to modify this to include an id of some kind
        sendBuffer = Encoding.ASCII.GetBytes(toSend);
        client.Send(sendBuffer);
    }

    //make sure there is a disconnect if the player exits the game
    private void OnDestroy()
    {
        if (isStarted)
        {
            Disconnect();
        }
    }
}
