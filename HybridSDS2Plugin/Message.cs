using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ImportAcadAsMembers
{
    /// <summary>
    /// An abstract class for messages coming in from SDS2.
    /// </summary>
    abstract class Message
    {
    }

    class SelectJob : Message
    {
        public SelectJob(XmlElement body)
        {
            Job = body.GetAttribute("name");
            Repository = body.GetAttribute("repository");
        }
        public string Job { get; private set; }
        public string Repository { get; private set; }
    }
}
