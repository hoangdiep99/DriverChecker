// ================================================================
// DRIVER CHECKER v1.2
// - Kiem tra & Cap nhat Driver tu Microsoft Update
// - Sao luu & Khoi phuc Driver (Backup/Restore) khong can mang
// - Tu dong tao Restore Point truoc khi cai driver
// - Ho tro Command-line CLI (/backup, /restore, /install-all)
// - Thanh tim kiem & bo loc nhanh thiet bi
// - Icon logo tren Form & Taskbar
// - Ghi log chi tiet moi thao tac
// Tuong thich C# 5 / .NET Framework 4.x
// ================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriverChecker
{
    // ======== Logger ========
    public static class Logger
    {
        private static string _logPath;
        private static readonly object _lock = new object();

        public static void Init(string exePath)
        {
            string dir = Path.GetDirectoryName(exePath);
            if (string.IsNullOrEmpty(dir)) dir = AppDomain.CurrentDomain.BaseDirectory;
            _logPath = Path.Combine(dir, "DriverChecker.log");
            Log("========================================");
            Log("Driver Checker v1.2 started");
            Log(string.Format("OS: {0}", Environment.OSVersion));
            Log(string.Format("Machine: {0}", Environment.MachineName));
            Log("========================================");
        }

        public static void Log(string message)
        {
            if (string.IsNullOrEmpty(_logPath)) return;
            lock (_lock)
            {
                try
                {
                    string line = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}\r\n", DateTime.Now, message);
                    File.AppendAllText(_logPath, line, Encoding.UTF8);
                }
                catch { }
            }
        }

        public static string LogPath { get { return _logPath; } }
    }

    // ======== Model thiet bi phan cung ========
    public class HardwareDevice
    {
        public string DeviceName;
        public string Category;
        public string Manufacturer;
        public string CurrentVersion;
        public string DriverDate;
        public string HardwareID;
        public string Status;
        public Color StatusColor;
        public dynamic AvailableUpdate;
        public string UpdateTitle;
        public string NewVersion;

        public HardwareDevice()
        {
            DeviceName = "";
            Category = "";
            Manufacturer = "";
            CurrentVersion = "";
            DriverDate = "";
            HardwareID = "";
            Status = "\u2705 Da moi nhat";
            StatusColor = Color.FromArgb(22, 163, 74);
            AvailableUpdate = null;
            UpdateTitle = "";
            NewVersion = "";
        }
    }

    // ======== Form chinh ========
    public class MainForm : Form
    {
        // === Tab controls ===
        private TabControl tabControl;
        private TabPage tabScan;
        private TabPage tabBackup;

        // === Tab 1: Kiem tra & Cap nhat ===
        private ListView lvDevices;
        private Button btnScan;
        private Button btnUpdate;
        private Button btnExport;
        private CheckBox chkRestorePoint;
        private TextBox txtSearch;
        private Button btnSelectAll;
        private Button btnDeselectAll;

        // === Tab 2: Sao luu & Khoi phuc ===
        private Button btnBackup;
        private Button btnRestore;
        private Button btnBrowseBackup;
        private Button btnBrowseRestore;
        private TextBox txtBackupPath;
        private TextBox txtRestorePath;
        private ListView lvBackupLog;
        private Label lblBackupInfo;

        // === Chung ===
        private ProgressBar progressBar;
        private Label lblStatus;
        private Label lblTitle;
        private Label lblSummary;
        private Panel topPanel;
        private Panel bottomPanel;
        private Button btnOpenLog;

        // Data
        private List<HardwareDevice> deviceList = new List<HardwareDevice>();
        private int updatesAvailable = 0;

        // Category map
        private static readonly Dictionary<string, string> CategoryNames = InitCategoryNames();

        private static Dictionary<string, string> InitCategoryNames()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            d["DISPLAY"] = "\U0001F5A5 Card do hoa";
            d["NET"] = "\U0001F310 Card mang";
            d["MEDIA"] = "\U0001F50A Am thanh";
            d["BLUETOOTH"] = "\U0001F4F6 Bluetooth";
            d["SYSTEM"] = "\u2699 He thong";
            d["HDC"] = "\U0001F4BE Bo dieu khien dia";
            d["SCSIADAPTER"] = "\U0001F4BF Bo dieu khien SCSI";
            d["USB"] = "\U0001F50C USB";
            d["HIDCLASS"] = "\U0001F5B1 Thiet bi nhap";
            d["FIRMWARE"] = "\U0001F527 Firmware";
            d["PROCESSOR"] = "\U0001F9E0 Bo xu ly";
            d["BATTERY"] = "\U0001F50B Pin";
            d["CAMERA"] = "\U0001F4F7 Camera";
            d["IMAGE"] = "\U0001F4F7 Hinh anh";
            d["MONITOR"] = "\U0001F5A5 Man hinh";
            d["PRINTQUEUE"] = "\U0001F5A8 May in";
            d["BIOMETRIC"] = "\U0001F510 Sinh trac hoc";
            d["SENSOR"] = "\U0001F4E1 Cam bien";
            d["SOFTWAREDEVICE"] = "\U0001F4E6 Thiet bi ao";
            return d;
        }

        // ======== Entry point ========
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            Logger.Init(exePath);

            // Xu ly Command-Line Argument (neu chay bang lenh / script)
            if (args != null && args.Length > 0)
            {
                if (HandleCommandLine(args))
                    return;
            }

            Application.Run(new MainForm());
            Logger.Log("Application closed.");
        }

        // Xu ly CLI
        private static bool HandleCommandLine(string[] args)
        {
            string cmd = args[0].ToLower();
            if (cmd == "/backup" && args.Length >= 2)
            {
                string path = args[1];
                Logger.Log("CLI: Chay backup den " + path);
                try
                {
                    if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                    var psi = new ProcessStartInfo("pnputil.exe", "/export-driver * \"" + path + "\"");
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    var p = Process.Start(psi);
                    p.WaitForExit();
                    Environment.Exit(p.ExitCode);
                }
                catch { Environment.Exit(1); }
                return true;
            }
            else if (cmd == "/restore" && args.Length >= 2)
            {
                string path = args[1];
                Logger.Log("CLI: Chay restore tu " + path);
                try
                {
                    var psi = new ProcessStartInfo("pnputil.exe", "/add-driver \"" + path + "\\*.inf\" /subdirs /install");
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    var p = Process.Start(psi);
                    p.WaitForExit();
                    Environment.Exit(p.ExitCode);
                }
                catch { Environment.Exit(1); }
                return true;
            }
            return false;
        }

        public MainForm()
        {
            InitUI();
        }

        // ================================================================
        //  GIAO DIEN
        // ================================================================
        private void InitUI()
        {
            this.Text = "Driver Checker v1.2 - Kiem Tra, Cap Nhat & Sao Luu Driver";
            this.Size = new Size(1100, 700);
            this.MinimumSize = new Size(880, 560);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9.5f);
            this.BackColor = Color.FromArgb(245, 247, 250);

            // Gan Icon tu Executable len Form & Taskbar
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            // === TOP PANEL ===
            topPanel = new Panel();
            topPanel.Dock = DockStyle.Top;
            topPanel.Height = 65;
            topPanel.BackColor = Color.White;
            topPanel.Padding = new Padding(20, 10, 20, 6);
            topPanel.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(Color.FromArgb(229, 231, 235), 1))
                    e.Graphics.DrawLine(pen, 0, topPanel.Height - 1, topPanel.Width, topPanel.Height - 1);
            };

            lblTitle = new Label();
            lblTitle.Text = "\U0001F50D Driver Checker v1.2";
            lblTitle.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(17, 24, 39);
            lblTitle.Location = new Point(20, 8);
            lblTitle.AutoSize = true;

            lblSummary = new Label();
            lblSummary.Text = "Kiem tra, cap nhat tu Microsoft & Sao luu/Khoi phuc driver khong can mang. Tao Restore Point tu dong.";
            lblSummary.Font = new Font("Segoe UI", 8.8f);
            lblSummary.ForeColor = Color.FromArgb(107, 114, 128);
            lblSummary.Location = new Point(22, 38);
            lblSummary.AutoSize = true;

            topPanel.Controls.Add(lblTitle);
            topPanel.Controls.Add(lblSummary);

            // === TAB CONTROL ===
            tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;
            tabControl.Font = new Font("Segoe UI", 10f);
            tabControl.Padding = new Point(16, 6);

            // ------ TAB 1: KIEM TRA & CAP NHAT ------
            tabScan = new TabPage("\U0001F50D  Kiem Tra & Cap Nhat Driver");
            tabScan.BackColor = Color.FromArgb(245, 247, 250);
            tabScan.Padding = new Padding(0);
            BuildScanTab();
            tabControl.TabPages.Add(tabScan);

            // ------ TAB 2: SAO LUU & KHOI PHUC ------
            tabBackup = new TabPage("\U0001F4BE  Sao Luu & Khoi Phuc Driver");
            tabBackup.BackColor = Color.FromArgb(245, 247, 250);
            tabBackup.Padding = new Padding(0);
            BuildBackupTab();
            tabControl.TabPages.Add(tabBackup);

            // === BOTTOM PANEL ===
            bottomPanel = new Panel();
            bottomPanel.Dock = DockStyle.Bottom;
            bottomPanel.Height = 42;
            bottomPanel.BackColor = Color.White;
            bottomPanel.Padding = new Padding(20, 6, 20, 6);
            bottomPanel.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(Color.FromArgb(229, 231, 235), 1))
                    e.Graphics.DrawLine(pen, 0, 0, bottomPanel.Width, 0);
            };

            lblStatus = new Label();
            lblStatus.Text = "San sang. Bam \"Quet & Kiem Tra\" de bat dau...";
            lblStatus.Location = new Point(20, 10);
            lblStatus.AutoSize = true;
            lblStatus.ForeColor = Color.FromArgb(107, 114, 128);
            lblStatus.Font = new Font("Segoe UI", 9f);

            progressBar = new ProgressBar();
            progressBar.Size = new Size(220, 20);
            progressBar.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            progressBar.Visible = false;
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.MarqueeAnimationSpeed = 30;
            progressBar.Location = new Point(bottomPanel.Width - 300, 10);

            btnOpenLog = new Button();
            btnOpenLog.Text = "\U0001F4CB Log";
            btnOpenLog.Size = new Size(60, 24);
            btnOpenLog.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            btnOpenLog.Location = new Point(bottomPanel.Width - 75, 8);
            btnOpenLog.FlatStyle = FlatStyle.Flat;
            btnOpenLog.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219);
            btnOpenLog.Font = new Font("Segoe UI", 8.5f);
            btnOpenLog.Cursor = Cursors.Hand;
            btnOpenLog.Click += delegate(object s, EventArgs e)
            {
                if (File.Exists(Logger.LogPath))
                    Process.Start("notepad.exe", Logger.LogPath);
                else
                    MessageBox.Show("Chua co file log nao.", "Thong bao");
            };

            bottomPanel.Resize += delegate(object s, EventArgs e)
            {
                progressBar.Location = new Point(bottomPanel.Width - 300, 10);
                btnOpenLog.Location = new Point(bottomPanel.Width - 75, 8);
            };

            bottomPanel.Controls.Add(lblStatus);
            bottomPanel.Controls.Add(progressBar);
            bottomPanel.Controls.Add(btnOpenLog);

            // === Add to Form ===
            this.Controls.Add(tabControl);
            this.Controls.Add(topPanel);
            this.Controls.Add(bottomPanel);

            // Auto-scan khi mo app
            this.Shown += async delegate(object s, EventArgs e) { await RunFullScanAsync(); };
        }

        // ================================================================
        //  TAB 1: KIEM TRA & CAP NHAT
        // ================================================================
        private void BuildScanTab()
        {
            Panel btnPanel = new Panel();
            btnPanel.Dock = DockStyle.Top;
            btnPanel.Height = 50;
            btnPanel.BackColor = Color.White;
            btnPanel.Padding = new Padding(12, 8, 12, 8);

            btnScan = CreateButton("\U0001F50D  Quet & Kiem Tra", Color.FromArgb(37, 99, 235), true);
            btnScan.Location = new Point(12, 8);
            btnScan.Size = new Size(165, 34);
            btnScan.Click += async delegate(object s, EventArgs e) { await RunFullScanAsync(); };

            btnUpdate = CreateButton("\u26A1  Cap Nhat Driver", Color.FromArgb(22, 163, 74), true);
            btnUpdate.Location = new Point(184, 8);
            btnUpdate.Size = new Size(160, 34);
            btnUpdate.Enabled = false;
            btnUpdate.Click += async delegate(object s, EventArgs e) { await InstallSelectedAsync(); };

            btnExport = CreateButton("\U0001F4C4  Xuat Bao Cao", Color.FromArgb(107, 114, 128), false);
            btnExport.Location = new Point(351, 8);
            btnExport.Size = new Size(135, 34);
            btnExport.Click += ExportReport;

            chkRestorePoint = new CheckBox();
            chkRestorePoint.Text = "\U0001F6E1 Tao Restore Point";
            chkRestorePoint.Checked = true;
            chkRestorePoint.Location = new Point(495, 14);
            chkRestorePoint.AutoSize = true;
            chkRestorePoint.Font = new Font("Segoe UI", 9f);
            chkRestorePoint.ForeColor = Color.FromArgb(75, 85, 99);

            // O tim kiem nhanh ben phai
            Label lblSearch = new Label();
            lblSearch.Text = "\U0001F50E Loc:";
            lblSearch.Location = new Point(660, 15);
            lblSearch.AutoSize = true;
            lblSearch.Font = new Font("Segoe UI", 9f);
            lblSearch.ForeColor = Color.FromArgb(107, 114, 128);

            txtSearch = new TextBox();
            txtSearch.Font = new Font("Segoe UI", 9.5f);
            txtSearch.Location = new Point(700, 11);
            txtSearch.Size = new Size(160, 26);
            txtSearch.TextChanged += delegate(object s, EventArgs e)
            {
                PopulateListView(txtSearch.Text.Trim());
            };

            btnSelectAll = new Button();
            btnSelectAll.Text = "Tat ca";
            btnSelectAll.Size = new Size(58, 28);
            btnSelectAll.Location = new Point(868, 10);
            btnSelectAll.FlatStyle = FlatStyle.Flat;
            btnSelectAll.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219);
            btnSelectAll.Font = new Font("Segoe UI", 8.5f);
            btnSelectAll.Click += delegate(object s, EventArgs e)
            {
                foreach (ListViewItem item in lvDevices.Items) item.Checked = true;
            };

            btnDeselectAll = new Button();
            btnDeselectAll.Text = "Bo chon";
            btnDeselectAll.Size = new Size(68, 28);
            btnDeselectAll.Location = new Point(930, 10);
            btnDeselectAll.FlatStyle = FlatStyle.Flat;
            btnDeselectAll.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219);
            btnDeselectAll.Font = new Font("Segoe UI", 8.5f);
            btnDeselectAll.Click += delegate(object s, EventArgs e)
            {
                foreach (ListViewItem item in lvDevices.Items) item.Checked = false;
            };

            btnPanel.Controls.Add(btnScan);
            btnPanel.Controls.Add(btnUpdate);
            btnPanel.Controls.Add(btnExport);
            btnPanel.Controls.Add(chkRestorePoint);
            btnPanel.Controls.Add(lblSearch);
            btnPanel.Controls.Add(txtSearch);
            btnPanel.Controls.Add(btnSelectAll);
            btnPanel.Controls.Add(btnDeselectAll);

            // ListView
            lvDevices = new ListView();
            lvDevices.Dock = DockStyle.Fill;
            lvDevices.View = View.Details;
            lvDevices.FullRowSelect = true;
            lvDevices.GridLines = true;
            lvDevices.CheckBoxes = true;
            lvDevices.Font = new Font("Segoe UI", 9f);
            lvDevices.BackColor = Color.White;
            lvDevices.BorderStyle = BorderStyle.None;

            lvDevices.Columns.Add("", 35, HorizontalAlignment.Center);
            lvDevices.Columns.Add("Phan loai", 140);
            lvDevices.Columns.Add("Ten thiet bi", 250);
            lvDevices.Columns.Add("Nha san xuat", 115);
            lvDevices.Columns.Add("Version hien tai", 125);
            lvDevices.Columns.Add("Version moi", 125);
            lvDevices.Columns.Add("Ngay Driver", 90);
            lvDevices.Columns.Add("Trang thai", 175);

            tabScan.Controls.Add(lvDevices);
            tabScan.Controls.Add(btnPanel);
        }

        // ================================================================
        //  TAB 2: SAO LUU & KHOI PHUC
        // ================================================================
        private void BuildBackupTab()
        {
            Panel mainPanel = new Panel();
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.Padding = new Padding(20);

            // --- Khung Sao luu ---
            GroupBox grpBackup = new GroupBox();
            grpBackup.Text = "\U0001F4BE  Sao luu Driver (Backup)";
            grpBackup.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            grpBackup.Dock = DockStyle.Top;
            grpBackup.Height = 130;
            grpBackup.Padding = new Padding(15, 20, 15, 10);
            grpBackup.ForeColor = Color.FromArgb(37, 99, 235);

            Label lblBackupDesc = new Label();
            lblBackupDesc.Text = "Xuat toan bo driver OEM dang cai tren may ra thu muc. Copy ra USB de dung khi cai lai Windows.";
            lblBackupDesc.Font = new Font("Segoe UI", 9f);
            lblBackupDesc.ForeColor = Color.FromArgb(75, 85, 99);
            lblBackupDesc.Location = new Point(15, 25);
            lblBackupDesc.Size = new Size(700, 20);

            Label lblBkPath = new Label();
            lblBkPath.Text = "Thu muc luu:";
            lblBkPath.Font = new Font("Segoe UI", 9f);
            lblBkPath.ForeColor = Color.FromArgb(55, 65, 81);
            lblBkPath.Location = new Point(15, 55);
            lblBkPath.AutoSize = true;

            txtBackupPath = new TextBox();
            txtBackupPath.Font = new Font("Segoe UI", 9.5f);
            txtBackupPath.Location = new Point(100, 52);
            txtBackupPath.Size = new Size(500, 28);
            txtBackupPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "DriverBackup");

            btnBrowseBackup = new Button();
            btnBrowseBackup.Text = "...";
            btnBrowseBackup.Location = new Point(608, 51);
            btnBrowseBackup.Size = new Size(35, 28);
            btnBrowseBackup.Click += delegate(object s, EventArgs e)
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Chon thu muc de luu driver backup";
                    if (fbd.ShowDialog() == DialogResult.OK)
                        txtBackupPath.Text = fbd.SelectedPath;
                }
            };

            btnBackup = CreateButton("\U0001F4BE  Bat Dau Sao Luu", Color.FromArgb(37, 99, 235), true);
            btnBackup.Location = new Point(15, 88);
            btnBackup.Size = new Size(185, 34);
            btnBackup.Click += async delegate(object s, EventArgs e) { await BackupDriversAsync(); };

            lblBackupInfo = new Label();
            lblBackupInfo.Text = "";
            lblBackupInfo.Font = new Font("Segoe UI", 9f);
            lblBackupInfo.ForeColor = Color.FromArgb(22, 163, 74);
            lblBackupInfo.Location = new Point(210, 95);
            lblBackupInfo.AutoSize = true;

            grpBackup.Controls.Add(lblBackupDesc);
            grpBackup.Controls.Add(lblBkPath);
            grpBackup.Controls.Add(txtBackupPath);
            grpBackup.Controls.Add(btnBrowseBackup);
            grpBackup.Controls.Add(btnBackup);
            grpBackup.Controls.Add(lblBackupInfo);

            // --- Khung Khoi phuc ---
            GroupBox grpRestore = new GroupBox();
            grpRestore.Text = "\U0001F504  Khoi phuc Driver (Restore)";
            grpRestore.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            grpRestore.Dock = DockStyle.Top;
            grpRestore.Height = 130;
            grpRestore.Padding = new Padding(15, 20, 15, 10);
            grpRestore.ForeColor = Color.FromArgb(22, 163, 74);

            Label lblRestoreDesc = new Label();
            lblRestoreDesc.Text = "Chon thu muc chua driver da sao luu truoc do. Tat ca driver se duoc cai lai vao may (KHONG CAN MANG).";
            lblRestoreDesc.Font = new Font("Segoe UI", 9f);
            lblRestoreDesc.ForeColor = Color.FromArgb(75, 85, 99);
            lblRestoreDesc.Location = new Point(15, 25);
            lblRestoreDesc.Size = new Size(750, 20);

            Label lblRsPath = new Label();
            lblRsPath.Text = "Thu muc backup:";
            lblRsPath.Font = new Font("Segoe UI", 9f);
            lblRsPath.ForeColor = Color.FromArgb(55, 65, 81);
            lblRsPath.Location = new Point(15, 55);
            lblRsPath.AutoSize = true;

            txtRestorePath = new TextBox();
            txtRestorePath.Font = new Font("Segoe UI", 9.5f);
            txtRestorePath.Location = new Point(125, 52);
            txtRestorePath.Size = new Size(475, 28);

            btnBrowseRestore = new Button();
            btnBrowseRestore.Text = "...";
            btnBrowseRestore.Location = new Point(608, 51);
            btnBrowseRestore.Size = new Size(35, 28);
            btnBrowseRestore.Click += delegate(object s, EventArgs e)
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Chon thu muc chua driver backup";
                    if (fbd.ShowDialog() == DialogResult.OK)
                        txtRestorePath.Text = fbd.SelectedPath;
                }
            };

            btnRestore = CreateButton("\U0001F504  Bat Dau Khoi Phuc", Color.FromArgb(22, 163, 74), true);
            btnRestore.Location = new Point(15, 88);
            btnRestore.Size = new Size(195, 34);
            btnRestore.Click += async delegate(object s, EventArgs e) { await RestoreDriversAsync(); };

            grpRestore.Controls.Add(lblRestoreDesc);
            grpRestore.Controls.Add(lblRsPath);
            grpRestore.Controls.Add(txtRestorePath);
            grpRestore.Controls.Add(btnBrowseRestore);
            grpRestore.Controls.Add(btnRestore);

            // --- Log hien thi ===
            lvBackupLog = new ListView();
            lvBackupLog.Dock = DockStyle.Fill;
            lvBackupLog.View = View.Details;
            lvBackupLog.FullRowSelect = true;
            lvBackupLog.GridLines = true;
            lvBackupLog.Font = new Font("Consolas", 9f);
            lvBackupLog.BackColor = Color.FromArgb(249, 250, 251);
            lvBackupLog.BorderStyle = BorderStyle.FixedSingle;
            lvBackupLog.Columns.Add("Thoi gian", 85);
            lvBackupLog.Columns.Add("Thao tac", 110);
            lvBackupLog.Columns.Add("Chi tiet", 700);

            Panel spacer1 = new Panel();
            spacer1.Dock = DockStyle.Top;
            spacer1.Height = 8;
            spacer1.BackColor = Color.FromArgb(245, 247, 250);

            Panel spacer2 = new Panel();
            spacer2.Dock = DockStyle.Top;
            spacer2.Height = 8;
            spacer2.BackColor = Color.FromArgb(245, 247, 250);

            mainPanel.Controls.Add(lvBackupLog);
            mainPanel.Controls.Add(spacer2);
            mainPanel.Controls.Add(grpRestore);
            mainPanel.Controls.Add(spacer1);
            mainPanel.Controls.Add(grpBackup);

            tabBackup.Controls.Add(mainPanel);
        }

        // ================================================================
        //  QUET PHAN CUNG + KIEM TRA CAP NHAT
        // ================================================================
        private async Task RunFullScanAsync()
        {
            btnScan.Enabled = false;
            btnUpdate.Enabled = false;
            lvDevices.Items.Clear();
            deviceList.Clear();
            updatesAvailable = 0;
            progressBar.Visible = true;
            lblStatus.Text = "\u23F3 Dang quet danh sach phan cung tren may...";
            lblStatus.ForeColor = Color.FromArgb(37, 99, 235);
            Logger.Log("--- Bat dau quet phan cung ---");

            await Task.Run(new Action(delegate()
            {
                try
                {
                    var searcher = new ManagementObjectSearcher(
                        "SELECT DeviceName, DeviceClass, Manufacturer, DriverVersion, DriverDate, DeviceID, HardWareID " +
                        "FROM Win32_PnPSignedDriver WHERE DeviceName IS NOT NULL");

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string deviceClass = (obj["DeviceClass"] ?? "").ToString().Trim();
                        if (string.IsNullOrEmpty(deviceClass)) continue;
                        if (!CategoryNames.ContainsKey(deviceClass)) continue;

                        string deviceName = (obj["DeviceName"] ?? "").ToString().Trim();
                        if (string.IsNullOrEmpty(deviceName)) continue;

                        string nameLower = deviceName.ToLower();
                        if (nameLower.Contains("root") && nameLower.Contains("enumerator")) continue;
                        if (nameLower == "plug and play software device enumerator") continue;
                        if (nameLower == "microsoft system management bios driver") continue;

                        string rawDate = (obj["DriverDate"] ?? "").ToString();
                        string formattedDate = "N/A";
                        if (rawDate.Length >= 8)
                            formattedDate = rawDate.Substring(0, 4) + "-" + rawDate.Substring(4, 2) + "-" + rawDate.Substring(6, 2);

                        string hwId = "";
                        try
                        {
                            var hwIds = obj["HardWareID"] as string[];
                            if (hwIds != null && hwIds.Length > 0) hwId = hwIds[0];
                        }
                        catch { }
                        if (string.IsNullOrEmpty(hwId))
                            hwId = (obj["DeviceID"] ?? "").ToString();

                        var dev = new HardwareDevice();
                        dev.DeviceName = deviceName;
                        dev.Category = CategoryNames.ContainsKey(deviceClass) ? CategoryNames[deviceClass] : deviceClass;
                        dev.Manufacturer = (obj["Manufacturer"] ?? "").ToString();
                        dev.CurrentVersion = (obj["DriverVersion"] ?? "Chua cai").ToString();
                        dev.DriverDate = formattedDate;
                        dev.HardwareID = hwId;
                        deviceList.Add(dev);

                        Logger.Log(string.Format("  Tim thay: [{0}] {1} - v{2}", deviceClass, deviceName, dev.CurrentVersion));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log("LOI WMI: " + ex.Message);
                    SafeInvoke(delegate()
                    {
                        MessageBox.Show("Loi khi quet phan cung: " + ex.Message,
                            "Loi WMI", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
            }));

            Logger.Log(string.Format("Tong so thiet bi tim thay: {0}", deviceList.Count));
            deviceList = deviceList.OrderBy(d => d.Category).ThenBy(d => d.DeviceName).ToList();
            PopulateListView();
            lblStatus.Text = string.Format(
                "\u23F3 Da tim thay {0} thiet bi. Dang kiem tra ban cap nhat tu Microsoft...", deviceList.Count);

            // Windows Update
            Logger.Log("--- Ket noi Windows Update ---");
            await Task.Run(new Action(delegate()
            {
                try
                {
                    try
                    {
                        var wuSvc = System.ServiceProcess.ServiceController.GetServices()
                            .FirstOrDefault(s => s.ServiceName == "wuauserv");
                        if (wuSvc != null && wuSvc.Status != System.ServiceProcess.ServiceControllerStatus.Running)
                        {
                            wuSvc.Start();
                            wuSvc.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                            Logger.Log("Da khoi dong dich vu Windows Update.");
                        }
                    }
                    catch (Exception ex) { Logger.Log("Canh bao: Khong the khoi dong wuauserv: " + ex.Message); }

                    Type sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
                    if (sessionType == null)
                    {
                        Logger.Log("LOI: Khong tim thay Windows Update API.");
                        SafeInvoke(delegate()
                        {
                            lblStatus.Text = "\u26A0 Khong tim thay Windows Update API tren may nay.";
                            lblStatus.ForeColor = Color.FromArgb(202, 138, 4);
                        });
                        return;
                    }

                    dynamic session = Activator.CreateInstance(sessionType);
                    dynamic uSearcher = session.CreateUpdateSearcher();
                    Logger.Log("Dang tim kiem driver tu Microsoft Update Catalog...");
                    dynamic result = uSearcher.Search("IsInstalled=0 and Type='Driver'");
                    int count = result.Updates.Count;
                    Logger.Log(string.Format("Tim thay {0} ban cap nhat driver.", count));

                    if (count == 0)
                    {
                        SafeInvoke(delegate()
                        {
                            lblStatus.Text = string.Format(
                                "\u2705 Tat ca {0} thiet bi deu da co driver moi nhat!", deviceList.Count);
                            lblStatus.ForeColor = Color.FromArgb(22, 163, 74);
                        });
                        return;
                    }

                    for (int i = 0; i < count; i++)
                    {
                        dynamic update = result.Updates.Item(i);
                        string updateTitle = update.Title.ToString();
                        string newVer = ExtractVersionFromTitle(updateTitle);

                        bool matched = false;
                        foreach (var dev in deviceList)
                        {
                            if (IsDriverMatch(dev, updateTitle))
                            {
                                dev.Status = "\U0001F504 Co ban cap nhat moi";
                                dev.StatusColor = Color.FromArgb(202, 138, 4);
                                dev.AvailableUpdate = update;
                                dev.UpdateTitle = updateTitle;
                                dev.NewVersion = newVer;
                                updatesAvailable++;
                                matched = true;
                                Logger.Log(string.Format("  Cap nhat: {0} -> {1}", dev.DeviceName, updateTitle));
                                break;
                            }
                        }

                        if (!matched)
                        {
                            string cat = GuessCategory(updateTitle);
                            var newDev = new HardwareDevice();
                            newDev.DeviceName = updateTitle;
                            newDev.Category = cat;
                            newDev.CurrentVersion = "Chua co";
                            newDev.DriverDate = "N/A";
                            newDev.Status = "\U0001F534 Can cai dat";
                            newDev.StatusColor = Color.FromArgb(220, 38, 38);
                            newDev.AvailableUpdate = update;
                            newDev.UpdateTitle = updateTitle;
                            newDev.NewVersion = newVer;
                            deviceList.Add(newDev);
                            updatesAvailable++;
                            Logger.Log(string.Format("  Driver moi: {0}", updateTitle));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log("LOI Windows Update: " + ex.Message);
                    SafeInvoke(delegate()
                    {
                        lblStatus.Text = "\u26A0 Loi ket noi Windows Update: " + ex.Message;
                        lblStatus.ForeColor = Color.FromArgb(220, 38, 38);
                    });
                }
            }));

            PopulateListView();
            progressBar.Visible = false;
            btnScan.Enabled = true;
            btnUpdate.Enabled = updatesAvailable > 0;

            if (updatesAvailable > 0)
            {
                lblStatus.Text = string.Format(
                    "Hoan tat: {0} thiet bi | \U0001F504 {1} ban cap nhat kha dung.",
                    deviceList.Count, updatesAvailable);
                lblStatus.ForeColor = Color.FromArgb(202, 138, 4);
            }
            else
            {
                lblStatus.Text = string.Format(
                    "\u2705 Hoan tat: {0} thiet bi - Tat ca da co driver moi nhat!",
                    deviceList.Count);
                lblStatus.ForeColor = Color.FromArgb(22, 163, 74);
            }
            Logger.Log(string.Format("--- Ket thuc quet: {0} thiet bi, {1} cap nhat ---", deviceList.Count, updatesAvailable));
        }

        // ================================================================
        //  CAI DAT DRIVER + RESTORE POINT
        // ================================================================
        private async Task InstallSelectedAsync()
        {
            var toInstall = new List<HardwareDevice>();
            foreach (ListViewItem lvi in lvDevices.Items)
            {
                if (lvi.Checked)
                {
                    var dev = lvi.Tag as HardwareDevice;
                    if (dev != null && dev.AvailableUpdate != null)
                        toInstall.Add(dev);
                }
            }

            if (toInstall.Count == 0)
            {
                MessageBox.Show("Vui long tick chon it nhat 1 driver can cap nhat!",
                    "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                string.Format("Ban co chac muon tai va cai dat {0} driver tu Microsoft?", toInstall.Count),
                "Xac nhan cap nhat", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            btnScan.Enabled = false;
            btnUpdate.Enabled = false;
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Maximum = toInstall.Count * 2 + (chkRestorePoint.Checked ? 1 : 0);
            progressBar.Value = 0;

            Logger.Log(string.Format("--- Bat dau cai dat {0} driver ---", toInstall.Count));

            if (chkRestorePoint.Checked)
            {
                lblStatus.Text = "\U0001F6E1 Dang tao diem khoi phuc he thong (Restore Point)...";
                lblStatus.ForeColor = Color.FromArgb(37, 99, 235);

                bool rpOk = await Task.Run(new Func<bool>(delegate() { return CreateRestorePoint(); }));
                SafeInvoke(delegate() { progressBar.Value = Math.Min(progressBar.Value + 1, progressBar.Maximum); });

                if (!rpOk)
                {
                    var cont = MessageBox.Show(
                        "Khong the tao Restore Point!\nBan co muon tiep tuc cai dat driver khong?",
                        "Canh bao", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (cont != DialogResult.Yes)
                    {
                        btnScan.Enabled = true;
                        btnUpdate.Enabled = true;
                        progressBar.Visible = false;
                        return;
                    }
                }
            }

            bool needReboot = false;
            int successCount = 0;
            int failCount = 0;

            await Task.Run(new Action(delegate()
            {
                try
                {
                    Type sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
                    dynamic session = Activator.CreateInstance(sessionType);
                    Type collType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl");

                    foreach (var dev in toInstall)
                    {
                        SafeInvoke(delegate()
                        {
                            lblStatus.Text = "\u2B07 Dang tai: " + dev.DeviceName + "...";
                            lblStatus.ForeColor = Color.FromArgb(37, 99, 235);
                        });
                        Logger.Log("Tai xuong: " + dev.DeviceName);

                        try
                        {
                            try { dev.AvailableUpdate.AcceptEula(); } catch { }

                            dynamic dlColl = Activator.CreateInstance(collType);
                            dlColl.Add(dev.AvailableUpdate);

                            dynamic downloader = session.CreateUpdateDownloader();
                            downloader.Updates = dlColl;
                            downloader.Download();
                            SafeInvoke(delegate() { progressBar.Value = Math.Min(progressBar.Value + 1, progressBar.Maximum); });

                            SafeInvoke(delegate() { lblStatus.Text = "\u2699 Dang cai dat: " + dev.DeviceName + "..."; });
                            Logger.Log("Cai dat: " + dev.DeviceName);

                            dynamic installer = session.CreateUpdateInstaller();
                            installer.Updates = dlColl;
                            dynamic instResult = installer.Install();

                            int resultCode = instResult.GetUpdateResult(0).ResultCode;
                            if (resultCode == 2 || resultCode == 3)
                            {
                                successCount++;
                                dev.Status = "\u2705 Da cap nhat!";
                                dev.StatusColor = Color.FromArgb(22, 163, 74);
                                Logger.Log("THANH CONG: " + dev.DeviceName);
                                if ((bool)instResult.RebootRequired) needReboot = true;
                            }
                            else
                            {
                                failCount++;
                                dev.Status = string.Format("\u274C That bai (Ma: {0})", resultCode);
                                dev.StatusColor = Color.FromArgb(220, 38, 38);
                                Logger.Log(string.Format("THAT BAI: {0} - Ma loi: {1}", dev.DeviceName, resultCode));
                            }
                        }
                        catch (Exception ex)
                        {
                            failCount++;
                            string msg = ex.Message;
                            if (msg.Length > 60) msg = msg.Substring(0, 60);
                            dev.Status = "\u274C Loi: " + msg;
                            dev.StatusColor = Color.FromArgb(220, 38, 38);
                            Logger.Log(string.Format("LOI: {0} - {1}", dev.DeviceName, ex.Message));
                        }

                        SafeInvoke(delegate() { progressBar.Value = Math.Min(progressBar.Value + 1, progressBar.Maximum); });
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log("LOI NGHIEM TRONG: " + ex.Message);
                    SafeInvoke(delegate()
                    {
                        MessageBox.Show("Loi: " + ex.Message, "Loi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
            }));

            Logger.Log(string.Format("--- Ket thuc cai dat: {0} thanh cong, {1} that bai ---", successCount, failCount));
            PopulateListView();
            progressBar.Visible = false;
            btnScan.Enabled = true;

            string resultMsg = string.Format("Hoan tat cap nhat!\n\n\u2705 Thanh cong: {0}\n\u274C That bai: {1}", successCount, failCount);
            lblStatus.Text = string.Format("\u2705 Hoan tat: {0} thanh cong, {1} that bai.", successCount, failCount);
            lblStatus.ForeColor = failCount == 0 ? Color.FromArgb(22, 163, 74) : Color.FromArgb(202, 138, 4);

            if (needReboot)
            {
                resultMsg += "\n\n\u26A0 Mot so driver yeu cau KHOI DONG LAI MAY.\nBan co muon Restart ngay?";
                if (MessageBox.Show(resultMsg, "Hoan tat", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Process.Start("shutdown.exe", "/r /t 5 /c \"Khoi dong lai de hoan tat cap nhat Driver.\"");
                }
            }
            else
            {
                MessageBox.Show(resultMsg, "Hoan tat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // ================================================================
        //  TAO RESTORE POINT
        // ================================================================
        private bool CreateRestorePoint()
        {
            Logger.Log("Dang tao System Restore Point...");
            try
            {
                try
                {
                    var enableSR = new ManagementClass("root\\default", "SystemRestore", new ObjectGetOptions());
                    var inParams = enableSR.GetMethodParameters("Enable");
                    inParams["Drive"] = "C:\\";
                    enableSR.InvokeMethod("Enable", inParams, null);
                }
                catch { }

                var srClass = new ManagementClass("root\\default", "SystemRestore", new ObjectGetOptions());
                var createParams = srClass.GetMethodParameters("CreateRestorePoint");
                createParams["Description"] = string.Format("Driver Checker - Truoc khi cap nhat driver ({0:dd/MM/yyyy HH:mm})", DateTime.Now);
                createParams["RestorePointType"] = 10;
                createParams["EventType"] = 100;

                var result = srClass.InvokeMethod("CreateRestorePoint", createParams, null);
                uint retVal = (uint)result["ReturnValue"];

                if (retVal == 0)
                {
                    Logger.Log("Tao Restore Point THANH CONG.");
                    return true;
                }
                else
                {
                    Logger.Log(string.Format("Tao Restore Point THAT BAI - Ma: {0}", retVal));
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Log("LOI tao Restore Point: " + ex.Message);
                return false;
            }
        }

        // ================================================================
        //  BACKUP DRIVER
        // ================================================================
        private async Task BackupDriversAsync()
        {
            string backupPath = txtBackupPath.Text.Trim();
            if (string.IsNullOrEmpty(backupPath))
            {
                MessageBox.Show("Vui long nhap duong dan thu muc luu!", "Thong bao");
                return;
            }

            btnBackup.Enabled = false;
            btnRestore.Enabled = false;
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;
            lblStatus.Text = "\U0001F4BE Dang sao luu toan bo driver...";
            lblStatus.ForeColor = Color.FromArgb(37, 99, 235);
            Logger.Log("--- Bat dau Backup Driver ---");
            Logger.Log("Thu muc dich: " + backupPath);

            AddBackupLog("SAO LUU", "Bat dau sao luu driver...");

            bool success = false;
            string output = "";

            await Task.Run(new Action(delegate()
            {
                try
                {
                    if (!Directory.Exists(backupPath))
                        Directory.CreateDirectory(backupPath);

                    var psi = new ProcessStartInfo();
                    psi.FileName = "pnputil.exe";
                    psi.Arguments = "/export-driver * \"" + backupPath + "\"";
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    psi.CreateNoWindow = true;

                    var proc = Process.Start(psi);
                    output = proc.StandardOutput.ReadToEnd();
                    string error = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();

                    success = proc.ExitCode == 0;
                }
                catch (Exception ex)
                {
                    Logger.Log("LOI Backup: " + ex.Message);
                    output = ex.Message;
                }
            }));

            progressBar.Visible = false;
            btnBackup.Enabled = true;
            btnRestore.Enabled = true;

            if (success)
            {
                int drvCount = 0;
                try { drvCount = Directory.GetDirectories(backupPath).Length; } catch { }

                string msg = string.Format("Sao luu THANH CONG!\n{0} goi driver da duoc xuat ra:\n{1}", drvCount, backupPath);
                lblStatus.Text = string.Format("\u2705 Backup thanh cong: {0} goi driver.", drvCount);
                lblStatus.ForeColor = Color.FromArgb(22, 163, 74);
                lblBackupInfo.Text = string.Format("\u2705 {0} goi driver da luu.", drvCount);
                Logger.Log(string.Format("Backup THANH CONG: {0} goi driver.", drvCount));
                AddBackupLog("THANH CONG", string.Format("{0} goi driver da luu tai: {1}", drvCount, backupPath));

                if (MessageBox.Show(msg + "\n\nMo thu muc xem?", "Thanh cong",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    Process.Start("explorer.exe", backupPath);
                }
            }
            else
            {
                lblStatus.Text = "\u274C Backup that bai!";
                lblStatus.ForeColor = Color.FromArgb(220, 38, 38);
                lblBackupInfo.Text = "\u274C That bai!";
                lblBackupInfo.ForeColor = Color.FromArgb(220, 38, 38);
                Logger.Log("Backup THAT BAI: " + output);
                AddBackupLog("THAT BAI", output);
                MessageBox.Show("Sao luu that bai!\n" + output, "Loi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ================================================================
        //  RESTORE DRIVER
        // ================================================================
        private async Task RestoreDriversAsync()
        {
            string restorePath = txtRestorePath.Text.Trim();
            if (string.IsNullOrEmpty(restorePath) || !Directory.Exists(restorePath))
            {
                MessageBox.Show("Thu muc backup khong ton tai!\nVui long chon lai.", "Thong bao");
                return;
            }

            int infCount = 0;
            try { infCount = Directory.GetFiles(restorePath, "*.inf", SearchOption.AllDirectories).Length; } catch { }
            if (infCount == 0)
            {
                MessageBox.Show("Khong tim thay file driver (.inf) nao trong thu muc nay!", "Thong bao");
                return;
            }

            var confirm = MessageBox.Show(
                string.Format("Tim thay {0} file driver (.inf).\nBan co chac muon khoi phuc tat ca vao may?", infCount),
                "Xac nhan khoi phuc", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            btnBackup.Enabled = false;
            btnRestore.Enabled = false;
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;
            lblStatus.Text = "\U0001F504 Dang khoi phuc driver tu backup...";
            lblStatus.ForeColor = Color.FromArgb(37, 99, 235);
            Logger.Log("--- Bat dau Restore Driver ---");
            Logger.Log("Thu muc nguon: " + restorePath);
            AddBackupLog("KHOI PHUC", string.Format("Bat dau khoi phuc {0} driver tu: {1}", infCount, restorePath));

            if (chkRestorePoint.Checked)
            {
                lblStatus.Text = "\U0001F6E1 Dang tao Restore Point...";
                await Task.Run(new Action(delegate() { CreateRestorePoint(); }));
            }

            bool success = false;
            string output = "";

            await Task.Run(new Action(delegate()
            {
                try
                {
                    var psi = new ProcessStartInfo();
                    psi.FileName = "pnputil.exe";
                    psi.Arguments = "/add-driver \"" + restorePath + "\\*.inf\" /subdirs /install";
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    psi.CreateNoWindow = true;

                    var proc = Process.Start(psi);
                    output = proc.StandardOutput.ReadToEnd();
                    string error = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();

                    success = proc.ExitCode == 0 || proc.ExitCode == 3010;
                }
                catch (Exception ex)
                {
                    Logger.Log("LOI Restore: " + ex.Message);
                    output = ex.Message;
                }
            }));

            progressBar.Visible = false;
            btnBackup.Enabled = true;
            btnRestore.Enabled = true;

            if (success)
            {
                lblStatus.Text = "\u2705 Khoi phuc driver thanh cong!";
                lblStatus.ForeColor = Color.FromArgb(22, 163, 74);
                Logger.Log("Restore THANH CONG.");
                AddBackupLog("THANH CONG", "Da khoi phuc driver thanh cong.");

                if (MessageBox.Show("Khoi phuc driver thanh cong!\nBan co muon khoi dong lai may de hoan tat?",
                    "Thanh cong", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Process.Start("shutdown.exe", "/r /t 5 /c \"Khoi dong lai de hoan tat khoi phuc Driver.\"");
                }
            }
            else
            {
                lblStatus.Text = "\u274C Khoi phuc that bai!";
                lblStatus.ForeColor = Color.FromArgb(220, 38, 38);
                Logger.Log("Restore THAT BAI: " + output);
                AddBackupLog("THAT BAI", output);
                MessageBox.Show("Khoi phuc that bai!\n" + output, "Loi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ================================================================
        //  XUAT BAO CAO
        // ================================================================
        private void ExportReport(object sender, EventArgs e)
        {
            if (deviceList.Count == 0)
            {
                MessageBox.Show("Chua co du lieu. Hay bam Quet truoc!", "Thong bao");
                return;
            }

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Text File|*.txt|CSV File|*.csv";
                sfd.FileName = string.Format("DriverReport_{0}_{1:yyyyMMdd_HHmmss}", Environment.MachineName, DateTime.Now);

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var lines = new List<string>();
                        lines.Add("=====================================================================");
                        lines.Add(string.Format("  BAO CAO DRIVER - May: {0}", Environment.MachineName));
                        lines.Add(string.Format("  Thoi gian: {0:dd/MM/yyyy HH:mm:ss}", DateTime.Now));
                        lines.Add(string.Format("  He dieu hanh: {0}", Environment.OSVersion));
                        lines.Add("=====================================================================");
                        lines.Add("");

                        foreach (var dev in deviceList)
                        {
                            lines.Add(string.Format("[{0}] {1}", dev.Category, dev.DeviceName));
                            lines.Add(string.Format("   Nha SX:       {0}", dev.Manufacturer));
                            lines.Add(string.Format("   Phien ban:    {0}", dev.CurrentVersion));
                            if (!string.IsNullOrEmpty(dev.NewVersion))
                                lines.Add(string.Format("   Version moi:  {0}", dev.NewVersion));
                            lines.Add(string.Format("   Ngay:         {0}", dev.DriverDate));
                            lines.Add(string.Format("   Trang thai:   {0}", dev.Status));
                            if (!string.IsNullOrEmpty(dev.UpdateTitle))
                                lines.Add(string.Format("   Ban cap nhat: {0}", dev.UpdateTitle));
                            lines.Add("---------------------------------------------------------------------");
                        }

                        lines.Add("");
                        lines.Add(string.Format("Tong so thiet bi: {0}", deviceList.Count));
                        lines.Add(string.Format("So ban cap nhat kha dung: {0}", updatesAvailable));

                        File.WriteAllLines(sfd.FileName, lines.ToArray(), Encoding.UTF8);
                        Logger.Log("Xuat bao cao: " + sfd.FileName);
                        MessageBox.Show("Da xuat bao cao:\n" + sfd.FileName, "Thanh cong",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Loi khi xuat: " + ex.Message, "Loi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        // ================================================================
        //  HELPER FUNCTIONS
        // ================================================================

        private void PopulateListView(string filter = "")
        {
            lvDevices.BeginUpdate();
            lvDevices.Items.Clear();

            string f = filter.ToLower();

            foreach (var dev in deviceList)
            {
                if (!string.IsNullOrEmpty(f))
                {
                    bool match = dev.DeviceName.ToLower().Contains(f) ||
                                 dev.Category.ToLower().Contains(f) ||
                                 dev.Manufacturer.ToLower().Contains(f) ||
                                 dev.Status.ToLower().Contains(f);
                    if (!match) continue;
                }

                var lvi = new ListViewItem("");
                lvi.SubItems.Add(dev.Category);
                lvi.SubItems.Add(dev.DeviceName);
                lvi.SubItems.Add(dev.Manufacturer);
                lvi.SubItems.Add(dev.CurrentVersion);
                lvi.SubItems.Add(dev.NewVersion);
                lvi.SubItems.Add(dev.DriverDate);
                lvi.SubItems.Add(dev.Status);
                lvi.Tag = dev;
                lvi.ForeColor = dev.StatusColor;

                if (dev.AvailableUpdate != null)
                {
                    lvi.Checked = true;
                    lvi.BackColor = Color.FromArgb(255, 251, 235);
                }

                lvDevices.Items.Add(lvi);
            }

            lvDevices.EndUpdate();
        }

        private bool IsDriverMatch(HardwareDevice dev, string updateTitle)
        {
            string title = updateTitle.ToLower();
            string name = dev.DeviceName.ToLower();

            if (title.Contains(name) || name.Contains(title)) return true;

            var keywords = name.Split(new[] { ' ', '-', '_', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 4).ToArray();
            if (keywords.Length >= 2)
            {
                int matchCount = keywords.Count(k => title.Contains(k));
                if (matchCount >= 2) return true;
            }

            if (!string.IsNullOrEmpty(dev.HardwareID))
            {
                string hwLower = dev.HardwareID.ToLower();
                int venIdx = hwLower.IndexOf("ven_");
                int devIdx = hwLower.IndexOf("dev_");
                if (venIdx >= 0 && devIdx >= 0)
                {
                    string venPart = hwLower.Substring(venIdx, Math.Min(8, hwLower.Length - venIdx));
                    string devPart = hwLower.Substring(devIdx, Math.Min(8, hwLower.Length - devIdx));
                    if (title.Contains(venPart) && title.Contains(devPart)) return true;
                }
            }

            if (!string.IsNullOrEmpty(dev.Manufacturer) && dev.Manufacturer.Length >= 3)
            {
                string mfr = dev.Manufacturer.ToLower();
                if (title.Contains(mfr) && keywords.Any(k => title.Contains(k))) return true;
            }

            return false;
        }

        private string ExtractVersionFromTitle(string title)
        {
            int lastDash = title.LastIndexOf(" - ");
            if (lastDash > 0 && lastDash < title.Length - 3)
            {
                string candidate = title.Substring(lastDash + 3).Trim();
                if (candidate.Length >= 3 && candidate.Length <= 30)
                {
                    bool hasDigit = false;
                    bool hasDot = false;
                    foreach (char c in candidate)
                    {
                        if (char.IsDigit(c)) hasDigit = true;
                        if (c == '.') hasDot = true;
                    }
                    if (hasDigit && hasDot) return candidate;
                }
            }
            return "";
        }

        private string GuessCategory(string updateTitle)
        {
            string tl = updateTitle.ToLower();
            if (tl.Contains("display") || tl.Contains("graphics") || tl.Contains("nvidia") || tl.Contains("amd") || tl.Contains("geforce") || tl.Contains("radeon"))
                return "\U0001F5A5 Card do hoa";
            if (tl.Contains("network") || tl.Contains("wifi") || tl.Contains("wireless") || tl.Contains("ethernet") || tl.Contains("wi-fi") || tl.Contains("lan"))
                return "\U0001F310 Card mang";
            if (tl.Contains("audio") || tl.Contains("sound") || tl.Contains("realtek high"))
                return "\U0001F50A Am thanh";
            if (tl.Contains("bluetooth"))
                return "\U0001F4F6 Bluetooth";
            if (tl.Contains("firmware"))
                return "\U0001F527 Firmware";
            if (tl.Contains("camera") || tl.Contains("webcam"))
                return "\U0001F4F7 Camera";
            if (tl.Contains("usb"))
                return "\U0001F50C USB";
            return "\U0001F4E6 Driver moi";
        }

        private Button CreateButton(string text, Color bgColor, bool bold)
        {
            var btn = new Button();
            btn.Text = text;
            btn.BackColor = bgColor;
            btn.ForeColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.Cursor = Cursors.Hand;
            btn.Font = new Font("Segoe UI", 9.2f, bold ? FontStyle.Bold : FontStyle.Regular);
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void AddBackupLog(string action, string detail)
        {
            SafeInvoke(delegate()
            {
                var lvi = new ListViewItem(DateTime.Now.ToString("HH:mm:ss"));
                lvi.SubItems.Add(action);
                lvi.SubItems.Add(detail);

                if (action.Contains("THAT BAI") || action.Contains("LOI"))
                    lvi.ForeColor = Color.FromArgb(220, 38, 38);
                else if (action.Contains("THANH CONG"))
                    lvi.ForeColor = Color.FromArgb(22, 163, 74);
                else
                    lvi.ForeColor = Color.FromArgb(37, 99, 235);

                lvBackupLog.Items.Insert(0, lvi);
            });
        }

        private void SafeInvoke(Action action)
        {
            if (this.InvokeRequired)
                this.Invoke(action);
            else
                action();
        }
    }
}
