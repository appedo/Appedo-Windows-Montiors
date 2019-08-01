using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Configuration;
using System.Text;

namespace AgentCore
{
    /// <summary>
    /// Write Data to log file
    /// </summary>
    public static class DataFileHandler
    {
        //private static bool isDataLogRunning = false;
        //private static bool isNotifLogRunning = false;
        //private static bool isdebugLogRunning = false;
        public static Queue<string> dataQueue = new Queue<string>();
        public static Queue<string> notifnQ = new Queue<string>();
        public static Queue<string> debugQ = new Queue<string>();

        public static void sendToDataQueue(string strMessage)
        {
            try
            {
                dataQueue.Enqueue(strMessage);
                //if (!isDataLogRunning)
                //{
                //    isDataLogRunning = true;
                //    dataQueueWriteToFile(); 
                //}
            }
            catch { }
        }
        public static void dataQueueWriteToFile()
        {
            try
            {
                sendToNotifnQ(getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tdataQueueWriteToFile()\tAgent Version " + Assembly.GetEntryAssembly().GetName().Version.ToString());
                sendToNotifnQ(getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tdataQueueWriteToFile()\tThread Started");
                new Thread(() =>
                {
                    try
                    {
                        while( Agent._runCond)
                        {
                            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                            Directory.CreateDirectory(baseDir + "datalog");
                            StreamWriter file = null;
                            string path = baseDir + @"\datalog\counterData_" + DateTime.Now.ToString("yyyy-MM-dd") + ".dat";
                            if (!File.Exists(path)) { file = File.CreateText(path); }
                            else { file = File.AppendText(path); }
                            StringBuilder dataCollection = new StringBuilder();
                            int i = 0;
                            while (dataQueue.Count > 0)
                            {
                                dataCollection.Append(dataQueue.Dequeue()).Append("\n");
                                i++;
                                if (i > 500)
                                {
                                    file.BaseStream.Seek(0L, SeekOrigin.End);
                                    file.Write(dataCollection);
                                    dataCollection.Clear();
                                }
                            }
                            file.BaseStream.Seek(0L, SeekOrigin.End);
                            file.Write(dataCollection);
                            file.Flush();
                            file.Close();
                            dataCollection.Clear();
                            Thread.Sleep(10000);
                        }
//                        isDataLogRunning = false;
                    }
                    catch (Exception ex)
                    {
                        ExceptionHandler.WritetoEventLog(getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tDataFileHandler.dataQueueWriteToFile()\tInside Thread\t" + ex.Message);
                    }

                }).Start();
            }
            catch (Exception ex)
            {
                ExceptionHandler.WritetoEventLog(getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tDataFileHandler.dataQueueWriteToFile()\tbefore Thread creation\t" + ex.Message);
            }
        }

        /// <summary>
        /// Write Notification to log file
        /// </summary>
        public static void sendToNotifnQ(string strMessage)
        {
            try
            {
                strMessage = "notification###"+AgentCore.Agent._uuid +"\t"+ strMessage;
                notifnQ.Enqueue(strMessage);
                //if (!isNotifLogRunning)
                //{
                //    isNotifLogRunning = true;
                //    notifnQWriteToFile();
                //}
            }
            catch { }
        }
        public static void notifnQWriteToFile()
        {
            try
            {
                sendToNotifnQ(getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tnotifnQWriteToFile()\tThread Started");
                new Thread(() =>
                {
                    try
                    {
                        while (Agent._runCond)
                        {
                            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                            Directory.CreateDirectory(baseDir + "notificationlog");
                            StreamWriter file = null;
                            string path = baseDir + @"\notificationlog\notification_" + DateTime.Now.ToString("yyyy-MM-dd") + ".dat";
                            if (!File.Exists(path)) 
                            { 
                                file = File.CreateText(path); 
                                //file.WriteLine("datetime \t level \t method \t information \t exception");
                            }
                            else { file = File.AppendText(path); }
                            StringBuilder dataCollection = new StringBuilder();
                            int i = 0;
                            while (notifnQ.Count > 0)
                            {
                                dataCollection.Append(notifnQ.Dequeue()).Append("\n");
                                i++;
                                if (i > 500)
                                {
                                    file.BaseStream.Seek(0L, SeekOrigin.End);
                                    file.Write(dataCollection);
                                    dataCollection.Clear();
                                }
                            }
                            file.BaseStream.Seek(0L, SeekOrigin.End);
                            file.Write(dataCollection);
                            file.Flush();
                            file.Close();
                            dataCollection.Clear();
                            Thread.Sleep(10000);
                        }
//                        isNotifLogRunning = false;
                    }
                    catch (Exception ex)
                    {
                        ExceptionHandler.WritetoEventLog(getDTTZ() +"\tCritical\t"+Environment.MachineName+ "\tDataFileHandler.notifnQWriteToFile()\tafter Thread creation\t" + ex.Message);
                    }

                }).Start();
            }
            catch (Exception ex)
            {
                ExceptionHandler.WritetoEventLog(getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tDataFileHandler.notifnQWriteToFile()\tbefore Thread creation\t" + ex.Message);
            }
        }
        /// <summary>
        /// Write Debug info to log file
        /// </summary>
        public static void sendToDebugQ(string strMessage)
        {
            try
            {
                debugQ.Enqueue(strMessage);
                //if (!isdebugLogRunning)
                //{
                //    isdebugLogRunning = true;
                //    debugQWriteToFile();
                //}
            }
            catch { }
        }
        public static void debugQWriteToFile()
        {
            try
            {
                sendToNotifnQ(getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tdebugQWriteToFile()\tThread Started");
                new Thread(() =>
                    {
                        try
                        {
                            while (Agent._runCond)
                            {
                                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                                Directory.CreateDirectory(baseDir + "debugdata");
                                StreamWriter file = null;
                                string path = baseDir + @"\debugdata\debug_" + DateTime.Now.ToString("yyyy-MM-dd") + ".dat";
                                if (!File.Exists(path)) { file = File.CreateText(path); file.WriteLine("datetime\tlevel\tmethod\tinformation\texception"); }
                                else { file = File.AppendText(path); }
                                StringBuilder dataCollection = new StringBuilder();
                                int i = 0;
                                while (debugQ.Count > 0)
                                {
                                    dataCollection.Append(debugQ.Dequeue()).Append("\n");
                                    i++;
                                    if (i > 500)
                                    {
                                        file.BaseStream.Seek(0L, SeekOrigin.End);
                                        file.Write(dataCollection);
                                        dataCollection.Clear();
                                    }
                                }
                                file.BaseStream.Seek(0L, SeekOrigin.End);
                                file.Write(dataCollection);
                                file.Flush();
                                file.Close();
                                dataCollection.Clear();
                                Thread.Sleep(10000);
                            }
                            //isdebugLogRunning = false;
                        }
                        catch (Exception ex)
                        {
                            ExceptionHandler.WritetoEventLog(getDTTZ()+"\tCritical\t"+Environment.MachineName + "\tDataFileHandler.debugQWriteToFile()\tafter Thread creation\t" + ex.Message);
                        }

                    }).Start();
            }
            catch (Exception ex)
            {
                ExceptionHandler.WritetoEventLog(getDTTZ() + "\tCritical\t" + Environment.MachineName + "\tDataFileHandler.debugQWriteToFile()\tbefore Thread creation\t" + ex.Message);
            }
        }

        public static string getDTTZ()
        {
            return DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzzzzz");
        }
    }
}
