﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DeltaSockets
{
    /// <summary>
    /// Class SocketServer.
    /// </summary>
    public class SocketServer
    {
        public SocketServerConsole myLogger = new SocketServerConsole(null);

        public const int minBufferSize = 1024;

        /// <summary>
        /// The lerped port
        /// </summary>
        public const int DefPort = 7776;

        /// <summary>
        /// The server socket
        /// </summary>
        public Socket ServerSocket;

        /// <summary>
        /// The permision
        /// </summary>
        public SocketPermission Permision;

        /// <summary>
        /// The ip
        /// </summary>
        public IPAddress IP;

        /// <summary>
        /// The port
        /// </summary>
        public int Port;

        private IPEndPoint _endpoint;
        private byte[] byteData = new byte[minBufferSize];

        /// <summary>
        /// All done
        /// </summary>
        public static ManualResetEvent allDone = new ManualResetEvent(false);

        /// <summary>
        /// The routing table
        /// </summary>
        public static Dictionary<int, Socket> routingTable = new Dictionary<int, Socket>();

        private readonly static List<int> closedClients = new List<int>();

        //private static Tuple<ulong, List<byte>>

        private static bool debug;

        //private Logger logger;

        internal IPEndPoint IPEnd
        {
            get
            {
                if (IP != null)
                {
                    if (_endpoint == null)
                        _endpoint = new IPEndPoint(IP, Port);
                    return _endpoint;
                }
                else return null;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketServer"/> class.
        /// </summary>
        /// <param name="debug">if set to <c>true</c> [debug].</param>
        /// <param name="doConnection">if set to <c>true</c> [do connection].</param>
        public SocketServer(bool debug, bool doConnection = false) :
            this(new SocketPermission(NetworkAccess.Accept, TransportType.Tcp, "", SocketPermission.AllPorts), Dns.GetHostEntry("").AddressList[0], DefPort, SocketType.Stream, ProtocolType.Tcp, debug, doConnection)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketServer"/> class.
        /// </summary>
        /// <param name="ip">The ip.</param>
        /// <param name="port">The port.</param>
        /// <param name="debug">if set to <c>true</c> [debug].</param>
        /// <param name="doConnection">if set to <c>true</c> [do connection].</param>
        public SocketServer(string ip, int port, bool debug, bool doConnection = false) :
            this(new SocketPermission(NetworkAccess.Accept, TransportType.Tcp, "", SocketPermission.AllPorts), IPAddress.Parse(ip), port, SocketType.Stream, ProtocolType.Tcp, debug, doConnection)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketServer"/> class.
        /// </summary>
        /// <param name="permission">The permission.</param>
        /// <param name="ipAddr">The ip addr.</param>
        /// <param name="port">The port.</param>
        /// <param name="sType">Type of the s.</param>
        /// <param name="pType">Type of the p.</param>
        /// <param name="curDebug">if set to <c>true</c> [current debug].</param>
        /// <param name="doConnection">if set to <c>true</c> [do connection].</param>
        public SocketServer(SocketPermission permission, IPAddress ipAddr, int port, SocketType sType, ProtocolType pType, bool curDebug, bool doConnection = false)
        {
            permission.Demand();

            IP = ipAddr;
            Port = port;

            debug = curDebug;
            //logger = new Logger(Path.Combine(Path.GetDirectoryName(Path.Combine(Application.dataPath, LerpedCore.defaultLogFilePath)), "server-logger.log"));

            ServerSocket = new Socket(ipAddr.AddressFamily, sType, pType);

            if (doConnection) ServerSocket.Bind(IPEnd);
        }

        /// <summary>
        /// Comes the alive.
        /// </summary>
        public void ComeAlive()
        {
            if (IPEnd != null)
            {
                try
                {
                    ServerSocket.Bind(IPEnd);
                    ServerSocket.Listen(10); //El servidor se prepara para recebir la conexion de 10 clientes simultaneamente

                    Console.WriteLine("Waiting for a connection...");
                    ServerSocket.BeginAccept(new AsyncCallback(OnAccept), ServerSocket);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            else Console.WriteLine("Destination IP isn't defined!");
        }

        private void OnAccept(IAsyncResult ar)
        {
            try
            {
                Socket clientSocket = ServerSocket.EndAccept(ar);

                //Start listening for more clients
                ServerSocket.BeginAccept(new AsyncCallback(OnAccept), null);

                //Once the client connects then start receiving the commands from it
                clientSocket.BeginReceive(byteData, 0, byteData.Length, SocketFlags.None,
                    new AsyncCallback(OnReceive), clientSocket);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void OnReceive(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState;
                int bytesRead = handler.EndReceive(ar);

                SocketMessage sm = null;
                bool serialized = SocketManager.Deserialize(byteData, out sm, SocketDbgType.Server);

                if (bytesRead > 0 && bytesRead < minBufferSize)
                {
                    //string str = Encoding.Unicode.GetString(byteData, 0, bytesRead); //Obtiene la longitud en bytes de los datos pasados y los transforma en una string
                    Console.WriteLine("Server readed block of {0} bytes", bytesRead);

                    if (sm != null)
                    {
                        if (sm.TypeString == typeof(string).Name)
                            DoStringAction(sm, handler);
                        else
                            DoServerError("Not supported type!");
                    }
                    else
                    {
                        if (serialized)
                            myLogger.Log("Null object passed though the socket! Ignore...");
                        else
                            DoServerError("Cannot deserialize!");
                    }
                }
                else if (bytesRead > minBufferSize)
                {
                    //Check if deserialize is true && sm != null

                    Console.WriteLine("Server is reading big block of {0} bytes!", bytesRead);
                    SendToOtherClients(sm, bytesRead);
                    byteData = new byte[minBufferSize]; //Reset the buffer
                }

                //Continua escuchando, para listar el próximo mensaje, recursividad asíncrona.
                handler.BeginReceive(byteData, 0, byteData.Length, SocketFlags.None,
                    new AsyncCallback(OnReceive), handler);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void DoStringAction(SocketMessage sm, Socket handler)
        {
            switch (sm.StringValue)
            {
                case "<conn>":
                    routingTable.Add(sm.id, handler);
                    break;
                case "<close_clients>":
                    routingTable[sm.id].Send(Encoding.Unicode.GetBytes("<close>")); //First, close the client that
                    foreach (KeyValuePair<int, Socket> soc in routingTable)
                        if (soc.Key != sm.id) //Then, close the others one
                            soc.Value.Send(Encoding.Unicode.GetBytes("<close>"));
                    break;
                case "<client_closed>":
                    closedClients.Add(sm.id);
                    if (closedClients.Count == routingTable.Count)
                        CloseServer(); //Close the server, when all the clients has been closed.
                    break;
                case "<stop>":
                    CloseServer();
                    break;
                default:
                    string blockSizeId = "Block_Size:";
                    if (sm.StringValue.StartsWith(blockSizeId))
                    {
                        int bytesToRead = int.Parse(sm.StringValue.Substring(blockSizeId.Length - 1));
                        byteData = new byte[bytesToRead]; //The next message will be of this size...
                    }
                    else
                        DoServerError("Cannot de-encrypt the message!");
                    break;
            }
        }

        private void DoServerError(string msg)
        {
            CloseServer();
            Console.WriteLine("CLOSING SERVER due to: " + msg);
        }

        private void SendToOtherClients(SocketMessage sm, int bytesRead)
        {
            myLogger.Log("---------------------------");
            myLogger.Log("Client with ID {0} sent {1} bytes (JSON).", sm.id, bytesRead);
            myLogger.Log("Sending to the other clients.");
            myLogger.Log("---------------------------");
            myLogger.Log("");

            //Send to the other clients
            foreach (KeyValuePair<int, Socket> soc in routingTable)
                if (soc.Key != sm.id)
                    soc.Value.Send(byteData);
        }

        /// <summary>
        /// Called when [send].
        /// </summary>
        /// <param name="ar">The ar.</param>
        public void OnSend(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;
                client.EndSend(ar);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Closes the server.
        /// </summary>
        public void CloseServer()
        {
            if (ServerSocket.Connected)
            {
                Console.WriteLine("Closing server");
                ServerSocket.Shutdown(SocketShutdown.Receive);
                ServerSocket.Close();
            }
            else Console.WriteLine("If you want to close something, you have to be first connected!");
        }

        public void Dispose()
        {
            Console.WriteLine("Disposing server");
            CloseServer();
        }
    }

    public class SocketServerConsole
    {
        private readonly Control printer;

        private SocketServerConsole()
        {
        }

        public SocketServerConsole(Control c)
        {
            printer = c;
        }

        public void Log(string str, params object[] str0)
        {
            Log(string.Format(str, str0));
        }

        public void Log(string str)
        {
            Console.WriteLine(str);
#if LOG_SERVER
            if (printer != null)
            {
                if (printer.InvokeRequired) //De esto hice una versión mejorada
                    printer.Invoke(new MethodInvoker(() => { printer.Text += str + Environment.NewLine; }));
            }
            else
                Console.WriteLine("You must define 'myLogger' field of type 'SocketServerConsole' inside 'SocketServer' in order to use this feature.");
#endif
        }
    }
}