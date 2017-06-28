using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amie.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            Amie.Service.ServiceRunner server = new Service.ServiceRunner();
            server.Start();
        }
    }
}
