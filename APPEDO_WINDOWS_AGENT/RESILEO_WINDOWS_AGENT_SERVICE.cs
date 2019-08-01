using AgentCore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RESILEO_WINDOWS_AGENT_v2
{
    partial class RESILEO_WINDOWS_AGENT_SERVICE_v2 : ServiceBase
    {
        /// <summary>
        /// Public Constructor for WindowsService.
        /// - Put all of your Initialization code here.
        /// </summary>
        string serviceName = "RESILEO_WINDOWS_AGENT_SERVICE_v2";

        public RESILEO_WINDOWS_AGENT_SERVICE_v2()
        {
            this.ServiceName = serviceName;
            this.EventLog.Source = serviceName;
            this.EventLog.Log = "Application";

            // These Flags set whether or not to handle that specific
            //  type of event. Set to true if you need it, false otherwise.
            this.CanHandlePowerEvent = true;
            this.CanHandleSessionChangeEvent = true;
            this.CanPauseAndContinue = true;
            this.CanShutdown = true;
            this.CanStop = true;

            //if (!EventLog.SourceExists(serviceName))
            //    EventLog.CreateEventSource(serviceName, "Application");
        }

        /// <summary>
        /// The Main Thread: This is where your Service is Run.
        /// </summary>
        static void Main()
        {
            DateTime startTime = DateTime.Now;
            ////#if(!DEBUG)
            //// ServiceBase.Run(new APPEDO_CLR_PROFILER_AGENT());
            ////#else
            ////            APPEDO_CLR_PROFILER_AGENT myServ = new APPEDO_CLR_PROFILER_AGENT();
            ////            myServ.OnStart(null);
            ////            // here Process is my Service function
            ////            // that will run when my service onstart is call
            ////            // you need to call your own method or function name here instead of Process();
            ////#endif
            //on debug mode the below two lines are to be enabled for Windows Agent SErvice to start
            //RESILEO_WINDOWS_AGENT_SERVICE_v2 myServ = new RESILEO_WINDOWS_AGENT_SERVICE_v2();
            //myServ.OnStart(null);

            ServiceBase.Run(new RESILEO_WINDOWS_AGENT_SERVICE_v2());
          if (ExceptionHandler.DebugEnabled) ExceptionHandler.LogDebugMessage(startTime, System.Reflection.MethodBase.GetCurrentMethod().Name);
        }

        /// <summary>
        /// Dispose of objects that need it here.
        /// </summary>
        /// <param name="disposing">Whether or not disposing is going on.</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <summary>
        /// OnStart: Put startup code here
        ///  - Start threads, get inital data, etc.
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            DateTime startTime=DateTime.Now;
            base.OnStart(args);
            try
            {
                new Agent("start").StartAgent();
              
            }
            catch (Exception ex)
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\t OnStart()\tIssue in Starting Service " + ex.Message);
                ExceptionHandler.WritetoEventLog(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\t OnStart()\tIssue in Starting Service " + ex.Message + ex.StackTrace);
            }
        }
     
        /// <summary>
        /// OnStop: Put your stop code here
        /// - Stop threads, set final data, etc.
        /// </summary>
        protected override void OnStop()
        {
            DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tWarning\t" + Environment.MachineName + "\tOnStop()\tService Stop Initiated");
            new Agent("stop").StopAgent();
            base.OnStop();
        }

        /// <summary>
        /// OnPause: Put your pause code here
        /// - Pause working threads, etc.
        /// </summary>
        protected override void OnPause()
        {
            base.OnPause();
        }

        /// <summary>
        /// OnContinue: Put your continue code here
        /// - Un-pause working threads, etc.
        /// </summary>
        protected override void OnContinue()
        {
            base.OnContinue();
        }

        /// <summary>
        /// OnShutdown(): Called when the System is shutting down
        /// - Put code here when you need special handling
        ///   of code that deals with a system shutdown, such
        ///   as saving special data before shutdown.
        /// </summary>
        protected override void OnShutdown()
        {
            base.OnShutdown();
        }

        /// <summary>
        /// OnCustomCommand(): If you need to send a command to your
        ///   service without the need for Remoting or Sockets, use
        ///   this method to do custom methods.
        /// </summary>
        /// <param name="command">Arbitrary Integer between 128 & 256</param>
        protected override void OnCustomCommand(int command)
        {
            //  A custom command can be sent to a service by using this method:
            //#  int command = 128; //Some Arbitrary number between 128 & 256
            //#  ServiceController sc = new ServiceController("NameOfService");
            //#  sc.ExecuteCommand(command);

            base.OnCustomCommand(command);
        }

        /// <summary>
        /// OnPowerEvent(): Useful for detecting power status changes,
        ///   such as going into Suspend mode or Low Battery for laptops.
        /// </summary>
        /// <param name="powerStatus">The Power Broadcase Status (BatteryLow, Suspend, etc.)</param>
        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            return base.OnPowerEvent(powerStatus);
        }

        /// <summary>
        /// OnSessionChange(): To handle a change event from a Terminal Server session.
        ///   Useful if you need to determine when a user logs in remotely or logs off,
        ///   or when someone logs into the console.
        /// </summary>
        /// <param name="changeDescription"></param>
        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
            base.OnSessionChange(changeDescription);
        }
    }
}
