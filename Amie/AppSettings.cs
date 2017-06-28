using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Amie
{
    public class AppSettings
    {
        public static string ProductName = "Amie Update Service";
        public static string Version = "1.0.0.2";

        public static int TCPServerPort
        {
            get
            {
                return int.Parse(ConfigurationManager.AppSettings["TCPServerPort"]);
            }
        }

        public static string PrivateKey
        {
            get
            {
                return "this is the crazy key it should probably be encrypted so it can't be seen over the network.";
            }
        }

        public static string TCPServerAddress
        {
            get
            {
                //if IP from config is empty, use the first one we find.
                string localIP = ConfigurationManager.AppSettings["TCPServerAddress"];
                if (string.IsNullOrEmpty(localIP))
                    localIP = GetLocalIPAddress();

                return localIP;
            }
        }

        public static int TCPClientPort
        {
            get
            {
                return int.Parse(ConfigurationManager.AppSettings["TCPClientPort"]);
            }
        }

        public static string TCPClientAddress
        {
            get
            {
                //if IP from config is empty, use the first one we find.
                string localIP = ConfigurationManager.AppSettings["TCPClientAddress"];
                if (string.IsNullOrEmpty(localIP))
                    localIP = GetLocalIPAddress();

                return localIP;
            }
        }

        /// <summary>
        /// The path where updates will be extracted and ran from.  For example if the zip file is structured like this Application/Web then the base update path should be something like C:/Websites/.
        /// </summary>
        public static string BaseUpdatePath
        {
            get
            {
                return ConfigurationManager.AppSettings["BaseUpdatePath"];
            }
        }

        private static string GetLocalIPAddress()
        {
            string localIP;
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                try
                {
                    socket.Connect("10.0.2.4", 65530);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    localIP = endPoint.Address.ToString();
                }
                catch (Exception)
                {
                    localIP = IPAddress.Loopback.ToString();
                }

            }
            return localIP;
        }
    }
}
