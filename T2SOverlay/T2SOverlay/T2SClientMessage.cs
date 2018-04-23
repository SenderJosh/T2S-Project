using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace T2SOverlay
{
    /// <summary>
    /// The message a client sends out will contain information about an Image, its FirstConnect property, and the Message
    /// ProfilePicture should NOT be sent everytime. Only when UpdateProfile is true. 
    ///     NOTICE: On first connect, UpdateProfile MUST be true in order to provide the other clients the profile picture on connect along with username
    /// If Message = null, there's no message
    /// 
    /// MacAddr should always be filled as this will be our method of detecting uniqueness between clients
    /// For the sake of tests, we will also use username to pair with MacAddr
    /// The server will automatically pair the MacAddr to the socket for the purpose of tracking disconnects
    /// </summary>
    public class T2SClientMessage
    {
        public bool Connected { get; set; } = true; //To be modified by the server. If this is ever false, remove
        public string MacAddr { get; set; } = null;
        public string Username { get; set; } = null;
        public bool FirstConnect { get; set; } = false; //If first connect, server logic should send JSON information of EVERYONE and keep track of their current ProfilePicture
        public bool UpdateProfile { get; set; } = false;
        public byte[] ProfilePicture { get; set; } = null; //string so it can be converted by json
        public string Message { get; set; } = null;
    }
}
