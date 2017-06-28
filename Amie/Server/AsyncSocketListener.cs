using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;


namespace Amie.Server
{
    public delegate void MessageReceivedHandler(int id, byte[] msg);
    public delegate void MessageSubmittedHandler(int id, bool close);

    public sealed class AsyncSocketListener //: IAsyncSocketListener
    {
        private const ushort ClientConnectionLimit = 5;

        private static readonly AsyncSocketListener instance = new AsyncSocketListener();

        private readonly ManualResetEvent mre = new ManualResetEvent(false);
        internal readonly IDictionary<int, StateObject> clients = new Dictionary<int, StateObject>();

        public event MessageReceivedHandler MessageReceived;

        public event MessageSubmittedHandler MessageSubmitted;

        private AsyncSocketListener()
        {
        }

        public static AsyncSocketListener Instance
        {
            get
            {
                return instance;
            }
        }

        /* Starts the AsyncSocketListener */
        public void StartListening(string ipAddress, int port)
        {
            //var host = Dns.GetHostEntry(string.Empty);
            //var ip = host.AddressList[3];
            var endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);

            try
            {
                using (var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    listener.Bind(endpoint);
                    listener.Listen(ClientConnectionLimit);
                    while (true)
                    {
                        this.mre.Reset();
                        listener.BeginAccept(this.OnClientConnect, listener);
                        this.mre.WaitOne();
                    }
                }
            }
            catch (SocketException se)
            {
                Amie.Logger.Info("Error starting listening {0}", se.Message);
            }
        }

        /* Gets a socket from the clients dictionary by his Id. */
        private StateObject GetClient(int id)
        {
            StateObject state;

            return this.clients.TryGetValue(id, out state) ? state : null;
        }

        /* Checks if the socket is connected. */
        public bool IsConnected(int id)
        {
            var state = this.GetClient(id);

            if (state == null) return false;

            return !(state.Listener.Poll(1000, SelectMode.SelectRead) && state.Listener.Available == 0);
        }

        /* Add a socket to the clients dictionary. Lock clients temporary to handle multiple access.
         * ReceiveCallback raise a event, after the message receive complete. */

        public void OnClientConnect(IAsyncResult result)
        {
            this.mre.Set();

            try
            {
                StateObject state;

                lock (this.clients)
                {
                    Amie.Logger.Info("Client attempting to connect");
                    var id = !this.clients.Any() ? 1 : this.clients.Keys.Max() + 1;

                    state = new StateObject(((Socket)result.AsyncState).EndAccept(result), id);
                    this.clients.Add(id, state);
                    Amie.Logger.Info("Client connected. Clinet Id " + id);
                }

                state.Listener.BeginReceive(state.Buffer, 0, state.BufferSize, SocketFlags.None, this.ReceiveCallback, state);
            }
            catch (SocketException ex)
            {
                Amie.Logger.Error("Error connecting the client {0}.", ex.ToString());
            }
        }

        public void ReceiveCallback(IAsyncResult result)
        {
            var state = (StateObject)result.AsyncState;

            try
            {
                var receive = state.Listener.EndReceive(result);

                //We are expecting a bytearray now, text so i am going to skip this and send the bytes to the message recieved handler.
                //if (receive > 0)
                //{
                //    state.Append(Encoding.UTF8.GetString(state.Buffer, 0, receive));
                //}

                if (receive == state.BufferSize)
                {
                    state.Listener.BeginReceive(state.Buffer, 0, state.BufferSize, SocketFlags.None, this.ReceiveCallback, state);
                }
                else
                {
                    var messageReceived = this.MessageReceived;

                    if (messageReceived != null)
                    {
                        messageReceived(state.Id, state.Buffer);
                    }

                    state.Reset();
                }
            }
            catch (SocketException ex)
            {
                Amie.Logger.Error("Error in ReceiveCallback {0}.", ex.ToString());
            }
        }


        /* Send(int id, String msg, bool close) use bool to close the connection after the message sent. */
        public void Send(int id, string msg, bool close)
        {
            var state = this.GetClient(id);

            if (state == null)
            {
                throw new Exception("Client does not exist.");
            }

            if (!this.IsConnected(state.Id))
            {
                throw new Exception("Destination socket is not connected.");
            }

            try
            {
                var send = Encoding.UTF8.GetBytes(msg);

                state.Close = close;
                state.Listener.BeginSend(send, 0, send.Length, SocketFlags.None, this.SendCallback, state);
            }
            catch (SocketException se)
            {
                Amie.Logger.Error("Error in sending data to the client {0}.", se.ToString());
            }
            catch (ArgumentException ae)
            {
                Amie.Logger.Error("Error in sending data to the client {0}.", ae.ToString());
            }
        }

        private void SendCallback(IAsyncResult result)
        {
            var state = (StateObject)result.AsyncState;

            try
            {
                state.Listener.EndSend(result);
            }
            catch (SocketException)
            {
                // TODO:
            }
            catch (ObjectDisposedException)
            {
                // TODO:
            }
            finally
            {
                var messageSubmitted = this.MessageSubmitted;

                if (messageSubmitted != null)
                {
                    messageSubmitted(state.Id, state.Close);
                }
            }
        }

        public void Close(int id)
        {
            var state = this.GetClient(id);

            if (state == null)
            {
                throw new Exception("Client does not exist.");
            }

            try
            {
                state.Listener.Shutdown(SocketShutdown.Both);
                state.Listener.Close();
            }
            catch (SocketException)
            {
                // TODO:
            }
            finally
            {
                lock (this.clients)
                {
                    this.clients.Remove(state.Id);
                    Amie.Logger.Info("Client disconnected with Id {0}", state.Id.ToString());
                }
            }
        }

        public void Dispose()
        {
            foreach (var id in this.clients.Keys)
            {
                this.Close(id);
            }

            this.mre.Dispose();
        }
    }
}
