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
    /// Write exceptions to log file
    /// </summary>
    public static class ExceptionHandler
    {

        private static bool isErrorLogRunning = false;
        
        public static Queue<string> errorLogs = new Queue<string>();
        public static void WritetoEventLog(string strMessage)
        {
            try
            {
                if (strMessage.EndsWith("Thread was being aborted.") == false)
                {
                    errorLogs.Enqueue(strMessage);
                }
            }
            catch { }
        }
        public static void LogErrors()
        {
            try
            {
                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\tInfo\t" + Environment.MachineName + "\tLogErrors()\tExceptionHandler.LogErrors()\tTimer Started");
                System.Timers.Timer t = new System.Timers.Timer(10000);
                t.Elapsed += new System.Timers.ElapsedEventHandler(drainQueueException);
                t.Enabled = Agent._runCond;
            }
            catch
            {

            }
        }
        public static void drainQueueException(object source, System.Timers.ElapsedEventArgs e)
        {
            if (!isErrorLogRunning && Agent._runCond)
            {
                isErrorLogRunning = true;
//                DataFileHandler.sendToNotifnQ(DataFileHandler.getDTTZ() + "\t Info \t" + Environment.MachineName + " \t drainQueueException() \t ExceptionHandler.LogErrors() \t Thread Started");
                try
                {
                    Boolean fileheader = false;
                    //                            Directory.CreateDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "Exception");
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    Directory.CreateDirectory(baseDir + "errorlog");
                    string path = baseDir + @"\errorlog\error_" + DateTime.Now.ToString("yyyy-MM-dd") + ".log";
                    if (!File.Exists(path))
                    {
                        fileheader = true;
                    }
                    FileStream stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                    StreamWriter writer = new StreamWriter(stream);
                    StringBuilder dataCollection = new StringBuilder();
                    int i = 0;
                    while (errorLogs.Count > 0)
                    {
                        if (fileheader)
                        {
                            dataCollection.Append("datetime \t level \t method \t information \t exception").Append("\n");
                            fileheader = false;

                        }
                        dataCollection.Append(errorLogs.Dequeue()).Append("\n");
                        i++;
                        if (i > 500)
                        {
                            writer.BaseStream.Seek(0L, SeekOrigin.End);
                            writer.Write(dataCollection);
                            dataCollection.Clear();
                        }
                    }
                    writer.BaseStream.Seek(0L, SeekOrigin.End);
                    writer.Write(dataCollection);
                    writer.Flush();
                    writer.Close();
                    stream.Close();
                    dataCollection.Clear();
                    baseDir = null;
                    path = null;

                }
                catch
                {

                }
                isErrorLogRunning = false;
            }   
        }

        public static void WriteResponse(string filename, string strMessage)
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\Response\\";
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                string path = directoryPath + filename.Replace("/", "");
                FileStream stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                StreamWriter writer = new StreamWriter(stream);
                writer.BaseStream.Seek(0L, SeekOrigin.End);
                writer.WriteLine(strMessage);
                writer.Flush();
                writer.Close();
                stream.Close();
            }
            catch { }
        }
        public static void WriteResponseImage(string filename, System.Drawing.Image image)
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\Response\\";
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                string path = directoryPath + filename.Replace("/", "");
                image.Save(path);
            }
            catch { }
        }
        public static void WriteRequest(string filename, string strMessage)
        {
            try
            {
                string directoryPath =Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location) + "\\Request\\";
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                string path = directoryPath + filename.Replace("/", "");
                FileStream stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                StreamWriter writer = new StreamWriter(stream);
                writer.BaseStream.Seek(0L, SeekOrigin.End);
                writer.WriteLine(strMessage);
                writer.Flush();
                writer.Close();
                stream.Close();
            }
            catch { }
        }
        public static void WriteRepository(string strMessage)
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                string path = directoryPath + "\\Repository.xml";
                if (File.Exists(path)) File.Delete(path);
                FileStream stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                StreamWriter writer = new StreamWriter(stream);
                writer.BaseStream.Seek(0L, SeekOrigin.End);
                writer.WriteLine(strMessage);
                writer.Flush();
                writer.Close();
                stream.Close();
            }
            catch { }
        }
        public static void WriteRunTimeException(string strMessage)
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                string path = directoryPath + "\\RunTimeException_" + DateTime.Now.ToString("dd_MMM_yyy_hh_mm_ss") + ".xml";
                if (File.Exists(path)) File.Delete(path);
                FileStream stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                StreamWriter writer = new StreamWriter(stream);
                writer.BaseStream.Seek(0L, SeekOrigin.End);
                writer.WriteLine(strMessage);
                writer.Flush();
                writer.Close();
                stream.Close();
            }
            catch { }
        }

        #region DebugMessage
        public static bool DebugEnabled = false;
        private static bool isdebugRunning = false;
        public static Queue<string> debugLogs = new Queue<string>();
        static ExceptionHandler()
        {
            if (System.Configuration.ConfigurationManager.AppSettings["debugmode"] != null)
            {
                bool result = false;
                if (bool.TryParse(ConfigurationManager.AppSettings["debugmode"], out result))
                {
                    DebugEnabled = result == false ? false : true;
                }
            }
        }
        public static void LogDebugMessage(DateTime startTime,string methodName)
        {
            if(DebugEnabled==true)
            {
                try
                {
                    debugLogs.Enqueue(new StringBuilder().AppendFormat("{0},{1},{2}", DataFileHandler.getDTTZ(), methodName, (DateTime.Now - startTime).TotalMilliseconds.ToString()).ToString());
                    if (isdebugRunning == false)
                    {
                        isdebugRunning = true;
                        LogDebug();
                    }
                }
                catch { }
            }
        }
        private static void LogDebug()
        {
            try
            {
                new Thread(() =>
                {
                    try
                    {
                        while (true && Agent._runCond)
                        {
                            if (debugLogs.Count > 0)
                            {
                                string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"_Debug.log";
                                FileStream stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                                StreamWriter writer = new StreamWriter(stream);
                                while (debugLogs.Count > 0)
                                {
                                    writer.BaseStream.Seek(0L, SeekOrigin.End);
                                    writer.WriteLine(debugLogs.Dequeue());
                                }
                                writer.Flush();
                                writer.Close();
                                stream.Close();
                            }
                            else
                            {
                                Thread.Sleep(5000);
                            }

                        }
                    }
                    catch
                    {

                    }

                }).Start();
            }
            catch
            {

            }
        }
        #endregion

    }
}
