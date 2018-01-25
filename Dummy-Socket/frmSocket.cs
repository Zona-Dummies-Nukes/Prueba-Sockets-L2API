﻿using DeltaSockets;
using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace Dummy_Socket
{
    public enum SocketState { NotStarted, ClientStarted, ServerStarted }

    public partial class frmSocket : EnhancedForm<frmSocket>
    {
        public SocketClient client;
        public SocketServer server;

        public bool disableAutoName;

        public int socketID;

        private SocketState _state;

        private static BackgroundWorker workerObject;

        public SocketState state
        {
            get
            {
                return _state;
            }
            set
            {
                SocketState oldstate = _state;
                _state = value;
                if (_state == SocketState.ClientStarted)
                {
                    clientConnect.Text = "¡Conectado!";
                    DisableControls(false);
                }
                else if (_state == SocketState.ServerStarted)
                {
                    startServer.Text = "¡Server arrancado!";
                    DisableControls(true);
                }
                else
                {
                    if (oldstate == SocketState.ClientStarted)
                    {
                        clientConnect.Text = "Conectarse";
                        EnableControls(true);
                    }
                    else if (oldstate == SocketState.ServerStarted)
                    {
                        startServer.Text = "Arrancar servidor";
                        EnableControls(false);
                    }
                }
            }
        }

        public const string notValidClientConn = "Por favor, revisa que los campos IP y puerto sean válidos en la pestaña clientes.",
                            notValidServerConn = "Por favor, revisa que los campos IP y puerto sean válidos en la pestaña servidores.";

        public frmSocket()
        {
            InitializeComponent();
        }

        private void frmSocket_Load(object sender, EventArgs e)
        {
            if (!disableAutoName)
                clientName.Text = string.Format("Client{0}", new Random().Next(0, 9999));
        }

        public void ShowServerTab()
        {
            if (tabControl1.SelectedTab != tabPage1)
                tabControl1.SelectedTab = tabPage1;
        }

        public void ShowClientTab()
        {
            if (tabControl1.SelectedTab != tabPage2)
                tabControl1.SelectedTab = tabPage2;
        }

        public void Start(bool isClient)
        {
            bool succ = false;
            socketID = ++frmMain.lastID;
            if (isClient)
            {
                if (client == null)
                {
                    if (ValidateClient())
                    {
                        workerObject = new BackgroundWorker() { WorkerSupportsCancellation = true };
                        workerObject.DoWork += (s, ev) =>
                        {
                            client.DoConnection();
                        };

                        client = new SocketClient(clientIP.Text, (ushort)clientPort.Value, ClientAction());
                        client.myLogger = new SocketClientConsole(receivedMsgs, false);
                        workerObject.RunWorkerAsync();

                        succ = true;
                    }
                    else
                        client.myLogger.LogError(notValidClientConn);
                }
            }
            else
            {
                if (ValidateServer())
                {
                    server = new SocketServer(new SocketPermission(NetworkAccess.Accept, TransportType.Tcp, "", SocketPermission.AllPorts), IPAddress.Parse(serverIP.Text), (int)serverPort.Value, SocketType.Stream, ProtocolType.Tcp, true);
                    server.myLogger = new SocketServerConsole(serverLog);
                    server.StartServer();

                    succ = true;
                }
                else
                    client.myLogger.LogError(notValidServerConn);
            }
            if (succ)
            {
                state = isClient ? SocketState.ClientStarted : SocketState.ServerStarted;
                frmMain.socketIns.Add(socketID, new SocketInstance(this, isClient));
            }
        }

        private Action<object> ClientAction()
        {
            return (o) =>
            {
                //byte[] bytes = new byte[1024];

                SocketMessage sm = null;
                if (o != null) //client.ReceiveData(out sm))
                    Console.WriteLine("My ID: {0}, Id received: {1}\nType: {2}\nMessage: {3}", client.Id, sm.id, o.GetType().Name, sm.msg);
                else
                    Console.WriteLine("Error receiving data!"); //Error fixing

                if (client != null)
                    client.myLogger.Log(sm.msg.ToString());
                else
                    Console.WriteLine("Client closed unexpectly!");
            };
        }

        private bool ValidateClient()
        {
            return !string.IsNullOrWhiteSpace(clientIP.Text);
        }

        private bool ValidateServer()
        {
            return !string.IsNullOrWhiteSpace(serverIP.Text);
        }

        private void startServer_Click(object sender, EventArgs e)
        {
            Start(false);
        }

        private void clientConnect_Click(object sender, EventArgs e)
        {
            Start(true);
        }

        private void sendMsg_Click(object sender, EventArgs e)
        {
            client.SendMessageToServer(clientMsg.Text);
            clientMsg.Text = "";
        }

        public void SetName(string name)
        {
            clientName.Text = name;
        }

        private void frmSocket_Closing(object sender, FormClosingEventArgs e)
        {
            if (state == SocketState.ClientStarted)
                ClientClosing();
            if (state == SocketState.ServerStarted)
                ServerClosing();
        }

        private void ClientClosing()
        {
            client.Dispose();
            workerObject.CancelAsync();
            CommonClosing();
        }

        private void ServerClosing()
        {
            server.Dispose();
            CommonClosing();
        }

        private void CommonClosing()
        {
            Console.WriteLine("Closing chiringuito.");
        }

        private void EnableControls(bool isClient)
        {
            ControlControls(isClient, true);
        }

        private void DisableControls(bool isClient)
        {
            ControlControls(isClient, false);
        }

        internal void ControlControls(bool isClient, bool enable)
        {
            TabPage tb = tabPage2;
            if (!isClient) tb = tabPage1;

            foreach (Control c in tb.Controls)
                c.Enabled = enable;
        }
    }
}