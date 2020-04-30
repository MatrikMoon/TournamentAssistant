using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace BattleSaberShared.Sockets
{
    public class ConnectedClient
    {
        public string guid;
        public Socket workSocket = null;
        public const int BufferSize = 1024;
        public byte[] buffer = new byte[BufferSize];
        public List<byte> accumulatedBytes = new List<byte>();
    }

    public class Server
    {
        public event Action<ConnectedClient, Packet> PacketRecieved;
        public event Action<ConnectedClient> ClientConnected;
        public event Action<ConnectedClient> ClientDisconnected;

        public bool Enabled { get; set; } = true;

        private List<ConnectedClient> clients = new List<ConnectedClient>();
        private Socket server;
        private int port;

        private static ManualResetEvent accpeting = new ManualResetEvent(false);

        public Server(int port)
        {
            this.port = port;
        }

        public void Start()
        {
            IPAddress ipAddress = IPAddress.Any;
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

            server = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            server.Bind(localEndPoint);
            server.Listen(100);

            while (Enabled)
            {
                // Set the event to nonsignaled state.  
                accpeting.Reset();

                // Start an asynchronous socket to listen for connections.  
                Logger.Debug("Waiting for a connection...");
                server.BeginAccept(new AsyncCallback(AcceptCallback), server);

                // Wait until a connection is made before continuing.  
                accpeting.WaitOne();
            }
        }

        public void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.  
            accpeting.Set();

            try
            {
                Socket listener = (Socket)ar.AsyncState;
                Socket handler = listener.EndAccept(ar);

                ConnectedClient connectedClient = new ConnectedClient();
                connectedClient.workSocket = handler;
                connectedClient.guid = Guid.NewGuid().ToString();

                lock (clients)
                {
                    clients.Add(connectedClient);
                }

                ClientConnected?.Invoke(connectedClient);

                handler.BeginReceive(connectedClient.buffer, 0, ConnectedClient.BufferSize, 0, new AsyncCallback(ReadCallback), connectedClient);
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
            }
        }

        public void ReadCallback(IAsyncResult ar)
        {
            ConnectedClient player = (ConnectedClient)ar.AsyncState;

            try
            {
                Socket handler = player.workSocket;

                // Read data from the client socket.   
                int bytesRead = handler.EndReceive(ar);

                //Logger.Debug($"READ {bytesRead} BYTES");

                if (bytesRead > 0)
                {
                    var currentBytes = new byte[bytesRead];
                    Buffer.BlockCopy(player.buffer, 0, currentBytes, 0, bytesRead);

                    player.accumulatedBytes.AddRange(currentBytes);
                    if (player.accumulatedBytes.Count >= Packet.packetHeaderSize)
                    {
                        //If we're not at the start of a packet, increment our position until we are, or we run out of bytes
                        var accumulatedBytes = player.accumulatedBytes.ToArray();
                        while (!Packet.StreamIsAtPacket(accumulatedBytes) && accumulatedBytes.Length >= Packet.packetHeaderSize)
                        {
                            player.accumulatedBytes.RemoveAt(0);
                            accumulatedBytes = player.accumulatedBytes.ToArray();
                        }

                        if (Packet.PotentiallyValidPacket(accumulatedBytes))
                        {
                            var readPacket = Packet.FromBytes(accumulatedBytes);
                            PacketRecieved?.Invoke(player, readPacket);

                            //Remove the bytes which we've already used from the accumulated List
                            player.accumulatedBytes.RemoveRange(0, readPacket.Size);
                        }
                    }

                    // Not all data received. Get more.
                    handler.BeginReceive(player.buffer, 0, ConnectedClient.BufferSize, 0, new AsyncCallback(ReadCallback), player);
                }
                else
                {
                    //Reading zero bytes is a sign of disconnect
                    ClientDisconnected_Internal(player);
                }
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
                ClientDisconnected_Internal(player);
            }
        }

        private void ClientDisconnected_Internal(ConnectedClient player)
        {
            lock (clients)
            {
                clients.Remove(player);
            }
            ClientDisconnected?.Invoke(player);
        }

        public void Send(string guid, byte[] data) => Send(new string[] { guid }, data);

        public void Send(string[] guids, byte[] data)
        {
            try
            {
                lock (clients)
                {
                    foreach (var connectedClient in clients.Where(x => guids.Contains(x.guid))) Send(connectedClient, data);
                }
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
            }
        }

        private void Send(ConnectedClient connectedClient, byte[] data)
        {
            try
            {
                connectedClient.workSocket.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), connectedClient);
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
                ClientDisconnected_Internal(connectedClient);
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            ConnectedClient connectedClient = (ConnectedClient)ar.AsyncState;

            try
            {
                // Retrieve the socket from the state object.  
                var handler = connectedClient.workSocket;

                // Complete sending the data to the remote device.  
                int bytesSent = handler.EndSend(ar);
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
                ClientDisconnected_Internal(connectedClient);
            }
        }

        public void Shutdown()
        {
            Enabled = false;
            if (server.Connected) server.Shutdown(SocketShutdown.Both);
            server.Close();
        }
    }
}
