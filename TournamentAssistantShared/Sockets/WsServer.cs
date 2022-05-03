using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Utilities;

/*
 * Code kindly provided by Danny, seems to be sourced from here:
 * https://stackoverflow.com/questions/10200910/creating-a-hello-world-websocket-example
 */

namespace TournamentAssistantShared.Sockets
{
    public class WsServer
    {
        public event Func<ConnectedUser, Packet, Task> PacketReceived;
        public event Func<ConnectedUser, Task> ClientConnected;
        public event Func<ConnectedUser, Task> ClientDisconnected;

        public bool Enabled { get; set; } = true;

        private List<ConnectedUser> clients = new List<ConnectedUser>();
        private Socket ipv4Server;
        private Socket ipv6Server;
        private int port;

        static SHA1 sha1 = SHA1.Create();

        public enum EOpcodeType
        {
            Fragment = 0,
            Text = 1,
            Binary = 2,
            ClosedConnection = 8,
            Ping = 9,
            Pong = 10
        }

        public async Task Start()
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

            Func<Socket, Task> processClient = async (clientSocket) =>
            {
                var connectedUser = new ConnectedUser
                {
                    socket = clientSocket,
                    networkStream = new NetworkStream(clientSocket, ownsSocket: true),
                    id = Guid.NewGuid()
                };

                byte[] buffer = new byte[1024];
                string headerResponse = string.Empty;
                if ((ipv4Server != null && ipv4Server.IsBound) || (ipv6Server != null && ipv6Server.IsBound))
                {
                    var i = await connectedUser.networkStream.ReadAsync(buffer, 0, 1024);
                    headerResponse = (Encoding.UTF8.GetString(buffer)).Substring(0, i);
                }

                if (clientSocket != null)
                {
                    /* Handshaking and managing ClientSocket */
                    if (headerResponse != "")
                    {
                        var key = headerResponse.Replace("ey:", "`")
                            .Split('`')[1]
                            .Replace("\r", "").Split('\n')[0]
                            .Trim();

                        byte[] ComputeHash(string str)
                        {
                            return sha1.ComputeHash(Encoding.ASCII.GetBytes(str));
                        }

                        string AcceptKey(ref string key)
                        {
                            string longKey = key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                            byte[] hashBytes = ComputeHash(longKey);
                            return Convert.ToBase64String(hashBytes);
                        }

                        var acceptKey = AcceptKey(ref key);

                        var newLine = "\r\n";

                        var response = "HTTP/1.1 101 Switching Protocols" + newLine
                            + "Upgrade: websocket" + newLine
                            + "Connection: Upgrade" + newLine
                            + "Sec-WebSocket-Accept: " + acceptKey +
                            newLine +
                            newLine;
                        clientSocket.Send(Encoding.UTF8.GetBytes(response));
                    }
                }

                lock (clients)
                {
                    clients.Add(connectedUser);
                }

                if (ClientConnected != null) await ClientConnected.Invoke(connectedUser);

                ReceiveLoop(connectedUser);
            };

            Func<Task> ipv4Accept = async () =>
            {
                while (Enabled)
                {
                    // Start an asynchronous socket to listen for connections.  
                    Logger.Debug($"Waiting for an IPV4 connection on {ipv4Address}:{port} ...");
                    var clientSocket = await ipv4Server.AcceptAsync();
                    Logger.Debug($"Accepted connection on {ipv4Address}:{port} ...");

                    await processClient(clientSocket);
                }
            };

            Func<Task> ipv6Accept = async () =>
            {
                while (Enabled)
                {
                    // Start an asynchronous socket to listen for connections.  
                    Logger.Debug($"Waiting for an IPV6 connection on {ipv6Address}:{port} ...");
                    var clientSocket = await ipv6Server.AcceptAsync();
                    Logger.Debug($"Accpeted connection on {ipv6Address}:{port} ...");

                    await processClient(clientSocket);
                }
            };

            await ipv4Accept();
            //Task.Run(ipv4Accept);
            //ipv6Accept();
        }

        public WsServer(int port)
        {
            this.port = port;
        }

        private async void ReceiveLoop(ConnectedUser player)
        {
            try
            {
                // Begin receiving the data from the remote device.  
                while (player?.socket?.Connected ?? false)
                {
                    var bytesRead = await player.networkStream.ReadAsync(player.buffer, 0, ConnectedUser.BufferSize);

                    if (bytesRead > 0)
                    {
                        var currentBytes = new byte[bytesRead];
                        Buffer.BlockCopy(player.buffer.ToArray(), 0, currentBytes, 0, bytesRead);

                        Packet readPacket = null;
                        bool fin = (currentBytes[0] & 0b10000000) != 0,
                            mask = (currentBytes[1] & 0b10000000) != 0;

                        int opcode = currentBytes[0] & 0b00001111,
                            msglen = currentBytes[1] - 128,
                            offset = 2;

                        if (opcode == 0x8)
                        {
                            await Send(player, GetFrameFromPacket(null, EOpcodeType.ClosedConnection));
                            throw new Exception("Stream ended");
                        }

                        if (msglen == 126)
                        {
                            msglen = BitConverter.ToUInt16(new byte[] { currentBytes[3], currentBytes[2] }, 0);
                            offset = 4;
                        }

                        if (msglen == 0)
                        {
                            Logger.Debug("msglen == 0");
                        }
                        else if (mask)
                        {
                            byte[] decoded = new byte[msglen];
                            byte[] masks = new byte[4] { currentBytes[offset], currentBytes[offset + 1], currentBytes[offset + 2], currentBytes[offset + 3] };
                            offset += 4;

                            for (int i = 0; i < msglen; ++i)
                            {
                                decoded[i] = (byte)(currentBytes[offset + i] ^ masks[i % 4]);
                            }

                            player.accumulatedBytes.AddRange(decoded);
                            var accumulatedBytes = player.accumulatedBytes.ToArray();
                            if (accumulatedBytes.Length == msglen)
                            {
                                try
                                {
                                    readPacket = decoded.ProtoDeserialize<Packet>();
                                    PacketReceived?.Invoke(player, readPacket);
                                }
                                catch (Exception e)
                                {
                                    Logger.Error(e.Message);
                                    Logger.Error(e.StackTrace);
                                }
                                player.accumulatedBytes.Clear();
                                accumulatedBytes = player.accumulatedBytes.ToArray();
                            }
                        }
                    }
                    else if (bytesRead == 0)
                    {
                        await Send(player, GetFrameFromPacket(null, EOpcodeType.ClosedConnection));
                        throw new Exception("Stream ended");
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                await ClientDisconnected_Internal(player);
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
                await ClientDisconnected_Internal(player);
            }
        }

        private async Task ClientDisconnected_Internal(ConnectedUser player)
        {
            lock (clients)
            {
                clients.Remove(player);
            }
            if (ClientDisconnected != null) await ClientDisconnected.Invoke(player);
        }

        public async Task Broadcast(Packet packet)
        {
            try
            {
                var clientList = new List<ConnectedUser>();
                lock (clients)
                {
                    //We don't necessarily need to await this
                    foreach (var connectedClient in clients) clientList.Add(connectedClient);
                }
                await Task.WhenAll(clientList.Select(x => Send(x, packet)).ToArray());
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
            }
        }

        public async Task Send(Guid id, Packet packet) => await Send(new Guid[] { id }, packet);

        public async Task Send(Guid[] ids, Packet packet)
        {
            try
            {
                var clientList = new List<ConnectedUser>();
                lock (clients)
                {
                    //We don't necessarily need to await this
                    foreach (var ConnectedClient in clients.Where(x => ids.Contains(x.id))) clientList.Add(ConnectedClient);
                }
                await Task.WhenAll(clientList.Select(x => Send(x, packet)).ToArray());
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
            }
        }

        private async Task Send(ConnectedUser ConnectedClient, Packet packet)
        {
            try
            {
                var bytes = GetFrameFromPacket(packet);
                await ConnectedClient.networkStream.WriteAsync(bytes.Array, 0, bytes.Count);
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
                await ClientDisconnected_Internal(ConnectedClient);
            }
        }

        private async Task Send(ConnectedUser ConnectedClient, ArraySegment<byte> data)
        {
            try
            {
                await ConnectedClient.socket.SendAsync(data, SocketFlags.None);
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
                await ClientDisconnected_Internal(ConnectedClient);
            }
        }

        public static ArraySegment<byte> GetFrameFromPacket(Packet message, EOpcodeType opcode = EOpcodeType.Binary)
        {
            byte[] response;
            byte[] bytesRaw = message != null ? message.ProtoSerialize() : Array.Empty<byte>();
            byte[] frame = new byte[10];
            int length = bytesRaw.Length;

            frame[0] = (byte)(128 + (int)opcode);

            int indexStartRawData;
            if (length <= 125)
            {
                frame[1] = (byte)length;
                indexStartRawData = 2;
            }
            else if (length >= 126 && length <= 65535)
            {
                frame[1] = (byte)126;
                frame[2] = (byte)((length >> 8) & 255);
                frame[3] = (byte)(length & 255);
                indexStartRawData = 4;
            }
            else
            {
                frame[1] = (byte)127;
                frame[2] = (byte)((length >> 56) & 255);
                frame[3] = (byte)((length >> 48) & 255);
                frame[4] = (byte)((length >> 40) & 255);
                frame[5] = (byte)((length >> 32) & 255);
                frame[6] = (byte)((length >> 24) & 255);
                frame[7] = (byte)((length >> 16) & 255);
                frame[8] = (byte)((length >> 8) & 255);
                frame[9] = (byte)(length & 255);

                indexStartRawData = 10;
            }

            response = new byte[indexStartRawData + length];

            int i, reponseIdx = 0;

            //Add the frame bytes to the reponse
            for (i = 0; i < indexStartRawData; i++)
            {
                response[reponseIdx] = frame[i];
                reponseIdx++;
            }

            //Add the data bytes to the response
            for (i = 0; i < length; i++)
            {
                response[reponseIdx] = bytesRaw[i];
                reponseIdx++;
            }

            return new ArraySegment<byte>(response);
        }

        /// <summary>
        /// Waits for a response to the request with the designated ID, or if none is supplied, 
        /// any request at all. After which, it will unsubscribe from the event
        /// </summary>
        /// <param name="clientId">The id of the client to which to send the request</param>
        /// <param name="requestPacket">The packet to send</param>
        /// <param name="onRecieved">A Function executed when a matching Packet is received. If no <paramref name="id"/> is provided, this will trigger on any Packet with an id.
        /// This Function should return a boolean indicating whether or not the request was satisfied. For example, if it returns True, the event subscription is cancelled and the timer
        /// destroyed, and no more messages will be parsed through the Function. If it returns false, it is assumed that the Packet was unsatisfactory, and the Function will continue to receive
        /// potential matches.</param>
        /// <param name="id">The id of the Packet to wait for. Optional. If none is provided, all Packets with ids will be sent to <paramref name="onRecieved"/>.</param>
        /// <param name="onTimeout">A Function that executes in the event of a timeout. Optional.</param>
        /// <param name="timeout">Duration in milliseconds before the wait times out.</param>
        /// <returns></returns>
        public async Task SendAndAwaitResponse(Guid clientId, Packet requestPacket, Func<Packet, Task<bool>> onRecieved, string id = null, Func<Task> onTimeout = null, int timeout = 5000)
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
                    if (await onRecieved(responsePacket))
                    {
                        PacketReceived -= receivedPacket;

                        registration.Dispose();
                        cancellationTokenSource.Dispose();
                    };
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
        }
    }
}