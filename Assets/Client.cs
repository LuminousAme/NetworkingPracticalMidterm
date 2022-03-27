using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System;
using System.Linq;

public class Client : MonoBehaviour
{
    //singleton
    public static Client instance;

    public string ipAddress = "127.0.0.1";
    bool isStarted = false;

    private Socket TcpClient;
    private Socket UdpClient;

    private IPEndPoint TcpRemoteEP;
    private IPEndPoint UdpRemoteEP;
    private EndPoint UdpAbstractRemoteEP;

    private byte[] sendBuffer = new byte[512];
    private byte[] recieveBuffer = new byte[512];
    private int clientId; //asigned by the server
    public int GetClientId() => clientId;

    private float timeBetweenConnectionChecks = 1f, elapsedTime = 0f;

    public static Action onConnect;
    public static Action onDisconnect;

    bool SendUdpPackets = false;

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
                if(!IsConnected(TcpClient))
                {
                    //if the connection has been lost, release the socket and set it to not started   
                    Disconnect();
                    return;
                }
            }
            //tcp
            try
            {
                int recv = TcpClient.Receive(recieveBuffer);
                string data = Encoding.ASCII.GetString(recieveBuffer, 0, recv);
                string[] splitData = data.Split('$');

                if (splitData.Length == 4 && splitData[0] == "1" && splitData[2] == "msg")
                {
                    string sender = (splitData[1] == "2") ? "Server" : "Other Player:";
                    string msg = splitData[3];

                    //send the message to the chat, there should be a function in a chat manager to handle this
                }

                if (data == "Hey this is some data to continually send via tcp so that it doesn't go offline") SendUdpPackets = false;
                else if (data == "GoAheadAndPlay!") SendUdpPackets = true;
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode != SocketError.WouldBlock) Debug.Log(e.ToString());
            }

            //udp
            try
            {
                int recv = UdpClient.ReceiveFrom(recieveBuffer, ref UdpAbstractRemoteEP);

                if(recv > 0)
                {
                    string data = Encoding.ASCII.GetString(recieveBuffer, 0, recv);
                    string[] splitData = data.Split('$');

                    Debug.Log("ID: " + splitData[0]);

                    if (splitData[1] == "0")
                    {
                        int targetId = int.Parse(splitData[0]);

                        const int size = sizeof(float) * 3;
                        byte[] temp = new byte[size];
                        Buffer.BlockCopy(recieveBuffer, recv - size, temp, 0, size);

                        float[] floatarr = new float[3];
                        if (temp.Length == size)
                        {
                            Buffer.BlockCopy(temp, 0, floatarr, 0, temp.Length);

                            Vector3 newPos = new Vector3(floatarr[0], floatarr[1], floatarr[2]);

                            cube targetCube = FindObjectsOfType<cube>().ToList().Find(c => c.GetCubeId() == int.Parse(splitData[0]));
                            if (targetCube != null) targetCube.SetPosition(newPos);
                        }
                    }
                }
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode != SocketError.WouldBlock) Debug.Log(e.ToString());
            }
        }
    }

    public void StartClient()
    {
        IPAddress serverIP = IPAddress.Parse(ipAddress);
        TcpRemoteEP = new IPEndPoint(serverIP, 11111);
        TcpClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        //every second check the client is still active, if it fails check again in half a second, if both fail then register the socket as disconnected
        SetKeepAliveValues(TcpClient, 1000, 500); 
        //it needs to be blocking when first connecting or it might not connect properly, once the connection is established it will be made non-blocking
        TcpClient.Blocking = true;

        SendUdpPackets = false;

        //attempt a connection
        try
        {
            IPHostEntry hostInfo = Dns.GetHostEntry(Dns.GetHostName());

            //acutally connect
            TcpClient.Connect(TcpRemoteEP);

            //receiving the id
            int recv = TcpClient.Receive(recieveBuffer);
            string fromServer = Encoding.ASCII.GetString(recieveBuffer, 0, recv);
            string[] splitData = fromServer.Split('$');
            clientId = int.Parse(splitData[1]);
            Debug.Log(clientId);

            //once the connection has been complete we now want the socket to be nonblocking
            TcpClient.Blocking = false;

            UdpRemoteEP = new IPEndPoint(serverIP, 11112);
            UdpAbstractRemoteEP = (EndPoint)UdpRemoteEP;

            UdpClient = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            UdpClient.Blocking = true;

            sendBuffer = Encoding.ASCII.GetBytes("Hello! I'm here to join the server!");
            UdpClient.SendTo(sendBuffer, UdpRemoteEP);

            /*
            int rec = UdpClient.ReceiveFrom(recieveBuffer, ref UdpAbstractRemoteEP);
            Debug.Log("Recieved from server: " + Encoding.ASCII.GetString(recieveBuffer, 0, rec));
            */


            UdpClient.Blocking = false;

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
        if(IsConnected(TcpClient))
        {
            string toSend = "0$" + clientId.ToString() + "$quit";
            sendBuffer = Encoding.ASCII.GetBytes(toSend);
            TcpClient.Send(sendBuffer);
        }
        TcpClient.Shutdown(SocketShutdown.Both);
        TcpClient.Close();
        isStarted = false;
        onDisconnect?.Invoke();
    }

    public void SendMessageToOtherPlayers(string msg)
    {
        string toSend =  "1$" + clientId.ToString() + "$msg$" + msg; //will probably want to modify this to include an id of some kind
        sendBuffer = Encoding.ASCII.GetBytes(toSend);
        TcpClient.Send(sendBuffer);
    }

    public void SendPosUpdate(Vector3 pos)
    {
        //block copy the data to send to the server, so it can then send it to all of the other clients
        float[] floatarr = { pos.x, pos.y, pos.z};
        byte[] temp = new byte[sizeof(float) * floatarr.Length];
        Buffer.BlockCopy(floatarr, 0, temp, 0, sizeof(float) * floatarr.Length); //should be 15 floats

        string toSend = clientId.ToString() + "$0$";

        //this is jank but hopefully works
        byte[] temp2 = Encoding.ASCII.GetBytes(toSend);
        byte[] temp3 = new byte[temp.Length + temp2.Length];
        Array.Copy(temp2, temp3, temp2.Length);
        Array.Copy(temp, 0, temp3, temp2.Length, temp.Length);
        sendBuffer = temp3;
        if(SendUdpPackets) UdpClient.SendTo(sendBuffer, UdpRemoteEP);
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
