using Fleck;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Sockets;
using TournamentAssistantShared.Utilities;

namespace TournamentAssistantServer.Sockets
{
    public class Server
    {
        public event Func<ConnectedUser, Packet, Task> PacketReceived;
        public event Func<ConnectedUser, Task> ClientConnected;
        public event Func<ConnectedUser, Task> ClientDisconnected;

        public bool Enabled { get; set; } = true;

        private List<ConnectedUser> _clients = new List<ConnectedUser>();

        private Socket ipv4Server;
        private Socket ipv6Server;
        private WebSocketServer webSocketServer;
        private int port;
        private int websocketPort;

        private X509Certificate2 cert;

        public Server(int port, X509Certificate2 cert, int websocketPort = 0)
        {
            this.port = port;
            this.cert = cert;
            this.websocketPort = websocketPort;
        }

        // Blocks while setting up listeners
        public void Start()
        {
            var ipv4Address = IPAddress.Any;
            var ipv6Address = IPAddress.IPv6Any;
            var localIPV4EndPoint = new IPEndPoint(ipv4Address, port);
            var localIPV6EndPoint = new IPEndPoint(ipv6Address, port);

            ipv4Server = new Socket(ipv4Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            ipv6Server = new Socket(ipv6Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            ipv4Server.Bind(localIPV4EndPoint);
            ipv6Server.Bind(localIPV6EndPoint);

            ipv4Server.Listen(100);
            ipv6Server.Listen(100);

            async Task processClient(Socket clientSocket)
            {
                var connectedUser = new ConnectedUser
                {
                    socket = clientSocket,
                    id = Guid.NewGuid(),
                    sslStream = new SslStream(new NetworkStream(clientSocket, ownsSocket: true))
                };

                try
                {
                    connectedUser.sslStream.AuthenticateAsServer(cert);

                    AddUser(connectedUser);

                    if (ClientConnected != null) await ClientConnected.Invoke(connectedUser);

                    ReceiveLoop(connectedUser);
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                    Logger.Error(e.Message);
                    Logger.Error(e.StackTrace);
                }
            }

            async Task processWebsocketClient(IWebSocketConnection websocketConnection)
            {
                var connectedUser = new ConnectedUser
                {
                    id = websocketConnection.ConnectionInfo.Id,
                    websocketConnection = websocketConnection,
                    websocketSendSemaphore = new(1),
                };

                AddUser(connectedUser);

                if (ClientConnected != null) await ClientConnected.Invoke(connectedUser);
            }

            async Task ipv4Accept()
            {
                while (Enabled)
                {
                    // Start an asynchronous socket to listen for connections.  
                    Logger.Debug($"Waiting for an IPV4 connection on {ipv4Address}:{port} ...");
                    var clientSocket = await ipv4Server.AcceptAsync();
                    Logger.Debug($"Accepted connection on {ipv4Address}:{port} ...");

                    await processClient(clientSocket);
                }
            }

            async Task ipv6Accept()
            {
                while (Enabled)
                {
                    // Start an asynchronous socket to listen for connections.  
                    Logger.Debug($"Waiting for an IPV6 connection on {ipv6Address}:{port} ...");
                    var clientSocket = await ipv6Server.AcceptAsync();
                    Logger.Debug($"Accpeted connection on {ipv6Address}:{port} ...");

                    await processClient(clientSocket);
                }
            }

            Task websocketAccept()
            {
                try
                {
                    webSocketServer = new WebSocketServer($"wss://0.0.0.0:{websocketPort}");
                    webSocketServer.Certificate = cert;
                    webSocketServer.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12;

                    webSocketServer.Start(socket =>
                    {
                        socket.OnClose = async () =>
                        {
                            Logger.Warning($"OnClose: {socket.ConnectionInfo.Id}");
                            var player = GetUserById(socket.ConnectionInfo.Id);

                            if (player != null)
                            {
                                await ClientDisconnected_Internal(player);
                            }
                        };

                        socket.OnBinary = async receiveResult =>
                        {
                            var player = GetUserById(socket.ConnectionInfo.Id);

                            // Accept the connection if the client doesn't exist yet
                            if (player == null)
                            {
                                Logger.Debug($"Accpeted WebSocket connection on {webSocketServer.Location} ...");
                                await processWebsocketClient(socket);
                                Logger.Success($"Client Connected: {socket.ConnectionInfo.Id}");

                                player = GetUserById(socket.ConnectionInfo.Id);
                            }

                            try
                            {
                                var readPacket = receiveResult.ProtoDeserialize<Packet>();
                                if (PacketReceived != null) await PacketReceived.Invoke(player, readPacket);
                            }
                            catch
                            {
                                Logger.Error("Caught websocket deserialization error, closing websocket");
                                socket.Close();
                            }
                        };
                    });

                    Logger.Debug($"Waiting for a WebSocket connection on {webSocketServer.Location} ...");
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }

                return Task.CompletedTask;
            }

            Task.Run(ipv4Accept);
            Task.Run(ipv6Accept);

            // Accept websocket connections if a port is specified
            if (websocketPort > 0)
            {
                Task.Run(websocketAccept);
            }
        }

        private async void ReceiveLoop(ConnectedUser player)
        {
            try
            {
                var streamEnded = false;
                // Begin receiving the data from the remote device.  
                while ((player?.socket?.Connected ?? false) && !streamEnded)
                {
                    var bytesRead = await player.sslStream.ReadAsync(player.buffer, 0, ConnectedUser.BUFFER_SIZE);
                    if (bytesRead > 0)
                    {
                        var currentBytes = new byte[bytesRead];
                        Buffer.BlockCopy(player.buffer, 0, currentBytes, 0, bytesRead);

                        player.accumulatedBytes.AddRange(currentBytes);
                        if (player.accumulatedBytes.Count >= PacketWrapper.packetHeaderSize)
                        {
                            // If we're not at the start of a packet, increment our position until we are, or we run out of bytes
                            var accumulatedBytes = player.accumulatedBytes.ToArray();
                            while (accumulatedBytes.Length >= PacketWrapper.packetHeaderSize &&
                                   !PacketWrapper.StreamIsAtPacket(accumulatedBytes))
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
                                    if (PacketReceived?.Invoke(player, readPacket.Payload) is Task task)
                                    {
                                        await task;
                                    }
                                }
                                catch (Exception e)
                                {
                                    Logger.Error(e.Message);
                                    Logger.Error(e.StackTrace);
                                }

                                // Remove the bytes which we've already used from the accumulated List
                                // If the packet failed to parse, skip the header so that the rest of the packet is consumed by the above vailidity check on the next run
                                player.accumulatedBytes.RemoveRange(0, readPacket?.Size ?? PacketWrapper.packetHeaderSize);
                                accumulatedBytes = player.accumulatedBytes.ToArray();
                            }
                        }
                    }
                    else if (bytesRead == 0)
                    {
                        streamEnded = true;
                        Logger.Debug($"Server: client {player.id} has disconnected");
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
            }
            finally
            {
                await ClientDisconnected_Internal(player);
            }
        }

        // Thread-safe helpers
        private ConnectedUser GetUserById(Guid id)
        {
            lock (_clients)
            {
                return _clients.FirstOrDefault(x => x.id == id);
            }
        }

        private List<ConnectedUser> GetUsersById(Guid[] ids)
        {
            lock (_clients)
            {
                return _clients.Where(x => ids.Contains(x.id)).ToList();
            }
        }

        private List<ConnectedUser> GetUsers()
        {
            lock (_clients)
            {
                return _clients.ToList();
            }
        }

        private void AddUser(ConnectedUser player)
        {
            lock (_clients)
            {
                _clients.Add(player);
            }
        }

        private bool RemoveUser(ConnectedUser player)
        {
            lock (_clients)
            {
                return _clients.Remove(player);
            }
        }

        private async Task ClientDisconnected_Internal(ConnectedUser player)
        {
            if (RemoveUser(player))
            {
                if (ClientDisconnected != null) await ClientDisconnected.Invoke(player);
            }
        }

        public async Task Broadcast(PacketWrapper packet)
        {
            try
            {
                await Task.WhenAll(GetUsers().Select(x => Send(x, packet)).ToArray());
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
            }
        }

        public async Task Send(Guid id, PacketWrapper packet) => await Send(new Guid[] { id }, packet);

        public async Task Send(Guid[] ids, PacketWrapper packet)
        {
            try
            {
                await Task.WhenAll(GetUsersById(ids).Select(x => Send(x, packet)).ToArray());
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
            }
        }

        private async Task Send(ConnectedUser connectedUser, PacketWrapper packet)
        {
            try
            {
                if (connectedUser.sslStream != null)
                {
                    var data = packet.ToBytes();
                    await connectedUser.sslStream.WriteAsync(data, 0, data.Length);
                }
                else if (connectedUser.websocketConnection != null && connectedUser.websocketConnection.IsAvailable)
                {
                    var data = packet.Payload.ProtoSerialize();
                    await connectedUser.websocketConnection.Send(data);
                }
                else throw new Exception("ConnectedUser must have either a networkStream or websocketContext to send data");
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
                await ClientDisconnected_Internal(connectedUser);
            }
        }

        /// <summary>
        /// Waits for a response to the request with the designated ID, or if none is supplied, 
        /// any request at all. After which, it will unsubscribe from the event
        /// </summary>
        /// <param name="clientId">The id of the client to which to send the request</param>
        /// <param name="requestPacket">The packet to send</param>
        /// <param name="onReceived">A Function executed when a matching Packet is received. If no <paramref name="id"/> is provided, this will trigger on any Packet with an id.
        /// This Function should return a boolean indicating whether or not the request was satisfied. For example, if it returns True, the event subscription is cancelled and the timer
        /// destroyed, and no more messages will be parsed through the Function. If it returns false, it is assumed that the Packet was unsatisfactory, and the Function will continue to receive
        /// potential matches.</param>
        /// <param name="id">The id of the Packet to wait for. Optional. If none is provided, all Packets with ids will be sent to <paramref name="onReceived"/>.</param>
        /// <param name="onTimeout">A Function that executes in the event of a timeout. Optional.</param>
        /// <param name="timeout">Duration in milliseconds before the wait times out.</param>
        /// <returns></returns>
        public async Task SendAndAwaitResponse(Guid clientId, PacketWrapper requestPacket,
            Func<Packet, Task<bool>> onReceived, string id = null, Func<Task> onTimeout = null, int timeout = 5000)
        {
            Func<ConnectedUser, Packet, Task> receivedPacket = null;

            //TODO: I don't think Register awaits async callbacks 
            var cancellationTokenSource = new CancellationTokenSource();
            var registration = cancellationTokenSource.Token.Register(async () =>
            {
                PacketReceived -= receivedPacket;
                if (onTimeout != null) await onTimeout.Invoke();

                cancellationTokenSource.Dispose();
            });

            receivedPacket = async (client, responsePacket) =>
            {
                if (clientId == client.id && (id == null || responsePacket.Id.ToString() == id))
                {
                    if (await onReceived(responsePacket))
                    {
                        PacketReceived -= receivedPacket;

                        registration.Dispose();
                        cancellationTokenSource.Dispose();
                    }
                }
            };

            cancellationTokenSource.CancelAfter(timeout);
            PacketReceived += receivedPacket;

            await Send(clientId, requestPacket);
        }

        public void Shutdown()
        {
            Enabled = false;
            if (ipv4Server.Connected) ipv4Server.Shutdown(SocketShutdown.Both);
            ipv4Server.Close();

            if (ipv6Server.Connected) ipv6Server.Shutdown(SocketShutdown.Both);
            ipv6Server.Close();

            if (webSocketServer != null)
            {
                webSocketServer.Dispose();
            }
        }
    }
}