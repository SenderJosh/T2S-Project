using System;
using System.Collections.Generic;
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
        private static readonly List<Pair> clientSocketPairs = new List<Pair>();
        private const int BUFFER_SIZE = 2048;
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
            foreach (Pair pair in clientSocketPairs)
            {
                pair.Socket.Shutdown(SocketShutdown.Both);
                pair.Socket.Close();
            }

            serverSocket.Close();
        }

        private static void AcceptCallback(IAsyncResult AR)
        {
            Socket socket;

            try
            {
                socket = serverSocket.EndAccept(AR);
            }
            catch (ObjectDisposedException) // I cannot seem to avoid this (on exit when properly closing sockets)
            {
                return;
            }

            Client client = new Client(null, null);
            client.Hash_Code = client.GetHashCode();
            clientSocketPairs.Add(new Pair(socket, client)); //temp store null client until first ReceiveCallBack is given
            socket.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, socket);
            Console.WriteLine("Client connected, waiting for request...");
            serverSocket.BeginAccept(AcceptCallback, null);
        }

        private static void ReceiveCallback(IAsyncResult AR)
        {
            Socket current = (Socket)AR.AsyncState;
            int received;
            Pair pair = null;

            //Find socket associated pair
            foreach(Pair p in clientSocketPairs)
            {
                if (p.Socket.Equals(current))
                {
                    pair = p;
                    break;
                }
            }

            if(pair == null)
            {
                pair = new Pair(current, null);
                clientSocketPairs.Add(pair);
            }

            try
            {
                received = current.EndReceive(AR);
            }
            catch (SocketException)
            {
                Console.WriteLine("Client forcefully disconnected");
                // Don't shutdown because the socket may be disposed and its disconnected anyway.
                current.Close();
                clientSocketPairs.Remove(pair);
                return;
            }

            byte[] recBuf = new byte[received];
            Array.Copy(buffer, recBuf, received);
            string text = Encoding.ASCII.GetString(recBuf);
            Console.WriteLine("Received Text: " + text);
            Console.WriteLine("There are " + clientSocketPairs.Count + " clients");
            //Send to all connected sockets except self
            foreach (Pair p in clientSocketPairs)
            {
                if(p.Socket != current)
                    p.Socket.Send(Encoding.ASCII.GetBytes(text));
            }

            current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, current);
        }

        /// <summary>
        /// We will now host a list of Pairs rather than a list of Sockets, for the purpose of keeping serializable user-client data for a multi-client chat server
        /// </summary>
        class Pair
        {
            public Socket Socket;
            public Client Client;

            public Pair(Socket sock, Client client)
            {
                this.Socket = sock;
                this.Client = client;
            }
        }
    }
}
