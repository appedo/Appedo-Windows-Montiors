using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace AgentCore
{
    /// <summary>
    /// Utility class used to communicate with Appedo through TCP.
    /// Protocol format:
    /// 
    /// ...............................
    /// Operation: Body-length
    /// headername1= headervalue1
    /// headername2= headervalue2
    /// headernameN= headervalueN
    /// 
    /// Body
    /// ................................
    /// 
    /// Example1:
    /// 
    /// TEST: 0
    /// 
    /// 
    /// .................................
    /// Example2:
    /// 
    /// TEST: 6
    /// runid= 522
    /// 
    /// detail
    /// .................................
    /// </summary>
    public class Trasport
    {

        #region The private fields

        private int ReadBufferSize = 8192;
        private string _ipaddress = string.Empty;

        #endregion

        #region The public property

        public string IPAddressStr { get { return _ipaddress; } set { _ipaddress = value; } }
        public TcpClient tcpClient;
        public bool Connected
        {
            get
            {
                if (tcpClient.Connected == true) return true;
                else return false;
            }
            private set { }
        }

        #endregion

        #region The constructor

        /// <summary>
        /// Create object using ipaddress and port. Request time out is 120sec
        /// </summary>
        /// <param name="ipaddress"></param>
        /// <param name="port"></param>
        public Trasport(string ipaddress, string port)
        {
            _ipaddress = ipaddress;
            tcpClient = Connect(ipaddress, port);
            tcpClient.ReceiveTimeout = 120000;
        }

        /// <summary>
       /// Create object using ipaddress and port. Request time is given by client
       /// </summary>
       /// <param name="ipaddress"></param>
       /// <param name="port"></param>
       /// <param name="requesttimeout">Receive time out</param>
        public Trasport(string ipaddress, string port, int requesttimeout)
        {
            _ipaddress = ipaddress;
            tcpClient = Connect(ipaddress, port);
            tcpClient.ReceiveTimeout = requesttimeout;
        }

        /// <summary>
        /// Create object using TcpClient.
        /// </summary>
        /// <param name="client"></param>
        public Trasport(TcpClient client)
        {
            _ipaddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            tcpClient = client;
        }

        #endregion

        #region The public methods

        /// <summary>
        /// To close TCP connection
        /// </summary>
        public void Close()
        {
            if (tcpClient.Connected == true) tcpClient.Close();
        }

        /// <summary>
        /// Read data from tcp client stream.
        /// </summary>
        /// <returns>Received data and formatted as per protocol</returns>
        public TrasportData Receive()
        {
            Stream stream = tcpClient.GetStream();
            TrasportData objTrasportData = new TrasportData();

            StringBuilder header = new StringBuilder();
            StringBuilder response = new StringBuilder();
            int readCount = 0;
            int contentLength = 0;

            header.Append(ReadHeader(stream));
            objTrasportData.Operation = new Regex("(.*): ([0-9]*)").Match(header.ToString()).Groups[1].Value;

            //Read header info.
            foreach (Match match in (new Regex("(.*)= (.*)\r\n").Matches(header.ToString())))
            {
                objTrasportData.Header.Add(match.Groups[1].Value, match.Groups[2].Value);
            }

            //Read body content length.
            contentLength = Convert.ToInt32(new Regex("(.*): ([0-9]*)").Match(header.ToString()).Groups[2].Value);
            byte[] bytes = new byte[contentLength];

            //Read request body
            while (readCount < contentLength)
            {
                readCount += stream.Read(bytes, readCount, contentLength - readCount);

            }
            objTrasportData.DataStream.Write(bytes, 0, contentLength);

            //Seek memory stream to begin
            if (objTrasportData.DataStream.Length > 0) objTrasportData.DataStream.Seek(0, SeekOrigin.Begin);
            return objTrasportData;
        }

        /// <summary>
        /// Read data from tcp client stream and store in file.
        /// </summary>
        /// <param name="filePath">Storing file path</param>
        /// <returns>Received data and formatted as per protocol</returns>
        public TrasportData Receive(string filePath)
        {
            Stream stream = tcpClient.GetStream();
            TrasportData objTrasportData = new TrasportData();

            StringBuilder header = new StringBuilder();
            StringBuilder response = new StringBuilder();
            int readCount = 0;
            int contentLength = 0;

            header.Append(ReadHeader(stream));
            objTrasportData.Operation = new Regex("(.*): ([0-9]*)").Match(header.ToString()).Groups[1].Value;

            //Read header info.
            foreach (Match match in (new Regex("(.*)= (.*)\r\n").Matches(header.ToString())))
            {
                objTrasportData.Header.Add(match.Groups[1].Value, match.Groups[2].Value);
            }

            //Read body content length.
            contentLength = Convert.ToInt32(new Regex("(.*): ([0-9]*)").Match(header.ToString()).Groups[2].Value);

            byte[] bytes = new byte[ReadBufferSize];

            //Create file to store received data.
            using (FileStream file = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                while (contentLength > 0)
                {
                    readCount = 0;

                    //Read data bucket by bucket(bucket size is ReadBufferSize)
                    if (contentLength >= ReadBufferSize)
                    {
                        readCount = stream.Read(bytes, 0, ReadBufferSize);
                        file.Write(bytes, 0, readCount);
                        contentLength = contentLength - readCount;
                    }
                     //Read last bucket data 
                    else
                    {
                        readCount = stream.Read(bytes, 0, contentLength);
                        file.Write(bytes, 0, readCount);
                        contentLength = contentLength - readCount;
                    }
                }
            }
            objTrasportData.FilePath = filePath;
            return objTrasportData;
        }

        /// <summary>
        /// Read data from tcp client stream and notify totalByte, totalByteRecceived, success information.
        /// </summary>
        /// <param name="filePath">File path where to store data.</param>
        /// <param name="totalByte">Total byte to be received.</param>
        /// <param name="totalByteRecceived">Total byte received.</param>
        /// <param name="success">Whether data received successfully.</param>
        /// <returns></returns>
        public TrasportData Receive(string filePath, ref long totalByte, ref long totalByteRecceived, ref bool success)
        {
            Stream stream = tcpClient.GetStream();
            TrasportData objTrasportData = new TrasportData();

            StringBuilder header = new StringBuilder();
            StringBuilder response = new StringBuilder();
            int readCount = 0;
            int contentLength = 0;

            header.Append(ReadHeader(stream));
            objTrasportData.Operation = new Regex("(.*): ([0-9]*)").Match(header.ToString()).Groups[1].Value;

            //Read header info.
            foreach (Match match in (new Regex("(.*)= (.*)\r\n").Matches(header.ToString())))
            {
                objTrasportData.Header.Add(match.Groups[1].Value, match.Groups[2].Value);
            }
            contentLength = Convert.ToInt32(new Regex("(.*): ([0-9]*)").Match(header.ToString()).Groups[2].Value);
            totalByte = contentLength;

            byte[] bytes = new byte[ReadBufferSize];

            //Create file to store received data.
            using (FileStream file = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                while (contentLength > 0)
                {
                    //If user stopped receiving data.
                    if (success == false)
                    {
                        break;
                    }
                    readCount = 0;

                    //Read data bucket by bucket(bucket size is ReadBufferSize)
                    if (contentLength >= ReadBufferSize)
                    {
                        readCount = stream.Read(bytes, 0, ReadBufferSize);
                        totalByteRecceived += readCount;
                        file.Write(bytes, 0, readCount);
                        contentLength = contentLength - readCount;
                    }
                    //Read last bucket data 
                    else
                    {
                        readCount = stream.Read(bytes, 0, contentLength);
                        totalByteRecceived += readCount;
                        file.Write(bytes, 0, readCount);
                        contentLength = contentLength - readCount;
                    }
                }
            }
            objTrasportData.FilePath = filePath;
            return objTrasportData;
        }

        /// <summary>
        ///  Write data to tcp client stream and notify totalByte, totalByteUploaded,success information.
        /// </summary>
        /// <param name="objTrasportData">Data to be send</param>
        /// <param name="totalByte">Total byte to be send</param>
        /// <param name="totalByteUploaded">Total byte sent</param>
        /// <param name="success">Whether data uploaded successfully.</param>
        public void Send(TrasportData objTrasportData, ref long totalByte, ref long totalByteUploaded, ref bool success)
        {
            Socket socket = tcpClient.Client;
            socket.Send(objTrasportData.GetHeaderBytes());

            //Send data
            if (objTrasportData.FilePath == string.Empty)
            {
                totalByte = objTrasportData.DataStream.Length;
                totalByteUploaded = socket.Send(objTrasportData.DataStream.ToArray());

            }
            //Send file data
            else
            {
                byte[] buffer = new byte[8192];
                int readCount = 0;

                //Read file content
                using (FileStream file = new FileStream(objTrasportData.FilePath, FileMode.Open, FileAccess.Read))
                {
                    totalByte = file.Length;
                    while ((readCount = file.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        //If user break
                        if (success == false)
                        {
                            break;
                        }
                        totalByteUploaded += socket.Send(buffer, 0, readCount, SocketFlags.None);
                    }
                }
            }
        }

        /// <summary>
        /// Write data to tcp client stream.
        /// </summary>
        /// <param name="objTrasportData">Data to be send</</param>
        public void Send(TrasportData objTrasportData)
        {
            Socket socket = tcpClient.Client;
            socket.Send(objTrasportData.GetHeaderBytes());

            //Send data
            if (objTrasportData.FilePath == string.Empty)
            {
                socket.Send(objTrasportData.DataStream.ToArray());
            }
            //Send file data
            else
            {
                byte[] buffer = new byte[8192];
                int readCount = 0;
                int sum = 0;

                //Read file content
                using (FileStream file = new FileStream(objTrasportData.FilePath, FileMode.Open, FileAccess.Read))
                {
                    while ((readCount = file.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        socket.Send(buffer, 0, readCount, SocketFlags.None);
                        sum += readCount;
                    }
                }
            }
        }

        /// <summary>
        /// To send GZip file through tcp client
        /// </summary>
        /// <param name="objTrasportData">Data to be send</param>
        public void SendGZipFile(TrasportData objTrasportData)
        {
            Socket socket = tcpClient.Client;
            socket.Send(objTrasportData.GetHeaderBytes());
            //Send data
            if (objTrasportData.FilePath == string.Empty)
            {
                socket.Send(objTrasportData.DataStream.ToArray());
            }
            //Read GZipFile content
            else
            {
                byte[] buffer = new byte[8192];
                int readCount = 0;
                int sum = 0;

                //Read GZipFile content
                using (Stream file = new GZipStream(new FileStream(objTrasportData.FilePath, FileMode.Open, FileAccess.Read), CompressionMode.Compress))
                {
                    while ((readCount = file.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        socket.Send(buffer, 0, readCount, SocketFlags.None);
                        sum += readCount;
                    }
                }
            }
        }

        #endregion

        #region The private methods

        /// <summary>
        /// Establish connection to the given ipaddress and port.
        /// </summary>
        /// <param name="ipaddress">Ipaddress where we need to establish connection</param>
        /// <param name="port"></param>
        /// <returns></returns>
        private TcpClient Connect(string ipaddress, string port)
        {
            TcpClient client = new TcpClient();
            var result = client.BeginConnect(IPAddress.Parse(ipaddress), int.Parse(port), null, null);
            result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5));

            //Unable to connect.
            if (client.Connected == false)
            {
                throw new Exception("Failed to connect " + ipaddress);
            }
            client.ReceiveTimeout = 600000;
            client.SendTimeout = 600000;
            return client;
        }

        #endregion

        #region The static varialbles and methods

        /// <summary>
        /// Read header as per protocol from given stream.
        /// </summary>
        /// <param name="stream">Stream where data available</param>
        /// <returns>Header string</returns>
        private static string ReadHeader(Stream stream)
        {
            StringBuilder header = new StringBuilder();
            byte[] bytes = new byte[10];
            StringBuilder response = new StringBuilder();

            while (stream.Read(bytes, 0, 1) > 0)
            {
                header.Append(Encoding.Default.GetString(bytes, 0, 1));
                response.Append(Encoding.Default.GetString(bytes, 0, 1));

                if (bytes[0] == '\n' && header.ToString().EndsWith("\r\n\r\n"))
                    break;
            }
            return header.ToString();
        }

        #endregion

    }

    /// <summary>
    /// Data model used for Transport class
    /// </summary>
    public class TrasportData
    {

        #region The private fields

        private string _filePath = string.Empty;
        private string _operation = string.Empty;
        private Dictionary<string, string> _header = new Dictionary<string, string>();
        private MemoryStream _dataStream = new MemoryStream();

        #endregion

        #region The public property

        /// <summary>
        /// Buffer to store data.
        /// </summary>
        public MemoryStream DataStream
        {
            get { return _dataStream; }
            set { _dataStream = value; }
        }

        /// <summary>
        /// Store header values.
        /// </summary>
        public Dictionary<string, string> Header
        {
            get { return _header; }
            set { _header = value; }
        }

        /// <summary>
        /// Convert data in buffer into string.
        /// </summary>
        public string DataStr
        {
            get
            {
                if (DataStream.Length == 0) return string.Empty;
                else return ASCIIEncoding.ASCII.GetString(DataStream.ToArray());
            }

            set
            {
                if (value != null && value != string.Empty)
                {
                    DataStream.Write(ASCIIEncoding.ASCII.GetBytes(value), 0, value.Length);
                }
            }
        }

        /// <summary>
        /// File path to store data
        /// </summary>
        public string FilePath
        {
            get { return _filePath; }
            set { _filePath = value; }
        }

        /// <summary>
        /// Operation name.
        /// </summary>
        public string Operation
        {
            get { return _operation; }
            set { _operation = value; }
        }

        #endregion

        #region The constructor

        public TrasportData() { }

        /// <summary>
        /// To create data object with file type data.
        /// </summary>
        /// <param name="operation">Operation name</param>
        /// <param name="header">List of headers</param>
        /// <param name="filePath">File path where data exist</param>
        public TrasportData(string operation, Dictionary<string, string> header, string filePath)
        {
            this._operation = operation;
            this._header = header;
            this._filePath = filePath;
        }

        /// <summary>
        /// To create data object with string type data.
        /// </summary>
        /// <param name="operation">Operation name</param>
        /// <param name="data">Data string</param>
        /// <param name="header">List of headers</param>
        public TrasportData(string operation, string data, Dictionary<string, string> header)
        {
            this._operation = operation;
            this.DataStr = data;
            this._header = header;
        }

        #endregion

        #region The public property

        /// <summary>
        /// To convert list of header into string using protocol and convert that string into array of bytes. 
        /// </summary>
        /// <returns>Headers byte array</returns>
        public byte[] GetHeaderBytes()
        {
            StringBuilder header = new StringBuilder();
            header.AppendLine(string.Format("{0}: {1}", _operation, _filePath == string.Empty ? _dataStream.Length.ToString() : new FileInfo(_filePath).Length.ToString()));

            //Header has item
            if (_header != null && _header.Count > 0)
            {
                //Get header one by one
                foreach (string key in Header.Keys)
                {
                    header.AppendLine(string.Format("{0}= {1}", key, _header[key]));
                }
            }
            header.AppendLine();
            return ASCIIEncoding.ASCII.GetBytes(header.ToString());
        }

        /// <summary>
        /// Save buffer data into the file.
        /// </summary>
        /// <param name="filePath">File path where we have to store buffer data</param>
        public void Save(string filePath)
        {
            try
            {
                //Delete the file if already exists.
                if (File.Exists(filePath)) File.Delete(filePath);

                //Create file
                using (var fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    //Copy buffer data into file stream.
                    _dataStream.CopyTo(fileStream);
                }
            }
            catch (Exception ex)
            {
                ExceptionHandler.WritetoEventLog(ex.StackTrace + Environment.NewLine + ex.Message);
            }
        }

        #endregion

    }
}
