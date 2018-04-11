using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace T2SOverlay
{
    /// <summary>
    /// This class will be created serialized by the server, and then sent to the client (Pair(Socket, Client))
    /// 
    /// The client will send two pieces of information to the server; first a header to describe the upcoming byte length, then the serialized Client data
    /// </summary>
    [Serializable]
    public class Client
    {
        public int Hash_Code; //Assigned by the server (id, in case two usernames are the same)

        public Bitmap ProfilePicture;
        public string Username;

        public Client(Bitmap profilePicture, string username)
        {
            this.ProfilePicture = profilePicture;
            this.Username = username;
        }

    }
}
