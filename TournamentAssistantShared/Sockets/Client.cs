using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace TournamentAssistantShared.Sockets
{
    public class ClientPlayer
    {
        public Socket socket = null;
        public const int BufferSize = 1024;
        public byte[] buffer = new byte[BufferSize];
        public List<byte> accumulatedBytes = new List<byte>();
    }

    public class Client
    {
        public event Action<Packet> PacketReceived;
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
            try
            {
                if (!IPAddress.TryParse(endpoint, out var ipAddress))
                {
                    //If we want to default to ipv4, we should uncomment the following line. I'm leaving it
                    //as it is now so we can test ipv6/ipv4 mix stability
                    //IPAddress ipAddress = ipHostInfo.AddressList.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
                    IPHostEntry ipHostInfo = Dns.GetHostEntry(endpoint);
                    ipAddress = ipHostInfo.AddressList[0];
                }

                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

                player.socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                player.socket.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), player.socket);
            }
            catch
            {

            }
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
                            Packet readPacket = null;
                            try
                            {
                                readPacket = Packet.FromBytes(accumulatedBytes);
                                PacketReceived?.Invoke(readPacket);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }

                            //Remove the bytes which we've already used from the accumulated List
                            //If the packet failed to parse, skip the header so that the rest of the packet is consumed by the above vailidity check on the next run
                            player.accumulatedBytes.RemoveRange(0, readPacket?.Size ?? Packet.packetHeaderSize);
                            accumulatedBytes = player.accumulatedBytes.ToArray();
                        }
                    }

                    // Get the rest of the data.  
                    client.BeginReceive(player.buffer, 0, ClientPlayer.BufferSize, 0, new AsyncCallback(ReadCallback), player);
                }
            }
            catch (ObjectDisposedException)
            {
                ServerDisconnected_Internal();
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
                ServerDisconnected_Internal();
            }
        }

        public IAsyncResult Send(byte[] data)
        {
            return player.socket.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), player.socket);
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
            if (Connected) player.socket?.Shutdown(SocketShutdown.Both);
            player.socket?.Close();
        }
    }
}