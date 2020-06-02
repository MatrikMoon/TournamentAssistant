using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace TournamentAssistantShared.Sockets
{
    /*public class WatchingSocket : Socket
    {
        public WatchingSocket(SocketInformation socketInformation) : base(socketInformation) { }

        public WatchingSocket(SocketType socketType, ProtocolType protocolType) : base (socketType, protocolType) { }
        public WatchingSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType) : base(addressFamily, socketType, protocolType) { }

        protected new void Close()
        {
            base.Close();

            Logger.Error("Closing socket:");
            Logger.Error(Environment.StackTrace);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            Logger.Error("Disposing socket:");
            Logger.Error(Environment.StackTrace);
        }
    }*/

    public class ClientPlayer
    {
        public Socket socket = null;
        public const int BufferSize = 1024;
        public byte[] buffer = new byte[BufferSize];
        public List<byte> accumulatedBytes = new List<byte>();
    }

    public class Client
    {
        public event Action<Packet> PacketRecieved;
        public event Action ServerConnected;
        public event Action ServerFailedToConnect;
        public event Action ServerDisconnected;

        private int port;
        private string endpoint;
        private ClientPlayer player;

        public bool Connected
        {
            get
            {
                return player?.socket?.Connected ?? false;
            }
        }

        public Client(string endpoint, int port)
        {
            this.endpoint = endpoint;
            this.port = port;

            player = new ClientPlayer();
        }

        public void Start()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(endpoint);
            IPAddress ipAddress = ipHostInfo.AddressList[0];

            //IPAddress ipAddress = IPAddress.Loopback;
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

            player.socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            player.socket.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), player.socket);
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket client = (Socket)ar.AsyncState;

                // Complete the connection.  
                client.EndConnect(ar);

                //Signal Connection complete
                ServerConnected?.Invoke();

                // Begin receiving the data from the remote device.  
                client.BeginReceive(player.buffer, 0, ClientPlayer.BufferSize, 0, new AsyncCallback(ReadCallback), player);
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());

                ServerFailedToConnect?.Invoke();
            }
        }

        private void ReadCallback(IAsyncResult ar)
        {
            try
            {
                ClientPlayer player = (ClientPlayer)ar.AsyncState;
                Socket client = player.socket;

                // Read data from the remote device.  
                int bytesRead = client.EndReceive(ar);
                if (bytesRead > 0)
                {
                    var currentBytes = new byte[bytesRead];
                    Buffer.BlockCopy(player.buffer, 0, currentBytes, 0, bytesRead);

                    player.accumulatedBytes.AddRange(currentBytes);
                    if (player.accumulatedBytes.Count >= Packet.packetHeaderSize)
                    {
                        //If we're not at the start of a packet, increment our position until we are, or we run out of bytes
                        var accumulatedBytes = player.accumulatedBytes.ToArray();
                        while (accumulatedBytes.Length >= Packet.packetHeaderSize && !Packet.StreamIsAtPacket(accumulatedBytes))
                        {
                            player.accumulatedBytes.RemoveAt(0);
                            accumulatedBytes = player.accumulatedBytes.ToArray();
                        }

                        while (accumulatedBytes.Length >= Packet.packetHeaderSize && Packet.PotentiallyValidPacket(accumulatedBytes))
                        {
                            var readPacket = Packet.FromBytes(accumulatedBytes);
                            PacketRecieved?.Invoke(readPacket);

                            //Remove the bytes which we've already used from the accumulated List
                            player.accumulatedBytes.RemoveRange(0, readPacket.Size);
                            accumulatedBytes = player.accumulatedBytes.ToArray();
                        }
                    }

                    // Get the rest of the data.  
                    client.BeginReceive(player.buffer, 0, ClientPlayer.BufferSize, 0, new AsyncCallback(ReadCallback), player);
                }
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
                ServerDisconnected_Internal();
            }
        }

        public void Send(byte[] data)
        {
            player.socket.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), player.socket);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = client.EndSend(ar);
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
                ServerDisconnected_Internal();
            }
        }

        private void ServerDisconnected_Internal()
        {
            Shutdown();
            ServerDisconnected?.Invoke();
        }

        public void Shutdown()
        {
            if (player.socket.Connected) player.socket.Shutdown(SocketShutdown.Both);
            player.socket.Close();
        }
    }
}