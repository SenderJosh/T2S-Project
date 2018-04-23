using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace T2SOverlay
{
    public class Server
    {

        private static readonly Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private static readonly List<SocketPair> clientSockets = new List<SocketPair>();
        private const int BUFFER_SIZE = 33;
        private const int PORT = 100;
        private static readonly byte[] buffer = new byte[BUFFER_SIZE];

        public static void SetupServer()
        {
            Console.WriteLine("Setting up server...");
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, PORT));
            serverSocket.Listen(0);
            serverSocket.BeginAccept(AcceptCallback, null);
            Console.WriteLine("Server setup complete");
        }

        /// <summary>
        /// Close all connected client (we do not need to shutdown the server socket as its connections
        /// are already closed with the clients).
        /// </summary>
        public static void CloseAllSockets()
        {
            foreach (SocketPair sock in clientSockets)
            {
                sock.socket.Shutdown(SocketShutdown.Both);
                sock.socket.Close();
            }

            serverSocket.Close();
        }

        private static void AcceptCallback(IAsyncResult AR)
        {
            Socket sock;

            try
            {
                sock = serverSocket.EndAccept(AR);
            }
            catch (ObjectDisposedException) // I cannot seem to avoid this (on exit when properly closing sockets)
            {
                return;
            }

            clientSockets.Add(new SocketPair()
            {
                //Initialize socket
                socket = sock
            });
            sock.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, sock);
            Console.WriteLine("Client connected, waiting for request...");
            serverSocket.BeginAccept(AcceptCallback, null);
        }

        private static void ReceiveCallback(IAsyncResult AR)
        {
            Socket current = (Socket)AR.AsyncState;
            int received;

            try
            {
                received = current.EndReceive(AR);
            }
            catch (SocketException)
            {
                Console.WriteLine("Client forcefully disconnected");
                // Don't shutdown because the socket may be disposed and its disconnected anyway.
                current.Close();
                SocketPair socketToRemove = null;
                foreach(SocketPair s in clientSockets)
                {
                    if (s.socket.Equals(current))
                    {
                        socketToRemove = s;
                        break;
                    }
                }
                if(socketToRemove != null)
                    clientSockets.Remove(socketToRemove);
                return;
            }

            byte[] recBuf = new byte[received]; //Received buffer which should be the header to tell us the buffer length of the json message (should be length 33 with pipe at the end)
            Array.Copy(buffer, recBuf, received);

            int headerReceived;
            if(Int32.TryParse(Encoding.ASCII.GetString(recBuf).Split('|')[0], out headerReceived))
            {
                Console.WriteLine("header received: " + headerReceived);
                recBuf = new byte[headerReceived];
                received = current.Receive(recBuf, headerReceived, SocketFlags.None);
                if (received == 0)
                {
                    Console.WriteLine("--------------ERROR--------------");
                    Console.WriteLine("Receive Response failed");
                    Console.WriteLine("Expected to receive bytes: " + headerReceived);
                    current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, current);
                    return; //Nothing to receive (which would be REALLY weird because it's expecting to receive something so print stuff)
                }
            }

            string header = recBuf.Length.ToString();
            //If the header length is larger than the maximum length, we're gonna assume that the dude is trying to destroy someone with a fat receive. 
            //There's no reason for something to be this large
            //Therefore, just let the sender know that their message is waaaay too big
            if(header.Length < 32)
            {
                for (int i = header.Length; i < 32; i++)
                {
                    header += " "; //Append empty spaces until header is max length (32)
                }
                byte[] message = Encoding.ASCII.GetBytes((header + "|" + Encoding.ASCII.GetString(recBuf))); //Append message with the header
                
                //Send to all connected sockets except self
                foreach (SocketPair s in clientSockets)
                {
                    if (s.socket != current)
                    {
                        s.socket.Send(message);
                    }
                }
                Console.WriteLine(Encoding.ASCII.GetString(recBuf));
                Console.WriteLine("Size of this buffer: " + recBuf.Length);
                T2SClientMessage clientMessage = JsonConvert.DeserializeObject<T2SClientMessage>(Encoding.ASCII.GetString(recBuf));
                //If this is the first connection, we need to update our socketpair MacAddr for audit (also just send back to client)
                if (clientMessage.FirstConnect)
                {
                    foreach (SocketPair s in clientSockets)
                    {
                        if (s.socket == current)
                        {
                            s.MacAddr = clientMessage.MacAddr;
                        }
                    }
                    current.Send(message);
                }
            }
            else
            {
                Console.WriteLine("HEADER TOO LARGE!!! Header length: " + header.Length);
            }
            current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, current);
        }

        class SocketPair
        {
            public string MacAddr { get; set; } = null;
            public Socket socket { get; set; } = null;
        }
    }
}
