import os
import os.path
import time
import subprocess
import job
import model
from xml.dom.minidom import parseString, getDOMImplementation
from Commands import *
import param
import member
from member import Member, MemberLocate
from cons_line import ConsLine
import rect_plate
import tool_selection
import csv
import random
import string
import math
import Locator
import MemberBase
import Point3D
import Transform3D

class Child(object):
    def __init__(self, pid):
        self.ipipe = self.opipe = None
        while(True):
            try:
                if not self.ipipe:
                    self.ipipe = open(r'\\.\PIPE\SDS2HYBRID%iO' % pid, 'r+b', buffering=0)
                if not self.opipe:
                    self.opipe = open(r'\\.\PIPE\SDS2HYBRID%iI' % pid, 'r+b', buffering=0)
                break
            except:
                time.sleep(0.1)


        #how to get the real repository name?
        impl = getDOMImplementation()
        doc = impl.createDocument(None, "job", None)
        top = doc.documentElement
        top.setAttribute("name", job.JobName())
        top.setAttribute("repository", job.RepositoryPath())
        top.setAttribute("sequence", "0")
        self.Send(doc.toprettyxml())

    def Send(self, message):
        header = str(len(message)).zfill(10)
        self.opipe.write(header.encode())
        self.opipe.seek(0)
                #not sure why, but it's as if there's a character being eaten with
                #each write.  Adding a newline seems to be stable though:
        self.opipe.write(str(message + "\n").encode())
        self.opipe.seek(0)

    def Recv(self):
        self.ipipe.seek(0)
        header = self.ipipe.read(10)
        toread = int(header)
        dat = self.ipipe.read(toread)
        self.ipipe.seek(0)
        return dat

    def Close(self):
        self.ipipe.close()
        self.opipe.close()


def CreateSelectionMessage(selection, sequence):
    impl = getDOMImplementation()
    doc = impl.createDocument(None, "selection", None)
    top = doc.documentElement
    top.setAttribute("sequence", str(sequence))
    for i in selection:
        item = doc.createElement("item")
        item.setAttribute("member", str(i._as_tuple[0]))
        item.setAttribute("end", str(i._as_tuple[1]))
        item.setAttribute("material", str(i._as_tuple[2]))
        item.setAttribute("hole", str(i._as_tuple[3]))
        item.setAttribute("bolt", str(i._as_tuple[4]))
        item.setAttribute("weld", str(i._as_tuple[5]))
        top.appendChild(item)
    return doc.toprettyxml()

def ParseCommand(command):
    doc = parseString(command)
    top = doc.documentElement
    if top.nodeName == "command":
        attr = dict(top.attributes.items())
        val = ""
        if top.firstChild:
            val = str(top.firstChild.nodeValue)
        return "command", str(attr["type"]), attr["sequence"], val


Prompts = [
"",
"Locate Member:",
"Locate Members:",
"Locate Member End:",
"Locate Member Ends:",
"Locate Material:",
"Locate Materials:",
"Locate Bolt:",
"Locate Bolts:",
"Locate Weld:",
"Locate Welds:",
"Locate Material, bolt, or weld:",
"Locate Material, bolts, and welds:",
"Locate Hole:",
"Locate Holes:"
]

class HybridCommand(Command):
    def __init__(self):
        Command.__init__(
            self,
            operations=OperationClass.Member,
            listings=Listing.All,
            icons=IconSet(tuple()),
            long_description="Import CAD Data as Members",
            command_text="""Import CAD Data as Members""",
            alt_text="""""",
            documentation="""Import DWG or DXF Files. The lines and polylines in this file will be converted to members based on a mapping table defined by the user.""",
            clone=False,
            stations=(Station.Frame | Station.FrameReview | Station.FrameBim | Station.FrameErector | Station.FrameErectorPlus
                                          | Station.FrameApproval | Station.FrameFabricator | Station.FrameModelstn | Station.FrameDraftstn
                                          | Station.FrameLite | Station.FrameErectorPlus),
            type=CommandType.Command
        )

    def Invoke(self, args):
        directory = os.path.dirname(__file__)
        exe = os.path.join(directory, "ImportAcadAsMembers.exe")
        childProcess = subprocess.Popen([exe])

        pipe = Child(childProcess.pid)

        prompts = Prompts
        while(True):
            model.Deselect(model.GetSelection())
            try:
                message = pipe.Recv();
            except:
                break
            messageType, name, sequence, text = ParseCommand(message)

            if name == "0" or messageType == "finished":
                pipe.Close()
                break
            elif name == "1":
                sel = model.PreOrPostSelection(prompt=prompts[int(name)], location_fn=model.LocateSingle, filter_fn=model.IsMember)
            elif name == "2":
                sel = model.PreOrPostSelection(prompt=prompts[int(name)], location_fn=model.LocateMultiple, filter_fn=model.IsMember)
            elif name == "3":
                sel = model.PreOrPostSelection(prompt=prompts[int(name)], location_fn=model.LocateSingle, filter_fn=model.IsEnd)
            elif name == "4":
                sel = model.PreOrPostSelection(prompt=prompts[int(name)], location_fn=model.LocateMultiple, filter_fn=model.IsEnd)
            elif name == "5":
                sel = model.PreOrPostSelection(prompt=prompts[int(name)], location_fn=model.LocateSingle, filter_fn=model.IsMaterial)
            elif name == "6":
                sel = model.PreOrPostSelection(prompt=prompts[int(name)], location_fn=model.LocateMultiple, filter_fn=model.IsMaterial)
            elif name == "7":
                sel = model.PreOrPostSelection(prompt=prompts[int(name)], location_fn=model.LocateSingle, filter_fn=model.IsBolt)
            elif name == "8":
                sel = model.PreOrPostSelection(prompt=prompts[int(name)], location_fn=model.LocateMultiple, filter_fn=model.IsBolt)
            elif name == "9":
                sel = model.PreOrPostSelection(prompt=prompts[int(name)], location_fn=model.LocateSingle, filter_fn=model.IsWeld)
            elif name == "10":
                sel = model.PreOrPostSelection(prompt=prompts[int(name)], location_fn=model.LocateMultiple, filter_fn=model.IsWeld)
            elif name == "11":
                sel = model.PreOrPostSelection(prompt=prompts[int(name)], location_fn=model.LocateSingle, filter_fn=lambda m: model.IsWeld(m) or model.IsMaterial(m) or model.IsBolt(m))
            elif name == "12":
                sel = model.PreOrPostSelection(prompt=prompts[int(name)], location_fn=model.LocateMultiple, filter_fn=lambda m: model.IsWeld(m) or model.IsMaterial(m) or model.IsBolt(m))
            elif name == "13":
                sel = model.PreOrPostSelection(prompt=prompts[int(name)], location_fn=model.LocateSingle, filter_fn=model.IsHole)
            elif name == "14":
                sel = model.PreOrPostSelection(prompt=prompts[int(name)], location_fn=model.LocateMultiple, filter_fn=model.IsHole)
            elif name == "15":
                prompts = [text]*15
                continue #no reply for this.
            elif name == "16":
                prompts = [text]*16
                self.add_member(text)
                continue #no reply for this.
            elif name == "17":
                prompts = [text]*17
                self.add_construction_line(text)
                continue
            elif name == "18":
                prompts = [text]*18
                self.process_job()
                continue

            code,sel = sel
            message = CreateSelectionMessage(sel, sequence)

            try:
                pipe.Send(message)
            except:
                break
            prompts = Prompts

    def process_job(self):
        # Process
        try:
            job.ProcessJob()
        except Exception as e:
            param.NonBlockingWarning("Could not process the job! Probably you tried to process an empty job!")
            pass

    def add_construction_line(self, text):
        param.Units("metric")
        # assuming text command has been sent correctly, it consists of [CL], [leftEndX],[leftEndY],[leftEndZ],[rightEndX],[rightEndY],[rightEndZ]
        # we will split the text command and create the member
        commands = text.split(";")
        if len(commands) != 6:
            param.NonBlockingWarning("Invalid CL Create Command!")
            return
        try:
            cl1 = ConsLine()
            cl1.Point1 = (float(commands[0]),float(commands[1]),float(commands[2]))
            cl1.Point2 = (float(commands[3]),float(commands[4]),float(commands[5]))
            cl1.PenNumber = 'Blue'
            cl1.Add()
        except Exception as e:
            # it is overlapping or there is another problem, just do not create anything and warn the user about it
            param.NonBlockingWarning("Could not create Construction Line!")
            param.NonBlockingWarning("Error Message: %s" % (e))
            pass

    def add_member(self, text):
        param.Units("metric")
        # assuming text command has been sent correctly, it consists of [memberType],[sectionSize], [leftEndX],[leftEndY],[leftEndZ],[rightEndX],[rightEndY],[rightEndZ]
        # we will split the text command and create the member
        commands = text.split(";")
        if len(commands) != 8:
            param.NonBlockingWarning("Invalid Member Create Command!")
            return
        try:
            member1 = Member(commands[0])
            member1.SectionSize = commands[1]   
            member1.LeftEnd.Location = (float(commands[2]),float(commands[3]),float(commands[4]))
            member1.RightEnd.Location = (float(commands[5]),float(commands[6]),float(commands[7]))
            member1.Add()
        except Exception as e:
            # it is overlapping or there is another problem, just do not create anything and warn the user about it
            param.NonBlockingWarning("Could not create %s!" % (commands[0]))
            param.NonBlockingWarning("Error Message: %s" % (e))
            pass