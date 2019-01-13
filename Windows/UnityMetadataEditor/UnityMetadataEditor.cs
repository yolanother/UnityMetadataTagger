using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using UPHLib;

namespace UnityMetadataEditor {
    public partial class UnityMetadataEditor : Form {
        private SortedSet<string> unityVersions = new SortedSet<string>();
        private Regex unityHubExpression = new Regex("unityhub://(?<version>[^/]+)", RegexOptions.Compiled);
        private Regex regexFileNameVersion = new Regex(@"-(?<version>([0-9]+\.?)+).unitypackage", RegexOptions.Compiled);
        private SortedDictionary<string, UnityPackageFile> openFiles = new SortedDictionary<string, UnityPackageFile>();

        private SortedDictionary<string, UnityPackageFile> searchNames = new SortedDictionary<string, UnityPackageFile>();

        public WebResponse WebResponse { get; private set; }

        private HashSet<UnityPackageFile> modifiedFiles = new HashSet<UnityPackageFile>();
        private HashSet<object> modifiedFields = new HashSet<object>();

        public class UnityPackageFile : IComparable<UnityPackageFile> {
            private static string PACKAGE_EXTENSION = ".unitypackage";
            FileInfo file;
            UnityPackageHeader header;

            public bool Modified {
                get;
                set;
            }

            public string Name {
                get {
                    string name = this.file.Name.Replace(".unitypackage", "");
                    if (null != header) {
                        if (!string.IsNullOrEmpty(header["title"])) {
                            name = this.header["title"];
                        }
                        if (!string.IsNullOrEmpty(header["version"])) {
                            name += " [" + header["version"] + "]";
                        }
                    }

                    return name;
                }
            }

            public UnityPackageHeader Header {
                get {
                    return header;
                }
            }

            public String File {
                get {
                    return file.FullName;
                }
            }

            public String FileName {
                get {
                    return file.Name;
                }
            }

            public String SortKey {
                get {
                    return Name + File;
                }
            }

            public UnityPackageFile(string file) {
                this.file = new FileInfo(file);
            }

            public void LoadHeader() {
                header = UnityPackageHeader.OpenPackage(file.FullName);
            }

            public override string ToString() {
                string version = Header["version"];
                return (Modified ? "* " : "") + Name;
            }

            public int CompareTo(UnityPackageFile other) {
                return SortKey.CompareTo(other.SortKey);
            }

            public void Save() {
                if (Modified) {
                    string inputFile = File;
                    string tmpFile = inputFile + ".tmp";
                    Header.UpdatePackageFile(inputFile, tmpFile);

                    // Load header to make sure it is still parsable
                    try {
                        UnityPackageHeader updatedHeader = UnityPackageHeader.OpenPackage(tmpFile);
                        string outfile = File.Substring(0, File.Length - PACKAGE_EXTENSION.Length);
                        if (!string.IsNullOrEmpty(Header["version"]) && !outfile.Contains(Header["version"])) {
                            outfile += "-" + Header["version"];
                        }
                        outfile += PACKAGE_EXTENSION;
                        System.IO.File.Delete(inputFile);
                        System.IO.File.Move(tmpFile, outfile);
                        header = updatedHeader;
                        Modified = false;
                    } catch (Exception e) {
                        if(System.IO.File.Exists(tmpFile)) {
                            try {
                                System.IO.File.Delete(tmpFile);
                            } catch (IOException) {
                                // We'll ignore the delete for now and let the user discover it.
                                // Eventually we should add an additonal check or exception around this.
                            }
                        }
                        throw e;
                    }
                }
            }
        }


        public UnityMetadataEditor() {
            InitializeComponent();
            Thread thread = new Thread(LoadUnityVersions);
            thread.Start();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.DefaultExt = "unitypackage";
            openFileDialog.Multiselect = true;
            if (openFileDialog.ShowDialog() == DialogResult.OK) {
                foreach(string file in openFileDialog.FileNames) {
                    OpenUnityPackage(file);
                }
            }
        }

        private void OpenUnityPackage(string file, bool delayUpdate = false) {
            UnityPackageFile upf = new UnityPackageFile(file);
            upf.LoadHeader();
            string unityVersion = upf.Header["unity_version"];
            if (!string.IsNullOrEmpty(unityVersion)) {
                AddUnityVersion(unityVersion);
            }

            if (!openFiles.ContainsKey(upf.SortKey)) {
                string searchString = upf.Header["title"] + upf.File;
                searchString = searchString.ToLower().Replace(" ", "");
                searchNames.Add(searchString, upf);
                openFiles.Add(upf.SortKey, upf);
                if (!delayUpdate) {
                    lstOpenFiles.Items.Clear();
                    lstOpenFiles.Items.AddRange(openFiles.Values.ToArray());
                }
            }
        }

        private void AddUnityVersion(string unityVersion) {
            if (InvokeRequired) {
                Invoke((MethodInvoker)delegate { AddUnityVersion(unityVersion); });
                return;
            }

            if (!unityVersions.Contains(unityVersion)) {
                unityVersions.Add(unityVersion);
                txtVersionUnity.Items.Clear();
                txtVersionUnity.Items.AddRange(unityVersions.ToArray());
            }
        }

        private void openDirectoryToolStripMenuItem_Click(object sender, EventArgs e) {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            if(folderBrowserDialog.ShowDialog() == DialogResult.OK) {
                OpenDirectory(folderBrowserDialog.SelectedPath, true);
            }

            UpdateFileList();
        }

        private bool OpenDirectory(string selectedPath, bool delayUpdate = false) {
            foreach(string file in Directory.GetFiles(selectedPath, "*.unitypackage")) {
                try {
                    OpenUnityPackage(file, delayUpdate);
                } catch (Exception e) {
                    var result = MessageBox.Show(e.Message + "\n\nContinue loading remaining files?", "Load Error!", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                    if (result == DialogResult.Cancel) {
                        return false;
                    }
                }
            }

            foreach(string dir in Directory.GetDirectories(selectedPath)) {
                if (!OpenDirectory(dir)) return false;
            }

            return true;
        }

        private void AddUnityVersions(SortedSet<string> versions) {
            if (InvokeRequired) {
                Invoke((MethodInvoker)delegate { AddUnityVersions(versions); });
                return;
            }

            foreach(string version in versions) {
                AddUnityVersion(version);
            }
        }

        public void LoadUnityVersions() {
            SortedSet<string> versions = new SortedSet<string>();
            WebRequest request = WebRequest.Create("https://unity3d.com/get-unity/download/archive");
            WebResponse response = request.GetResponse();
            using(Stream stream = response.GetResponseStream()) {
                using (StreamReader reader = new StreamReader(stream)) {
                    string versionpage = reader.ReadToEnd();
                    Match match = unityHubExpression.Match(versionpage);
                    while(match.Success) {
                        string version = match.Groups["version"].Value;
                        versions.Add(version);
                        match = match.NextMatch();
                    }
                }
            }

            AddUnityVersions(versions);
        }

        private void UpdateField(UnityPackageFile file, TextBox field) {
            field.ModifiedChanged += Field_ModifiedChanged;
            field.Text = file.Header[field.Tag.ToString()];
        }

        private void Field_ModifiedChanged(object sender, EventArgs e) {
            modifiedFields.Add(sender);
        }

        private void ClearFields() {
            txtVersionAsset.Text = "";
            txtVersionId.Text = "";
            txtVersionUnity.Text = "";
            txtPackageId.Text = "";
            txtPackageTitle.Text = "";
            txtPackageDesc.Text = "";
            txtCategoryId.Text = "";
            txtCategoryLabel.Text = "";
            txtPublisherId.Text = "";
            txtPublisherLabel.Text = "";
        }

        private void EditHeader(UnityPackageFile file) {
            UpdateField(file, txtVersionAsset);
            UpdateField(file, txtVersionId);
            txtVersionUnity.Text = file.Header["unity_version"];
            UpdateField(file, txtPackageId);
            UpdateField(file, txtPackageTitle);
            UpdateField(file, txtPackageDesc);
            UpdateField(file, txtCategoryId);
            UpdateField(file, txtCategoryLabel);
            UpdateField(file, txtPublisherId);
            UpdateField(file, txtPublisherLabel);
        }

        private void LstOpenFiles_SelectedValueChanged(object sender, System.EventArgs e) {
            if(null != lstOpenFiles.SelectedItem) {
                EditHeader((UnityPackageFile) lstOpenFiles.SelectedItem);
            }
        }

        private void OnListBoxMouseMove(object sender, MouseEventArgs e) {
            string tip = "";

            //Get the item
            int index = lstOpenFiles.IndexFromPoint(e.Location);
            if ((index >= 0) && (index < lstOpenFiles.Items.Count)) {
                UnityPackageFile package = lstOpenFiles.Items[index] as UnityPackageFile;
                tip = package.File;
            }

            toolTip1.SetToolTip(lstOpenFiles, tip);
        }

        private void chkCalculateVersion_CheckedChanged(object sender, EventArgs e) {
            txtVersionAsset.Enabled = true;

            if(lstOpenFiles.SelectedItems.Count == 1) {
                UnityPackageFile package = lstOpenFiles.SelectedItem as UnityPackageFile;
                txtVersionAsset.Text = CalculateVersionFromFile(package, txtVersionAsset.Text);
            } else {
                txtVersionAsset.Enabled = !chkCalculateVersion.Checked;
            }
        }

        private string CalculateVersionFromFile(UnityPackageFile package, string version = "") {
            if (!string.IsNullOrEmpty(version)) return version;
            if (!string.IsNullOrEmpty(package.Header["version"])) return package.Header["version"];

            Match match = regexFileNameVersion.Match(package.File);
            if (match.Success) {
                version = match.Groups["version"].Value ?? "";
            }

            return version;
        }

        private void OnFieldModified(object sender, System.EventArgs e) {
            modifiedFields.Add(sender);
        }

        private void btnApply_Click(object sender, EventArgs e) {
            if(lstOpenFiles.SelectedItems.Count > 1) {
                DialogResult result = MessageBox.Show("Applying this change will apply any changed fields to all selected unity packages. Are you sure?", "Apply?", MessageBoxButtons.YesNo);
                if(DialogResult.No == result) {
                    return;
                }
            }

            List<UnityPackageFile> selected = new List<UnityPackageFile>();
            foreach(UnityPackageFile file in lstOpenFiles.SelectedItems) {
                selected.Add(file);
            }

            if(chkCalculateVersion.Checked) {
                modifiedFields.Add(txtVersionAsset);
            }

            foreach (object field in modifiedFields) {
                if(field is TextBox) {
                    TextBox textBox = field as TextBox;
                    if (textBox.Tag.ToString() == "version") {
                        // Special handling for version
                        if(chkCalculateVersion.Checked) {
                            foreach(UnityPackageFile file in lstOpenFiles.SelectedItems) {
                                file.Header["version"] = CalculateVersionFromFile(file);
                            }
                        } else {
                            // Leave version alone since it is file specific.
                        }
                    } else {
                        foreach (UnityPackageFile file in lstOpenFiles.SelectedItems) {
                            file.Header[textBox.Tag.ToString()] = textBox.Text;
                        }
                    }
                }
            }

            foreach (UnityPackageFile file in selected) {
                modifiedFiles.Add(file);
                file.Modified = true;
            }

            UpdateFileList();

            foreach (UnityPackageFile file in selected) {
                lstOpenFiles.SelectedItems.Add(file);
            }
        }

        private void UpdateFileList() {
            lstOpenFiles.Items.Clear();
            lstOpenFiles.Items.AddRange(openFiles.Values.ToArray());
        }

        private void closeOpenFilesToolStripMenuItem_Click(object sender, EventArgs e) {
            lstOpenFiles.Items.Clear();
            openFiles.Clear();
            searchNames.Clear();
        }

        private void txtFileFilter_TextChanged(object sender, EventArgs e) {
            if (txtFileFilter.Text == "") UpdateFileList();

            SortedSet<UnityPackageFile> matches = new SortedSet<UnityPackageFile>();
            string filter = txtFileFilter.Text.ToLower().Replace(" ", "");
            foreach(string search in searchNames.Keys) {
                if(search.Contains(filter)) {
                    matches.Add(searchNames[search]);
                }
            }

            lstOpenFiles.Items.Clear();
            lstOpenFiles.Items.AddRange(matches.ToArray());
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e) {
            foreach (UnityPackageFile file in modifiedFiles) {
                try {
                    file.Save();
                } catch (Exception err) {
                    var result = MessageBox.Show(err.Message + "\n\nContinue saving remaining files?", "Save Error!", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                    if(result == DialogResult.Cancel) {
                        break;
                    }
                }
            }

            modifiedFiles.Clear();
            foreach(UnityPackageFile file in openFiles.Values) {
                if (file.Modified) modifiedFiles.Add(file);
            }
        }
    }
}
