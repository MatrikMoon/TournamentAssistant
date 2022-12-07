using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;

namespace TournamentAssistantShared.Sockets
{
    public class ConnectedUser
    {
        public Guid id;

        public Socket socket = null;
        public NetworkStream networkStream = null;
        public HttpListenerWebSocketContext websocketContext = null;
        public SemaphoreSlim WebsocketSendSemaphore = null;

        public const int BUFFER_SIZE = 8192;
        public byte[] buffer = new byte[BUFFER_SIZE];
        public List<byte> accumulatedBytes = new();
    }
}
