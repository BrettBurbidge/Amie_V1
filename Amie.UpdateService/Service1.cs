using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Amie.UpdateService
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        private Amie.Service.ServiceRunner server = new Service.ServiceRunner();
        static System.Threading.Thread UpdateService;

        protected override void OnStart(string[] args)
        {
            UpdateService = new Thread(new ThreadStart(RunUpdateService));
            UpdateService.Start();
        }

        private void RunUpdateService()
        {
            server.Start();
        }

        public void StopUpdateService()
        {
            server.Stop();
            UpdateService.Abort();
        }
        
        protected override void OnStop()
        {
            StopUpdateService();
        }
    }
}
