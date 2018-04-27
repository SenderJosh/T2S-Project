using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace T2SOverlay
{
    public class Server
    {

        private static Socket serverSocket;
        private static readonly List<SocketPair> clientSockets = new List<SocketPair>();
        private const int BUFFER_SIZE = 33;
        private const int PORT = 100;
        private static readonly byte[] buffer = new byte[BUFFER_SIZE];

        public static void SetupServer()
        {
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
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
        
        //TODO send to all clients a disconnect notice
        public static void CloseAllSockets()
        {
            foreach (SocketPair sock in clientSockets)
            {
                if (sock.socket.Connected)
                {
                    sock.socket.Shutdown(SocketShutdown.Both);
                    sock.socket.Close();
                }
            }
            clientSockets.Clear();

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
            if (!current.Connected)
                return;
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
                if (socketToRemove != null)
                {
                    clientSockets.Remove(socketToRemove);
                    T2SClientMessage otherClient = new T2SClientMessage()
                    {
                        Connected = false,
                        MacAddr = socketToRemove.MacAddr
                    };
                    byte[] otherClientBuffer = MainWindow.ObjectToByteArray(otherClient);
                    byte[] otherClientBufferMessage = new byte[otherClientBuffer.Length + 33]; //message to send; appends length of header
                    string otherClientHeader = otherClientBuffer.Length.ToString();
                    for (int i = otherClientHeader.Length; i < 32; i++)
                    {
                        otherClientHeader += " "; //Append empty spaces until header is max length (32)
                    }
                    otherClientHeader += "|";

                    Array.Copy(Encoding.ASCII.GetBytes(otherClientHeader), otherClientBufferMessage, 33);
                    Array.Copy(otherClientBuffer, 0, otherClientBufferMessage, 33, otherClientBuffer.Length);

                    //Sendto everyone
                    foreach (SocketPair s in clientSockets)
                        s.socket.Send(otherClientBufferMessage);
                }
                return;
            }
            
            if(received == 0)
            {
                Console.WriteLine("No bytes received; closing socket");
                SocketPair socketToRemove = null;
                foreach (SocketPair s in clientSockets)
                {
                    if (s.socket.Equals(current))
                    {
                        socketToRemove = s;
                        break;
                    }
                }
                if(socketToRemove.socket.Connected)
                {
                    socketToRemove.socket.Close();
                }
                clientSockets.Remove(socketToRemove);
                T2SClientMessage otherClient = new T2SClientMessage()
                {
                    Connected = false,
                    MacAddr = socketToRemove.MacAddr
                };
                byte[] otherClientBuffer = MainWindow.ObjectToByteArray(otherClient);
                byte[] otherClientBufferMessage = new byte[otherClientBuffer.Length + 33]; //message to send; appends length of header
                string otherClientHeader = otherClientBuffer.Length.ToString();
                for (int i = otherClientHeader.Length; i < 32; i++)
                {
                    otherClientHeader += " "; //Append empty spaces until header is max length (32)
                }
                otherClientHeader += "|";

                Array.Copy(Encoding.ASCII.GetBytes(otherClientHeader), otherClientBufferMessage, 33);
                Array.Copy(otherClientBuffer, 0, otherClientBufferMessage, 33, otherClientBuffer.Length);

                //Sendto everyone
                foreach (SocketPair s in clientSockets)
                {
                    if(s.socket.Connected)
                    {
                        s.socket.Send(otherClientBufferMessage);
                    }
                }
                return;
            }

            byte[] recBuf = new byte[received]; //Received buffer which should be the header to tell us the buffer length of the json message (should be length 33 with pipe at the end)
            Array.Copy(buffer, recBuf, received);
            
            //Get until header is filled
            while (received < 33)
            {
                byte[] tempBuffer = new byte[33 - received];
                int appendableBytes = current.Receive(tempBuffer, 33 - received, SocketFlags.None);
                Array.Copy(tempBuffer, 0, recBuf, received, tempBuffer.Length);
                received += appendableBytes;
            }

            int headerReceived;
            if(Int32.TryParse(Encoding.ASCII.GetString(recBuf).Split('|')[0], out headerReceived))
            {
                recBuf = new byte[headerReceived];
                received = current.Receive(recBuf, headerReceived, SocketFlags.None);
                //Get until object is filled
                while (received < headerReceived)
                {
                    byte[] tempBuffer = new byte[headerReceived - received];
                    int appendableBytes = current.Receive(tempBuffer, headerReceived - received, SocketFlags.None);
                    Array.Copy(tempBuffer, 0, recBuf, received, tempBuffer.Length);
                    received += appendableBytes;
                }
            }
            byte[] temp = new byte[recBuf.Length];
            Array.Copy(recBuf, temp, temp.Length);
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

                byte[] headerBytes = Encoding.ASCII.GetBytes(header + "|");

                T2SClientMessage clientMessage = (T2SClientMessage)MainWindow.ByteArrayToObject(recBuf);
                temp = MainWindow.ObjectToByteArray(clientMessage);
                recBuf = new byte[temp.Length + headerBytes.Length];

                Array.Copy(headerBytes, recBuf, headerBytes.Length);
                Array.Copy(temp, 0, recBuf, 33, temp.Length);

                byte[] message = recBuf; //Append message with the header

                //Send to all connected sockets except self
                foreach (SocketPair s in clientSockets)
                {
                    if (s.socket != current)
                    {
                        s.socket.Send(message);
                    }
                }
                //If this is the first connection, we need to update our socketpair MacAddr for audit (also just send back to client)
                if (clientMessage.FirstConnect)
                {
                    Console.WriteLine("First connect: " + clientMessage.Username);
                    foreach (SocketPair s in clientSockets)
                    {
                        if (s.socket == current)
                        {
                            s.MacAddr = clientMessage.MacAddr;
                            s.Username = clientMessage.Username;
                            s.ProfilePicture = clientMessage.ProfilePicture;
                        }
                        else
                        {
                            T2SClientMessage otherClient = new T2SClientMessage()
                            {
                                FirstConnect = true,
                                ProfilePicture = s.ProfilePicture,
                                Username = s.Username,
                                MacAddr = s.MacAddr
                            };
                            byte[] otherClientBuffer = MainWindow.ObjectToByteArray(otherClient);
                            byte[] otherClientBufferMessage = new byte[otherClientBuffer.Length + 33]; //message to send; appends length of header
                            string otherClientHeader = otherClientBuffer.Length.ToString();
                            for (int i = otherClientHeader.Length; i < 32; i++)
                            {
                                otherClientHeader += " "; //Append empty spaces until header is max length (32)
                            }
                            otherClientHeader += "|";

                            Array.Copy(Encoding.ASCII.GetBytes(otherClientHeader), otherClientBufferMessage, 33);
                            Array.Copy(otherClientBuffer, 0, otherClientBufferMessage, 33, otherClientBuffer.Length);

                            current.Send(otherClientBufferMessage);
                        }
                    }
                    current.Send(message); //Send the profile back to themself
                }
            }
            else
            {
                Console.WriteLine("HEADER TOO LARGE!!! Header length: " + header.Length);
            }
            current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, current);
        }

        public bool IsConnected(Socket socket)
        {
            try
            {
                return !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
            }
            catch (SocketException) { return false; }
        }

        class SocketPair
        {
            public string MacAddr { get; set; } = null;
            public Socket socket { get; set; } = null;
            public byte[] ProfilePicture { get; set; } = null;
            public string Username { get; set; } = null;
        }
    }
}
