using System;
using DesignData.SDS2;
using Model = DesignData.SDS2.Model;
using ACadSharp;
using ACadSharp.IO;
using ACadSharp.Entities;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;
using DesignData.SDS2.Database;
using DesignData.SDS2.Model;
using HybridSDS2Plugin;
using ACadSharp.Exceptions;
using System.Xml;
using System.Drawing;
using System.Linq;
using System.Net;
using static ACadSharp.Objects.XRecord;
using System.Reflection;

namespace ImportAcadAsMembers
{   

    public class UserSetting
    {
        public string Name { get; set; }
        public string Profile { get; set; }
        public string Type { get; set; }
        public bool ByLayer { get; set; }
        public UserSetting(string name, string profile, string type, bool byLayer)
        {
            Name = name;
            Profile = profile;
            Type = type;
            ByLayer = byLayer;
        }
    }

    public class AcadObject
    {
        public Entity Acad_Entity { get; set; }
        public string Acad_Layer { get; set; }
        public Color Acad_Color { get; set; }
        public Coordinate Acad_MemberStartPoint { get; set; }
        public Coordinate Acad_MemberEndPoint { get; set; }
        public bool Flag_IsValidEntity { get; set; } = true;
        public string SDS2_CommandString { get; set; }
        public string SDS2_Type { get; set; }
        public string SDS2_Profile { get; set; }
        public ImportAcadAsMembers.Commands SDS2_Command { get; set; }

        public AcadObject(Polyline entity, Coordinate memberStartPoint, Coordinate memberEndPoint) {
            Acad_Entity = entity;
            Acad_Layer = entity.Layer.Name;
            Acad_Color = new Color(entity.Color.R, entity.Color.G, entity.Color.B);
      
            Acad_MemberStartPoint = memberStartPoint;
            Acad_MemberEndPoint = memberEndPoint;
        }

        public AcadObject(Line entity) { 
            Acad_Entity = entity;
            Acad_Layer = entity.Layer.Name;
            Acad_Color = new Color(entity.Color.R, entity.Color.G, entity.Color.B);

            Acad_MemberStartPoint = new Coordinate { X = entity.StartPoint.X, Y = entity.StartPoint.Y, Z = entity.StartPoint.Z };
            Acad_MemberEndPoint = new Coordinate { X = entity.EndPoint.X, Y = entity.EndPoint.Y, Z = entity.EndPoint.Z };          
        }

        public string CreateSDS2CommandStringMember(string type, string profile)
        {   
            SDS2_Type = type;
            SDS2_Profile = profile;

            string command = type + ";" + profile + ";"
                + Acad_MemberStartPoint.X + ";" + Acad_MemberStartPoint.Y + ";" + Acad_MemberStartPoint.Z
                + ";" + Acad_MemberEndPoint.X + ";" + Acad_MemberEndPoint.Y + ";" + Acad_MemberEndPoint.Z;
            SDS2_Command = Commands.CreateMember;
            return command;
        }
    }

    public class Color
    {
        public Color(int r, int g, int b) {
            R = r;
            G = g;
            B = b;
        }
        public int R { get; set; }
        public int G { get; set; }
        public int B { get; set; }
    }

    public class Coordinate
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class ApplicationHandler
    {
        public Dictionary<string, UserSetting> MappingSettings { get; set; }
        public List<AcadObject> AcadEntityContainer { get; set; }

        public ApplicationHandler() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
        }

        public void Start() {
            bool preferences_set = SetUserPreferences(true);
            if ((preferences_set))
            {
                OpenApplicationWindow();
            }         
        }

        private void OpenApplicationWindow()
        {
            Form form = new ApplicationStartForm(this);
            form.ShowDialog();
        }

        public bool SetUserPreferences(bool OpenFileIfNeeded)
        {
            MappingSettings = ReadUserSettingsFromJSON();
            if (MappingSettings == null && OpenFileIfNeeded)
            {
                ShowMessageBox("User settings file not found or invalid.", "Error");
                // open the settings file so the user may make changes to the faulty file
                // if that file does not exist, the user should reinstall the plugin. Error is however catched and shown in the console.
                OpenUserSettingsFileInTextEditor();
                return false;
            }
            return true;
        }

        public void ShowMessageBox(string message, string caption)
        {
            MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public string GetExampleDistanceForMemberIfTooLarge(int minLength, int maxLength)
        {   
            if(AcadEntityContainer.Count == 0)
            {
                return null;
            }
            // get the distance between the start and end point of the first acadobject
            double distance = Math.Sqrt(Math.Pow(AcadEntityContainer[0].Acad_MemberEndPoint.X - AcadEntityContainer[0].Acad_MemberStartPoint.X, 2) +
                Math.Pow(AcadEntityContainer[0].Acad_MemberEndPoint.Y - AcadEntityContainer[0].Acad_MemberStartPoint.Y, 2) +
                Math.Pow(AcadEntityContainer[0].Acad_MemberEndPoint.Z - AcadEntityContainer[0].Acad_MemberStartPoint.Z, 2));
            if (distance < minLength || distance > maxLength)
                return Math.Round(distance,0).ToString();
            else
                return null;
        }

        public bool AlignWorkpoints(int workpointOffset)
        {   
            Dictionary<Coordinate, List<AcadObject>> coordinateList = new Dictionary<Coordinate, List<AcadObject>>();
            // iterate over AcadEntityContainer, and save it to a dictionary with the startpoint as key and with the endpoint as key.
            // this creates two entries for each member, one for the startpoint and one for the endpoint.
            // save the acadobject as value in the dictionary. If two coordinates are the same, add the acadobject to the list.
            foreach (AcadObject acadObject in AcadEntityContainer)
            {
                if (coordinateList.ContainsKey(acadObject.Acad_MemberStartPoint))
                {
                    coordinateList[acadObject.Acad_MemberStartPoint].Add(acadObject);
                }
                else
                {
                    coordinateList.Add(acadObject.Acad_MemberStartPoint, new List<AcadObject> { acadObject });
                }
                if (coordinateList.ContainsKey(acadObject.Acad_MemberEndPoint))
                {
                    coordinateList[acadObject.Acad_MemberEndPoint].Add(acadObject);
                }
                else
                {
                    coordinateList.Add(acadObject.Acad_MemberEndPoint, new List<AcadObject> { acadObject });
                }
            }
            // Zet alle coördinaten in een lijst.
            List<Coordinate> keys = coordinateList.Keys.ToList();

            // Vergelijk elk paar coördinaten één keer.
            for (int i = 0; i < keys.Count; i++)
            {
                for (int j = i + 1; j < keys.Count; j++)
                {
                    Coordinate coord1 = keys[i];
                    Coordinate coord2 = keys[j];

                    // Controleer of de absolute verschillen in X, Y en Z binnen de workpointOffset vallen.
                    if (Math.Abs(coord1.X - coord2.X) <= workpointOffset &&
                        Math.Abs(coord1.Y - coord2.Y) <= workpointOffset &&
                        Math.Abs(coord1.Z - coord2.Z) <= workpointOffset)
                    {
                        // the coordinates are now close enough to be considered the same workpoint we will do the following:
                        // check how may times coord 1 and coord2 are in keys list. The one that occurs most times is the workpoint. Adjust the other coordinate to this workpoint.
                        int count1 = keys.Count(x => x.X == coord1.X && x.Y == coord1.Y && x.Z == coord1.Z);
                        int count2 = keys.Count(x => x.X == coord2.X && x.Y == coord2.Y && x.Z == coord2.Z);
                        if (count1 > count2)
                        {
                            // coord1 is the workpoint
                            foreach (AcadObject acadObject in AcadEntityContainer)
                            {
                                // now we need to adjust the endpoint of the acadobject to the workpoint, but we do not know if the startpoint or endpoint is the workpoint.
                                // so let us figure that out first.
                                if (acadObject.Acad_MemberStartPoint == coord2)
                                {
                                    acadObject.Acad_MemberStartPoint = coord1;
                                }
                                else if (acadObject.Acad_MemberEndPoint == coord2)
                                {
                                    acadObject.Acad_MemberEndPoint = coord1;
                                }
                            }
                        }
                        else
                        {
                            // coord2 is the workpoint
                            foreach (AcadObject acadObject in AcadEntityContainer)
                            {
                                // now we need to adjust the endpoint of the acadobject to the workpoint, but we do not know if the startpoint or endpoint is the workpoint.
                                // so let us figure that out first.
                                if (acadObject.Acad_MemberStartPoint == coord1)
                                {
                                    acadObject.Acad_MemberStartPoint = coord2;
                                }
                                else if (acadObject.Acad_MemberEndPoint == coord1)
                                {
                                    acadObject.Acad_MemberEndPoint = coord2;
                                }
                            }
                        }
                    }
                }
            }
             return true;
        }

        public bool ShowYesNoMessageBox(string message, string caption)
        {
            DialogResult dialogResult = MessageBox.Show(message, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (dialogResult == DialogResult.Yes)
            {
                return true;
            }
            return false;
        }

        public bool UpdateEntityCoordinates(string unit)
        {
            // update the acadobject start and enpoint based on unit (millimeter, centimeter, decimeter, meter, kilometer, inches)
            // SDS2 python script is always in mm
            if (unit == "millimeter")
            {
                unit = "1";
            }
            else if (unit == "centimeter")
            {
                unit = "10";
            }
            else if (unit == "decimeter")
            {
                unit = "100";
            }
            else if (unit == "meter")
            {
                unit = "1000";
            }
            else if (unit == "kilometer")
            {
                unit = "1000000";
            }
            else if(unit == "inches")
            {
                unit = "25.4"; // as the python script uses mm, we need to convert inches to mm
            }
            else if (unit == "")
            {
                // no unit was entered by the user.
                return false;
            }
           
            // multiply the unit with the coordinates and round the resulting coordinates to nearest integer
            foreach (AcadObject acadObject in AcadEntityContainer)
            {
                acadObject.Acad_MemberStartPoint.X = Math.Round(acadObject.Acad_MemberStartPoint.X * Convert.ToDouble(unit), 0);
                acadObject.Acad_MemberStartPoint.Y = Math.Round(acadObject.Acad_MemberStartPoint.Y * Convert.ToDouble(unit), 0);
                acadObject.Acad_MemberStartPoint.Z = Math.Round(acadObject.Acad_MemberStartPoint.Z * Convert.ToDouble(unit), 0);
                acadObject.Acad_MemberEndPoint.X = Math.Round(acadObject.Acad_MemberEndPoint.X * Convert.ToDouble(unit), 0);
                acadObject.Acad_MemberEndPoint.Y = Math.Round(acadObject.Acad_MemberEndPoint.Y * Convert.ToDouble(unit), 0);
                acadObject.Acad_MemberEndPoint.Z = Math.Round(acadObject.Acad_MemberEndPoint.Z * Convert.ToDouble(unit), 0);
            }
            return true;            
        }

        public bool RemoveInvalidEntities()
        {
            // remove entities that are not supported by the plugin
            AcadEntityContainer.RemoveAll(x => x.Flag_IsValidEntity == false);
            if (AcadEntityContainer.Count == 0)
            {
                return false;
            }
            return true;
        }

        public HashSet<string> MatchAcadEntitiesWithUserMapping(bool byLayer)
        {
            HashSet<string> invalidLayers = new HashSet<string>();
            // match the acad objects to the user settings
            foreach (AcadObject acadObject in AcadEntityContainer)
            {
                string map_ele;
                if(byLayer)
                {
                    // by layer
                    map_ele = acadObject.Acad_Layer.ToLower();
                } else
                {
                    // by color
                    map_ele = (acadObject.Acad_Color.R + "," + acadObject.Acad_Color.G + "," + acadObject.Acad_Color.B);
                }
                MappingSettings.TryGetValue(map_ele, out UserSetting userSetting);
                if (userSetting != null)
                {
                    acadObject.SDS2_CommandString = acadObject.CreateSDS2CommandStringMember(userSetting.Type, userSetting.Profile);
                }
                else
                {
                    acadObject.Flag_IsValidEntity = false;
                    invalidLayers.Add(map_ele);
                }
            }
            // make invalidLayers unique values
            return invalidLayers;
        }

        public bool startSDS2Connection(bool process)
        {
            
            // connect with SDS2 and create elements
            using (SDS2Connect connection = new SDS2Connect())
            {   
                if(!process)
                {
                    // connection was only made due to SDS2 expecting some communication, even if it is "no communication"
                    return false;
                }
                else
                {
                    foreach (AcadObject acadObject in AcadEntityContainer)
                    {
                        if (acadObject.SDS2_CommandString != null)
                        {
                            connection.SendCommand(acadObject.SDS2_Command, acadObject.SDS2_CommandString);
                        }
                    }
                    if(AcadEntityContainer.Count > 0)
                        connection.SendCommand(Commands.ProcessJob, "");
                }                 
            }
            return true;
        }

        public void OpenUserSettingsFileInTextEditor()
        {
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\mapping.json";
            if (File.Exists(path))
            {
                System.Diagnostics.Process.Start("notepad.exe", path);
            }
        }

        private Dictionary<string,UserSetting> ReadUserSettingsFromJSON()
        {
            Dictionary<string, UserSetting> mappingsettings = new Dictionary<string, UserSetting>();
            
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\mapping.json";
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                    // iterate over the "mapping" object in JSON
                    foreach (var item in jsonObj.mapping)
                    {
                        string name = item.name.Value;
                        string profile = item.profile.Value;
                        string type = item.type.Value;
                        string map = item.map.Value;
                        bool byLayer = map == "bylayer";
                        mappingsettings.Add(name.ToLower(), new UserSetting(name.ToLower(), profile, type, byLayer));
                    }

                    //mappingsettings.Add
                    return mappingsettings;
                }
                catch (Exception) {}
            }
            return null;
        }

        private bool ParseEntity(Entity entity)
        {
            if (entity is Line)
            {
                AcadEntityContainer.Add(new AcadObject(entity as Line));
                return true;
            }
            else if (entity is Polyline)
            {
                // we have to iterate over the vertices and create multiple members per couple of vertices so: 0-1, 1-2, 2-3, etc.
                Polyline polyline = entity as Polyline;
                if (polyline != null)
                {
                    for (int i = 0; i < polyline.Vertices.Count - 1; i++)
                    {
                        AcadEntityContainer.Add(new AcadObject(polyline, new Coordinate { X = polyline.Vertices[i].Location.X, Y = polyline.Vertices[i].Location.Y, Z = polyline.Vertices[i].Location.Z },
                            new Coordinate { X = polyline.Vertices[i + 1].Location.X, Y = polyline.Vertices[i + 1].Location.Y, Z = polyline.Vertices[i + 1].Location.Z }));
                    }
                    return true;
                }
            }
            return false;
        }

        public bool ReadFile(string path)
        {
            AcadEntityContainer = new List<AcadObject>();
            string fileExtension = Path.GetExtension(path).ToLower();

            if (fileExtension == ".dwg")
            {
                try
                {
                    // For DWG files, use DwgReader.
                    using (var dwgReader = new DwgReader(path))
                    {
                        // Read the DWG file. The returned object is assumed to contain a ModelSpace property.
                        var drawing = dwgReader.Read();
                        // Iterate through all entities in ModelSpace.
                        foreach (var entity in drawing.ModelSpace.Entities)
                        {
                            ParseEntity(entity);
                        }
                    }
                }
                catch (CadNotSupportedException)
                {
                    return false;
                }
                
            }
            else if (fileExtension == ".dxf")
            {
                try
                {
                    // For DXF files, use DxfReader.
                    using (var dxfReader = new DxfReader(path))
                    {
                        var drawing = dxfReader.Read();
                        // Iterate through all entities in ModelSpace.
                        foreach (var entity in drawing.ModelSpace.Entities)
                        {
                            ParseEntity(entity);
                        }
                    }
                }
                catch (CadNotSupportedException)
                {
                    return false;
                }
                
            }
            else
            {
                return false;
            }
            return true;
        }

        public string OpenFileDialog()
        {
            string path = null;
            // Create a new instance of the OpenFileDialog.
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "DWG data (*.dwg)|*.dwg|DXF data (*.dxf)|*.dxf",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            // open the dialog and return the file path
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                // Retrieve the selected file's path.
                path = openFileDialog.FileName;
            }
            return path;
        }
    }

    class Program
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0052:Remove unread private members", Justification = "SDS/2 Dynamic Linker")]

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                bool loaded_sds2 = DesignData.SDS2.Linker.Link(DesignData.SDS2.MajorVersion.Linked);
                Run(args);
            }
            finally
            {
            }
        }

        static void Run(string[] args)
        {
                // Start the application handler
                ApplicationHandler appHandler = new ApplicationHandler();
                appHandler.Start();
        }

        // Process a notification form the reader
        private static void onNotification(object sender, NotificationEventArgs e)
        {
            //Console.WriteLine(e.Message);
        }
    }
}
