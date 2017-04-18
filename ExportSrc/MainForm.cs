using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

namespace ExportSrc
{
    public partial class MainForm : Form
    {
        private Settings _settings = Settings.GetDefault();
        public MainForm()
        {
            InitializeComponent();
            InitBinding();

            Trace.Listeners.Add(new LabelTraceListener(labelProgress));
        }

        private void InitBinding()
        {
            this.settingsBindingSource.DataSource = _settings;
            this.filterBindingSource.DataSource = _settings;
            this.filterBindingSource.DataMember = "Filters";
            this.replacementsBindingSource.DataSource = _settings;
            this.replacementsBindingSource.DataMember = "Replacements";
            this.excludedProjectsBindingSource.DataSource = _settings;
            this.excludedProjectsBindingSource.DataMember = "ExcludedProjects";
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new AboutBox().ShowDialog(this);
        }

        private void SourceDirectoryButton_Click(object sender, EventArgs e)
        {
            DialogResult result = folderBrowserDialog1.ShowDialog();
            if (result == DialogResult.OK || result == DialogResult.Yes)
            {
                textBoxSource.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private void DestinationDirectoryButton_Click(object sender, EventArgs e)
        {
            DialogResult result = folderBrowserDialog1.ShowDialog();
            if (result == DialogResult.OK || result == DialogResult.Yes)
            {
                textBoxDestination.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private void ProcessButton_Click(object sender, EventArgs e)
        {
            string src = textBoxSource.Text;
            string dst = textBoxDestination.Text;
            if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(dst))
            {
                MessageBox.Show("Please select source and destination directories.");
                return;
            }

            button3.Enabled = false;
            ThreadPool.QueueUserWorkItem(o =>
            {
                Exporter exporter = new Exporter(src, _settings);
                var result = exporter.Export(dst);
                this.BeginInvoke((Action)(() =>
                {
                    button3.Enabled = true;
                    labelProgress.Text = string.Empty;
                }));
            });

        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files)
            {
                TextBox txt = sender as TextBox;
                if (txt != null)
                {
                    txt.Text = file;
                    return;
                }

                if (string.IsNullOrEmpty(textBoxSource.Text))
                {
                    textBoxSource.Text = file;
                    return;
                }

                if (string.IsNullOrEmpty(textBoxDestination.Text))
                {
                    textBoxDestination.Text = file;
                    return;
                }

                textBoxSource.Text = file;
            }
        }

        private string _settingPath = null;

        private void SaveAs()
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.AddExtension = true;
            dialog.AutoUpgradeEnabled = true;
            dialog.Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*";

            DialogResult dialogResult = dialog.ShowDialog(this);
            if (dialogResult == DialogResult.OK || dialogResult == DialogResult.Yes)
            {
                Save(dialog.FileName);
            }
        }

        private void Save(string path)
        {
            if (path == null)
            {
                if (_settingPath == null)
                    return;

                path = _settingPath;
            }

            try
            {
                var xmlWriterSettings = new XmlWriterSettings();
                xmlWriterSettings.CloseOutput = true;
                xmlWriterSettings.Indent = true;
                xmlWriterSettings.NamespaceHandling = NamespaceHandling.OmitDuplicates;

                using (var writer = XmlWriter.Create(path, xmlWriterSettings))
                {
                    _settings.Serialize(writer);
                }

                _settingPath = path;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }


        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_settingPath == null)
            {
                SaveAs();
            }
            else
            {
                Save(_settingPath);
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*";
            DialogResult dialogResult = dialog.ShowDialog(this);
            if (dialogResult == DialogResult.OK || dialogResult == DialogResult.Yes)
            {
                try
                {
                    Settings settings = Settings.Deserialize(dialog.FileName);
                    _settings = settings;
                    InitBinding();

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveAs();
        }

        private void dataGridView3_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void dataGridView3_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files)
            {
                // TODO handle sln file

                try
                {
                    // csproj
                    XmlDocument doc = new XmlDocument();
                    doc.Load(file);

                    XmlNamespaceManager xmlnsmgr = new XmlNamespaceManager(doc.NameTable);
                    xmlnsmgr.AddNamespace("msbuild", "http://schemas.microsoft.com/developer/msbuild/2003");

                    var node = doc.SelectSingleNode("//msbuild:ProjectGuid", xmlnsmgr);
                    if (node == null)
                        return;

                    Guid projectGuid;
                    if (Guid.TryParse(node.InnerText, out projectGuid))
                    {
                        Project project = new Project();
                        project.Id = projectGuid;
                        project.Name = Path.GetFileNameWithoutExtension(file);
                        excludedProjectsBindingSource.Add(project);
                    }
                }
                catch (Exception ex)
                {
                }
            }
        }
    }
}
