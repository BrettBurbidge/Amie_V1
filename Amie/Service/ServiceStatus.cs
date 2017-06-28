using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amie.Service
{
    public class ServiceStatus
    {
        public static string Status 
        {
            get
            {
                var result = "Unknown";
                try
                {
                    System.ServiceProcess.ServiceController sc = new System.ServiceProcess.ServiceController(AppSettings.ProductName);
                    result = sc.Status.ToString();
                }
                catch (Exception ex)
                {
                    result = ex.Message;
                }
                return result;
            }
        }
    }
}
