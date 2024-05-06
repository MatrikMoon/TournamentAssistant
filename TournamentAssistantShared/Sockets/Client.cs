using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace TournamentAssistantShared.Sockets
{
    public class Client
    {
        public event Func<PacketWrapper, Task> PacketReceived;
        public event Func<Task> ServerConnected;
        public event Func<Task> ServerFailedToConnect;
        public event Func<Task> ServerDisconnected;

        private int port;
        private string endpoint;
        private ConnectedUser player;

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

            player = new ConnectedUser();
        }

        public async Task Start()
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

            try
            {
                //Try to connect to the server
                await player.socket.ConnectAsync(remoteEP);
                var client = player.socket;

                //Try to authenticate with SSL
                //TODO: I believe a unity bug is keeping validation from functioning properly. Once the unity version changes,
                //we should ABSOLUTELY remove this validation function override
                player.sslStream = new SslStream(new NetworkStream(client, ownsSocket: true), false, new RemoteCertificateValidationCallback((sender, certificate, chain, sslPolicyErrors) => true));
                player.sslStream.AuthenticateAsClient(endpoint);

                ReceiveLoop();

                //Signal Connection complete
                if (ServerConnected != null) await ServerConnected.Invoke();
            }
            catch (ObjectDisposedException)
            {
                // Shutdown() called while connecting
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());

                if (ServerFailedToConnect != null) await ServerFailedToConnect.Invoke();
            }
        }

        private async void ReceiveLoop()
        {
            try
            {
                // Begin receiving the data from the remote device.  
                while (Connected)
                {
                    var bytesRead = await player.sslStream.ReadAsync(player.buffer, 0, ConnectedUser.BUFFER_SIZE);
                    if (bytesRead > 0)
                    {
                        var currentBytes = new byte[bytesRead];
                        Buffer.BlockCopy(player.buffer, 0, currentBytes, 0, bytesRead);

                        player.accumulatedBytes.AddRange(currentBytes);
                        if (player.accumulatedBytes.Count >= PacketWrapper.packetHeaderSize)
                        {
                            //If we're not at the start of a packet, increment our position until we are, or we run out of bytes
                            var accumulatedBytes = player.accumulatedBytes.ToArray();
                            while (accumulatedBytes.Length >= PacketWrapper.packetHeaderSize && !PacketWrapper.StreamIsAtPacket(accumulatedBytes))
                            {
                                player.accumulatedBytes.RemoveAt(0);
                                accumulatedBytes = player.accumulatedBytes.ToArray();
                            }

                            while (accumulatedBytes.Length >= PacketWrapper.packetHeaderSize && PacketWrapper.PotentiallyValidPacket(accumulatedBytes))
                            {
                                PacketWrapper readPacket = null;
                                try
                                {
                                    readPacket = PacketWrapper.FromBytes(accumulatedBytes);
                                    await PacketReceived?.Invoke(readPacket);
                                }
                                catch (Exception e)
                                {
                                    Logger.Error(e.Message);
                                    Logger.Error(e.StackTrace);
                                }

                                //Remove the bytes which we've already used from the accumulated List
                                //If the packet failed to parse, skip the header so that the rest of the packet is consumed by the above vailidity check on the next run
                                player.accumulatedBytes.RemoveRange(0, readPacket?.Size ?? PacketWrapper.packetHeaderSize);
                                accumulatedBytes = player.accumulatedBytes.ToArray();
                            }
                        }
                    }
                    else if (bytesRead == 0) throw new Exception("Client: connection ended gracefully");
                }
            }
            catch (ObjectDisposedException)
            {
                await ServerDisconnected_Internal();
            }
            catch (IOException e)
            {
                //995 is the abort error code, which happens when Shutdown() is called before reaching the receive loop. This used to
                //instead manifest as a 0 byte read result, but that seems to no longer be the case after async refactoring
                if ((e.InnerException is SocketException se) && se.ErrorCode != 995)
                {
                    Logger.Debug(se.ToString());
                }
                else if (e.InnerException is SocketException)
                {
                    Logger.Debug("Client: connection ended gracefully");
                }
                else
                {
                    Logger.Error("Unhandled error:");
                    Logger.Error(e.Message);
                    Logger.Error(e.StackTrace);
                }
                await ServerDisconnected_Internal();
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
                await ServerDisconnected_Internal();
            }
        }

        public async Task Send(PacketWrapper packet)
        {
            var data = packet.ToBytes();
            try
            {
                await player.sslStream.WriteAsync(data, 0, data.Length);
            }
            catch (SocketException e)
            {
                await ServerDisconnected_Internal();

                throw e; //Ancestor functions will handle this and likely reset the connection
            }
        }

        private async Task ServerDisconnected_Internal()
        {
            Shutdown();
            if (ServerDisconnected != null) await ServerDisconnected.Invoke();
        }

        public void Shutdown()
        {
            player.sslStream?.Dispose();
            player.socket?.Close();
        }
    }
}