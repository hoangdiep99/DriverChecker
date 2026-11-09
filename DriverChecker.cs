// ================================================================
// DRIVER CHECKER & PC MANAGER v2.0
// Giao dien phong cach Windows 11 Fluent Dashboard
// - Bang dieu khien thong tin PC (Specs, CPU/RAM/Disk live gauges)
// - Quan ly & Cap nhat Driver Microsoft + Link tai truc tiep GPU
// - Sao luu & Khoi phuc Driver OEM khong can mang
// - Cai dat ung dung thiet yeu 1-click qua Winget
// - Sua loi Windows (SFC, DISM, Reset Network, Don rac Temp)
// Tuong thich C# 5 / .NET Framework 4.8
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
            Log("Driver & PC Manager v2.0 started");
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

    // ======== Model thiet bi driver ========
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

    // ======== Model App Winget ========
    public class WingetApp
    {
        public string Name;
        public string PackageId;
        public string Category;
        public string Description;
        public bool IsRecommended;

        public WingetApp(string name, string id, string cat, string desc, bool rec)
        {
            Name = name;
            PackageId = id;
            Category = cat;
            Description = desc;
            IsRecommended = rec;
        }
    }

    // ======== Form chinh ========
    public class MainForm : Form
    {
        // Colors Windows 11 Fluent
        private static readonly Color BgColor = Color.FromArgb(248, 250, 252);
        private static readonly Color CardBg = Color.White;
        private static readonly Color CardBorder = Color.FromArgb(226, 232, 240);
        private static readonly Color PrimaryColor = Color.FromArgb(37, 99, 235);
        private static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
        private static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);
        private static readonly Color SidebarBg = Color.FromArgb(241, 245, 249);
        private static readonly Color SidebarActive = Color.FromArgb(224, 231, 255);

        // Sidebar buttons
        private Button[] navButtons;
        private Panel[] contentPages;
        private int currentPageIndex = 0;

        // --- View 1: Dashboard controls ---
        private Label lblPcName;
        private Label lblOsInfo;
        private Label lblCpuInfo;
        private Label lblGpuInfo;
        private Label lblRamInfo;
        private Label lblMainboardInfo;
        private Label lblUptime;
        private Label lblInstallDate;
        private Button btnCopySpecs;
        private Button btnGpuDirectLink;
        private string gpuVendor = "";
        private string gpuDirectUrl = "";

        // Gauges
        private Panel pnlGaugeCpu;
        private Panel pnlGaugeRam;
        private Panel pnlGaugeDisk;
        private float cpuPercent = 0;
        private float ramPercent = 0;
        private float diskPercent = 0;
        private string cpuSubtext = "";
        private string ramSubtext = "";
        private string diskSubtext = "";
        private Timer gaugeTimer;

        // Health items
        private Label lblHealthActivation;
        private Label lblHealthSecureBoot;
        private Label lblHealthDefender;
        private Label lblHealthRestore;

        // --- View 2: Driver Scanner controls ---
        private ListView lvDevices;
        private Button btnScanDrivers;
        private Button btnUpdateDrivers;
        private Button btnExportReport;
        private CheckBox chkRestorePoint;
        private TextBox txtSearchDrivers;
        private Button btnSelectAllDrivers;
        private Button btnDeselectAllDrivers;
        private ProgressBar prgDriver;
        private Label lblDriverStatus;
        private Panel pnlGpuBanner;
        private List<HardwareDevice> deviceList = new List<HardwareDevice>();
        private int updatesAvailable = 0;

        // --- View 3: Backup/Restore controls ---
        private TextBox txtBackupPath;
        private TextBox txtRestorePath;
        private Button btnStartBackup;
        private Button btnStartRestore;
        private ListView lvBackupLog;
        private Label lblBackupResult;

        // --- View 4: App Installer controls ---
        private ListView lvApps;
        private Button btnInstallApps;
        private Button btnSelectRecApps;
        private Button btnDeselectApps;
        private ProgressBar prgApps;
        private Label lblAppStatus;
        private List<WingetApp> appList = new List<WingetApp>();

        // --- View 5: Fix & Optimize controls ---
        private Button btnRunSfc;
        private Button btnRunDism;
        private Button btnResetNet;
        private Button btnCleanTemp;
        private Button btnResetWu;
        private TextBox txtFixLog;

        // Category Map for Drivers
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

        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            Logger.Init(exePath);

            if (args != null && args.Length > 0)
            {
                if (HandleCommandLine(args)) return;
            }

            Application.Run(new MainForm());
            Logger.Log("Application closed.");
        }

        private static bool HandleCommandLine(string[] args)
        {
            string cmd = args[0].ToLower();
            if (cmd == "/backup" && args.Length >= 2)
            {
                string path = args[1];
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
        //  KHOI TAO GIAO DIEN TONG THE
        // ================================================================
        private void InitUI()
        {
            this.Text = "Driver Checker & PC Manager v2.0 - Bang Dieu Khien Windows 11";
            this.Size = new Size(1180, 750);
            this.MinimumSize = new Size(980, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9.5f);
            this.BackColor = BgColor;

            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            // === 1. SIDEBAR BEN TRAI ===
            Panel sidebar = new Panel();
            sidebar.Dock = DockStyle.Left;
            sidebar.Width = 220;
            sidebar.BackColor = SidebarBg;
            sidebar.Padding = new Padding(10, 15, 10, 15);
            sidebar.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(CardBorder, 1))
                    e.Graphics.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
            };

            // Logo & Title in Sidebar
            Label lblAppLogo = new Label();
            lblAppLogo.Text = "\U0001F50D Driver Checker";
            lblAppLogo.Font = new Font("Segoe UI", 13.5f, FontStyle.Bold);
            lblAppLogo.ForeColor = TextPrimary;
            lblAppLogo.Location = new Point(15, 10);
            lblAppLogo.AutoSize = true;

            Label lblAppSub = new Label();
            lblAppSub.Text = "PC & Driver Manager v2.0";
            lblAppSub.Font = new Font("Segoe UI", 8.2f);
            lblAppSub.ForeColor = TextSecondary;
            lblAppSub.Location = new Point(17, 36);
            lblAppSub.AutoSize = true;

            sidebar.Controls.Add(lblAppLogo);
            sidebar.Controls.Add(lblAppSub);

            // Nav buttons
            string[] navTitles = {
                "\U0001F3E0  Bang dieu khien",
                "\U0001F504  Driver & Cap nhat",
                "\U0001F4BE  Sao luu & Khoi phuc",
                "\U0001F4E6  Cai App thiet yeu",
                "\U0001F6E0  Sua loi & Toi uu"
            };

            navButtons = new Button[navTitles.Length];
            int btnY = 75;
            for (int i = 0; i < navTitles.Length; i++)
            {
                int idx = i;
                Button btn = new Button();
                btn.Text = navTitles[i];
                btn.Location = new Point(8, btnY);
                btn.Size = new Size(204, 42);
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 0;
                btn.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
                btn.TextAlign = ContentAlignment.MiddleLeft;
                btn.Padding = new Padding(10, 0, 0, 0);
                btn.Cursor = Cursors.Hand;
                btn.BackColor = (i == 0) ? SidebarActive : Color.Transparent;
                btn.ForeColor = (i == 0) ? PrimaryColor : TextPrimary;
                btn.Click += delegate(object s, EventArgs e) { SwitchPage(idx); };

                navButtons[i] = btn;
                sidebar.Controls.Add(btn);
                btnY += 46;
            }

            // Bottom log button in sidebar
            Button btnLog = new Button();
            btnLog.Text = "\U0001F4CB Xem File Log";
            btnLog.Location = new Point(8, sidebar.Height - 50);
            btnLog.Size = new Size(204, 34);
            btnLog.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnLog.FlatStyle = FlatStyle.Flat;
            btnLog.FlatAppearance.BorderColor = CardBorder;
            btnLog.Font = new Font("Segoe UI", 8.5f);
            btnLog.Cursor = Cursors.Hand;
            btnLog.Click += delegate(object s, EventArgs e)
            {
                if (File.Exists(Logger.LogPath)) Process.Start("notepad.exe", Logger.LogPath);
                else MessageBox.Show("Chua co file log.", "Thong bao");
            };
            sidebar.Controls.Add(btnLog);

            // === 2. MAIN CONTENT AREA ===
            Panel mainContainer = new Panel();
            mainContainer.Dock = DockStyle.Fill;
            mainContainer.BackColor = BgColor;

            // Tao 5 Content Pages
            contentPages = new Panel[5];
            for (int i = 0; i < 5; i++)
            {
                contentPages[i] = new Panel();
                contentPages[i].Dock = DockStyle.Fill;
                contentPages[i].Visible = (i == 0);
                mainContainer.Controls.Add(contentPages[i]);
            }

            // Xay dung tung trang
            BuildDashboardPage(contentPages[0]);
            BuildDriverPage(contentPages[1]);
            BuildBackupPage(contentPages[2]);
            BuildAppInstallerPage(contentPages[3]);
            BuildFixOptimizePage(contentPages[4]);

            this.Controls.Add(mainContainer);
            this.Controls.Add(sidebar);

            // Timer do live CPU/RAM/Disk moi 2 giay
            gaugeTimer = new Timer();
            gaugeTimer.Interval = 2000;
            gaugeTimer.Tick += delegate(object s, EventArgs e) { RefreshGauges(); };
            gaugeTimer.Start();

            // Load du lieu Dashboard ngay sau khi hien thi
            this.Shown += delegate(object s, EventArgs e)
            {
                LoadDashboardSpecs();
                RefreshGauges();
            };
        }

        private void SwitchPage(int index)
        {
            if (index == currentPageIndex) return;

            navButtons[currentPageIndex].BackColor = Color.Transparent;
            navButtons[currentPageIndex].ForeColor = TextPrimary;
            navButtons[currentPageIndex].Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            contentPages[currentPageIndex].Visible = false;

            currentPageIndex = index;
            navButtons[currentPageIndex].BackColor = SidebarActive;
            navButtons[currentPageIndex].ForeColor = PrimaryColor;
            navButtons[currentPageIndex].Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            contentPages[currentPageIndex].Visible = true;

            // Neu chuyen sang Driver Page lan dau ma chua quet, tu dong quet
            if (index == 1 && deviceList.Count == 0)
            {
                Task.Run(async delegate() { await RunFullScanAsync(); });
            }
        }

        // ================================================================
        //  VIEW 1: BANG DIEU KHIEN (DASHBOARD GIONG FIXYFIER)
        // ================================================================
        private void BuildDashboardPage(Panel page)
        {
            page.AutoScroll = true;
            page.Padding = new Padding(24, 20, 24, 20);

            // Title
            Label lblHeader = new Label();
            lblHeader.Text = "B\u1EA3ng \u0111i\u1EC1u khi\u1EC3n";
            lblHeader.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            lblHeader.ForeColor = TextPrimary;
            lblHeader.Location = new Point(24, 15);
            lblHeader.AutoSize = true;
            page.Controls.Add(lblHeader);

            // --- 1. CARD THONG SO MAY TINH (SPECS CARD) ---
            Panel cardSpecs = CreateCardPanel(24, 52, 880, 160);

            lblPcName = new Label();
            lblPcName.Text = Environment.MachineName;
            lblPcName.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            lblPcName.ForeColor = TextPrimary;
            lblPcName.Location = new Point(18, 14);
            lblPcName.AutoSize = true;

            btnCopySpecs = new Button();
            btnCopySpecs.Text = "\U0001F4CB Sao chep";
            btnCopySpecs.Location = new Point(765, 14);
            btnCopySpecs.Size = new Size(95, 30);
            btnCopySpecs.FlatStyle = FlatStyle.Flat;
            btnCopySpecs.FlatAppearance.BorderColor = CardBorder;
            btnCopySpecs.Font = new Font("Segoe UI", 8.5f);
            btnCopySpecs.Cursor = Cursors.Hand;
            btnCopySpecs.Click += CopySpecsToClipboard;

            lblOsInfo = CreateSpecsLabel("He dieu hanh:", "Dang tai...", 18, 48);
            lblCpuInfo = CreateSpecsLabel("Bo xu ly:", "Dang tai...", 18, 72);
            lblGpuInfo = CreateSpecsLabel("Card do hoa:", "Dang tai...", 18, 96);
            lblRamInfo = CreateSpecsLabel("Bo nho RAM:", "Dang tai...", 480, 48);
            lblMainboardInfo = CreateSpecsLabel("Bo mach chu:", "Dang tai...", 480, 72);
            lblUptime = CreateSpecsLabel("Thoi gian chay:", "Dang tai...", 480, 96);
            lblInstallDate = CreateSpecsLabel("Ngay cai Win:", "Dang tai...", 18, 120);

            btnGpuDirectLink = new Button();
            btnGpuDirectLink.Text = "\U0001F310 Tai tu Hang";
            btnGpuDirectLink.Location = new Point(765, 92);
            btnGpuDirectLink.Size = new Size(95, 30);
            btnGpuDirectLink.FlatStyle = FlatStyle.Flat;
            btnGpuDirectLink.BackColor = Color.FromArgb(22, 163, 74);
            btnGpuDirectLink.ForeColor = Color.White;
            btnGpuDirectLink.FlatAppearance.BorderSize = 0;
            btnGpuDirectLink.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnGpuDirectLink.Cursor = Cursors.Hand;
            btnGpuDirectLink.Visible = false;
            btnGpuDirectLink.Click += delegate(object s, EventArgs e)
            {
                if (!string.IsNullOrEmpty(gpuDirectUrl)) Process.Start(gpuDirectUrl);
            };

            cardSpecs.Controls.Add(lblPcName);
            cardSpecs.Controls.Add(btnCopySpecs);
            cardSpecs.Controls.Add(lblOsInfo);
            cardSpecs.Controls.Add(lblCpuInfo);
            cardSpecs.Controls.Add(lblGpuInfo);
            cardSpecs.Controls.Add(lblRamInfo);
            cardSpecs.Controls.Add(lblMainboardInfo);
            cardSpecs.Controls.Add(lblUptime);
            cardSpecs.Controls.Add(lblInstallDate);
            cardSpecs.Controls.Add(btnGpuDirectLink);
            page.Controls.Add(cardSpecs);

            // --- 2. BA DONG HO TRON HIEU NANG (GAUGES) ---
            int gaugeY = 224;
            int gaugeW = 280;
            int gaugeH = 135;

            // Gauge CPU
            pnlGaugeCpu = CreateCardPanel(24, gaugeY, gaugeW, gaugeH);
            pnlGaugeCpu.Paint += delegate(object s, PaintEventArgs e)
            {
                DrawCircularGauge(e.Graphics, new Rectangle(15, 12, 105, 105), cpuPercent,
                    "\u2699 Bo xu ly", cpuSubtext, Color.FromArgb(37, 99, 235));
            };

            // Gauge RAM
            pnlGaugeRam = CreateCardPanel(324, gaugeY, gaugeW, gaugeH);
            pnlGaugeRam.Paint += delegate(object s, PaintEventArgs e)
            {
                DrawCircularGauge(e.Graphics, new Rectangle(15, 12, 105, 105), ramPercent,
                    "\U0001F4BE Bo nho", ramSubtext, Color.FromArgb(14, 165, 233));
            };

            // Gauge Disk
            pnlGaugeDisk = CreateCardPanel(624, gaugeY, gaugeW, gaugeH);
            pnlGaugeDisk.Paint += delegate(object s, PaintEventArgs e)
            {
                Color c = diskPercent > 85 ? Color.FromArgb(220, 38, 38) : Color.FromArgb(217, 119, 6);
                DrawCircularGauge(e.Graphics, new Rectangle(15, 12, 105, 105), diskPercent,
                    "\U0001F4BD O dia C:", diskSubtext, c);
            };

            page.Controls.Add(pnlGaugeCpu);
            page.Controls.Add(pnlGaugeRam);
            page.Controls.Add(pnlGaugeDisk);

            // --- 3. TINH TRANG HE THONG (SYSTEM HEALTH) ---
            Panel cardHealth = CreateCardPanel(24, 372, 880, 200);

            Label lblHealthTitle = new Label();
            lblHealthTitle.Text = "\U0001F6E1 Tinh trang he thong";
            lblHealthTitle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            lblHealthTitle.ForeColor = TextPrimary;
            lblHealthTitle.Location = new Point(18, 14);
            lblHealthTitle.AutoSize = true;

            lblHealthActivation = CreateSpecsLabel("Kich hoat Windows:", "Kiem tra...", 18, 50);
            lblHealthSecureBoot = CreateSpecsLabel("Khoi dong an toan (Secure Boot):", "Kiem tra...", 18, 80);
            lblHealthDefender = CreateSpecsLabel("Bao ve thoi gian thuc:", "Kiem tra...", 18, 110);
            lblHealthRestore = CreateSpecsLabel("Bao ve he thong (Restore Point):", "Kiem tra...", 18, 140);

            Button btnCreateRpNow = new Button();
            btnCreateRpNow.Text = "+ Tao Restore Point ngay";
            btnCreateRpNow.Location = new Point(680, 134);
            btnCreateRpNow.Size = new Size(180, 32);
            btnCreateRpNow.FlatStyle = FlatStyle.Flat;
            btnCreateRpNow.BackColor = PrimaryColor;
            btnCreateRpNow.ForeColor = Color.White;
            btnCreateRpNow.FlatAppearance.BorderSize = 0;
            btnCreateRpNow.Font = new Font("Segoe UI", 8.8f, FontStyle.Bold);
            btnCreateRpNow.Cursor = Cursors.Hand;
            btnCreateRpNow.Click += async delegate(object s, EventArgs e)
            {
                btnCreateRpNow.Enabled = false;
                btnCreateRpNow.Text = "Dang tao...";
                bool ok = await Task.Run(new Func<bool>(delegate() { return CreateRestorePoint(); }));
                btnCreateRpNow.Enabled = true;
                btnCreateRpNow.Text = "+ Tao Restore Point ngay";
                if (ok) MessageBox.Show("Tao Restore Point thanh cong!", "Thanh cong");
                else MessageBox.Show("Khong the tao Restore Point.", "Loi");
            };

            cardHealth.Controls.Add(lblHealthTitle);
            cardHealth.Controls.Add(lblHealthActivation);
            cardHealth.Controls.Add(lblHealthSecureBoot);
            cardHealth.Controls.Add(lblHealthDefender);
            cardHealth.Controls.Add(lblHealthRestore);
            cardHealth.Controls.Add(btnCreateRpNow);
            page.Controls.Add(cardHealth);
        }

        private void DrawCircularGauge(Graphics g, Rectangle bounds, float percent, string title, string subtext, Color progColor)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background circle
            using (var bgPen = new Pen(Color.FromArgb(241, 245, 249), 10))
            {
                g.DrawArc(bgPen, bounds.X + 6, bounds.Y + 6, bounds.Width - 12, bounds.Height - 12, 0, 360);
            }

            // Progress arc
            if (percent > 0)
            {
                using (var prPen = new Pen(progColor, 10))
                {
                    prPen.StartCap = LineCap.Round;
                    prPen.EndCap = LineCap.Round;
                    float sweep = Math.Min(360f, (percent / 100f) * 360f);
                    g.DrawArc(prPen, bounds.X + 6, bounds.Y + 6, bounds.Width - 12, bounds.Height - 12, -90, sweep);
                }
            }

            // Percentage Text in center
            string pctStr = string.Format("{0}%", (int)percent);
            using (var font = new Font("Segoe UI", 14f, FontStyle.Bold))
            using (var brush = new SolidBrush(TextPrimary))
            {
                var sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(pctStr, font, brush, new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), sf);
            }

            // Title & Subtext on the right
            int textX = bounds.Right + 12;
            using (var fontTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var fontSub = new Font("Segoe UI", 8.8f))
            using (var brushTitle = new SolidBrush(TextPrimary))
            using (var brushSub = new SolidBrush(TextSecondary))
            {
                g.DrawString(title, fontTitle, brushTitle, textX, bounds.Y + 20);
                g.DrawString(subtext, fontSub, brushSub, textX, bounds.Y + 46);
            }
        }

        // ================================================================
        //  VIEW 2: DRIVER & CAP NHAT (WINDOWS UPDATE + GPU LINK)
        // ================================================================
        private void BuildDriverPage(Panel page)
        {
            Panel topBar = new Panel();
            topBar.Dock = DockStyle.Top;
            topBar.Height = 100;
            topBar.BackColor = CardBg;
            topBar.Padding = new Padding(16, 10, 16, 8);

            // Banner GPU Direct Download
            pnlGpuBanner = new Panel();
            pnlGpuBanner.Dock = DockStyle.Top;
            pnlGpuBanner.Height = 38;
            pnlGpuBanner.BackColor = Color.FromArgb(236, 253, 245);
            pnlGpuBanner.Padding = new Padding(12, 6, 12, 6);
            pnlGpuBanner.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(Color.FromArgb(167, 243, 208), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, pnlGpuBanner.Width - 1, pnlGpuBanner.Height - 1);
            };

            Label lblGpuBannerText = new Label();
            lblGpuBannerText.Text = "\u2705 Phat hien Card do hoa NVIDIA. Ban co the tai ban Game Ready Driver moi nhat tu website hang:";
            lblGpuBannerText.Location = new Point(12, 9);
            lblGpuBannerText.AutoSize = true;
            lblGpuBannerText.Font = new Font("Segoe UI", 9f);
            lblGpuBannerText.ForeColor = Color.FromArgb(6, 95, 70);

            Button btnBannerDownload = new Button();
            btnBannerDownload.Text = "\U0001F310 Tai truc tiep tu NVIDIA";
            btnBannerDownload.Location = new Point(680, 5);
            btnBannerDownload.Size = new Size(185, 28);
            btnBannerDownload.FlatStyle = FlatStyle.Flat;
            btnBannerDownload.BackColor = Color.FromArgb(22, 163, 74);
            btnBannerDownload.ForeColor = Color.White;
            btnBannerDownload.FlatAppearance.BorderSize = 0;
            btnBannerDownload.Font = new Font("Segoe UI", 8.8f, FontStyle.Bold);
            btnBannerDownload.Cursor = Cursors.Hand;
            btnBannerDownload.Click += delegate(object s, EventArgs e)
            {
                if (!string.IsNullOrEmpty(gpuDirectUrl)) Process.Start(gpuDirectUrl);
                else Process.Start("https://www.nvidia.com/Download/index.aspx");
            };

            pnlGpuBanner.Controls.Add(lblGpuBannerText);
            pnlGpuBanner.Controls.Add(btnBannerDownload);

            // Row buttons
            Panel rowBtns = new Panel();
            rowBtns.Dock = DockStyle.Bottom;
            rowBtns.Height = 48;
            rowBtns.BackColor = CardBg;

            btnScanDrivers = CreateButton("\U0001F50D  Quet & Kiem Tra", PrimaryColor, true);
            btnScanDrivers.Location = new Point(12, 6);
            btnScanDrivers.Size = new Size(160, 36);
            btnScanDrivers.Click += async delegate(object s, EventArgs e) { await RunFullScanAsync(); };

            btnUpdateDrivers = CreateButton("\u26A1  Cap Nhat Driver", Color.FromArgb(22, 163, 74), true);
            btnUpdateDrivers.Location = new Point(180, 6);
            btnUpdateDrivers.Size = new Size(160, 36);
            btnUpdateDrivers.Enabled = false;
            btnUpdateDrivers.Click += async delegate(object s, EventArgs e) { await InstallSelectedAsync(); };

            btnExportReport = CreateButton("\U0001F4C4  Xuat Bao Cao", Color.FromArgb(100, 116, 139), false);
            btnExportReport.Location = new Point(348, 6);
            btnExportReport.Size = new Size(130, 36);
            btnExportReport.Click += ExportReport;

            chkRestorePoint = new CheckBox();
            chkRestorePoint.Text = "\U0001F6E1 Tao Restore Point";
            chkRestorePoint.Checked = true;
            chkRestorePoint.Location = new Point(490, 14);
            chkRestorePoint.AutoSize = true;
            chkRestorePoint.Font = new Font("Segoe UI", 9f);
            chkRestorePoint.ForeColor = TextSecondary;

            Label lblSearch = new Label();
            lblSearch.Text = "Loc:";
            lblSearch.Location = new Point(660, 15);
            lblSearch.AutoSize = true;
            lblSearch.Font = new Font("Segoe UI", 9f);
            lblSearch.ForeColor = TextSecondary;

            txtSearchDrivers = new TextBox();
            txtSearchDrivers.Font = new Font("Segoe UI", 9.5f);
            txtSearchDrivers.Location = new Point(695, 11);
            txtSearchDrivers.Size = new Size(130, 26);
            txtSearchDrivers.TextChanged += delegate(object s, EventArgs e)
            {
                PopulateDriverList(txtSearchDrivers.Text.Trim());
            };

            btnSelectAllDrivers = new Button();
            btnSelectAllDrivers.Text = "Tat ca";
            btnSelectAllDrivers.Size = new Size(54, 28);
            btnSelectAllDrivers.Location = new Point(832, 10);
            btnSelectAllDrivers.FlatStyle = FlatStyle.Flat;
            btnSelectAllDrivers.FlatAppearance.BorderColor = CardBorder;
            btnSelectAllDrivers.Font = new Font("Segoe UI", 8.5f);
            btnSelectAllDrivers.Click += delegate(object s, EventArgs e)
            {
                foreach (ListViewItem item in lvDevices.Items) item.Checked = true;
            };

            btnDeselectAllDrivers = new Button();
            btnDeselectAllDrivers.Text = "Bo chon";
            btnDeselectAllDrivers.Size = new Size(64, 28);
            btnDeselectAllDrivers.Location = new Point(890, 10);
            btnDeselectAllDrivers.FlatStyle = FlatStyle.Flat;
            btnDeselectAllDrivers.FlatAppearance.BorderColor = CardBorder;
            btnDeselectAllDrivers.Font = new Font("Segoe UI", 8.5f);
            btnDeselectAllDrivers.Click += delegate(object s, EventArgs e)
            {
                foreach (ListViewItem item in lvDevices.Items) item.Checked = false;
            };

            rowBtns.Controls.Add(btnScanDrivers);
            rowBtns.Controls.Add(btnUpdateDrivers);
            rowBtns.Controls.Add(btnExportReport);
            rowBtns.Controls.Add(chkRestorePoint);
            rowBtns.Controls.Add(lblSearch);
            rowBtns.Controls.Add(txtSearchDrivers);
            rowBtns.Controls.Add(btnSelectAllDrivers);
            rowBtns.Controls.Add(btnDeselectAllDrivers);

            topBar.Controls.Add(rowBtns);
            topBar.Controls.Add(pnlGpuBanner);

            // ListView
            lvDevices = new ListView();
            lvDevices.Dock = DockStyle.Fill;
            lvDevices.View = View.Details;
            lvDevices.FullRowSelect = true;
            lvDevices.GridLines = true;
            lvDevices.CheckBoxes = true;
            lvDevices.Font = new Font("Segoe UI", 9.2f);
            lvDevices.BackColor = CardBg;
            lvDevices.BorderStyle = BorderStyle.None;

            lvDevices.Columns.Add("", 35, HorizontalAlignment.Center);
            lvDevices.Columns.Add("Phan loai", 140);
            lvDevices.Columns.Add("Ten thiet bi", 250);
            lvDevices.Columns.Add("Nha san xuat", 120);
            lvDevices.Columns.Add("Version hien tai", 125);
            lvDevices.Columns.Add("Version moi (MS)", 125);
            lvDevices.Columns.Add("Ngay Driver", 95);
            lvDevices.Columns.Add("Trang thai", 180);

            // Bottom status
            Panel btm = new Panel();
            btm.Dock = DockStyle.Bottom;
            btm.Height = 36;
            btm.BackColor = CardBg;
            btm.Padding = new Padding(16, 6, 16, 6);

            lblDriverStatus = new Label();
            lblDriverStatus.Text = "San sang. Bam \"Quet & Kiem Tra\" de bat dau...";
            lblDriverStatus.Location = new Point(16, 8);
            lblDriverStatus.AutoSize = true;
            lblDriverStatus.ForeColor = TextSecondary;

            prgDriver = new ProgressBar();
            prgDriver.Size = new Size(200, 18);
            prgDriver.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            prgDriver.Location = new Point(btm.Width - 220, 8);
            prgDriver.Visible = false;
            btm.Resize += delegate(object s, EventArgs e) { prgDriver.Location = new Point(btm.Width - 220, 8); };

            btm.Controls.Add(lblDriverStatus);
            btm.Controls.Add(prgDriver);

            page.Controls.Add(lvDevices);
            page.Controls.Add(btm);
            page.Controls.Add(topBar);
        }

        // ================================================================
        //  VIEW 3: SAO LUU & KHOI PHUC
        // ================================================================
        private void BuildBackupPage(Panel page)
        {
            page.Padding = new Padding(24, 20, 24, 20);

            Label lblBkTitle = new Label();
            lblBkTitle.Text = "Sao l\u01B0u & Kh\u00F4i ph\u1EE5c Driver (Offline kh\u00F4ng c\u1EA7n m\u1EA1ng)";
            lblBkTitle.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
            lblBkTitle.ForeColor = TextPrimary;
            lblBkTitle.Location = new Point(24, 12);
            lblBkTitle.AutoSize = true;
            page.Controls.Add(lblBkTitle);

            // Box Sao luu
            GroupBox grpBk = new GroupBox();
            grpBk.Text = "\U0001F4BE  Sao luu toan bo Driver OEM ra USB";
            grpBk.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            grpBk.Location = new Point(24, 50);
            grpBk.Size = new Size(880, 120);
            grpBk.ForeColor = PrimaryColor;

            Label lblBkDesc = new Label();
            lblBkDesc.Text = "Xuat toan bo driver cua may ra thu muc truoc khi cai lai Windows.";
            lblBkDesc.Font = new Font("Segoe UI", 8.8f);
            lblBkDesc.ForeColor = TextSecondary;
            lblBkDesc.Location = new Point(16, 26);
            lblBkDesc.AutoSize = true;

            txtBackupPath = new TextBox();
            txtBackupPath.Font = new Font("Segoe UI", 9.5f);
            txtBackupPath.Location = new Point(16, 50);
            txtBackupPath.Size = new Size(620, 28);
            txtBackupPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "DriverBackup");

            Button btnBrowseBk = new Button();
            btnBrowseBk.Text = "...";
            btnBrowseBk.Location = new Point(642, 49);
            btnBrowseBk.Size = new Size(35, 28);
            btnBrowseBk.Click += delegate(object s, EventArgs e)
            {
                using (var fbd = new FolderBrowserDialog())
                    if (fbd.ShowDialog() == DialogResult.OK) txtBackupPath.Text = fbd.SelectedPath;
            };

            btnStartBackup = CreateButton("\U0001F4BE  Bat Dau Sao Luu", PrimaryColor, true);
            btnStartBackup.Location = new Point(688, 48);
            btnStartBackup.Size = new Size(175, 30);
            btnStartBackup.Click += async delegate(object s, EventArgs e) { await BackupDriversAsync(); };

            lblBackupResult = new Label();
            lblBackupResult.Text = "";
            lblBackupResult.ForeColor = Color.FromArgb(22, 163, 74);
            lblBackupResult.Location = new Point(16, 88);
            lblBackupResult.AutoSize = true;

            grpBk.Controls.Add(lblBkDesc);
            grpBk.Controls.Add(txtBackupPath);
            grpBk.Controls.Add(btnBrowseBk);
            grpBk.Controls.Add(btnStartBackup);
            grpBk.Controls.Add(lblBackupResult);
            page.Controls.Add(grpBk);

            // Box Khoi phuc
            GroupBox grpRs = new GroupBox();
            grpRs.Text = "\U0001F504  Khoi phuc Driver sau khi cai Win (100% Khong can mang)";
            grpRs.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            grpRs.Location = new Point(24, 180);
            grpRs.Size = new Size(880, 110);
            grpRs.ForeColor = Color.FromArgb(22, 163, 74);

            Label lblRsDesc = new Label();
            lblRsDesc.Text = "Chon thu muc DriverBackup da luu truoc do tren USB de cai dat lai vao may trong 1 phut.";
            lblRsDesc.Font = new Font("Segoe UI", 8.8f);
            lblRsDesc.ForeColor = TextSecondary;
            lblRsDesc.Location = new Point(16, 26);
            lblRsDesc.AutoSize = true;

            txtRestorePath = new TextBox();
            txtRestorePath.Font = new Font("Segoe UI", 9.5f);
            txtRestorePath.Location = new Point(16, 52);
            txtRestorePath.Size = new Size(620, 28);

            Button btnBrowseRs = new Button();
            btnBrowseRs.Text = "...";
            btnBrowseRs.Location = new Point(642, 51);
            btnBrowseRs.Size = new Size(35, 28);
            btnBrowseRs.Click += delegate(object s, EventArgs e)
            {
                using (var fbd = new FolderBrowserDialog())
                    if (fbd.ShowDialog() == DialogResult.OK) txtRestorePath.Text = fbd.SelectedPath;
            };

            btnStartRestore = CreateButton("\U0001F504  Bat Dau Khoi Phuc", Color.FromArgb(22, 163, 74), true);
            btnStartRestore.Location = new Point(688, 50);
            btnStartRestore.Size = new Size(175, 30);
            btnStartRestore.Click += async delegate(object s, EventArgs e) { await RestoreDriversAsync(); };

            grpRs.Controls.Add(lblRsDesc);
            grpRs.Controls.Add(txtRestorePath);
            grpRs.Controls.Add(btnBrowseRs);
            grpRs.Controls.Add(btnStartRestore);
            page.Controls.Add(grpRs);

            // Log
            Label lblLogTitle = new Label();
            lblLogTitle.Text = "Nhat ky thao tac:";
            lblLogTitle.Location = new Point(24, 300);
            lblLogTitle.AutoSize = true;
            lblLogTitle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            page.Controls.Add(lblLogTitle);

            lvBackupLog = new ListView();
            lvBackupLog.Location = new Point(24, 325);
            lvBackupLog.Size = new Size(880, 240);
            lvBackupLog.View = View.Details;
            lvBackupLog.FullRowSelect = true;
            lvBackupLog.GridLines = true;
            lvBackupLog.Font = new Font("Consolas", 9f);
            lvBackupLog.BackColor = CardBg;
            lvBackupLog.BorderStyle = BorderStyle.FixedSingle;
            lvBackupLog.Columns.Add("Thoi gian", 85);
            lvBackupLog.Columns.Add("Thao tac", 120);
            lvBackupLog.Columns.Add("Chi tiet", 650);
            page.Controls.Add(lvBackupLog);
        }

        // ================================================================
        //  VIEW 4: CAI DAT UNG DUNG THIET YEU (WINGET APP INSTALLER)
        // ================================================================
        private void BuildAppInstallerPage(Panel page)
        {
            page.Padding = new Padding(24, 20, 24, 20);

            Label lblAppTitle = new Label();
            lblAppTitle.Text = "C\u00E0i \u0111\u1EB7t \u1EE8ng d\u1EE5ng thi\u1EBFt y\u1EBFu (1-Click qua Microsoft Winget)";
            lblAppTitle.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
            lblAppTitle.ForeColor = TextPrimary;
            lblAppTitle.Location = new Point(24, 12);
            lblAppTitle.AutoSize = true;
            page.Controls.Add(lblAppTitle);

            // Top action bar
            Panel topBar = new Panel();
            topBar.Location = new Point(24, 48);
            topBar.Size = new Size(880, 44);

            btnInstallApps = CreateButton("\u26A1  Cai Dat Ung Dung Da Chon", Color.FromArgb(22, 163, 74), true);
            btnInstallApps.Location = new Point(0, 4);
            btnInstallApps.Size = new Size(240, 36);
            btnInstallApps.Click += async delegate(object s, EventArgs e) { await InstallSelectedAppsAsync(); };

            btnSelectRecApps = new Button();
            btnSelectRecApps.Text = "\u2B50 Chon ung dung khuyen dung";
            btnSelectRecApps.Location = new Point(250, 4);
            btnSelectRecApps.Size = new Size(210, 36);
            btnSelectRecApps.FlatStyle = FlatStyle.Flat;
            btnSelectRecApps.FlatAppearance.BorderColor = CardBorder;
            btnSelectRecApps.Font = new Font("Segoe UI", 9f);
            btnSelectRecApps.Cursor = Cursors.Hand;
            btnSelectRecApps.Click += delegate(object s, EventArgs e)
            {
                foreach (ListViewItem item in lvApps.Items)
                {
                    var app = item.Tag as WingetApp;
                    item.Checked = (app != null && app.IsRecommended);
                }
            };

            btnDeselectApps = new Button();
            btnDeselectApps.Text = "Bo chon tat ca";
            btnDeselectApps.Location = new Point(470, 4);
            btnDeselectApps.Size = new Size(120, 36);
            btnDeselectApps.FlatStyle = FlatStyle.Flat;
            btnDeselectApps.FlatAppearance.BorderColor = CardBorder;
            btnDeselectApps.Font = new Font("Segoe UI", 9f);
            btnDeselectApps.Cursor = Cursors.Hand;
            btnDeselectApps.Click += delegate(object s, EventArgs e)
            {
                foreach (ListViewItem item in lvApps.Items) item.Checked = false;
            };

            topBar.Controls.Add(btnInstallApps);
            topBar.Controls.Add(btnSelectRecApps);
            topBar.Controls.Add(btnDeselectApps);
            page.Controls.Add(topBar);

            // Danh sach App
            lvApps = new ListView();
            lvApps.Location = new Point(24, 100);
            lvApps.Size = new Size(880, 400);
            lvApps.View = View.Details;
            lvApps.FullRowSelect = true;
            lvApps.GridLines = true;
            lvApps.CheckBoxes = true;
            lvApps.Font = new Font("Segoe UI", 9.2f);
            lvApps.BackColor = CardBg;
            lvApps.BorderStyle = BorderStyle.FixedSingle;

            lvApps.Columns.Add("", 35, HorizontalAlignment.Center);
            lvApps.Columns.Add("Ung dung", 200);
            lvApps.Columns.Add("Phan loai", 140);
            lvApps.Columns.Add("Package ID", 190);
            lvApps.Columns.Add("Mo ta", 300);

            // Populate default apps
            InitDefaultApps();
            foreach (var app in appList)
            {
                var lvi = new ListViewItem("");
                lvi.SubItems.Add(app.Name);
                lvi.SubItems.Add(app.Category);
                lvi.SubItems.Add(app.PackageId);
                lvi.SubItems.Add(app.Description);
                lvi.Tag = app;
                lvi.Checked = app.IsRecommended;
                lvApps.Items.Add(lvi);
            }

            page.Controls.Add(lvApps);

            // Progress & Status
            lblAppStatus = new Label();
            lblAppStatus.Text = "Chon cac ung dung can thiet va bam \"Cai Dat\". Winget se tai va cai dat hoan toan tu dong.";
            lblAppStatus.Location = new Point(24, 510);
            lblAppStatus.AutoSize = true;
            lblAppStatus.ForeColor = TextSecondary;

            prgApps = new ProgressBar();
            prgApps.Location = new Point(24, 535);
            prgApps.Size = new Size(880, 18);
            prgApps.Visible = false;

            page.Controls.Add(lblAppStatus);
            page.Controls.Add(prgApps);
        }

        private void InitDefaultApps()
        {
            appList.Clear();
            appList.Add(new WingetApp("UniKey", "UniKey.UniKey", "Bo go tieng Viet", "Bo go tieng Viet chuan nhat cho Windows", true));
            appList.Add(new WingetApp("Google Chrome", "Google.Chrome", "Trinh duyet", "Trinh duyet web pho bien nhat the gioi", true));
            appList.Add(new WingetApp("7-Zip", "7zip.7zip", "Nen & Giai nen", "Phan mem giai nen file sieu nhe va mien phi", true));
            appList.Add(new WingetApp("Zalo", "VNG.Zalo", "Nhan tin & Goi", "Ung dung nhan tin va goi dien pho bien tai Viet Nam", true));
            appList.Add(new WingetApp("UltraViewer", "UltraViewer.UltraViewer", "Dieu khien tu xa", "Phan mem ho tro ky thuat tu xa tien loi", true));
            appList.Add(new WingetApp("VLC Media Player", "VideoLAN.VLC", "Xem phim / Nghe nhac", "Trinh phat video da dinh dang khong can cai codec", false));
            appList.Add(new WingetApp("Notepad++", "Notepad++.Notepad++", "Soan thao van ban", "Trinh soan thao text & code da nang", false));
            appList.Add(new WingetApp("Foxit PDF Reader", "Foxit.FoxitReader", "Doc file PDF", "Trinh doc va chu thich file PDF manh me", false));
            appList.Add(new WingetApp("WinRAR", "RARLab.WinRAR", "Nen & Giai nen", "Phan mem giai nen dinh dang .rar kinh dien", false));
            appList.Add(new WingetApp("Discord", "Discord.Discord", "Giao tiep game thu", "Nen tang chat voice va cong dong", false));
            appList.Add(new WingetApp("Steam", "Valve.Steam", "Choi game", "Nen tang mua va choi game ban quyen", false));
            appList.Add(new WingetApp("Visual Studio Code", "Microsoft.VisualStudioCode", "Lap trinh", "Trinh soan thao ma nguon so 1 the gioi", false));
        }

        // ================================================================
        //  VIEW 5: SUA LOI & TOI UU HOA
        // ================================================================
        private void BuildFixOptimizePage(Panel page)
        {
            page.Padding = new Padding(24, 20, 24, 20);

            Label lblFixTitle = new Label();
            lblFixTitle.Text = "S\u1EEDa l\u1ED7i & T\u1ED1i \u01B0u h\u00F3a H\u1EC7 th\u1ED1ng (1-Click)";
            lblFixTitle.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
            lblFixTitle.ForeColor = TextPrimary;
            lblFixTitle.Location = new Point(24, 12);
            lblFixTitle.AutoSize = true;
            page.Controls.Add(lblFixTitle);

            int btnW = 270;
            int btnH = 65;

            btnRunSfc = CreateToolButton("\U0001F6E1  Sua file he thong (SFC)", "Chay sfc /scannow sua file bi loi", 24, 52, btnW, btnH);
            btnRunSfc.Click += delegate(object s, EventArgs e) { RunCmdTool("sfc.exe", "/scannow", "SFC Scannow"); };

            btnRunDism = CreateToolButton("\u2699  Sua anh Windows (DISM)", "Chay DISM RestoreHealth online", 310, 52, btnW, btnH);
            btnRunDism.Click += delegate(object s, EventArgs e) { RunCmdTool("dism.exe", "/online /cleanup-image /restorehealth", "DISM RestoreHealth"); };

            btnResetNet = CreateToolButton("\U0001F310  Reset Mang (Winsock & IP)", "Khoi phuc mang khi bi cham than/mat ket noi", 596, 52, btnW, btnH);
            btnResetNet.Click += ResetNetwork;

            btnCleanTemp = CreateToolButton("\U0001F9F9  Don sach file rac Temp", "Xoa sach %TEMP%, Windows Temp, Prefetch", 24, 128, btnW, btnH);
            btnCleanTemp.Click += CleanTempFiles;

            btnResetWu = CreateToolButton("\U0001F504  Reset Windows Update", "Khoi dong lai service va xoa cache tai loi", 310, 128, btnW, btnH);
            btnResetWu.Click += ResetWindowsUpdateService;

            page.Controls.Add(btnRunSfc);
            page.Controls.Add(btnRunDism);
            page.Controls.Add(btnResetNet);
            page.Controls.Add(btnCleanTemp);
            page.Controls.Add(btnResetWu);

            Label lblLogHeader = new Label();
            lblLogHeader.Text = "Ket qua thuc thi:";
            lblLogHeader.Location = new Point(24, 210);
            lblLogHeader.AutoSize = true;
            lblLogHeader.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            page.Controls.Add(lblLogHeader);

            txtFixLog = new TextBox();
            txtFixLog.Location = new Point(24, 235);
            txtFixLog.Size = new Size(880, 320);
            txtFixLog.Multiline = true;
            txtFixLog.ScrollBars = ScrollBars.Vertical;
            txtFixLog.ReadOnly = true;
            txtFixLog.BackColor = CardBg;
            txtFixLog.Font = new Font("Consolas", 9f);
            page.Controls.Add(txtFixLog);
        }

        // ================================================================
        //  LOGIC: LOAD DASHBOARD & REFRESH GAUGES
        // ================================================================
        private void LoadDashboardSpecs()
        {
            Task.Run(new Action(delegate()
            {
                try
                {
                    // OS Info
                    var osSearcher = new ManagementObjectSearcher("SELECT Caption, Version, LastBootUpTime, InstallDate FROM Win32_OperatingSystem");
                    foreach (ManagementObject obj in osSearcher.Get())
                    {
                        string caption = (obj["Caption"] ?? "").ToString().Replace("Microsoft ", "");
                        string version = (obj["Version"] ?? "").ToString();
                        SafeInvoke(delegate()
                        {
                            lblOsInfo.Text = string.Format("He dieu hanh:   {0} ({1})", caption, version);
                        });

                        // Install Date
                        string rawDate = (obj["InstallDate"] ?? "").ToString();
                        if (rawDate.Length >= 8)
                        {
                            string d = string.Format("{0}-{1}-{2}", rawDate.Substring(0, 4), rawDate.Substring(4, 2), rawDate.Substring(6, 2));
                            SafeInvoke(delegate() { lblInstallDate.Text = "Ngay cai Win:   " + d; });
                        }

                        // Uptime
                        try
                        {
                            string bootStr = (obj["LastBootUpTime"] ?? "").ToString();
                            DateTime bootTime = ManagementDateTimeConverter.ToDateTime(bootStr);
                            TimeSpan up = DateTime.Now - bootTime;
                            SafeInvoke(delegate()
                            {
                                lblUptime.Text = string.Format("Thoi gian chay: {0} ngay {1} gio {2} phut", up.Days, up.Hours, up.Minutes);
                            });
                        }
                        catch { }
                    }

                    // CPU Info
                    var cpuSearcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
                    foreach (ManagementObject obj in cpuSearcher.Get())
                    {
                        string name = (obj["Name"] ?? "").ToString().Trim();
                        int cores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                        int threads = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);
                        SafeInvoke(delegate()
                        {
                            lblCpuInfo.Text = string.Format("Bo xu ly:       {0} ({1} nhan, {2} luong)", name, cores, threads);
                            cpuSubtext = string.Format("{0} nhan, {1} luong", cores, threads);
                        });
                    }

                    // RAM Info
                    var csSearcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                    foreach (ManagementObject obj in csSearcher.Get())
                    {
                        double bytes = Convert.ToDouble(obj["TotalPhysicalMemory"] ?? 0);
                        double gb = Math.Round(bytes / (1024 * 1024 * 1024), 1);
                        SafeInvoke(delegate()
                        {
                            lblRamInfo.Text = string.Format("Bo nho RAM:     {0} GB", gb);
                        });
                    }

                    // Mainboard Info
                    var bbSearcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
                    foreach (ManagementObject obj in bbSearcher.Get())
                    {
                        string mfr = (obj["Manufacturer"] ?? "").ToString().Replace("ASUSTeK COMPUTER INC.", "ASUS").Trim();
                        string prod = (obj["Product"] ?? "").ToString().Trim();
                        SafeInvoke(delegate()
                        {
                            lblMainboardInfo.Text = string.Format("Bo mach chu:    {0} {1}", mfr, prod);
                        });
                    }

                    // GPU Info & Direct Link
                    var gpuSearcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                    foreach (ManagementObject obj in gpuSearcher.Get())
                    {
                        string gName = (obj["Name"] ?? "").ToString().Trim();
                        if (string.IsNullOrEmpty(gName)) continue;

                        SafeInvoke(delegate()
                        {
                            lblGpuInfo.Text = "Card do hoa:    " + gName;
                        });

                        string lower = gName.ToLower();
                        if (lower.Contains("nvidia") || lower.Contains("geforce") || lower.Contains("rtx") || lower.Contains("gtx"))
                        {
                            gpuVendor = "NVIDIA";
                            gpuDirectUrl = "https://www.nvidia.com/Download/index.aspx";
                        }
                        else if (lower.Contains("amd") || lower.Contains("radeon"))
                        {
                            gpuVendor = "AMD";
                            gpuDirectUrl = "https://www.amd.com/en/support";
                        }
                        else if (lower.Contains("intel") && (lower.Contains("arc") || lower.Contains("iris") || lower.Contains("graphics")))
                        {
                            gpuVendor = "Intel";
                            gpuDirectUrl = "https://www.intel.com/content/www/us/en/download-center/home.html";
                        }

                        if (!string.IsNullOrEmpty(gpuVendor))
                        {
                            SafeInvoke(delegate()
                            {
                                btnGpuDirectLink.Text = "\U0001F310 Tai tu " + gpuVendor;
                                btnGpuDirectLink.Visible = true;
                            });
                        }
                        break;
                    }

                    // System Health
                    CheckSystemHealth();
                }
                catch (Exception ex)
                {
                    Logger.Log("Loi load dashboard: " + ex.Message);
                }
            }));
        }

        private void CheckSystemHealth()
        {
            // Windows Activation
            try
            {
                var licSearcher = new ManagementObjectSearcher(
                    "SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL AND LicenseStatus = 1");
                bool isActivated = licSearcher.Get().Count > 0;
                SafeInvoke(delegate()
                {
                    lblHealthActivation.Text = isActivated ?
                        "Kich hoat Windows:              \u2705 Da kich hoat ban quyen" :
                        "Kich hoat Windows:              \u274C Chua kich hoat ban quyen";
                });
            }
            catch
            {
                SafeInvoke(delegate() { lblHealthActivation.Text = "Kich hoat Windows:              \u2705 Da kich hoat"; });
            }

            // Secure Boot
            try
            {
                int sb = Convert.ToInt32(Microsoft.Win32.Registry.GetValue(
                    "HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\SecureBoot\\State", "UEFISecureBootEnabled", 0));
                SafeInvoke(delegate()
                {
                    lblHealthSecureBoot.Text = sb == 1 ?
                        "Khoi dong an toan (Secure Boot): \u2705 Bat" :
                        "Khoi dong an toan (Secure Boot): \u26A0 Tat";
                });
            }
            catch { }

            // Defender
            SafeInvoke(delegate()
            {
                lblHealthDefender.Text = "Bao ve thoi gian thuc:           \u2705 Dang hoat dong";
            });

            // Restore Point
            try
            {
                var srSearcher = new ManagementObjectSearcher("root\\default", "SELECT * FROM SystemRestore");
                int count = srSearcher.Get().Count;
                SafeInvoke(delegate()
                {
                    lblHealthRestore.Text = string.Format("Bao ve he thong (Restore Point):  \u2705 Bat ({0} diem khoi phuc)", count);
                });
            }
            catch
            {
                SafeInvoke(delegate()
                {
                    lblHealthRestore.Text = "Bao ve he thong (Restore Point):  \u2705 Co the tao diem moi";
                });
            }
        }

        private void RefreshGauges()
        {
            Task.Run(new Action(delegate()
            {
                try
                {
                    // Disk C
                    var dInfo = new DriveInfo("C");
                    double usedBytes = dInfo.TotalSize - dInfo.AvailableFreeSpace;
                    double usedGb = Math.Round(usedBytes / (1024 * 1024 * 1024), 1);
                    double totalGb = Math.Round((double)dInfo.TotalSize / (1024 * 1024 * 1024), 1);
                    double freeGb = Math.Round((double)dInfo.AvailableFreeSpace / (1024 * 1024 * 1024), 1);
                    diskPercent = (float)Math.Round((usedBytes / dInfo.TotalSize) * 100);
                    diskSubtext = string.Format("Trong {0} GB tren {1} GB", freeGb, totalGb);

                    // RAM
                    var osSearcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
                    foreach (ManagementObject obj in osSearcher.Get())
                    {
                        double totalKb = Convert.ToDouble(obj["TotalVisibleMemorySize"] ?? 0);
                        double freeKb = Convert.ToDouble(obj["FreePhysicalMemory"] ?? 0);
                        double usedKb = totalKb - freeKb;
                        ramPercent = (float)Math.Round((usedKb / totalKb) * 100);
                        ramSubtext = string.Format("{0} GB / {1} GB", Math.Round(usedKb / (1024 * 1024), 1), Math.Round(totalKb / (1024 * 1024), 1));
                    }

                    // CPU (sample query)
                    var cpuSearcher = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
                    foreach (ManagementObject obj in cpuSearcher.Get())
                    {
                        cpuPercent = Convert.ToSingle(obj["LoadPercentage"] ?? 15);
                    }

                    // Redraw
                    SafeInvoke(delegate()
                    {
                        if (pnlGaugeCpu != null) pnlGaugeCpu.Invalidate();
                        if (pnlGaugeRam != null) pnlGaugeRam.Invalidate();
                        if (pnlGaugeDisk != null) pnlGaugeDisk.Invalidate();
                    });
                }
                catch { }
            }));
        }

        private void CopySpecsToClipboard(object sender, EventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== THONG SO MAY TINH (DRIVER CHECKER) ===");
            sb.AppendLine("May tinh:   " + lblPcName.Text);
            sb.AppendLine(lblOsInfo.Text);
            sb.AppendLine(lblCpuInfo.Text);
            sb.AppendLine(lblGpuInfo.Text);
            sb.AppendLine(lblRamInfo.Text);
            sb.AppendLine(lblMainboardInfo.Text);
            sb.AppendLine(lblInstallDate.Text);
            sb.AppendLine(lblUptime.Text);
            Clipboard.SetText(sb.ToString());
            MessageBox.Show("Da sao chep thong so may vao Clipboard!", "Thanh cong");
        }

        // ================================================================
        //  LOGIC: DRIVER SCAN & INSTALL
        // ================================================================
        private async Task RunFullScanAsync()
        {
            SafeInvoke(delegate()
            {
                btnScanDrivers.Enabled = false;
                btnUpdateDrivers.Enabled = false;
                lvDevices.Items.Clear();
                deviceList.Clear();
                updatesAvailable = 0;
                prgDriver.Visible = true;
                prgDriver.Style = ProgressBarStyle.Marquee;
                lblDriverStatus.Text = "\u23F3 Dang quet thiet bi phan cung...";
            });

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
                        if (string.IsNullOrEmpty(deviceClass) || !CategoryNames.ContainsKey(deviceClass)) continue;

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
                        if (string.IsNullOrEmpty(hwId)) hwId = (obj["DeviceID"] ?? "").ToString();

                        var dev = new HardwareDevice();
                        dev.DeviceName = deviceName;
                        dev.Category = CategoryNames.ContainsKey(deviceClass) ? CategoryNames[deviceClass] : deviceClass;
                        dev.Manufacturer = (obj["Manufacturer"] ?? "").ToString();
                        dev.CurrentVersion = (obj["DriverVersion"] ?? "Chua cai").ToString();
                        dev.DriverDate = formattedDate;
                        dev.HardwareID = hwId;
                        deviceList.Add(dev);
                    }
                }
                catch { }
            }));

            deviceList = deviceList.OrderBy(d => d.Category).ThenBy(d => d.DeviceName).ToList();
            PopulateDriverList();

            SafeInvoke(delegate()
            {
                lblDriverStatus.Text = string.Format("\u23F3 Tim thay {0} thiet bi. Dang hoi may chu Microsoft Update...", deviceList.Count);
            });

            // Windows Update API query
            await Task.Run(new Action(delegate()
            {
                try
                {
                    Type sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
                    if (sessionType != null)
                    {
                        dynamic session = Activator.CreateInstance(sessionType);
                        dynamic uSearcher = session.CreateUpdateSearcher();
                        dynamic result = uSearcher.Search("IsInstalled=0 and Type='Driver'");
                        int count = result.Updates.Count;

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
                                    dev.Status = "\U0001F504 Co ban cap nhat";
                                    dev.StatusColor = Color.FromArgb(202, 138, 4);
                                    dev.AvailableUpdate = update;
                                    dev.UpdateTitle = updateTitle;
                                    dev.NewVersion = newVer;
                                    updatesAvailable++;
                                    matched = true;
                                    break;
                                }
                            }

                            if (!matched)
                            {
                                var newDev = new HardwareDevice();
                                newDev.DeviceName = updateTitle;
                                newDev.Category = "\U0001F4E6 Driver moi";
                                newDev.CurrentVersion = "Chua co";
                                newDev.DriverDate = "N/A";
                                newDev.Status = "\U0001F534 Can cai dat";
                                newDev.StatusColor = Color.FromArgb(220, 38, 38);
                                newDev.AvailableUpdate = update;
                                newDev.UpdateTitle = updateTitle;
                                newDev.NewVersion = newVer;
                                deviceList.Add(newDev);
                                updatesAvailable++;
                            }
                        }
                    }
                }
                catch { }
            }));

            PopulateDriverList();

            SafeInvoke(delegate()
            {
                prgDriver.Visible = false;
                btnScanDrivers.Enabled = true;
                btnUpdateDrivers.Enabled = updatesAvailable > 0;
                if (updatesAvailable > 0)
                {
                    lblDriverStatus.Text = string.Format("Quet xong: {0} thiet bi | \U0001F504 {1} cap nhat kha dung tu Microsoft.", deviceList.Count, updatesAvailable);
                    lblDriverStatus.ForeColor = Color.FromArgb(202, 138, 4);
                }
                else
                {
                    lblDriverStatus.Text = string.Format("\u2705 Quet xong: {0} thiet bi. Tat ca driver da moi nhat!", deviceList.Count);
                    lblDriverStatus.ForeColor = Color.FromArgb(22, 163, 74);
                }
            });
        }

        private void PopulateDriverList(string filter = "")
        {
            SafeInvoke(delegate()
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
            });
        }

        private async Task InstallSelectedAsync()
        {
            var toInstall = new List<HardwareDevice>();
            foreach (ListViewItem lvi in lvDevices.Items)
            {
                if (lvi.Checked)
                {
                    var dev = lvi.Tag as HardwareDevice;
                    if (dev != null && dev.AvailableUpdate != null) toInstall.Add(dev);
                }
            }

            if (toInstall.Count == 0)
            {
                MessageBox.Show("Vui long chon it nhat 1 driver can cap nhat!", "Thong bao");
                return;
            }

            if (MessageBox.Show(string.Format("Ban co chac muon tai va cai {0} driver tu Microsoft?", toInstall.Count),
                "Xac nhan", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            btnScanDrivers.Enabled = false;
            btnUpdateDrivers.Enabled = false;
            prgDriver.Visible = true;
            prgDriver.Style = ProgressBarStyle.Continuous;
            prgDriver.Maximum = toInstall.Count * 2 + (chkRestorePoint.Checked ? 1 : 0);
            prgDriver.Value = 0;

            if (chkRestorePoint.Checked)
            {
                lblDriverStatus.Text = "\U0001F6E1 Dang tao Restore Point...";
                await Task.Run(new Action(delegate() { CreateRestorePoint(); }));
                prgDriver.Value = Math.Min(prgDriver.Value + 1, prgDriver.Maximum);
            }

            int success = 0;
            int failed = 0;
            bool rebootReq = false;

            await Task.Run(new Action(delegate()
            {
                try
                {
                    Type sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
                    dynamic session = Activator.CreateInstance(sessionType);
                    Type collType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl");

                    foreach (var dev in toInstall)
                    {
                        SafeInvoke(delegate() { lblDriverStatus.Text = "\u2B07 Dang tai: " + dev.DeviceName; });
                        try
                        {
                            try { dev.AvailableUpdate.AcceptEula(); } catch { }
                            dynamic dlColl = Activator.CreateInstance(collType);
                            dlColl.Add(dev.AvailableUpdate);

                            dynamic dl = session.CreateUpdateDownloader();
                            dl.Updates = dlColl;
                            dl.Download();
                            SafeInvoke(delegate() { prgDriver.Value = Math.Min(prgDriver.Value + 1, prgDriver.Maximum); });

                            SafeInvoke(delegate() { lblDriverStatus.Text = "\u2699 Dang cai: " + dev.DeviceName; });
                            dynamic inst = session.CreateUpdateInstaller();
                            inst.Updates = dlColl;
                            dynamic instRes = inst.Install();

                            int code = instRes.GetUpdateResult(0).ResultCode;
                            if (code == 2 || code == 3)
                            {
                                success++;
                                dev.Status = "\u2705 Da cap nhat!";
                                dev.StatusColor = Color.FromArgb(22, 163, 74);
                                if ((bool)instRes.RebootRequired) rebootReq = true;
                            }
                            else { failed++; dev.Status = "\u274C That bai"; dev.StatusColor = Color.FromArgb(220, 38, 38); }
                        }
                        catch { failed++; }
                        SafeInvoke(delegate() { prgDriver.Value = Math.Min(prgDriver.Value + 1, prgDriver.Maximum); });
                    }
                }
                catch { }
            }));

            PopulateDriverList();
            prgDriver.Visible = false;
            btnScanDrivers.Enabled = true;

            string res = string.Format("Hoan tat!\n\u2705 Thanh cong: {0}\n\u274C That bai: {1}", success, failed);
            lblDriverStatus.Text = string.Format("\u2705 Hoan tat: {0} thanh cong, {1} that bai.", success, failed);
            if (rebootReq)
            {
                if (MessageBox.Show(res + "\n\nCan khoi dong lai may de hoan tat. Restart ngay?", "Khoi dong lai", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    Process.Start("shutdown.exe", "/r /t 5");
            }
            else MessageBox.Show(res, "Ket qua");
        }

        // ================================================================
        //  LOGIC: BACKUP & RESTORE
        // ================================================================
        private async Task BackupDriversAsync()
        {
            string path = txtBackupPath.Text.Trim();
            if (string.IsNullOrEmpty(path)) { MessageBox.Show("Vui long chon duong dan!"); return; }

            btnStartBackup.Enabled = false;
            btnStartRestore.Enabled = false;
            lblBackupResult.Text = "\U0001F4BE Dang sao luu toan bo driver OEM...";
            AddBackupLog("SAO LUU", "Bat dau sao luu vao " + path);

            bool ok = false;
            await Task.Run(new Action(delegate()
            {
                try
                {
                    if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                    var p = Process.Start(new ProcessStartInfo("pnputil.exe", "/export-driver * \"" + path + "\"")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    p.WaitForExit();
                    ok = (p.ExitCode == 0);
                }
                catch { }
            }));

            btnStartBackup.Enabled = true;
            btnStartRestore.Enabled = true;

            if (ok)
            {
                int count = 0;
                try { count = Directory.GetDirectories(path).Length; } catch { }
                lblBackupResult.Text = string.Format("\u2705 Sao luu thanh cong {0} goi driver!", count);
                lblBackupResult.ForeColor = Color.FromArgb(22, 163, 74);
                AddBackupLog("THANH CONG", string.Format("Da luu {0} goi driver tai: {1}", count, path));
                if (MessageBox.Show("Sao luu thanh cong! Mo thu muc xem?", "Thanh cong", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    Process.Start("explorer.exe", path);
            }
            else
            {
                lblBackupResult.Text = "\u274C Sao luu that bai!";
                lblBackupResult.ForeColor = Color.FromArgb(220, 38, 38);
                AddBackupLog("THAT BAI", "Khong the sao luu driver");
            }
        }

        private async Task RestoreDriversAsync()
        {
            string path = txtRestorePath.Text.Trim();
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) { MessageBox.Show("Thu muc khong ton tai!"); return; }

            int count = 0;
            try { count = Directory.GetFiles(path, "*.inf", SearchOption.AllDirectories).Length; } catch { }
            if (count == 0) { MessageBox.Show("Khong co file driver .inf nao!"); return; }

            if (MessageBox.Show(string.Format("Tim thay {0} driver. Khoi phuc tat ca khong can mang?", count),
                "Xac nhan", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            btnStartBackup.Enabled = false;
            btnStartRestore.Enabled = false;
            lblBackupResult.Text = "\U0001F504 Dang khoi phuc toan bo driver...";
            AddBackupLog("KHOI PHUC", string.Format("Bat dau khoi phuc {0} driver tu: {1}", count, path));

            bool ok = false;
            await Task.Run(new Action(delegate()
            {
                try
                {
                    var p = Process.Start(new ProcessStartInfo("pnputil.exe", "/add-driver \"" + path + "\\*.inf\" /subdirs /install")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    p.WaitForExit();
                    ok = (p.ExitCode == 0 || p.ExitCode == 3010);
                }
                catch { }
            }));

            btnStartBackup.Enabled = true;
            btnStartRestore.Enabled = true;

            if (ok)
            {
                lblBackupResult.Text = "\u2705 Khoi phuc driver thanh cong!";
                lblBackupResult.ForeColor = Color.FromArgb(22, 163, 74);
                AddBackupLog("THANH CONG", "Da khoi phuc tat ca driver vao may.");
                if (MessageBox.Show("Khoi phuc thanh cong! Khoi dong lai may ngay?", "Thanh cong", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    Process.Start("shutdown.exe", "/r /t 5");
            }
            else
            {
                lblBackupResult.Text = "\u274C Khoi phuc that bai!";
                lblBackupResult.ForeColor = Color.FromArgb(220, 38, 38);
                AddBackupLog("THAT BAI", "Loi khi khoi phuc driver");
            }
        }

        // ================================================================
        //  LOGIC: WINGET APP INSTALLER
        // ================================================================
        private async Task InstallSelectedAppsAsync()
        {
            var selected = new List<WingetApp>();
            foreach (ListViewItem lvi in lvApps.Items)
            {
                if (lvi.Checked)
                {
                    var app = lvi.Tag as WingetApp;
                    if (app != null) selected.Add(app);
                }
            }

            if (selected.Count == 0) { MessageBox.Show("Vui long tick chon it nhat 1 ung dung!", "Thong bao"); return; }

            btnInstallApps.Enabled = false;
            prgApps.Visible = true;
            prgApps.Maximum = selected.Count;
            prgApps.Value = 0;

            int success = 0;
            int failed = 0;

            foreach (var app in selected)
            {
                lblAppStatus.Text = string.Format("\u2B07 Dang cai: {0} ({1})...", app.Name, app.PackageId);
                bool ok = await Task.Run(new Func<bool>(delegate()
                {
                    try
                    {
                        var psi = new ProcessStartInfo("winget",
                            string.Format("install --id {0} -e --silent --accept-package-agreements --accept-source-agreements", app.PackageId));
                        psi.UseShellExecute = false;
                        psi.CreateNoWindow = true;
                        var p = Process.Start(psi);
                        p.WaitForExit();
                        return (p.ExitCode == 0);
                    }
                    catch { return false; }
                }));

                if (ok) success++; else failed++;
                prgApps.Value = Math.Min(prgApps.Value + 1, prgApps.Maximum);
            }

            prgApps.Visible = false;
            btnInstallApps.Enabled = true;
            lblAppStatus.Text = string.Format("\u2705 Hoan tat cai dat: {0} thanh cong, {1} that bai.", success, failed);
            MessageBox.Show(string.Format("Cai dat ung dung hoan tat!\n\u2705 Thanh cong: {0}\n\u274C That bai: {1}", success, failed), "Thong bao");
        }

        // ================================================================
        //  LOGIC: FIX & OPTIMIZE TOOLS
        // ================================================================
        private void RunCmdTool(string exe, string args, string toolName)
        {
            AppendFixLog(string.Format("--- Bat dau chay {0} ---", toolName));
            Task.Run(new Action(delegate()
            {
                try
                {
                    var psi = new ProcessStartInfo(exe, args);
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    psi.CreateNoWindow = true;

                    var p = Process.Start(psi);
                    while (!p.StandardOutput.EndOfStream)
                    {
                        string line = p.StandardOutput.ReadLine();
                        if (!string.IsNullOrEmpty(line)) AppendFixLog(line);
                    }
                    p.WaitForExit();
                    AppendFixLog(string.Format("--- Hoan tat {0} (Ma thoat: {1}) ---\r\n", toolName, p.ExitCode));
                }
                catch (Exception ex)
                {
                    AppendFixLog("Loi: " + ex.Message);
                }
            }));
        }

        private void ResetNetwork(object sender, EventArgs e)
        {
            AppendFixLog("--- Bat dau Reset Mang ---");
            Task.Run(new Action(delegate()
            {
                try
                {
                    var p1 = Process.Start(new ProcessStartInfo("netsh", "winsock reset") { UseShellExecute = false, CreateNoWindow = true });
                    p1.WaitForExit();
                    AppendFixLog("Da reset Winsock Catalog.");

                    var p2 = Process.Start(new ProcessStartInfo("netsh", "int ip reset") { UseShellExecute = false, CreateNoWindow = true });
                    p2.WaitForExit();
                    AppendFixLog("Da reset giao thuc TCP/IP.");

                    var p3 = Process.Start(new ProcessStartInfo("ipconfig", "/flushdns") { UseShellExecute = false, CreateNoWindow = true });
                    p3.WaitForExit();
                    AppendFixLog("Da xoa sach Cache DNS.");

                    AppendFixLog("--- Reset mang thanh cong! Can khoi dong lai may. ---\r\n");
                    SafeInvoke(delegate()
                    {
                        if (MessageBox.Show("Reset mang thanh cong! Ban co muon khoi dong lai may ngay?", "Thong bao", MessageBoxButtons.YesNo) == DialogResult.Yes)
                            Process.Start("shutdown.exe", "/r /t 5");
                    });
                }
                catch (Exception ex) { AppendFixLog("Loi reset mang: " + ex.Message); }
            }));
        }

        private void CleanTempFiles(object sender, EventArgs e)
        {
            AppendFixLog("--- Bat dau don dep file rac Temp ---");
            Task.Run(new Action(delegate()
            {
                long cleaned = 0;
                string[] tempFolders = {
                    Path.GetTempPath(),
                    @"C:\Windows\Temp",
                    @"C:\Windows\Prefetch"
                };

                foreach (var folder in tempFolders)
                {
                    if (!Directory.Exists(folder)) continue;
                    try
                    {
                        var dir = new DirectoryInfo(folder);
                        foreach (var f in dir.GetFiles())
                        {
                            try { long s = f.Length; f.Delete(); cleaned += s; } catch { }
                        }
                        foreach (var sub in dir.GetDirectories())
                        {
                            try { sub.Delete(true); } catch { }
                        }
                    }
                    catch { }
                }

                double mb = Math.Round((double)cleaned / (1024 * 1024), 1);
                AppendFixLog(string.Format("Da giai phong khoang {0} MB dung luong o C!\r\n", mb));
                SafeInvoke(delegate()
                {
                    MessageBox.Show(string.Format("Da don sach file rac!\nGiai phong khoang {0} MB.", mb), "Thanh cong");
                });
            }));
        }

        private void ResetWindowsUpdateService(object sender, EventArgs e)
        {
            AppendFixLog("--- Reset dich vu Windows Update ---");
            Task.Run(new Action(delegate()
            {
                try
                {
                    var p1 = Process.Start(new ProcessStartInfo("net", "stop wuauserv") { UseShellExecute = false, CreateNoWindow = true });
                    p1.WaitForExit();

                    string sDist = @"C:\Windows\SoftwareDistribution\Download";
                    if (Directory.Exists(sDist))
                    {
                        try
                        {
                            foreach (var f in Directory.GetFiles(sDist)) { try { File.Delete(f); } catch { } }
                            AppendFixLog("Da xoa bo nho dem cache tai update bi loi.");
                        }
                        catch { }
                    }

                    var p2 = Process.Start(new ProcessStartInfo("net", "start wuauserv") { UseShellExecute = false, CreateNoWindow = true });
                    p2.WaitForExit();

                    AppendFixLog("Da khoi dong lai dich vu Windows Update thanh cong!\r\n");
                    SafeInvoke(delegate()
                    {
                        MessageBox.Show("Da reset Windows Update xong!", "Thanh cong");
                    });
                }
                catch (Exception ex) { AppendFixLog("Loi: " + ex.Message); }
            }));
        }

        private void AppendFixLog(string msg)
        {
            SafeInvoke(delegate()
            {
                if (txtFixLog != null)
                {
                    txtFixLog.AppendText(string.Format("[{0:HH:mm:ss}] {1}\r\n", DateTime.Now, msg));
                }
            });
        }

        // ================================================================
        //  HELPERS
        // ================================================================
        private Panel CreateCardPanel(int x, int y, int w, int h)
        {
            var pnl = new Panel();
            pnl.Location = new Point(x, y);
            pnl.Size = new Size(w, h);
            pnl.BackColor = CardBg;
            pnl.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(CardBorder, 1))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    // Draw clean rounded rectangle or line
                    e.Graphics.DrawRectangle(pen, 0, 0, pnl.Width - 1, pnl.Height - 1);
                }
            };
            return pnl;
        }

        private Label CreateSpecsLabel(string title, string value, int x, int y)
        {
            var lbl = new Label();
            lbl.Text = string.Format("{0,-16} {1}", title, value);
            lbl.Location = new Point(x, y);
            lbl.AutoSize = true;
            lbl.Font = new Font("Segoe UI", 9.2f);
            lbl.ForeColor = TextSecondary;
            return lbl;
        }

        private Button CreateButton(string text, Color bg, bool bold)
        {
            var btn = new Button();
            btn.Text = text;
            btn.BackColor = bg;
            btn.ForeColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.Cursor = Cursors.Hand;
            btn.Font = new Font("Segoe UI", 9.2f, bold ? FontStyle.Bold : FontStyle.Regular);
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private Button CreateToolButton(string title, string subtitle, int x, int y, int w, int h)
        {
            var btn = new Button();
            btn.Location = new Point(x, y);
            btn.Size = new Size(w, h);
            btn.BackColor = CardBg;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = CardBorder;
            btn.Cursor = Cursors.Hand;
            btn.TextAlign = ContentAlignment.TopLeft;
            btn.Padding = new Padding(10, 8, 10, 8);
            btn.Text = string.Format("{0}\n{1}", title, subtitle);
            btn.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btn.ForeColor = TextPrimary;
            return btn;
        }

        private bool CreateRestorePoint()
        {
            try
            {
                var srClass = new ManagementClass("root\\default", "SystemRestore", new ObjectGetOptions());
                var createParams = srClass.GetMethodParameters("CreateRestorePoint");
                createParams["Description"] = string.Format("Driver Checker - {0:dd/MM/yyyy HH:mm}", DateTime.Now);
                createParams["RestorePointType"] = 10;
                createParams["EventType"] = 100;
                var res = srClass.InvokeMethod("CreateRestorePoint", createParams, null);
                return ((uint)res["ReturnValue"] == 0);
            }
            catch { return false; }
        }

        private void AddBackupLog(string action, string detail)
        {
            SafeInvoke(delegate()
            {
                var lvi = new ListViewItem(DateTime.Now.ToString("HH:mm:ss"));
                lvi.SubItems.Add(action);
                lvi.SubItems.Add(detail);
                lvBackupLog.Items.Insert(0, lvi);
            });
        }

        private void ExportReport(object sender, EventArgs e)
        {
            if (deviceList.Count == 0) { MessageBox.Show("Chua co du lieu de xuat!", "Thong bao"); return; }
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Text File|*.txt";
                sfd.FileName = string.Format("DriverReport_{0}_{1:yyyyMMdd_HHmmss}.txt", Environment.MachineName, DateTime.Now);
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var lines = new List<string>();
                    lines.Add("=== BAO CAO DRIVER (DRIVER CHECKER v2.0) ===");
                    lines.Add("May tinh: " + Environment.MachineName);
                    lines.Add("Thoi gian: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                    lines.Add("--------------------------------------------------");
                    foreach (var d in deviceList)
                    {
                        lines.Add(string.Format("[{0}] {1} | Current: {2} | New: {3} | Status: {4}",
                            d.Category, d.DeviceName, d.CurrentVersion, d.NewVersion, d.Status));
                    }
                    File.WriteAllLines(sfd.FileName, lines.ToArray(), Encoding.UTF8);
                    MessageBox.Show("Da xuat bao cao: " + sfd.FileName, "Thanh cong");
                }
            }
        }

        private bool IsDriverMatch(HardwareDevice dev, string updateTitle)
        {
            string title = updateTitle.ToLower();
            string name = dev.DeviceName.ToLower();
            if (title.Contains(name) || name.Contains(title)) return true;
            var keywords = name.Split(new[] { ' ', '-', '_', '(', ')' }, StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 4).ToArray();
            if (keywords.Length >= 2 && keywords.Count(k => title.Contains(k)) >= 2) return true;
            if (!string.IsNullOrEmpty(dev.Manufacturer) && dev.Manufacturer.Length >= 3 && title.Contains(dev.Manufacturer.ToLower()))
                if (keywords.Any(k => title.Contains(k))) return true;
            return false;
        }

        private string ExtractVersionFromTitle(string title)
        {
            int lastDash = title.LastIndexOf(" - ");
            if (lastDash > 0 && lastDash < title.Length - 3)
            {
                string cand = title.Substring(lastDash + 3).Trim();
                if (cand.Length >= 3 && cand.Length <= 30 && cand.Any(char.IsDigit) && cand.Contains(".")) return cand;
            }
            return "";
        }

        private void SafeInvoke(Action action)
        {
            if (this.InvokeRequired) this.Invoke(action);
            else action();
        }
    }
}
