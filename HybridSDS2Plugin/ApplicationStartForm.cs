using ImportAcadAsMembers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HybridSDS2Plugin
{
    public partial class ApplicationStartForm : Form
    {
        private readonly ApplicationHandler handler;
        private bool createbyLayer = true;
        private string unit = "";
        private bool HasConnection = false;
        private bool alignWorkpoints = false;
        private string workpointOffset = "";

        public ApplicationStartForm(ApplicationHandler handler)
        {   
            this.handler = handler;
            InitializeComponent();
        }

        private bool IsValidPath(string path) => !string.IsNullOrEmpty(path);

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            string path = handler.OpenFileDialog();
            if (!IsValidPath(path))
            {
                handler.ShowMessageBox("Invalid file path.", "Error");
            }
            else
            {
                textfieldBrowse.Text = path;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            this.Close();
            // still need to start a connection as otherwise SDS2 will crash
            StartConnectionIfNeeded(false);
        }

        private void StartConnectionIfNeeded(bool process)
        {
            if (!HasConnection)
                handler.startSDS2Connection(process);
        }

        private void ApplicationStartForm_Load(object sender, EventArgs e)
        {
            textBox1.Enabled = false;
            buttonImport.Enabled = false;
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void textfieldBrowse_TextChanged(object sender, EventArgs e)
        {
            if(comboBox1.Text != "")
                buttonImport.Enabled = true;
        }

        private void buttonImport_Click(object sender, EventArgs e)
        {

            bool status_openFile = handler.ReadFile(textfieldBrowse.Text);
            if (!status_openFile)
            {
                handler.ShowMessageBox("Error reading file.", "Error");
                return;
            }

            bool status_invalidEntities = handler.RemoveInvalidEntities();
            if (!status_invalidEntities)
            {
                handler.ShowMessageBox("No valid objects found in the file. Only Lines and Polylines are currently supported.", "Error");
                return;
            }

            bool status_coordinateUpdate = handler.UpdateEntityCoordinates(unit);
            if (!status_coordinateUpdate)
            {
                handler.ShowMessageBox("Coordinates could not be transformed. Maybe you chose an incorrect unit.", "Error");
                return;
            }

            if (!string.IsNullOrEmpty(workpointOffset) && alignWorkpoints)
            {
                if (!int.TryParse(workpointOffset, out int offset))
                {
                    handler.ShowMessageBox("Workpoint offset must be a valid number.", "Error");
                    return;
                }

                bool status_workpoint = handler.AlignWorkpoints(offset);
                if (!status_workpoint)
                {
                    handler.ShowMessageBox("Workpoints could not be aligned. Maybe you entered an incorrect offset.", "Error");
                    return;
                }
            }

            HashSet<string> status_mapping = handler.MatchAcadEntitiesWithUserMapping(createbyLayer);
            if (status_mapping.Count > 0)
            {

                bool user_wants_continue = handler.ShowYesNoMessageBox("Some elements could not be mapped to the table. Probably there is an unrecognized layer. Do you wish to continue and ignore these elements?\nUnrecognized Layers: " + string.Join(",", status_mapping), "Error");
                if(!user_wants_continue)
                {
                    return;
                }
                else
                {
                    if (!handler.RemoveInvalidEntities())
                    {
                        handler.ShowMessageBox("No valid objects found in the file. Only Lines and Polylines are currently supported.", "Error");
                        return;
                    }
                }
            }
            
            var exampleDistance = handler.GetExampleDistanceForMemberIfTooLarge(300,16000);
            if(exampleDistance != null)
            {
                bool user_wants_continue = handler.ShowYesNoMessageBox("The unit chosen for import is probably incorrect. (example member length: " + exampleDistance + " mm). Do you still wish to continue?", "Warning");
                if (!user_wants_continue)
                {
                    return;
                }
            }

            HasConnection = true;
            bool status_createEntities = handler.startSDS2Connection(true);
            if (!status_createEntities)
            {
                handler.ShowMessageBox("There was a communication error with SDS2 that prevented the creation of the members. Try to restart the tool and/or SDS2 and try again.", "Error");
                return;
            }
            this.Close();
            return;
        }

        private void radioButtonBL_CheckedChanged(object sender, EventArgs e)
        {
            createbyLayer = radioButtonBL.Checked;
        }

        private void radioButtonBC_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void textBoxStatus_TextChanged(object sender, EventArgs e)
        {

        }

        private void buttonMapping_Click(object sender, EventArgs e)
        {
            handler.OpenUserSettingsFileInTextEditor();
            bool has_saved_file = handler.ShowYesNoMessageBox("After you have saved the file, press 'Yes' to reload the mapping.", "Info");
            if (has_saved_file)
            {
                handler.SetUserPreferences(false);
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            unit = comboBox1.SelectedItem.ToString();
            if(textfieldBrowse.Text != "")
                buttonImport.Enabled = true;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            alignWorkpoints = checkBox1.Checked;
            textBox1.Enabled = checkBox1.Checked;
         
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            workpointOffset = textBox1.Text;
        }
    }
}
