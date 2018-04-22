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
    /// ProfilePicture should NOT be sent everytime. Only when UpdatePicture is true. 
    ///     NOTICE: On first connect, UpdatePicture MUST be true in order to provide the other clients the profile picture on connect
    /// If Message = null, there's no message
    /// </summary>
    class T2SClientMessage
    {
        public bool UpdatePicture { get; set; } = false;
        public Image ProfilePicture { get; set; } = null;
        public string Message { get; set; } = null;
    }
}
