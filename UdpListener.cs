﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Microsoft.Samples.Kinect.BodyBasics
{
    public class UdpListener
    {
        //public List<CloudMessage> PendingRequests;

        private UdpClient _udpClient = null;
        private IPEndPoint _anyIP;

        private int _port;
        public uint MessageCount;
        int _limit; // TMA: To keep track of the number of bytes sent.

        byte[] _finalBytes; // TMA: To point to the bytes that will be send.

        public List<CloudMessage> PendingRequests;
        public List<TcpSender> Clients;

        public int Port
        {
            get { return _port; }
            set { _port = value; }
        }

        
        public UdpListener(int port)
        {
            _port = port;
            PendingRequests = new List<CloudMessage>();
            Clients = new List<TcpSender>();
        }

        public void UdpRestart()
        {
            if (_udpClient != null)
            {
                _udpClient.Close();
            }

            PendingRequests = new List<CloudMessage>();

            _anyIP = new IPEndPoint(IPAddress.Any, _port);

            _udpClient = new UdpClient(_anyIP);

            _udpClient.BeginReceive(new AsyncCallback(this.ReceiveCallback), null);

            Console.WriteLine("[UDPListener] Receiving in port: " + _port);
        }

        public void ReceiveCallback(IAsyncResult ar)
        {
            Console.WriteLine("[UDPListener] Received request: " + _port);
            try { 
                Byte[] receiveBytes = _udpClient.EndReceive(ar, ref _anyIP);
                string request = Encoding.ASCII.GetString(receiveBytes);
               
                string[] msg = request.Split(MessageSeparators.L0);
                if (msg[0] == "CloudMessage")
                {
                    PendingRequests.Add(new CloudMessage(msg[1]));
                }
                _udpClient.BeginReceive(new AsyncCallback(this.ReceiveCallback), null);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error on callback" + e.Message);
            }
        }

        public void ProcessRequests(List<byte> byteList)
        {
            List<CloudMessage> todelete = new List<CloudMessage>();
            for (int i = 0; i < PendingRequests.Count; i++)
            {
                CloudMessage cm = PendingRequests[i];
                //Stop
                if (cm.Mode == 2)
                {
                    foreach (CloudMessage cm2 in PendingRequests)
                    {
                        if (cm.ReplyIpAddress.ToString() == cm2.ReplyIpAddress.ToString() &&
                            cm.Port == cm2.Port)
                            todelete.Add(cm2);
                    }
                    continue;
                }
                //Failsafe
                if (todelete.Contains(cm))
                {
                    continue;
                }
               if(cm.Mode == 1)
                {
                    TcpSender newclient = new TcpSender();
                    newclient.connect(cm.ReplyIpAddress.ToString(), cm.Port);
                    Clients.Add(newclient);
                    todelete.Add(cm);
                }

                if(cm.Mode == 0) {
                    // TMA: Get the bytes from the ArrayList
                    byte[] points_bytes = byteList.ToArray();
                    // This is the heading for every package.
                    string msg = "CloudMessage" + MessageSeparators.L0 + Environment.MachineName + MessageSeparators.L1 + MessageCount + MessageSeparators.L1; // String to tag the sensor
                                                                                                                                                               // Get the heading bytes.
                    int remainder = 4 - (msg.Length % 4);
                    while (remainder-- > 0) msg = 'C' + msg;

                    byte[] msgBytes = Encoding.ASCII.GetBytes(msg); // Convert to bytes

                    IPEndPoint ep = new IPEndPoint(cm.ReplyIpAddress, cm.Port);
                    for (_limit = 0; _limit < points_bytes.Length; _limit += 8000) // Each packet has 500 points (16 * 500 = 8000 bytes)
                    {
                        if (_limit + 8000 > points_bytes.Length) // If there are less points than 500
                        {
                            _finalBytes = new byte[msgBytes.Length + points_bytes.Length - _limit];
                            Array.Copy(msgBytes, 0, _finalBytes, 0, msgBytes.Length);
                            Array.Copy(points_bytes, _limit, _finalBytes, msgBytes.Length, points_bytes.Length - _limit);
                        }
                        else // If there are more or 500 points to send
                        {
                            _finalBytes = new byte[msgBytes.Length + 8000];
                            Array.Copy(msgBytes, 0, _finalBytes, 0, msgBytes.Length);
                            Array.Copy(points_bytes, _limit, _finalBytes, msgBytes.Length, 8000);
                        }
                        
                        try
                        {
                            System.Threading.Thread.Sleep(2);
                            _udpClient.Send(_finalBytes, _finalBytes.Length, ep); // Send the bytes

                        }
                        catch (Exception e)
                        {
                            var exceptionMessage = "Error sending data to " + cm.ReplyIpAddress.ToString() + " " + e.Message;
                            Console.WriteLine(exceptionMessage);
                        }
                    }

                    msgBytes = Encoding.ASCII.GetBytes(msg); // Convert to bytes
                    System.Threading.Thread.Sleep(2);
                    _udpClient.Send(msgBytes, msgBytes.Length, ep); // Send the bytes
                    todelete.Add(cm);
                }
            }

            foreach (CloudMessage cm in todelete)
            {
                PendingRequests.Remove(cm);
            }
            MessageCount++;
        }

        public void OnApplicationQuit()
        {
            if (_udpClient != null) _udpClient.Close();
        }

    }
}