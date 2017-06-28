using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Net;
using System.Threading;

namespace Amie.Service
{
    public class ServiceRunner
    {

        private static Server.AsyncSocketListener server = (Server.AsyncSocketListener)Server.AsyncSocketListener.Instance;

        public void Start()
        {
            Amie.Logger.Info("Starting {0}...", Amie.AppSettings.ProductName);
            //Console.Title = "Amie Update Server";

            Thread tcpServer = new Thread(StartTCPServer);
            tcpServer.Start();
        }

        private static void StartTCPServer()
        {
            string ipaddress = AppSettings.TCPServerAddress;
            int port = AppSettings.TCPServerPort;

            Amie.Logger.Info("{2} running {0}:{1}...", ipaddress, port.ToString(), Amie.AppSettings.ProductName);
            server.MessageReceived += Server_MessageReceived;
            server.StartListening(ipaddress, port);
        }

        public void Stop()
        {
            Amie.Logger.Info("Stopping {0}...", Amie.AppSettings.ProductName);

            foreach (var item in server.clients)
            {
                server.Close(item.Key);
            }
            server.Dispose();
        }

        private static void Server_MessageReceived(int id, byte[] msg)
        {
            try
            {
                //This working becuase the update is under 6MB to update that size go fix this in StateObject for now...
                //TODO: Fix the buffer so it's dynamic.  Maybe based on some shared EOL byte or something...so we know the message is complete.
                UpdateSettings settings = UpdateSettings.Deserialize(msg);
                if (settings != null)
                {
                    Amie.Logger.Info("Message Recieved from the client {0}.", id.ToString());
                    Updater.ExtractUpdateAndRun(settings);
                }
            }
            catch (Exception ex)
            {
                Amie.Logger.Error("Error receiving message from the client {0}.", ex.ToString());
            }
        }
    }
}
