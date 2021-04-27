using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;


namespace TournamentAssistantShared.Sockets
{
    public class WsServer
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
            ipv6Accept();
            
            
        }

        public WsServer(int port)
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
                

                try
                {
                    byte[] buffer = new byte[1024];
                    string headerResponse = "";
                    if ((ipv4Server != null && ipv4Server.IsBound) || (ipv6Server != null && ipv6Server.IsBound))
                    {
                        var i = handler.Receive(buffer);
                        headerResponse = (Encoding.UTF8.GetString(buffer)).Substring(0,i);
                    }

                    if (handler != null)
                    {
                        /* Handshaking and managing ClientSocket */
                        if (headerResponse != "")
                        {
                            var key = headerResponse.Replace("ey:", "`")
                                .Split('`')[1]
                                .Replace("\r", "").Split('\n')[0]
                                .Trim();
                            var test1 = AcceptKey(ref key);

                            var newLine = "\r\n";

                            var response = "HTTP/1.1 101 Switching Protocols" + newLine
                              + "Upgrade: websocket" + newLine
                              + "Connection: Upgrade" + newLine
                              + "Sec-WebSocket-Accept: " + test1 +
                              newLine +
                              newLine;
                            handler.Send(Encoding.UTF8.GetBytes(response));
                        }
                        // var i = handler.Receive(buffer);
                        // char[] chars = new char[i];
                        //
                        // System.Text.Decoder d = System.Text.Encoding.UTF8.GetDecoder();
                        // int charLen = d.GetChars(buffer, 0, i, chars, 0);
                        // System.String recv = new System.String(chars);
                        // Logger.Debug(recv);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e.ToString());
                }
                finally
                {
                    if ((ipv4Server != null && ipv4Server.IsBound) || (ipv6Server != null && ipv6Server.IsBound))
                    {
                        if (ipv4Server != null) ipv4Server.BeginAccept(new AsyncCallback(IPV4AcceptCallback), ipv4Server);
                        if (ipv6Server != null) ipv6Server.BeginAccept(new AsyncCallback(IPV6AcceptCallback), ipv6Server);
                    }
                }
                

                lock (clients)
                {
                    clients.Add(connectedClient);
                }

                ClientConnected?.Invoke(connectedClient);

                handler.BeginReceive(connectedClient.buffer, 0, ConnectedClient.BufferSize, 0, new AsyncCallback(ReadCallback), connectedClient);
            }
            catch (ObjectDisposedException) {
                Logger.Debug("ObjectDisposedException in Server AcceptCallback. This is expected during server shutdowns, such as during address verification");
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
            }
        }
        
        private string guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        private string AcceptKey(ref string key)
        {
            string longKey = key + guid;
            byte[] hashBytes = ComputeHash(longKey);
            return Convert.ToBase64String(hashBytes);
        }

        static SHA1 sha1 = SHA1.Create();
        private byte[] ComputeHash(string str)
        {
            return sha1.ComputeHash(Encoding.ASCII.GetBytes(str));
        }

        private void ReadCallback(IAsyncResult ar)
        {
            ConnectedClient player = (ConnectedClient)ar.AsyncState;

            try
            {
                Socket handler = player.workSocket;

                // Read data from the client socket.   
                int bytesRead = handler.EndReceive(ar);

                // Logger.Debug($"READ {bytesRead} BYTES");

                if (bytesRead > 0)
                {
                    var currentBytes = new byte[bytesRead];
                    Buffer.BlockCopy(player.buffer, 0, currentBytes, 0, bytesRead);
                    // player.accumulatedBytes.AddRange(currentBytes);
                    Packet readPacket = null;
                    bool fin = (currentBytes[0] & 0b10000000) != 0,
                        mask = (currentBytes[1] & 0b10000000) != 0;
                            
                    int opcode = currentBytes[0] & 0b00001111,
                        msglen = currentBytes[1] - 128, 
                        offset = 2;

                    if (opcode == 0x8)
                    {
                        ClientDisconnected_Internal(player);
                        return;
                    }
                    
                    if (msglen == 126) {
                        msglen = BitConverter.ToUInt16(new byte[] { currentBytes[3], currentBytes[2] }, 0);
                        offset = 4;
                    } else if (msglen == 127)
                    {
                        // idk something 
                    }

                    if (msglen == 0)
                    {
                        Logger.Debug("msglen == 0");
                    }
                    else if (mask) {
                        byte[] decoded = new byte[msglen];
                        byte[] masks = new byte[4] { currentBytes[offset], currentBytes[offset + 1], currentBytes[offset + 2], currentBytes[offset + 3] };
                        offset += 4;
            
                        for (int i = 0; i < msglen; ++i)
                            decoded[i] = (byte)(currentBytes[offset + i] ^ masks[i % 4]);
                        // accumulatedBytes
                        player.accumulatedBytes.AddRange(decoded);
                        var accumulatedBytes = player.accumulatedBytes.ToArray();
                        if (accumulatedBytes.Length == msglen)
                        {
                            string text = Encoding.UTF8.GetString(decoded);
                            try
                            {
                                readPacket = Packet.FromJSON(text);
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

        public void JsonBroadcast(string json)
        {
            try
            {
                lock (clients)
                {
                    foreach (var connectedClient in clients)
                    {
                        connectedClient.workSocket.Send(GetFrameFromString(json));
                    };
                }
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
            }
        }

        public void JsonSend(Guid id, string json)
        {
            try
            {
                foreach (var connectedClient in clients.Where(x => id == x.id)) connectedClient.workSocket.Send(GetFrameFromString(json));
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
            }
        }
        
        public enum EOpcodeType
        {
            Fragment = 0,
            Text = 1,
            Binary = 2,
            ClosedConnection = 8,
            Ping = 9,
            Pong = 10
        }
        
        public static byte[] GetFrameFromString(string Message, EOpcodeType Opcode = EOpcodeType.Text)
        {
            byte[] response;
            byte[] bytesRaw = Encoding.Default.GetBytes(Message);
            byte[] frame = new byte[10];

            int indexStartRawData = -1;
            int length = bytesRaw.Length;

            frame[0] = (byte)(128 + (int)Opcode);
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

            return response;
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