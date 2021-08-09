using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TournamentAssistantShared.Sockets
{
    public class ConnectedClient
    {
        public Guid id;
        public Socket workSocket = null;
        public const int BufferSize = 8192;
        public byte[] buffer = new byte[BufferSize];
        public List<byte> accumulatedBytes = new List<byte>();
    }

    public class Server
    {
        public event Action<ConnectedClient, Packet> PacketReceived;
        public event Action<ConnectedClient> ClientConnected;
        public event Action<ConnectedClient> ClientDisconnected;

        public bool Enabled { get; set; } = true;

        private List<ConnectedClient> clients = new List<ConnectedClient>();
        private Socket ipv4Server;
        private Socket ipv6Server;
        private int port;

        private static ManualResetEvent acceptingIPV4 = new ManualResetEvent(false);
        private static ManualResetEvent acceptingIPV6 = new ManualResetEvent(false);

        //Does not block
        public void Start()
        {

            IPAddress ipv4Address = IPAddress.Any;
            IPAddress ipv6Address = IPAddress.IPv6Any;
            IPEndPoint localIPV4EndPoint = new IPEndPoint(ipv4Address, port);
            IPEndPoint localIPV6EndPoint = new IPEndPoint(ipv6Address, port);

            ipv4Server = new Socket(ipv4Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            ipv6Server = new Socket(ipv6Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            ipv4Server.Bind(localIPV4EndPoint);
            ipv6Server.Bind(localIPV6EndPoint);

            ipv4Server.Listen(100);
            ipv6Server.Listen(100);

            Action ipv4Accept = () =>
            {
                while (Enabled)
                {
                    // Set the event to nonsignaled state.  
                    acceptingIPV4.Reset();

                    // Start an asynchronous socket to listen for connections.  
                    Logger.Debug($"Waiting for an IPV4 connection on {ipv4Address}:{port} ...");
                    ipv4Server.BeginAccept(new AsyncCallback(IPV4AcceptCallback), ipv4Server);

                    // Wait until a connection is made before continuing.  
                    acceptingIPV4.WaitOne();
                }
            };

            Action ipv6Accept = () =>
            {
                while (Enabled)
                {
                    // Set the event to nonsignaled state.  
                    acceptingIPV6.Reset();

                    // Start an asynchronous socket to listen for connections.  
                    Logger.Debug($"Waiting for an IPV6 connection on {ipv6Address}:{port} ...");
                    ipv6Server.BeginAccept(new AsyncCallback(IPV6AcceptCallback), ipv6Server);

                    // Wait until a connection is made before continuing.  
                    acceptingIPV6.WaitOne();
                }
            };

            Task.Run(ipv4Accept);
            Task.Run(ipv6Accept);
        }

        public Server(int port)
        {
            this.port = port;
        }

        private void IPV4AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.  
            acceptingIPV4.Set();
            AcceptCallback(ar);
        }

        private void IPV6AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.  
            acceptingIPV6.Set();
            AcceptCallback(ar);
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                Socket listener = (Socket)ar.AsyncState;
                Socket handler = listener.EndAccept(ar);

                ConnectedClient connectedClient = new ConnectedClient();
                connectedClient.workSocket = handler;
                connectedClient.id = Guid.NewGuid();

                lock (clients)
                {
                    clients.Add(connectedClient);
                }

                ClientConnected?.Invoke(connectedClient);

                handler.BeginReceive(connectedClient.buffer, 0, ConnectedClient.BufferSize, 0, new AsyncCallback(ReadCallback), connectedClient);
            }
            catch (ObjectDisposedException)
            {
                Logger.Debug("ObjectDisposedException in Server AcceptCallback. This is expected during server shutdowns, such as during address verification");
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
            }
        }

        private void ReadCallback(IAsyncResult ar)
        {
            ConnectedClient player = (ConnectedClient)ar.AsyncState;

            try
            {
                Socket handler = player.workSocket;

                // Read data from the client socket.   
                int bytesRead = handler.EndReceive(ar);
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

                        while ((accumulatedBytes.Length >= Packet.packetHeaderSize && Packet.PotentiallyValidPacket(accumulatedBytes)))
                        {
                            Packet readPacket = null;
                            try
                            {
                                readPacket = Packet.FromBytes(accumulatedBytes);
                                PacketReceived?.Invoke(player, readPacket);
                            }
                            catch (Exception e)
                            {
                                Logger.Error(e.Message);
                                Logger.Error(e.StackTrace);
                            }

                            //Remove the bytes which we've already used from the accumulated List
                            //If the packet failed to parse, skip the header so that the rest of the packet is consumed by the above vailidity check on the next run
                            player.accumulatedBytes.RemoveRange(0, readPacket?.Size ?? Packet.packetHeaderSize);
                            accumulatedBytes = player.accumulatedBytes.ToArray();
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

        public void Broadcast(byte[] data)
        {
            try
            {
                lock (clients)
                {
                    foreach (var connectedClient in clients) Send(connectedClient, data);
                }
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
            }
        }



        public void Send(Guid id, byte[] data) => Send(new Guid[] { id }, data);

        public void Send(Guid[] ids, byte[] data)
        {
            try
            {
                lock (clients)
                {
                    foreach (var connectedClient in clients.Where(x => ids.Contains(x.id))) Send(connectedClient, data);
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
            if (ipv4Server.Connected) ipv4Server.Shutdown(SocketShutdown.Both);
            ipv4Server.Close();

            if (ipv6Server.Connected) ipv6Server.Shutdown(SocketShutdown.Both);
            ipv6Server.Close();
        }
    }
}
