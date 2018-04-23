using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace T2SOverlay
{
    /// <summary>
    /// Message that gets displayed on the front-end client
    /// </summary>
    class T2SUser
    {
        public string MacAddr { get; set; } = null;
        public string Username { get; set; } = null;
        public byte[] ProfilePicture { get; set; } = null; //string so it can be converted by json
    }
}
