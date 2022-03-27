using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NetworkingPracticalMidtermServer
{
    static class MidtermServer
    {
        static byte[] recieveBuffer = new byte[1024];
        static int rec = 0;
        static byte[] sendBuffer = new byte[1024];
        static Socket TcpServer;
        static Socket UDPServer;
        static EndPoint remoteClient;

        static Socket client0;
        static Socket client1;
        static int nextClient = 0;

        static EndPoint client0EP;
        static EndPoint client1EP;

        //check if a socket is connected
        static bool IsConnected(Socket s)
        {
            //taken from here https://stackoverflow.com/questions/2661764/how-to-check-if-a-socket-is-connected-disconnected-in-c/2661876#2661876
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
        static void SetKeepAliveValues(Socket s, int keepAliveTime, int keepAliveInterval)
        {
            //based on code from here https://stackoverflow.com/questions/2661764/how-to-check-if-a-socket-is-connected-disconnected-in-c/2661876#2661876
            int size = sizeof(uint);
            byte[] values = new byte[size * 3];

            BitConverter.GetBytes((uint)(1)).CopyTo(values, 0);
            BitConverter.GetBytes((uint)keepAliveTime).CopyTo(values, size);
            BitConverter.GetBytes((uint)keepAliveInterval).CopyTo(values, size * 2);

            byte[] outvalues = BitConverter.GetBytes(0);

            s.IOControl(IOControlCode.KeepAliveValues, values, outvalues);
        }

        static void CheckForTcpForwarding(Socket sender, Socket reciever)
        {
            //only try if both tcp sockets are connected
            if((sender != null && IsConnected(sender)) && (reciever != null && IsConnected(reciever)))
            {
                try
                {
                    int rec = sender.Receive(recieveBuffer);
                    if (rec > 0)
                    {
                        string data = Encoding.ASCII.GetString(recieveBuffer, 0, rec);
                        string[] splitData = data.Split('$');

                        if (splitData[0] != "0" && splitData[2] != "quit")
                        {
                            byte[] forwardBuffer = new byte[rec];
                            Buffer.BlockCopy(recieveBuffer, 0, forwardBuffer, 0, rec);
                            reciever.Send(forwardBuffer);
                        }
                        else
                        {
                            sender.Shutdown(SocketShutdown.Both);
                            sender.Close();
                            //send a message to the reciever's chat telling them the sender has voluntarily disconnected
                            sendBuffer = Encoding.ASCII.GetBytes("1$2$msg$The other client has voluntarily disconnected");
                            reciever.Send(sendBuffer);
                        }
                    }
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode != SocketError.WouldBlock) Console.WriteLine(e.ToString());
                }
            }
        }

        static void CheckForUDPForwarding()
        {
            //only bother if there's already a tcp connection
            if ((client0 != null && IsConnected(client0)) && (client1 != null && IsConnected(client1))) {
                try
                {
                    int rec = UDPServer.ReceiveFrom(recieveBuffer, ref remoteClient);
                    if (rec > 0)
                    {
                        string data = Encoding.ASCII.GetString(recieveBuffer, 0, rec);
                        string[] splitData = data.Split('$');

                        byte[] forwardBuffer = new byte[rec];
                        Buffer.BlockCopy(recieveBuffer, 0, forwardBuffer, 0, rec);

                        //if client 0 sent the message, forward it to client 1
                        if(splitData[0] == "0")
                        {
                            UDPServer.SendTo(forwardBuffer, client1EP);
                        }
                        //if client 1 sent the message, forward it to client 0
                        else
                        {
                            UDPServer.SendTo(forwardBuffer, client0EP);
                        }
                    }
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode != SocketError.WouldBlock) Console.WriteLine(e.ToString());
                }
            }
        }

        static void Main(string[] args)
        {
            IPHostEntry hostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ip = null;

            for (int i = 0; i < hostInfo.AddressList.Length; i++)
            {
                //check for IPv4 address
                if (hostInfo.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                    ip = hostInfo.AddressList[i];
            }

            string input;
            Console.WriteLine("We've automatically detected the ip address to be {0}, do you want to use this ip? ", ip.ToString());
            Console.WriteLine("Type 'y' for yes, or 'n' to use the local host instead ");

            do
            {
                input = Console.ReadLine();
                if(input != "y" && input != "n")
                {
                    Console.WriteLine("please only input 'y' or 'n' ");
                }
            }
            while (input != "y" && input != "n");

            if (input == "n") ip = IPAddress.Parse("127.0.0.1");

            Console.WriteLine("Server IP Address: {0}", ip.ToString());

            //setup tcp server
            try
            {
                IPEndPoint localEP = new IPEndPoint(ip, 11111);
                TcpServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                TcpServer.Blocking = false;

                TcpServer.Bind(localEP);
                TcpServer.Listen(10);
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode != SocketError.WouldBlock) Console.WriteLine(e.ToString());
            }

            //setup udp server
            try
            {
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                remoteClient = (EndPoint)remoteEP;

                IPEndPoint localEP = new IPEndPoint(ip, 11112);
                UDPServer = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                UDPServer.Blocking = false;

                UDPServer.Bind(localEP);
                //no need to listen since UDP is connectionless
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode != SocketError.WouldBlock) Console.WriteLine(e.ToString());
            }

            while(true)
            {
                Thread.Sleep(50);

                //accept new connections
                try
                {
                    TcpServer.Listen(10);
                    Socket newConnection = TcpServer.Accept();
                    int thisClient;
                    if (nextClient == 0)
                    {
                        client0 = newConnection;
                        nextClient++;
                        thisClient = 0;
                    }
                    else
                    {
                        client1 = newConnection;
                        nextClient = 0;
                        thisClient = 1;
                    }

                    //these are values I personally find work well , checking for the connection every 1000ms, and allowing 500ms to try again before true disconnect - Ame
                    SetKeepAliveValues(newConnection, 1000, 500);
                    IPEndPoint temp = (IPEndPoint)newConnection.RemoteEndPoint;
                    Console.WriteLine("Connected: " + temp.Address.ToString());

                    sendBuffer = Encoding.ASCII.GetBytes("Hello Welcome to the server your id is in the next split$" + thisClient.ToString()); //either 0 or 1
                    Console.WriteLine("Before TCP Send");
                    newConnection.Send(sendBuffer);
                    Console.WriteLine("After TCP Send");


                    UDPServer.Blocking = true;
                    Console.WriteLine("Before UDP Recieve");
                    int rec = UDPServer.ReceiveFrom(recieveBuffer, ref remoteClient);
                    Console.WriteLine("Recieved from client {0}: {1}", thisClient, Encoding.ASCII.GetString(recieveBuffer, 0, rec));

                    if (thisClient == 0)
                    {
                        client0EP = remoteClient;
                        Console.WriteLine("UDP transfer with client 0 setup");
                    }
                    else
                    {
                        client1EP = remoteClient;
                        Console.WriteLine("UDP transfer with client 1 setup");
                    }
                    /*
                    sendBuffer = Encoding.ASCII.GetBytes("Thank you for joining!");
                    UDPServer.SendTo(sendBuffer, remoteClient);
                    Console.WriteLine("Aftter UDP Send");
                    */
                    UDPServer.Blocking = false;
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode != SocketError.WouldBlock) Console.WriteLine(e.ToString());
                }

                CheckForTcpForwarding(client0, client1);
                CheckForTcpForwarding(client1, client0);

                for (int i = 0; i < 2; i++) CheckForUDPForwarding(); //run for the number of expected connected clients
            }
        }
    }
}
