using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;


namespace Amie.Service
{

    public class AsyncClient2
    {
        // ManualResetEvent instances signal completion.
        private ManualResetEvent connectDone = new ManualResetEvent(false);
        //private ManualResetEvent sendDone = new ManualResetEvent(false);
        private ManualResetEvent receiveDone = new ManualResetEvent(false);

        // The response from the remote device.
        private String response = String.Empty;

        /// <summary>
        /// The Socket used in this connection
        /// </summary>
        public Socket Client { get; set; }

        /// <summary>
        /// The endpoint for the client.  Basicly the server or teleboss.
        /// </summary>
        public IPEndPoint EndPoint { get; set; }

        /// <summary>
        /// Used to determin if the connection has been reset.  
        /// This is used in ReceiveCallBack method becuase sometimes that method is called 3 or 4 times when a 
        /// receive fails and we only want to call it once.
        /// </summary>
        private bool ConnectionHasBeenReset { get; set; }


        public void StartClient(string ipaddress, int port)
        {

            // Establish the remote endpoint for the socket.
            //IPAddress ipAddress = IPAddress.Loopback;
            IPAddress ipAddress = IPAddress.Parse(ipaddress);
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);
            EndPoint = remoteEP;

            //Reset these just in case we are re-using this connection.
            connectDone = new ManualResetEvent(false);
            //sendDone = new ManualResetEvent(false);
            receiveDone = new ManualResetEvent(false);

            // Create a TCP/IP socket.
            Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            Console.WriteLine("Attempting connection on {0}:{1}", ipAddress.ToString(), port.ToString());
            SetupConnection(EndPoint);
            ConnectionHasBeenReset = false;

        }


        private void SetupConnection(IPEndPoint remoteEP)
        {
            // Connect to the remote endpoint.
            Client.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), Client);
            connectDone.WaitOne();

            // Receive the response from the remote device.
            Receive(Client);
            receiveDone.WaitOne();

            // Write the response to the console.
            Console.WriteLine("Response received : {0}", response);
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete the connection.
                client.EndConnect(ar);

                Console.WriteLine("Socket connected to {0}", client.RemoteEndPoint.ToString());

                // Signal that the connection has been made.
                connectDone.Set();
            }
            catch (Exception)
            {
                //Server was not listening on this ip/port. Try again in 10 seconds.
                Console.WriteLine("Failed to connect to {0}:{1}. Trying again in 10 seconds.", EndPoint.Address.ToString(), EndPoint.Port.ToString());
                Thread.Sleep(10000);
                connectDone.Close();
                //StartClient(Element);

            }
        }

        private void Receive(Socket client)
        {
            try
            {
                // Create the state object.
                StateObject state = new StateObject();
                state.workSocket = client;

                // Begin receiving the data from the remote device.
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            // Retrieve the state object and the client socket 
            // from the asynchronous state object.
            StateObject state = (StateObject)ar.AsyncState;
            Socket client = state.workSocket;

            try
            {

                // Read data from the remote device.
                int bytesRead = client.EndReceive(ar);

                if (bytesRead > 0)
                {
                    string thisMessage = Encoding.ASCII.GetString(state.buffer, 0, bytesRead);
                    // There might be more data, so store the data received so far.
                    state.sb.Append(thisMessage);


                    //API.ElementCollectionLogAPI.AddDataToLog(Element.ID, DateTime.Now, state.sb.ToString());

                    //Console.WriteLine(state.sb.ToString());
                    Console.WriteLine(thisMessage);

                    // Get the rest of the data.
                    client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
                }
                else
                {
                    // All the data has arrived; put it in response.
                    if (state.sb.Length > 1)
                    {
                        response = state.sb.ToString();
                    }
                    // Signal that all bytes have been received.
                    receiveDone.Set();
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Error receiving data.  Server shutdown.  Restarting connection in 10 seconds.");

                //reset the connection handle
                connectDone.Close();
                //Reset the receive handle
                receiveDone.Close();
                //Reset the send handle
                //sendDone.Close();

                Thread.Sleep(10000);
                if (client != null)
                {
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                    client = null;
                }
                //StartClient(Element);
            }
        }

        public void Disconnect()
        {
            // Release the socket.
            if (Client != null)
            {
                Client.Shutdown(SocketShutdown.Both);
                Client.Close();
            }

        }

        // State object for receiving data from remote device.
        public class StateObject
        {
            // Client socket.
            public Socket workSocket = null;
            // Size of receive buffer.
            public const int BufferSize = 256;
            // Receive buffer.
            public byte[] buffer = new byte[BufferSize];
            // Received data string.
            public StringBuilder sb = new StringBuilder();
        }

    }
}
