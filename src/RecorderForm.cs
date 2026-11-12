using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ChargerDrama
{
    // Lets the user record their own voice for every reaction category.
    // Takes are saved as <voicesRoot>\<voice>\<key>-<n>.wav using the winmm MCI recorder.
    internal sealed class RecorderForm : Form
    {
        private const string DefaultVoiceName = "My Voice";
        private const int MaxNameLength = 40;
        private const double MaxRecordSeconds = 15.0;
        private const string MicHint = "Check that a microphone is connected and that 'Let desktop apps access your microphone' is on in Windows Settings > Privacy & security > Microphone.";
        private static readonly string[] AudioExtensions = new string[] { ".wav", ".mp3", ".m4a", ".wma", ".aac" };

        // ---- winmm (private) ----
        [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciSendStringW")]
        private static extern int mciSendString(string command, StringBuilder returnString, int returnLength, IntPtr hwndCallback);

        [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciGetErrorStringW")]
        private static extern bool mciGetErrorString(int errorCode, StringBuilder buffer, int bufferLength);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetShortPathNameW")]
        private static extern int GetShortPathName(string longPath, StringBuilder shortPath, int bufferLength);

        // ---- state ----
        private readonly string voicesRoot;
        private readonly IList<VoiceCategory> categories;
        private readonly Action<string> playFile;
        private readonly Action stopPlayback;

        private string voiceName;
        private bool anythingRecorded;
        private bool recording;
        private string recAlias;
        private int aliasCounter;
        private Stopwatch recWatch;
        private bool suppressEvents;
        private readonly List<string> takePaths = new List<string>();

        // ---- controls ----
        private TextBox nameBox;
        private LinkLabel openFolderLink;
        private ListBox categoryList;
        private Label titleLabel;
        private Label exampleLabel;
        private Button recordButton;
        private ListBox takesList;
        private Button playButton;
        private Button deleteButton;
        private Button doneButton;
        private Timer recTimer;

        public string VoiceName { get { return voiceName; } private set { voiceName = value; } }
        public bool AnythingRecorded { get { return anythingRecorded; } private set { anythingRecorded = value; } }

        public RecorderForm(string voicesRoot, string initialVoiceName, IList<VoiceCategory> categories, Action<string> playFile, Action stopPlayback)
        {
            this.voicesRoot = voicesRoot;
            this.categories = categories ?? new List<VoiceCategory>();
            this.playFile = playFile ?? delegate(string p) { };
            this.stopPlayback = stopPlayback ?? delegate() { };
            this.voiceName = SanitizeName(string.IsNullOrEmpty(initialVoiceName) ? DefaultVoiceName : initialVoiceName);

            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9.75f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            Text = "Record your own voice - Charger Drama";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch (Exception) { }
            ClientSize = new Size(760, 500);

            BuildUi();

            recTimer = new Timer();
            recTimer.Interval = 100;
            recTimer.Tick += OnRecTimerTick;

            RefreshCategoryList(0);
            RefreshTakes(null);
            UpdateEnabledState();
        }

        // ================= UI construction =================

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.Padding = new Padding(12);
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            // Top row
            FlowLayoutPanel top = new FlowLayoutPanel();
            top.Dock = DockStyle.Fill;
            top.AutoSize = true;
            top.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            top.WrapContents = false;
            top.Margin = new Padding(0, 0, 0, 8);

            Label nameLabel = new Label();
            nameLabel.Text = "Voice name:";
            nameLabel.AutoSize = true;
            nameLabel.Margin = new Padding(0, 6, 6, 0);
            top.Controls.Add(nameLabel);

            nameBox = new TextBox();
            nameBox.Width = 240;
            nameBox.MaxLength = MaxNameLength;
            nameBox.Text = voiceName;
            nameBox.Margin = new Padding(0, 3, 12, 0);
            nameBox.Enter += OnNameEnter;
            nameBox.Leave += OnNameLeave;
            nameBox.KeyDown += OnNameKeyDown;
            top.Controls.Add(nameBox);

            openFolderLink = new LinkLabel();
            openFolderLink.Text = "Open folder";
            openFolderLink.AutoSize = true;
            openFolderLink.Margin = new Padding(0, 6, 0, 0);
            openFolderLink.LinkClicked += OnOpenFolder;
            top.Controls.Add(openFolderLink);
            root.Controls.Add(top, 0, 0);

            // Middle: list + right panel
            TableLayoutPanel mid = new TableLayoutPanel();
            mid.Dock = DockStyle.Fill;
            mid.ColumnCount = 2;
            mid.RowCount = 1;
            mid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310f));
            mid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            mid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            mid.Margin = new Padding(0);

            categoryList = new ListBox();
            categoryList.Dock = DockStyle.Fill;
            categoryList.IntegralHeight = false;
            categoryList.Margin = new Padding(0, 0, 12, 0);
            categoryList.SelectedIndexChanged += OnCategoryChanged;
            mid.Controls.Add(categoryList, 0, 0);

            TableLayoutPanel right = new TableLayoutPanel();
            right.Dock = DockStyle.Fill;
            right.ColumnCount = 1;
            right.RowCount = 7;
            right.Margin = new Padding(0);
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // title
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // say something
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // example
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // record
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // takes
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // buttons
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // tip

            titleLabel = new Label();
            titleLabel.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            titleLabel.AutoSize = false;
            titleLabel.Height = 30;
            titleLabel.Dock = DockStyle.Fill;
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.Margin = new Padding(0);
            right.Controls.Add(titleLabel, 0, 0);

            Label sayLabel = new Label();
            sayLabel.Text = "Say something like:";
            sayLabel.AutoSize = true;
            sayLabel.Margin = new Padding(0, 4, 0, 2);
            right.Controls.Add(sayLabel, 0, 1);

            exampleLabel = new Label();
            exampleLabel.Font = new Font("Segoe UI", 9.75f, FontStyle.Italic);
            exampleLabel.AutoSize = false;
            exampleLabel.Height = 44;
            exampleLabel.Dock = DockStyle.Fill;
            exampleLabel.Margin = new Padding(0, 0, 0, 8);
            right.Controls.Add(exampleLabel, 0, 2);

            recordButton = new Button();
            recordButton.Text = RecordText();
            recordButton.Height = 48;
            recordButton.Dock = DockStyle.Fill;
            recordButton.FlatStyle = FlatStyle.Flat;
            recordButton.FlatAppearance.BorderSize = 0;
            recordButton.BackColor = Color.FromArgb(200, 30, 30);
            recordButton.ForeColor = Color.White;
            recordButton.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            recordButton.UseVisualStyleBackColor = false;
            recordButton.Margin = new Padding(0, 0, 0, 8);
            recordButton.Click += OnRecordClick;
            right.Controls.Add(recordButton, 0, 3);

            takesList = new ListBox();
            takesList.Dock = DockStyle.Fill;
            takesList.IntegralHeight = false;
            takesList.Margin = new Padding(0, 0, 0, 8);
            takesList.SelectedIndexChanged += OnTakeChanged;
            takesList.DoubleClick += OnPlayClick;
            right.Controls.Add(takesList, 0, 4);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buttons.WrapContents = false;
            buttons.Dock = DockStyle.Fill;
            buttons.Margin = new Padding(0, 0, 0, 8);

            playButton = new Button();
            playButton.Text = "\u25B6 Play";
            playButton.AutoSize = true;
            playButton.MinimumSize = new Size(90, 30);
            playButton.Margin = new Padding(0, 0, 8, 0);
            playButton.Click += OnPlayClick;
            buttons.Controls.Add(playButton);

            deleteButton = new Button();
            deleteButton.Text = "Delete";
            deleteButton.AutoSize = true;
            deleteButton.MinimumSize = new Size(90, 30);
            deleteButton.Margin = new Padding(0);
            deleteButton.Click += OnDeleteClick;
            buttons.Controls.Add(deleteButton);
            right.Controls.Add(buttons, 0, 5);

            Label tip = new Label();
            tip.Text = "Tip: record 2-3 short takes per reaction. The app picks one at random each time.";
            tip.ForeColor = Color.Gray;
            tip.AutoSize = false;
            tip.Height = 40;
            tip.Dock = DockStyle.Fill;
            tip.Margin = new Padding(0);
            right.Controls.Add(tip, 0, 6);

            mid.Controls.Add(right, 1, 0);
            root.Controls.Add(mid, 0, 1);

            // Bottom row
            FlowLayoutPanel bottom = new FlowLayoutPanel();
            bottom.Dock = DockStyle.Fill;
            bottom.AutoSize = true;
            bottom.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            bottom.FlowDirection = FlowDirection.RightToLeft;
            bottom.WrapContents = false;
            bottom.Margin = new Padding(0, 8, 0, 0);

            doneButton = new Button();
            doneButton.Text = "Done";
            doneButton.DialogResult = DialogResult.OK;
            doneButton.AutoSize = true;
            doneButton.MinimumSize = new Size(100, 32);
            doneButton.Margin = new Padding(0);
            bottom.Controls.Add(doneButton);
            root.Controls.Add(bottom, 0, 2);

            AcceptButton = doneButton;
        }

        // ================= helpers: names & files =================

        private static string SanitizeName(string raw)
        {
            string s = raw == null ? string.Empty : raw.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder();
            foreach (char c in s)
            {
                if (Array.IndexOf(invalid, c) < 0) sb.Append(c);
            }
            s = sb.ToString().Trim();
            if (s.Length > MaxNameLength) s = s.Substring(0, MaxNameLength).Trim();
            s = s.TrimEnd('.', ' ');
            if (s.Length == 0) s = DefaultVoiceName;
            return s;
        }

        private string VoiceFolder()
        {
            return Path.Combine(voicesRoot, voiceName);
        }

        private static bool IsAudioFile(string path)
        {
            string ext = Path.GetExtension(path);
            foreach (string e in AudioExtensions)
            {
                if (string.Equals(e, ext, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private List<string> GetTakes(string key)
        {
            List<string> result = new List<string>();
            string folder = VoiceFolder();
            try
            {
                if (!Directory.Exists(folder)) return result;
                foreach (string f in Directory.GetFiles(folder))
                {
                    if (!IsAudioFile(f)) continue;
                    string b = Path.GetFileNameWithoutExtension(f);
                    if (b.StartsWith(key, StringComparison.OrdinalIgnoreCase)) result.Add(f);
                }
            }
            catch (Exception) { }
            result.Sort(delegate(string a, string b)
            {
                string na = Path.GetFileName(a);
                string nb = Path.GetFileName(b);
                if (na.Length != nb.Length) return na.Length.CompareTo(nb.Length);
                return string.Compare(na, nb, StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        private string NextTakePath(string key)
        {
            string folder = VoiceFolder();
            HashSet<int> used = new HashSet<int>();
            try
            {
                if (Directory.Exists(folder))
                {
                    string prefix = key + "-";
                    foreach (string f in Directory.GetFiles(folder))
                    {
                        string b = Path.GetFileNameWithoutExtension(f);
                        if (!b.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                        int n;
                        if (int.TryParse(b.Substring(prefix.Length), out n)) used.Add(n);
                    }
                }
            }
            catch (Exception) { }
            int next = 1;
            while (used.Contains(next)) next++;
            return Path.Combine(folder, key + "-" + next.ToString() + ".wav");
        }

        // Returns duration in seconds, or -1 if unknown.
        private static double ReadWavSeconds(string path)
        {
            if (!string.Equals(Path.GetExtension(path), ".wav", StringComparison.OrdinalIgnoreCase)) return -1;
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    if (fs.Length < 12) return -1;
                    string riff = new string(br.ReadChars(4));
                    br.ReadInt32();
                    string wave = new string(br.ReadChars(4));
                    if (riff != "RIFF" || wave != "WAVE") return -1;
                    int byteRate = 0;
                    while (fs.Position + 8 <= fs.Length)
                    {
                        string id = new string(br.ReadChars(4));
                        uint size = br.ReadUInt32();
                        long dataStart = fs.Position;
                        if (id == "fmt ")
                        {
                            if (size >= 12)
                            {
                                br.ReadInt16(); // format
                                br.ReadInt16(); // channels
                                br.ReadInt32(); // sample rate
                                byteRate = br.ReadInt32();
                            }
                        }
                        else if (id == "data")
                        {
                            long actual = fs.Length - dataStart;
                            long len = size;
                            if (len > actual) len = actual;
                            if (byteRate <= 0) return -1;
                            return (double)len / byteRate;
                        }
                        long nextPos = dataStart + size + (size & 1);
                        if (nextPos > fs.Length) break;
                        fs.Position = nextPos;
                    }
                }
            }
            catch (Exception) { }
            return -1;
        }

        // ================= refreshing =================

        private string CategoryText(VoiceCategory c)
        {
            return c.Title + "  (" + GetTakes(c.Key).Count.ToString() + ")";
        }

        private VoiceCategory CurrentCategory()
        {
            int i = categoryList.SelectedIndex;
            if (i < 0 || i >= categories.Count) return null;
            return categories[i];
        }

        private void RefreshCategoryList(int selectIndex)
        {
            suppressEvents = true;
            try
            {
                categoryList.BeginUpdate();
                categoryList.Items.Clear();
                foreach (VoiceCategory c in categories) categoryList.Items.Add(CategoryText(c));
                categoryList.EndUpdate();
                if (selectIndex >= 0 && selectIndex < categoryList.Items.Count) categoryList.SelectedIndex = selectIndex;
            }
            finally { suppressEvents = false; }
        }

        private void RefreshCounts()
        {
            int idx = categoryList.SelectedIndex;
            RefreshCategoryList(idx);
        }

        private void RefreshTakes(string selectPath)
        {
            VoiceCategory c = CurrentCategory();
            suppressEvents = true;
            try
            {
                takePaths.Clear();
                takesList.BeginUpdate();
                takesList.Items.Clear();
                if (c == null)
                {
                    titleLabel.Text = string.Empty;
                    exampleLabel.Text = string.Empty;
                }
                else
                {
                    titleLabel.Text = c.Title;
                    exampleLabel.Text = c.Example;
                    foreach (string p in GetTakes(c.Key))
                    {
                        takePaths.Add(p);
                        double secs = ReadWavSeconds(p);
                        string text = Path.GetFileName(p);
                        if (secs >= 0) text += "   " + secs.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s";
                        takesList.Items.Add(text);
                    }
                }
                takesList.EndUpdate();
                int sel = -1;
                if (selectPath != null)
                {
                    for (int i = 0; i < takePaths.Count; i++)
                    {
                        if (string.Equals(takePaths[i], selectPath, StringComparison.OrdinalIgnoreCase)) { sel = i; break; }
                    }
                }
                if (sel < 0 && takePaths.Count > 0 && selectPath != null) sel = takePaths.Count - 1;
                if (sel >= 0) takesList.SelectedIndex = sel;
            }
            finally { suppressEvents = false; }
            UpdateEnabledState();
        }

        private void RefreshAll()
        {
            int idx = categoryList.SelectedIndex;
            if (idx < 0) idx = 0;
            RefreshCategoryList(idx);
            RefreshTakes(null);
        }

        private void UpdateEnabledState()
        {
            bool idle = !recording;
            categoryList.Enabled = idle;
            nameBox.Enabled = idle;
            openFolderLink.Enabled = idle;
            doneButton.Enabled = idle;
            bool hasTake = idle && takesList.SelectedIndex >= 0 && takesList.SelectedIndex < takePaths.Count;
            playButton.Enabled = hasTake;
            deleteButton.Enabled = hasTake;
            recordButton.Enabled = CurrentCategory() != null;
        }

        private static string RecordText()
        {
            return "\u25CF  Record";
        }

        // ================= name handling =================

        private void ApplyName()
        {
            if (recording) return;
            string clean = SanitizeName(nameBox.Text);
            if (nameBox.Text != clean) nameBox.Text = clean;
            if (!string.Equals(clean, voiceName, StringComparison.Ordinal))
            {
                voiceName = clean;
                RefreshAll();
            }
        }

        private void OnNameEnter(object sender, EventArgs e)
        {
            // Stop Enter from triggering Done while editing the name.
            AcceptButton = null;
        }

        private void OnNameLeave(object sender, EventArgs e)
        {
            AcceptButton = doneButton;
            ApplyName();
        }

        private void OnNameKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                ApplyName();
                nameBox.SelectAll();
            }
        }

        private void OnOpenFolder(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                string folder = VoiceFolder();
                Directory.CreateDirectory(folder);
                Process.Start("explorer.exe", "\"" + folder + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open the folder:\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ================= selection events =================

        private void OnCategoryChanged(object sender, EventArgs e)
        {
            if (suppressEvents) return;
            RefreshTakes(null);
        }

        private void OnTakeChanged(object sender, EventArgs e)
        {
            if (suppressEvents) return;
            UpdateEnabledState();
        }

        private void OnPlayClick(object sender, EventArgs e)
        {
            if (recording) return;
            int i = takesList.SelectedIndex;
            if (i < 0 || i >= takePaths.Count) return;
            try { playFile(takePaths[i]); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not play the file:\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnDeleteClick(object sender, EventArgs e)
        {
            if (recording) return;
            int i = takesList.SelectedIndex;
            if (i < 0 || i >= takePaths.Count) return;
            string path = takePaths[i];
            DialogResult r = MessageBox.Show(this, "Delete " + Path.GetFileName(path) + "?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (r != DialogResult.Yes) return;
            try { stopPlayback(); } catch (Exception) { }
            try { File.Delete(path); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not delete the file:\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            int idx = categoryList.SelectedIndex;
            RefreshCategoryList(idx);
            RefreshTakes(null);
        }

        // ================= recording =================

        private static string MciError(int code)
        {
            StringBuilder sb = new StringBuilder(256);
            if (!mciGetErrorString(code, sb, sb.Capacity)) return "MCI error " + code.ToString();
            return sb.ToString();
        }

        // Returns 0 on success, otherwise the MCI error code.
        private static int Mci(string command)
        {
            return mciSendString(command, null, 0, IntPtr.Zero);
        }

        private void ShowMicError(int code)
        {
            MessageBox.Show(this, MciError(code) + "\n\n" + MicHint, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // A temp file path without spaces (8.3 form if needed), or null if none could be found.
        private static string SpaceFreeTempFile()
        {
            try
            {
                string dir = Path.GetTempPath();
                if (dir.IndexOf(' ') >= 0)
                {
                    StringBuilder sb = new StringBuilder(520);
                    int n = GetShortPathName(dir, sb, sb.Capacity);
                    if (n <= 0 || n >= sb.Capacity) return null;
                    dir = sb.ToString();
                    if (dir.IndexOf(' ') >= 0) return null;
                }
                return Path.Combine(dir, "cdrec_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".wav");
            }
            catch (Exception) { return null; }
        }

        private void OnRecordClick(object sender, EventArgs e)
        {
            if (recording) StopRecording(true);
            else StartRecording();
        }

        private void StartRecording()
        {
            if (CurrentCategory() == null) return;
            ApplyName();
            aliasCounter++;
            string alias = "cdrec" + aliasCounter.ToString();
            int rc = Mci("open new type waveaudio alias " + alias);
            if (rc != 0) { ShowMicError(rc); return; }
            rc = Mci("set " + alias + " bitspersample 16 channels 1 samplespersec 44100 bytespersec 88200 alignment 2");
            if (rc != 0)
            {
                Mci("close " + alias);
                ShowMicError(rc);
                return;
            }
            rc = Mci("record " + alias);
            if (rc != 0)
            {
                Mci("close " + alias);
                ShowMicError(rc);
                return;
            }
            recAlias = alias;
            recording = true;
            recWatch = Stopwatch.StartNew();
            UpdateRecordText();
            recTimer.Start();
            UpdateEnabledState();
        }

        private void UpdateRecordText()
        {
            double s = recWatch == null ? 0 : recWatch.Elapsed.TotalSeconds;
            if (s > MaxRecordSeconds) s = MaxRecordSeconds;
            recordButton.Text = "\u25A0  Stop  (" + s.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s)";
        }

        private void OnRecTimerTick(object sender, EventArgs e)
        {
            if (!recording) return;
            if (recWatch.Elapsed.TotalSeconds >= MaxRecordSeconds)
            {
                StopRecording(true);
                return;
            }
            UpdateRecordText();
        }

        private void StopRecording(bool save)
        {
            if (!recording) return;
            recTimer.Stop();
            string alias = recAlias;
            VoiceCategory cat = CurrentCategory();
            recording = false;
            recAlias = null;
            recordButton.Text = RecordText();

            int rc = Mci("stop " + alias);
            if (!save || cat == null)
            {
                Mci("close " + alias);
                UpdateEnabledState();
                return;
            }
            if (rc != 0)
            {
                Mci("close " + alias);
                UpdateEnabledState();
                ShowMicError(rc);
                return;
            }

            string path = null;
            try
            {
                Directory.CreateDirectory(VoiceFolder());
                path = NextTakePath(cat.Key);
            }
            catch (Exception ex)
            {
                Mci("close " + alias);
                UpdateEnabledState();
                MessageBox.Show(this, "Could not create the voice folder:\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // MCI "save" fails with error 304 on any path containing a space (even quoted),
            // so save to a space-free temp file and move it into place.
            string tmp = SpaceFreeTempFile();
            string target = tmp ?? path;
            rc = Mci("save " + alias + " \"" + target + "\"");
            Mci("close " + alias);
            if (rc == 0 && tmp != null)
            {
                try { File.Move(tmp, path); }
                catch (Exception ex)
                {
                    try { File.Delete(tmp); } catch (Exception) { }
                    UpdateEnabledState();
                    MessageBox.Show(this, "Could not save the take:\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            if (rc != 0)
            {
                if (tmp != null) { try { File.Delete(tmp); } catch (Exception) { } }
                UpdateEnabledState();
                ShowMicError(rc);
                return;
            }

            anythingRecorded = true;
            int idx = categoryList.SelectedIndex;
            RefreshCategoryList(idx);
            RefreshTakes(path);
        }

        // ================= closing =================

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (recording) StopRecording(false);
            else ApplyName();
            try { stopPlayback(); } catch (Exception) { }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (recording)
                {
                    try { Mci("stop " + recAlias); Mci("close " + recAlias); } catch (Exception) { }
                    recording = false;
                }
                if (recTimer != null) recTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

