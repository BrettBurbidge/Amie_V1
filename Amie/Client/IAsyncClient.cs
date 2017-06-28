using System;

namespace Amie.Client
{
    public interface IAsyncClient : IDisposable
    {
        event ConnectedHandler Connected;

        event ClientMessageReceivedHandler MessageReceived;

        event ClientMessageSubmittedHandler MessageSubmitted;

        void StartClient(string ipAddress, int port);

        bool IsConnected();

        void Receive();

        void Send(string msg, bool close);
    }
}


