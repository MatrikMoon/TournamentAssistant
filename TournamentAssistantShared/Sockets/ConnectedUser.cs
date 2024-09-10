using Fleck;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;

namespace TournamentAssistantShared.Sockets
{
    public class ConnectedUser
    {
        public Guid id;

        // Sometimes, multiple clients can try to send data to the same client
        // before a previous write is complete, causing an exception. This will
        // be used to block writes to this client until any previous writes are complete
        public SemaphoreSlim writeSemaphore = new SemaphoreSlim(1, 1);
        
        public Socket socket = null;
        public SslStream sslStream = null;
        public IWebSocketConnection websocketConnection = null;
        public SemaphoreSlim websocketSendSemaphore = null;

        public const int BUFFER_SIZE = 8192;
        public byte[] buffer = new byte[BUFFER_SIZE];
        public List<byte> accumulatedBytes = new();
    }
}
