using System;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace WindowsService
{
    [RunInstaller(true)]
    public class RESILEO_WINDOWS_AGENT_SERVICE_INSTALLER : Installer
    {
        ServiceProcessInstaller serviceProcessInstaller = new ServiceProcessInstaller();
        ServiceInstaller serviceInstaller = new ServiceInstaller();
        /// <summary>
        /// Public Constructor for WindowsServiceInstaller.
        /// - Put all of your Initialization code here.
        /// </summary>
        string serviceName = "RESILEO_WINDOWS_AGENT_SERVICE_v2";

        public RESILEO_WINDOWS_AGENT_SERVICE_INSTALLER()
        {


            //# Service Account Information
            serviceProcessInstaller.Account = ServiceAccount.LocalSystem;
            serviceProcessInstaller.Username = null;
            serviceProcessInstaller.Password = null;

            //# Service Information
            serviceInstaller.DisplayName = serviceName;
            serviceInstaller.Description = serviceName;
            serviceInstaller.StartType = ServiceStartMode.Automatic;
           
            // This must be identical to the WindowsService.ServiceBase name
            // set in the constructor of WindowsService.cs
            serviceInstaller.ServiceName = serviceName;

            this.Installers.Add(serviceProcessInstaller);
            this.Installers.Add(serviceInstaller);
        }
        protected override void OnCommitted(System.Collections.IDictionary savedState)
        {
            base.OnCommitted(savedState);
//            new ServiceController(serviceInstaller.ServiceName).Start();
        }
    }
}
