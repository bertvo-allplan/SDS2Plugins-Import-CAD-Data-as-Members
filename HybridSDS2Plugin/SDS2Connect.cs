using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO.Pipes;
using DesignData.SDS2.Database;
using System.Xml;

namespace ImportAcadAsMembers
{
    /// <summary>
    /// Anything starting with 'Select' causes SDS/2 to do a selection.
    /// Anything ending with 's' selects multiple, or none.  Anything ending without it is a single selection.
    /// If the user cancels, you'll get a valid Selection back, but it will be empty.
    /// All of these Select* commands return a Selection type Message.
    /// </summary>
    public enum Commands
    {
        /// <summary>
        /// Don't ever use this one, it's for SDS2Connect to terminate the connection cleanly.
        /// </summary>
        Finished=0,
        /// <summary>
        /// Select a single member
        /// </summary>
        SelectMember,
        /// <summary>
        /// Select a list of members (0 or more)
        /// </summary>
        SelectMembers,
        /// <summary>
        /// Select a single end of a member
        /// </summary>
        SelectEnd,
        /// <summary>
        /// Select a list of ends of members (0 or more)
        /// </summary>
        SelectEnds,
        SelectMaterial,
        SelectMaterials,
        SelectBolt,
        SelectBolts,
        SelectWeld,
        SelectWelds,
        /// <summary>
        /// Select a bolt, weld, or material with one prompt.
        /// </summary>
        SelectMaterialBoltWeld,
        /// <summary>
        /// Select any number of bolts, welds, and materials with one prompt.
        /// </summary>
        SelectMaterialsBoltsWelds,
        SelectHole,
        SelectHoles,
        /// <summary>
        /// When sending this, call SendCommand with body:"your message".
        /// There will be no reply, so do not wait for one.
        /// </summary>
        SetNextPrompt,
        CreateMember,
        CreateConstructionLine,
        ProcessJob
    }
    /// <summary>
    /// Create one of these, be sure to Dispose when done.  This is how
    /// your program should talk back and forth with the running SDS2 process.
    /// 
    /// You can't do more than one of these per program invocation, the SDS2
    /// code is only expecting one and it's expecting it to stick around for
    /// the life of the program.
    /// </summary>
    class SDS2Connect: IDisposable
    {
        public SDS2Connect()
        {
            replies = new Dictionary<int, Message>();
            string directory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            PipePath = "SDS2HYBRID" + pid.ToString();
            nextsequence = 1;
            //open two pipes, one for messages from SDS2 and one for messages going out to SDS2
            //since this code constantly listens, we can't really share a single pipe (this .net
            //program will consume its own messages if it writes to the pipe it listens to)
            incoming = new NamedPipeServerStream(PipePath + "I", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.None);
            outgoing = new NamedPipeServerStream(PipePath + "O", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.None);

            incoming.WaitForConnection();
            outgoing.WaitForConnection();
            listener = new Thread(new ThreadStart(Listener));
            listener.IsBackground = true;
            listener.SetApartmentState(ApartmentState.STA);
            listener.Start();

            var job = WaitForReply(0) as SelectJob; //wait for the ready message, hard coded to sequence 0
            DataDirectory.Open(DataDirectory.Default);
            Repository repo = Repository.Default;
            //search for repo based on shadow path.
            foreach(var r in Repository.GetAllRepositories())
            {
                if (r.ShadowPath == job.Repository)
                    repo = r;
            }
            //search for job based on name
            foreach(var j in repo.JobIdentifiers)
            {
                if (j.Name == job.Job)
                    activeJob = Job.FindJob(j);
            }
            activeJob.Open();
        }
        private void Listener()
        {
            while (true)
            {
                //10 byte header to indicate message size.
                //this lets us have large, multi-GB, messages. let's never use that.
                byte[] header = new byte[10];
                int read = incoming.Read(header, 0, header.Length);
                if (read == 10)
                {
                    string hdr = Encoding.ASCII.GetString(header);
                    int headerSize;
                    if (int.TryParse(hdr, out headerSize))
                    {
                        if (headerSize > 0)
                        {
                            byte[] message = new byte[headerSize];
                            int readamount = incoming.Read(message, 0, headerSize);
                            if (readamount == headerSize)
                            {
                                XmlDocument doc = new XmlDocument();
                                doc.LoadXml(Encoding.ASCII.GetString(message));
                                string command = doc.DocumentElement.Name;
                                
                                if (command == "finished")
                                    break;
                                else
                                {
                                    int sequence;
                                    if(int.TryParse(doc.DocumentElement.GetAttribute("sequence"), out sequence))
                                    {
                                        switch(command)
                                        {
                                            case "selection":
                                                var items = doc.DocumentElement.GetElementsByTagName("item");
                                                //SDS2 returning a selection, to know what the selection request
                                                //was you need to use the sequence and remember what request you'd made
                                                Selection sel = new Selection(activeJob, items);
                                                lock(replies)
                                                {
                                                    replies[sequence] = sel;
                                                }
                                                break;
                                            case "job":
                                                //SDS2 telling us what job it wants opened, this is 
                                                //only ever done once and it's the first message from SDS2
                                                SelectJob s = new SelectJob(doc.DocumentElement);
                                                lock (replies)
                                                {
                                                    replies[sequence] = s;
                                                }
                                                break;
                                            default:
                                                lock(replies)
                                                {
                                                    replies[sequence] = null;
                                                }
                                                break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        private string MakeHeader(string message)
        {
            return message.Length.ToString("D10");
        }

        /// <summary>Ask SDS2 to do something, you can then wait for a reply with WaitForReply.</summary>
        /// <returns>a sequence, to get any reply pass this to GetReply</returns>
        public int SendCommand(Commands command, string body="")
        {
            int sequence = nextsequence;
            nextsequence++;
            string message = string.Format("<command type='{0}' sequence='{2}'>{1}</command>", (int)command, body, sequence);
            byte [] header = Encoding.ASCII.GetBytes(MakeHeader(message));
            outgoing.Write(header, 0, header.Length);

            byte[] msg = Encoding.ASCII.GetBytes(message);
            outgoing.Write(msg, 0, msg.Length);

            return sequence;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sequence">this was returned by SendCommand, pass it in here.</param>
        /// <returns>null if the reply hasn't come yet, or the message if it has.  
        /// downcast based on your expected reply.</returns>
        public Message GetReply(int sequence)
        {
            lock(replies)
            {
                if (!replies.ContainsKey(sequence))
                    return null;
                var ret = replies[sequence];
                replies.Remove(sequence);
                return ret;
            }
        }

        /// <summary>
        /// Just like GetReply, but this waits indefinitely for the message.
        /// </summary>
        public Message WaitForReply(int sequence)
        {
            while(true)
            {
                lock(replies)
                {
                    if(replies.ContainsKey(sequence))
                    {
                        var ret = replies[sequence];
                        replies.Remove(sequence);
                        return ret;
                    }
                }
                Thread.Sleep(100);
            }
        }
        public string PipePath { get; private set; }

        private Job activeJob;
        private NamedPipeServerStream incoming;
        private NamedPipeServerStream outgoing;
        private Thread listener;
        private int nextsequence;
        private Dictionary<int, Message> replies;

        void IDisposable.Dispose()
        {
            SendCommand(Commands.Finished);
            if (!listener.Join(3000))
                listener.Abort();//give it three seconds to close neatly
            outgoing.Dispose();
            incoming.Dispose();
        }
    }
}
