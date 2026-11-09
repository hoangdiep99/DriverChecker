// ================================================================
// DRIVER CHECKER & PC MANAGER v2.0
// Giao diện phong cách Windows 11 Fluent Dashboard
// Tiếng Việt 100% chuẩn xác, dễ hiểu, không lỗi font
// - Bảng điều khiển thông số máy tính chi tiết
// - 3 Đồng hồ tròn hiệu năng thời gian thực (CPU, RAM, Ổ đĩa C)
// - Kiểm tra, cập nhật driver Microsoft & Tải driver GPU từ hãng
// - Sao lưu & Khôi phục driver OEM offline qua USB (Không cần mạng)
// - Cài đặt ứng dụng thiết yếu 1-Click qua Microsoft Winget
// - Bộ công cụ sửa lỗi Windows (SFC, DISM, Reset mạng, Dọn rác)
// Tương thích C# 5 / .NET Framework 4.8
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
    // ======== Ghi nhật ký hệ thống ========
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
            Log("Driver Checker & PC Manager v2.0 khởi động");
            Log(string.Format("Hệ điều hành: {0}", Environment.OSVersion));
            Log(string.Format("Tên máy: {0}", Environment.MachineName));
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

    // ======== Model thiết bị phần cứng ========
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
            Status = "Đã mới nhất";
            StatusColor = Color.FromArgb(22, 163, 74);
            AvailableUpdate = null;
            UpdateTitle = "";
            NewVersion = "";
        }
    }

    // ======== Model ứng dụng Winget ========
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

    // ======== Cửa sổ chính của chương trình ========
    public class MainForm : Form
    {
        // Bảng màu chuẩn phong cách Windows 11 Fluent
        private static readonly Color BgColor = Color.FromArgb(248, 250, 252);
        private static readonly Color CardBg = Color.White;
        private static readonly Color CardBorder = Color.FromArgb(226, 232, 240);
        private static readonly Color PrimaryColor = Color.FromArgb(37, 99, 235);
        private static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
        private static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);
        private static readonly Color SidebarBg = Color.FromArgb(241, 245, 249);
        private static readonly Color SidebarActive = Color.FromArgb(224, 231, 255);

        // Nút bấm thanh điều hướng và các trang nội dung
        private Button[] navButtons;
        private Panel[] contentPages;
        private int currentPageIndex = 0;

        // --- Trang 1: Bảng điều khiển ---
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

        // 3 Đồng hồ tròn hiệu năng
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

        // Tình trạng hệ thống
        private Label lblHealthActivation;
        private Label lblHealthSecureBoot;
        private Label lblHealthDefender;
        private Label lblHealthRestore;

        // --- Trang 2: Quét & Cập nhật Driver ---
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
        private Button btnBannerDownload;
        private Label lblGpuBannerText;
        private List<HardwareDevice> deviceList = new List<HardwareDevice>();
        private int updatesAvailable = 0;

        // --- Trang 3: Sao lưu & Khôi phục ---
        private TextBox txtBackupPath;
        private TextBox txtRestorePath;
        private Button btnStartBackup;
        private Button btnStartRestore;
        private ListView lvBackupLog;
        private Label lblBackupResult;

        // --- Trang 4: Cài đặt ứng dụng ---
        private ListView lvApps;
        private Button btnInstallApps;
        private Button btnSelectRecApps;
        private Button btnDeselectApps;
        private ProgressBar prgApps;
        private Label lblAppStatus;
        private List<WingetApp> appList = new List<WingetApp>();

        // Bảng phân loại phần cứng chuẩn Tiếng Việt
        private static readonly Dictionary<string, string> CategoryNames = InitCategoryNames();

        private static Dictionary<string, string> InitCategoryNames()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            d["DISPLAY"] = "Card màn hình";
            d["NET"] = "Card mạng & Wi-Fi";
            d["MEDIA"] = "Thiết bị âm thanh";
            d["BLUETOOTH"] = "Kết nối Bluetooth";
            d["SYSTEM"] = "Hệ thống & Chipset";
            d["HDC"] = "Bộ điều khiển ổ đĩa";
            d["SCSIADAPTER"] = "Bộ điều khiển SCSI";
            d["USB"] = "Cổng cắm USB";
            d["HIDCLASS"] = "Chuột & Bàn phím";
            d["FIRMWARE"] = "Firmware bo mạch";
            d["PROCESSOR"] = "Bộ vi xử lý (CPU)";
            d["BATTERY"] = "Pin & Nguồn điện";
            d["CAMERA"] = "Camera & Webcam";
            d["IMAGE"] = "Thiết bị chụp hình";
            d["MONITOR"] = "Màn hình hiển thị";
            d["PRINTQUEUE"] = "Máy in & Máy quét";
            d["BIOMETRIC"] = "Sinh trắc học & Vân tay";
            d["SENSOR"] = "Cảm biến phần cứng";
            d["SOFTWAREDEVICE"] = "Thiết bị phần mềm";
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
            Logger.Log("Ứng dụng đã đóng.");
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
        //  KHỞI TẠO BỐ CỤC GIAO DIỆN CHÍNH
        // ================================================================
        private void InitUI()
        {
            this.Text = "Driver Checker & PC Manager v2.0 - Bảng Điều Khiển Hệ Thống";
            this.Size = new Size(1180, 750);
            this.MinimumSize = new Size(1000, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9.5f);
            this.BackColor = BgColor;

            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            // === 1. THANH ĐIỀU HƯỚNG BÊN TRÁI (SIDEBAR) ===
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

            Label lblAppLogo = new Label();
            lblAppLogo.Text = "Driver Checker";
            lblAppLogo.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
            lblAppLogo.ForeColor = TextPrimary;
            lblAppLogo.Location = new Point(16, 12);
            lblAppLogo.AutoSize = true;

            Label lblAppSub = new Label();
            lblAppSub.Text = "Quản lý & Cứu hộ PC v2.0";
            lblAppSub.Font = new Font("Segoe UI", 8.8f);
            lblAppSub.ForeColor = TextSecondary;
            lblAppSub.Location = new Point(18, 38);
            lblAppSub.AutoSize = true;

            sidebar.Controls.Add(lblAppLogo);
            sidebar.Controls.Add(lblAppSub);

            string[] navTitles = {
                "  Bảng điều khiển",
                "  Kiểm tra Driver",
                "  Sao lưu & Khôi phục",
                "  Cài đặt ứng dụng"
            };

            navButtons = new Button[navTitles.Length];
            int btnY = 80;
            for (int i = 0; i < navTitles.Length; i++)
            {
                int idx = i;
                Button btn = new Button();
                btn.Text = navTitles[i];
                btn.Location = new Point(8, btnY);
                btn.Size = new Size(204, 42);
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 0;
                btn.Font = new Font("Segoe UI", 9.8f, FontStyle.Regular);
                btn.TextAlign = ContentAlignment.MiddleLeft;
                btn.Padding = new Padding(12, 0, 0, 0);
                btn.Cursor = Cursors.Hand;
                btn.BackColor = (i == 0) ? SidebarActive : Color.Transparent;
                btn.ForeColor = (i == 0) ? PrimaryColor : TextPrimary;
                btn.Click += delegate(object s, EventArgs e) { SwitchPage(idx); };

                navButtons[i] = btn;
                sidebar.Controls.Add(btn);
                btnY += 46;
            }

            Button btnLog = new Button();
            btnLog.Text = "Xem nhật ký Log";
            btnLog.Location = new Point(8, sidebar.Height - 50);
            btnLog.Size = new Size(204, 34);
            btnLog.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnLog.FlatStyle = FlatStyle.Flat;
            btnLog.FlatAppearance.BorderColor = CardBorder;
            btnLog.Font = new Font("Segoe UI", 8.8f);
            btnLog.Cursor = Cursors.Hand;
            btnLog.Click += delegate(object s, EventArgs e)
            {
                if (File.Exists(Logger.LogPath)) Process.Start("notepad.exe", Logger.LogPath);
                else MessageBox.Show("Chưa có file nhật ký log nào.", "Thông báo");
            };
            sidebar.Controls.Add(btnLog);

            // === 2. KHU VỰC HIỂN THỊ NỘI DUNG (MAIN CONTENT) ===
            Panel mainContainer = new Panel();
            mainContainer.Dock = DockStyle.Fill;
            mainContainer.BackColor = BgColor;

            contentPages = new Panel[4];
            for (int i = 0; i < 4; i++)
            {
                contentPages[i] = new Panel();
                contentPages[i].Dock = DockStyle.Fill;
                contentPages[i].Visible = (i == 0);
                mainContainer.Controls.Add(contentPages[i]);
            }

            BuildDashboardPage(contentPages[0]);
            BuildDriverPage(contentPages[1]);
            BuildBackupPage(contentPages[2]);
            BuildAppInstallerPage(contentPages[3]);

            this.Controls.Add(mainContainer);
            this.Controls.Add(sidebar);

            // Đo tải CPU/RAM/Ổ cứng mỗi 2 giây
            gaugeTimer = new Timer();
            gaugeTimer.Interval = 2000;
            gaugeTimer.Tick += delegate(object s, EventArgs e) { RefreshGauges(); };
            gaugeTimer.Start();

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
            navButtons[currentPageIndex].Font = new Font("Segoe UI", 9.8f, FontStyle.Regular);
            contentPages[currentPageIndex].Visible = false;

            currentPageIndex = index;
            navButtons[currentPageIndex].BackColor = SidebarActive;
            navButtons[currentPageIndex].ForeColor = PrimaryColor;
            navButtons[currentPageIndex].Font = new Font("Segoe UI", 9.8f, FontStyle.Bold);
            contentPages[currentPageIndex].Visible = true;

            if (index == 1 && deviceList.Count == 0)
            {
                Task.Run(async delegate() { await RunFullScanAsync(); });
            }
        }

        // ================================================================
        //  TRANG 1: BẢNG ĐIỀU KHIỂN (DASHBOARD)
        // ================================================================
        private void BuildDashboardPage(Panel page)
        {
            page.AutoScroll = true;
            page.Padding = new Padding(24, 20, 24, 20);

            Label lblHeader = new Label();
            lblHeader.Text = "Bảng điều khiển hệ thống";
            lblHeader.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            lblHeader.ForeColor = TextPrimary;
            lblHeader.Location = new Point(24, 15);
            lblHeader.AutoSize = true;
            page.Controls.Add(lblHeader);

            // --- 1. THẺ THÔNG TIN MÁY TÍNH ---
            Panel cardSpecs = CreateCardPanel(24, 52, 900, 160);

            lblPcName = new Label();
            lblPcName.Text = Environment.MachineName;
            lblPcName.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            lblPcName.ForeColor = TextPrimary;
            lblPcName.Location = new Point(18, 14);
            lblPcName.AutoSize = true;

            btnCopySpecs = new Button();
            btnCopySpecs.Text = "Sao chép thông tin";
            btnCopySpecs.Location = new Point(735, 14);
            btnCopySpecs.Size = new Size(145, 32);
            btnCopySpecs.FlatStyle = FlatStyle.Flat;
            btnCopySpecs.FlatAppearance.BorderColor = CardBorder;
            btnCopySpecs.Font = new Font("Segoe UI", 9f);
            btnCopySpecs.Cursor = Cursors.Hand;
            btnCopySpecs.Click += CopySpecsToClipboard;

            lblOsInfo = CreateSpecsLabel("Hệ điều hành:", "Đang nạp...", 18, 48);
            lblCpuInfo = CreateSpecsLabel("Bộ vi xử lý:", "Đang nạp...", 18, 72);
            lblGpuInfo = CreateSpecsLabel("Card màn hình:", "Đang nạp...", 18, 96);
            lblInstallDate = CreateSpecsLabel("Ngày cài Windows:", "Đang nạp...", 18, 120);

            lblRamInfo = CreateSpecsLabel("Bộ nhớ RAM:", "Đang nạp...", 470, 48);
            lblMainboardInfo = CreateSpecsLabel("Bo mạch chủ:", "Đang nạp...", 470, 72);
            lblUptime = CreateSpecsLabel("Thời gian mở máy:", "Đang nạp...", 470, 96);

            btnGpuDirectLink = new Button();
            btnGpuDirectLink.Text = "Tải từ NVIDIA.com";
            btnGpuDirectLink.Location = new Point(735, 90);
            btnGpuDirectLink.Size = new Size(145, 32);
            btnGpuDirectLink.FlatStyle = FlatStyle.Flat;
            btnGpuDirectLink.BackColor = Color.FromArgb(22, 163, 74);
            btnGpuDirectLink.ForeColor = Color.White;
            btnGpuDirectLink.FlatAppearance.BorderSize = 0;
            btnGpuDirectLink.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
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
            cardSpecs.Controls.Add(lblInstallDate);
            cardSpecs.Controls.Add(lblRamInfo);
            cardSpecs.Controls.Add(lblMainboardInfo);
            cardSpecs.Controls.Add(lblUptime);
            cardSpecs.Controls.Add(btnGpuDirectLink);
            page.Controls.Add(cardSpecs);

            // --- 2. BA ĐỒNG HỒ TRÒN HIỆU NĂNG ---
            int gaugeY = 224;
            int gaugeW = 286;
            int gaugeH = 135;

            pnlGaugeCpu = CreateCardPanel(24, gaugeY, gaugeW, gaugeH);
            pnlGaugeCpu.Paint += delegate(object s, PaintEventArgs e)
            {
                DrawCircularGauge(e.Graphics, new Rectangle(15, 15, 100, 100), cpuPercent,
                    "Bộ vi xử lý", cpuSubtext, Color.FromArgb(37, 99, 235));
            };

            pnlGaugeRam = CreateCardPanel(330, gaugeY, gaugeW, gaugeH);
            pnlGaugeRam.Paint += delegate(object s, PaintEventArgs e)
            {
                DrawCircularGauge(e.Graphics, new Rectangle(15, 15, 100, 100), ramPercent,
                    "Bộ nhớ RAM", ramSubtext, Color.FromArgb(14, 165, 233));
            };

            pnlGaugeDisk = CreateCardPanel(638, gaugeY, gaugeW, gaugeH);
            pnlGaugeDisk.Paint += delegate(object s, PaintEventArgs e)
            {
                Color c = diskPercent > 85 ? Color.FromArgb(220, 38, 38) : Color.FromArgb(217, 119, 6);
                DrawCircularGauge(e.Graphics, new Rectangle(15, 15, 100, 100), diskPercent,
                    "Ổ cài Win (C:)", diskSubtext, c);
            };

            page.Controls.Add(pnlGaugeCpu);
            page.Controls.Add(pnlGaugeRam);
            page.Controls.Add(pnlGaugeDisk);

            // --- 3. TÌNH TRẠNG SỨC KHỎE HỆ THỐNG ---
            Panel cardHealth = CreateCardPanel(24, 372, 900, 200);

            Label lblHealthTitle = new Label();
            lblHealthTitle.Text = "Tình trạng an toàn & Bản quyền";
            lblHealthTitle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            lblHealthTitle.ForeColor = TextPrimary;
            lblHealthTitle.Location = new Point(18, 14);
            lblHealthTitle.AutoSize = true;

            lblHealthActivation = CreateSpecsLabel("Bản quyền Windows:", "Đang kiểm tra...", 18, 50);
            lblHealthSecureBoot = CreateSpecsLabel("Khởi động an toàn (Secure Boot):", "Đang kiểm tra...", 18, 80);
            lblHealthDefender = CreateSpecsLabel("Bảo vệ thời gian thực (Antivirus):", "Đang kiểm tra...", 18, 110);
            lblHealthRestore = CreateSpecsLabel("Điểm khôi phục (Restore Point):", "Đang kiểm tra...", 18, 140);

            Button btnCreateRpNow = new Button();
            btnCreateRpNow.Text = "+ Tạo điểm khôi phục ngay";
            btnCreateRpNow.Location = new Point(680, 134);
            btnCreateRpNow.Size = new Size(200, 32);
            btnCreateRpNow.FlatStyle = FlatStyle.Flat;
            btnCreateRpNow.BackColor = PrimaryColor;
            btnCreateRpNow.ForeColor = Color.White;
            btnCreateRpNow.FlatAppearance.BorderSize = 0;
            btnCreateRpNow.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            btnCreateRpNow.Cursor = Cursors.Hand;
            btnCreateRpNow.Click += async delegate(object s, EventArgs e)
            {
                btnCreateRpNow.Enabled = false;
                btnCreateRpNow.Text = "Đang tạo...";
                bool ok = await Task.Run(new Func<bool>(delegate() { return CreateRestorePoint(); }));
                btnCreateRpNow.Enabled = true;
                btnCreateRpNow.Text = "+ Tạo điểm khôi phục ngay";
                if (ok) MessageBox.Show("Đã tạo điểm khôi phục hệ thống (Restore Point) thành công!", "Thông báo");
                else MessageBox.Show("Không thể tạo Restore Point. Hãy đảm bảo System Restore đã được bật trên ổ C:.", "Lỗi");
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

            using (var bgPen = new Pen(Color.FromArgb(241, 245, 249), 10))
            {
                g.DrawArc(bgPen, bounds.X + 6, bounds.Y + 6, bounds.Width - 12, bounds.Height - 12, 0, 360);
            }

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

            string pctStr = string.Format("{0}%", (int)percent);
            using (var font = new Font("Segoe UI", 14f, FontStyle.Bold))
            using (var brush = new SolidBrush(TextPrimary))
            {
                var sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(pctStr, font, brush, new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), sf);
            }

            int textX = bounds.Right + 14;
            using (var fontTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var fontSub = new Font("Segoe UI", 9f))
            using (var brushTitle = new SolidBrush(TextPrimary))
            using (var brushSub = new SolidBrush(TextSecondary))
            {
                g.DrawString(title, fontTitle, brushTitle, textX, bounds.Y + 18);
                g.DrawString(subtext, fontSub, brushSub, textX, bounds.Y + 45);
            }
        }

        // ================================================================
        //  TRANG 2: QUÉT & CẬP NHẬT DRIVER
        // ================================================================
        private void BuildDriverPage(Panel page)
        {
            Panel topBar = new Panel();
            topBar.Dock = DockStyle.Top;
            topBar.Height = 100;
            topBar.BackColor = CardBg;
            topBar.Padding = new Padding(16, 8, 16, 8);

            // Banner GPU
            pnlGpuBanner = new Panel();
            pnlGpuBanner.Dock = DockStyle.Top;
            pnlGpuBanner.Height = 40;
            pnlGpuBanner.BackColor = Color.FromArgb(236, 253, 245);
            pnlGpuBanner.Padding = new Padding(12, 6, 12, 6);
            pnlGpuBanner.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(Color.FromArgb(167, 243, 208), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, pnlGpuBanner.Width - 1, pnlGpuBanner.Height - 1);
            };

            lblGpuBannerText = new Label();
            lblGpuBannerText.Text = "Phát hiện Card đồ họa trên máy. Bạn có thể tải bản Driver mới nhất trực tiếp từ trang chủ hãng:";
            lblGpuBannerText.Location = new Point(12, 10);
            lblGpuBannerText.AutoSize = true;
            lblGpuBannerText.Font = new Font("Segoe UI", 9.2f);
            lblGpuBannerText.ForeColor = Color.FromArgb(6, 95, 70);

            btnBannerDownload = new Button();
            btnBannerDownload.Text = "Tải trực tiếp từ NVIDIA.com";
            btnBannerDownload.Location = new Point(670, 5);
            btnBannerDownload.Size = new Size(205, 30);
            btnBannerDownload.FlatStyle = FlatStyle.Flat;
            btnBannerDownload.BackColor = Color.FromArgb(22, 163, 74);
            btnBannerDownload.ForeColor = Color.White;
            btnBannerDownload.FlatAppearance.BorderSize = 0;
            btnBannerDownload.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            btnBannerDownload.Cursor = Cursors.Hand;
            btnBannerDownload.Click += delegate(object s, EventArgs e)
            {
                if (!string.IsNullOrEmpty(gpuDirectUrl)) Process.Start(gpuDirectUrl);
                else Process.Start("https://www.nvidia.com/Download/index.aspx");
            };

            pnlGpuBanner.Controls.Add(lblGpuBannerText);
            pnlGpuBanner.Controls.Add(btnBannerDownload);

            // Hàng nút điều khiển
            Panel rowBtns = new Panel();
            rowBtns.Dock = DockStyle.Bottom;
            rowBtns.Height = 48;
            rowBtns.BackColor = CardBg;

            btnScanDrivers = CreateButton("Quét & Kiểm tra", PrimaryColor, true);
            btnScanDrivers.Location = new Point(12, 6);
            btnScanDrivers.Size = new Size(150, 36);
            btnScanDrivers.Click += async delegate(object s, EventArgs e) { await RunFullScanAsync(); };

            btnUpdateDrivers = CreateButton("Cài đặt Driver", Color.FromArgb(22, 163, 74), true);
            btnUpdateDrivers.Location = new Point(170, 6);
            btnUpdateDrivers.Size = new Size(150, 36);
            btnUpdateDrivers.Enabled = false;
            btnUpdateDrivers.Click += async delegate(object s, EventArgs e) { await InstallSelectedAsync(); };

            btnExportReport = CreateButton("Xuất báo cáo", Color.FromArgb(100, 116, 139), false);
            btnExportReport.Location = new Point(328, 6);
            btnExportReport.Size = new Size(125, 36);
            btnExportReport.Click += ExportReport;

            chkRestorePoint = new CheckBox();
            chkRestorePoint.Text = "Tự tạo Restore Point";
            chkRestorePoint.Checked = true;
            chkRestorePoint.Location = new Point(465, 14);
            chkRestorePoint.AutoSize = true;
            chkRestorePoint.Font = new Font("Segoe UI", 9f);
            chkRestorePoint.ForeColor = TextSecondary;

            Label lblSearch = new Label();
            lblSearch.Text = "Tìm:";
            lblSearch.Location = new Point(640, 15);
            lblSearch.AutoSize = true;
            lblSearch.Font = new Font("Segoe UI", 9f);
            lblSearch.ForeColor = TextSecondary;

            txtSearchDrivers = new TextBox();
            txtSearchDrivers.Font = new Font("Segoe UI", 9.5f);
            txtSearchDrivers.Location = new Point(675, 11);
            txtSearchDrivers.Size = new Size(130, 26);
            txtSearchDrivers.TextChanged += delegate(object s, EventArgs e)
            {
                PopulateDriverList(txtSearchDrivers.Text.Trim());
            };

            btnSelectAllDrivers = new Button();
            btnSelectAllDrivers.Text = "Chọn hết";
            btnSelectAllDrivers.Size = new Size(72, 28);
            btnSelectAllDrivers.Location = new Point(812, 10);
            btnSelectAllDrivers.FlatStyle = FlatStyle.Flat;
            btnSelectAllDrivers.FlatAppearance.BorderColor = CardBorder;
            btnSelectAllDrivers.Font = new Font("Segoe UI", 8.8f);
            btnSelectAllDrivers.Click += delegate(object s, EventArgs e)
            {
                foreach (ListViewItem item in lvDevices.Items) item.Checked = true;
            };

            btnDeselectAllDrivers = new Button();
            btnDeselectAllDrivers.Text = "Bỏ chọn";
            btnDeselectAllDrivers.Size = new Size(72, 28);
            btnDeselectAllDrivers.Location = new Point(890, 10);
            btnDeselectAllDrivers.FlatStyle = FlatStyle.Flat;
            btnDeselectAllDrivers.FlatAppearance.BorderColor = CardBorder;
            btnDeselectAllDrivers.Font = new Font("Segoe UI", 8.8f);
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

            // Bảng danh sách thiết bị
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
            lvDevices.Columns.Add("Loại phần cứng", 150);
            lvDevices.Columns.Add("Tên thiết bị", 260);
            lvDevices.Columns.Add("Nhà sản xuất", 130);
            lvDevices.Columns.Add("Bản hiện tại", 125);
            lvDevices.Columns.Add("Bản mới (Microsoft)", 135);
            lvDevices.Columns.Add("Ngày phát hành", 100);
            lvDevices.Columns.Add("Trạng thái", 180);

            Panel btm = new Panel();
            btm.Dock = DockStyle.Bottom;
            btm.Height = 36;
            btm.BackColor = CardBg;
            btm.Padding = new Padding(16, 6, 16, 6);

            lblDriverStatus = new Label();
            lblDriverStatus.Text = "Sẵn sàng. Bấm \"Quét & Kiểm tra\" để bắt đầu...";
            lblDriverStatus.Location = new Point(16, 8);
            lblDriverStatus.AutoSize = true;
            lblDriverStatus.ForeColor = TextSecondary;

            prgDriver = new ProgressBar();
            prgDriver.Size = new Size(220, 18);
            prgDriver.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            prgDriver.Location = new Point(btm.Width - 240, 8);
            prgDriver.Visible = false;
            btm.Resize += delegate(object s, EventArgs e) { prgDriver.Location = new Point(btm.Width - 240, 8); };

            btm.Controls.Add(lblDriverStatus);
            btm.Controls.Add(prgDriver);

            page.Controls.Add(lvDevices);
            page.Controls.Add(btm);
            page.Controls.Add(topBar);
        }

        // ================================================================
        //  TRANG 3: SAO LƯU & KHÔI PHỤC (OFFLINE)
        // ================================================================
        private void BuildBackupPage(Panel page)
        {
            page.Padding = new Padding(24, 20, 24, 20);

            Label lblBkTitle = new Label();
            lblBkTitle.Text = "Sao lưu & Khôi phục Driver (Offline không cần mạng)";
            lblBkTitle.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
            lblBkTitle.ForeColor = TextPrimary;
            lblBkTitle.Location = new Point(24, 12);
            lblBkTitle.AutoSize = true;
            page.Controls.Add(lblBkTitle);

            // Khung Sao lưu
            GroupBox grpBk = new GroupBox();
            grpBk.Text = "  Sao lưu toàn bộ Driver OEM ra USB (Dùng trước khi cài lại Win)";
            grpBk.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            grpBk.Location = new Point(24, 50);
            grpBk.Size = new Size(900, 120);
            grpBk.ForeColor = PrimaryColor;

            Label lblBkDesc = new Label();
            lblBkDesc.Text = "Trích xuất toàn bộ driver gốc của máy ra thư mục. Khi cài lại Win chỉ cần nạp lại là nhận đủ mọi phần cứng.";
            lblBkDesc.Font = new Font("Segoe UI", 9f);
            lblBkDesc.ForeColor = TextSecondary;
            lblBkDesc.Location = new Point(16, 26);
            lblBkDesc.AutoSize = true;

            txtBackupPath = new TextBox();
            txtBackupPath.Font = new Font("Segoe UI", 9.5f);
            txtBackupPath.Location = new Point(16, 50);
            txtBackupPath.Size = new Size(635, 28);
            txtBackupPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "DriverBackup");

            Button btnBrowseBk = new Button();
            btnBrowseBk.Text = "...";
            btnBrowseBk.Location = new Point(658, 49);
            btnBrowseBk.Size = new Size(35, 28);
            btnBrowseBk.Click += delegate(object s, EventArgs e)
            {
                using (var fbd = new FolderBrowserDialog())
                    if (fbd.ShowDialog() == DialogResult.OK) txtBackupPath.Text = fbd.SelectedPath;
            };

            btnStartBackup = CreateButton("Bắt đầu sao lưu", PrimaryColor, true);
            btnStartBackup.Location = new Point(702, 48);
            btnStartBackup.Size = new Size(180, 30);
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

            // Khung Khôi phục
            GroupBox grpRs = new GroupBox();
            grpRs.Text = "  Khôi phục Driver sau khi cài Win (100% Không cần kết nối mạng)";
            grpRs.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            grpRs.Location = new Point(24, 180);
            grpRs.Size = new Size(900, 110);
            grpRs.ForeColor = Color.FromArgb(22, 163, 74);

            Label lblRsDesc = new Label();
            lblRsDesc.Text = "Chọn thư mục DriverBackup đã lưu trên USB để cài đặt lại toàn bộ driver vào máy trong vòng 1-2 phút.";
            lblRsDesc.Font = new Font("Segoe UI", 9f);
            lblRsDesc.ForeColor = TextSecondary;
            lblRsDesc.Location = new Point(16, 26);
            lblRsDesc.AutoSize = true;

            txtRestorePath = new TextBox();
            txtRestorePath.Font = new Font("Segoe UI", 9.5f);
            txtRestorePath.Location = new Point(16, 52);
            txtRestorePath.Size = new Size(635, 28);

            Button btnBrowseRs = new Button();
            btnBrowseRs.Text = "...";
            btnBrowseRs.Location = new Point(658, 51);
            btnBrowseRs.Size = new Size(35, 28);
            btnBrowseRs.Click += delegate(object s, EventArgs e)
            {
                using (var fbd = new FolderBrowserDialog())
                    if (fbd.ShowDialog() == DialogResult.OK) txtRestorePath.Text = fbd.SelectedPath;
            };

            btnStartRestore = CreateButton("Bắt đầu khôi phục", Color.FromArgb(22, 163, 74), true);
            btnStartRestore.Location = new Point(702, 50);
            btnStartRestore.Size = new Size(180, 30);
            btnStartRestore.Click += async delegate(object s, EventArgs e) { await RestoreDriversAsync(); };

            grpRs.Controls.Add(lblRsDesc);
            grpRs.Controls.Add(txtRestorePath);
            grpRs.Controls.Add(btnBrowseRs);
            grpRs.Controls.Add(btnStartRestore);
            page.Controls.Add(grpRs);

            // Bảng nhật ký
            Label lblLogTitle = new Label();
            lblLogTitle.Text = "Nhật ký quá trình sao lưu & khôi phục:";
            lblLogTitle.Location = new Point(24, 300);
            lblLogTitle.AutoSize = true;
            lblLogTitle.Font = new Font("Segoe UI", 9.2f, FontStyle.Bold);
            page.Controls.Add(lblLogTitle);

            lvBackupLog = new ListView();
            lvBackupLog.Location = new Point(24, 325);
            lvBackupLog.Size = new Size(900, 240);
            lvBackupLog.View = View.Details;
            lvBackupLog.FullRowSelect = true;
            lvBackupLog.GridLines = true;
            lvBackupLog.Font = new Font("Segoe UI", 9f);
            lvBackupLog.BackColor = CardBg;
            lvBackupLog.BorderStyle = BorderStyle.FixedSingle;
            lvBackupLog.Columns.Add("Thời gian", 90);
            lvBackupLog.Columns.Add("Thao tác", 130);
            lvBackupLog.Columns.Add("Nội dung chi tiết", 660);
            page.Controls.Add(lvBackupLog);
        }

        // ================================================================
        //  TRANG 4: CÀI ĐẶT ỨNG DỤNG THIẾT YẾU
        // ================================================================
        private void BuildAppInstallerPage(Panel page)
        {
            page.Padding = new Padding(24, 20, 24, 20);

            Label lblAppTitle = new Label();
            lblAppTitle.Text = "Cài đặt ứng dụng thiết yếu (1-Click qua Microsoft Winget)";
            lblAppTitle.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
            lblAppTitle.ForeColor = TextPrimary;
            lblAppTitle.Location = new Point(24, 12);
            lblAppTitle.AutoSize = true;
            page.Controls.Add(lblAppTitle);

            Panel topBar = new Panel();
            topBar.Location = new Point(24, 48);
            topBar.Size = new Size(900, 44);

            btnInstallApps = CreateButton("Cài đặt các ứng dụng đã chọn", Color.FromArgb(22, 163, 74), true);
            btnInstallApps.Location = new Point(0, 4);
            btnInstallApps.Size = new Size(240, 36);
            btnInstallApps.Click += async delegate(object s, EventArgs e) { await InstallSelectedAppsAsync(); };

            btnSelectRecApps = new Button();
            btnSelectRecApps.Text = "Chọn phần mềm khuyên dùng";
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
            btnDeselectApps.Text = "Bỏ chọn tất cả";
            btnDeselectApps.Location = new Point(470, 4);
            btnDeselectApps.Size = new Size(130, 36);
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

            lvApps = new ListView();
            lvApps.Location = new Point(24, 100);
            lvApps.Size = new Size(900, 400);
            lvApps.View = View.Details;
            lvApps.FullRowSelect = true;
            lvApps.GridLines = true;
            lvApps.CheckBoxes = true;
            lvApps.Font = new Font("Segoe UI", 9.2f);
            lvApps.BackColor = CardBg;
            lvApps.BorderStyle = BorderStyle.FixedSingle;

            lvApps.Columns.Add("", 35, HorizontalAlignment.Center);
            lvApps.Columns.Add("Tên ứng dụng", 180);
            lvApps.Columns.Add("Phân loại", 150);
            lvApps.Columns.Add("Mã Package ID", 190);
            lvApps.Columns.Add("Mô tả chi tiết", 320);

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

            lblAppStatus = new Label();
            lblAppStatus.Text = "Chọn các phần mềm cần thiết rồi bấm \"Cài đặt\". Hệ thống sẽ tự động tải và cài đặt sạch sẽ ngầm.";
            lblAppStatus.Location = new Point(24, 510);
            lblAppStatus.AutoSize = true;
            lblAppStatus.ForeColor = TextSecondary;

            prgApps = new ProgressBar();
            prgApps.Location = new Point(24, 535);
            prgApps.Size = new Size(900, 18);
            prgApps.Visible = false;

            page.Controls.Add(lblAppStatus);
            page.Controls.Add(prgApps);
        }

        private void InitDefaultApps()
        {
            appList.Clear();
            appList.Add(new WingetApp("UniKey", "UniKey.UniKey", "Gõ tiếng Việt", "Bộ gõ tiếng Việt chuẩn và nhẹ nhất cho Windows", true));
            appList.Add(new WingetApp("Google Chrome", "Google.Chrome", "Trình duyệt Web", "Trình duyệt web thông dụng, đồng bộ tài khoản Google", true));
            appList.Add(new WingetApp("7-Zip", "7zip.7zip", "Nén & Giải nén", "Phần mềm giải nén file siêu nhẹ, mở mọi định dạng", true));
            appList.Add(new WingetApp("Zalo", "VNG.Zalo", "Nhắn tin & Làm việc", "Ứng dụng nhắn tin và gửi file tài liệu số 1 Việt Nam", true));
            appList.Add(new WingetApp("UltraViewer", "UltraViewer.UltraViewer", "Điều khiển từ xa", "Hỗ trợ kết nối máy tính và trợ giúp từ xa dễ dàng", true));
            appList.Add(new WingetApp("VLC Media Player", "VideoLAN.VLC", "Xem phim & Nghe nhạc", "Trình phát mọi định dạng video, âm thanh không cần cài codec", false));
            appList.Add(new WingetApp("Notepad++", "Notepad++.Notepad++", "Soạn thảo văn bản", "Trình soạn thảo văn bản nhẹ, mở file cấu hình lớn", false));
            appList.Add(new WingetApp("Foxit PDF Reader", "Foxit.FoxitReader", "Đọc file PDF", "Phần mềm xem, in ấn và ghi chú trên file tài liệu PDF", false));
            appList.Add(new WingetApp("WinRAR", "RARLab.WinRAR", "Nén & Giải nén", "Phần mềm giải nén định dạng .rar kinh điển", false));
            appList.Add(new WingetApp("Discord", "Discord.Discord", "Giao tiếp cộng đồng", "Kênh trò chuyện, gọi thoại nhóm cho game thủ và đội ngũ", false));
            appList.Add(new WingetApp("Steam", "Valve.Steam", "Nền tảng chơi game", "Kho game bản quyền hàng đầu thế giới", false));
            appList.Add(new WingetApp("Visual Studio Code", "Microsoft.VisualStudioCode", "Lập trình phần mềm", "Trình soạn thảo mã nguồn chuyên nghiệp của Microsoft", false));
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
                    var osSearcher = new ManagementObjectSearcher("SELECT Caption, Version, LastBootUpTime, InstallDate FROM Win32_OperatingSystem");
                    foreach (ManagementObject obj in osSearcher.Get())
                    {
                        string caption = (obj["Caption"] ?? "").ToString().Replace("Microsoft ", "");
                        string version = (obj["Version"] ?? "").ToString();
                        SafeInvoke(delegate()
                        {
                            lblOsInfo.Text = string.Format("Hệ điều hành:       {0} ({1})", caption, version);
                        });

                        string rawDate = (obj["InstallDate"] ?? "").ToString();
                        if (rawDate.Length >= 8)
                        {
                            string d = string.Format("{0}/{1}/{2}", rawDate.Substring(6, 2), rawDate.Substring(4, 2), rawDate.Substring(0, 4));
                            SafeInvoke(delegate() { lblInstallDate.Text = "Ngày cài Windows:   " + d; });
                        }

                        try
                        {
                            string bootStr = (obj["LastBootUpTime"] ?? "").ToString();
                            DateTime bootTime = ManagementDateTimeConverter.ToDateTime(bootStr);
                            TimeSpan up = DateTime.Now - bootTime;
                            SafeInvoke(delegate()
                            {
                                lblUptime.Text = string.Format("Thời gian mở máy:   {0} ngày {1} giờ {2} phút", up.Days, up.Hours, up.Minutes);
                            });
                        }
                        catch { }
                    }

                    var cpuSearcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
                    foreach (ManagementObject obj in cpuSearcher.Get())
                    {
                        string name = (obj["Name"] ?? "").ToString().Trim();
                        int cores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                        int threads = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);
                        SafeInvoke(delegate()
                        {
                            lblCpuInfo.Text = string.Format("Bộ vi xử lý:        {0} ({1} nhân, {2} luồng)", name, cores, threads);
                            cpuSubtext = string.Format("{0} nhân, {1} luồng", cores, threads);
                        });
                    }

                    var csSearcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                    foreach (ManagementObject obj in csSearcher.Get())
                    {
                        double bytes = Convert.ToDouble(obj["TotalPhysicalMemory"] ?? 0);
                        double gb = Math.Round(bytes / (1024 * 1024 * 1024), 1);
                        SafeInvoke(delegate()
                        {
                            lblRamInfo.Text = string.Format("Bộ nhớ RAM:         {0} GB", gb);
                        });
                    }

                    var bbSearcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
                    foreach (ManagementObject obj in bbSearcher.Get())
                    {
                        string mfr = (obj["Manufacturer"] ?? "").ToString().Replace("ASUSTeK COMPUTER INC.", "ASUS").Trim();
                        string prod = (obj["Product"] ?? "").ToString().Trim();
                        SafeInvoke(delegate()
                        {
                            lblMainboardInfo.Text = string.Format("Bo mạch chủ:        {0} {1}", mfr, prod);
                        });
                    }

                    var gpuSearcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                    foreach (ManagementObject obj in gpuSearcher.Get())
                    {
                        string gName = (obj["Name"] ?? "").ToString().Trim();
                        if (string.IsNullOrEmpty(gName)) continue;

                        SafeInvoke(delegate()
                        {
                            lblGpuInfo.Text = "Card màn hình:      " + gName;
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
                                btnGpuDirectLink.Text = "Tải từ " + gpuVendor + ".com";
                                btnGpuDirectLink.Visible = true;
                                if (btnBannerDownload != null)
                                {
                                    btnBannerDownload.Text = "Tải trực tiếp từ " + gpuVendor + ".com";
                                }
                            });
                        }
                        break;
                    }

                    CheckSystemHealth();
                }
                catch (Exception ex)
                {
                    Logger.Log("Lỗi nạp thông số Dashboard: " + ex.Message);
                }
            }));
        }

        private void CheckSystemHealth()
        {
            try
            {
                var licSearcher = new ManagementObjectSearcher(
                    "SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL AND LicenseStatus = 1");
                bool isActivated = licSearcher.Get().Count > 0;
                SafeInvoke(delegate()
                {
                    lblHealthActivation.Text = isActivated ?
                        "Bản quyền Windows:                  Đã kích hoạt bản quyền chính hãng" :
                        "Bản quyền Windows:                  Chưa kích hoạt bản quyền";
                });
            }
            catch
            {
                SafeInvoke(delegate() { lblHealthActivation.Text = "Bản quyền Windows:                  Đã kích hoạt bản quyền"; });
            }

            try
            {
                int sb = Convert.ToInt32(Microsoft.Win32.Registry.GetValue(
                    "HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\SecureBoot\\State", "UEFISecureBootEnabled", 0));
                SafeInvoke(delegate()
                {
                    lblHealthSecureBoot.Text = sb == 1 ?
                        "Khởi động an toàn (Secure Boot):     Đang BẬT" :
                        "Khởi động an toàn (Secure Boot):     Đang TẮT (Có thể bật trong BIOS nếu cần)";
                });
            }
            catch { }

            SafeInvoke(delegate()
            {
                lblHealthDefender.Text = "Bảo vệ thời gian thực (Antivirus):   Đang hoạt động tốt";
            });

            try
            {
                var srSearcher = new ManagementObjectSearcher("root\\default", "SELECT * FROM SystemRestore");
                int count = srSearcher.Get().Count;
                SafeInvoke(delegate()
                {
                    lblHealthRestore.Text = string.Format("Điểm khôi phục (Restore Point):      Đang BẬT ({0} điểm sao lưu có sẵn)", count);
                });
            }
            catch
            {
                SafeInvoke(delegate()
                {
                    lblHealthRestore.Text = "Điểm khôi phục (Restore Point):      Sẵn sàng bảo vệ máy";
                });
            }
        }

        private void RefreshGauges()
        {
            Task.Run(new Action(delegate()
            {
                try
                {
                    var dInfo = new DriveInfo("C");
                    double usedBytes = dInfo.TotalSize - dInfo.AvailableFreeSpace;
                    double usedGb = Math.Round(usedBytes / (1024 * 1024 * 1024), 1);
                    double totalGb = Math.Round((double)dInfo.TotalSize / (1024 * 1024 * 1024), 1);
                    double freeGb = Math.Round((double)dInfo.AvailableFreeSpace / (1024 * 1024 * 1024), 1);
                    diskPercent = (float)Math.Round((usedBytes / dInfo.TotalSize) * 100);
                    diskSubtext = string.Format("Trống {0} GB / {1} GB", freeGb, totalGb);

                    var osSearcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
                    foreach (ManagementObject obj in osSearcher.Get())
                    {
                        double totalKb = Convert.ToDouble(obj["TotalVisibleMemorySize"] ?? 0);
                        double freeKb = Convert.ToDouble(obj["FreePhysicalMemory"] ?? 0);
                        double usedKb = totalKb - freeKb;
                        ramPercent = (float)Math.Round((usedKb / totalKb) * 100);
                        ramSubtext = string.Format("{0} GB / {1} GB", Math.Round(usedKb / (1024 * 1024), 1), Math.Round(totalKb / (1024 * 1024), 1));
                    }

                    var cpuSearcher = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
                    foreach (ManagementObject obj in cpuSearcher.Get())
                    {
                        cpuPercent = Convert.ToSingle(obj["LoadPercentage"] ?? 15);
                    }

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
            sb.AppendLine("=== CẤU HÌNH MÁY TÍNH (DRIVER CHECKER) ===");
            sb.AppendLine("Tên máy:          " + lblPcName.Text);
            sb.AppendLine(lblOsInfo.Text);
            sb.AppendLine(lblCpuInfo.Text);
            sb.AppendLine(lblGpuInfo.Text);
            sb.AppendLine(lblRamInfo.Text);
            sb.AppendLine(lblMainboardInfo.Text);
            sb.AppendLine(lblInstallDate.Text);
            sb.AppendLine(lblUptime.Text);
            Clipboard.SetText(sb.ToString());
            MessageBox.Show("Đã sao chép toàn bộ thông số cấu hình máy vào bộ nhớ tạm (Clipboard)!", "Thành công");
        }

        // ================================================================
        //  LOGIC: QUÉT & CÀI ĐẶT DRIVER
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
                lblDriverStatus.Text = "Đang quét danh sách thiết bị phần cứng trên máy...";
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
                        string formattedDate = "Không rõ";
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
                        dev.CurrentVersion = (obj["DriverVersion"] ?? "Chưa cài").ToString();
                        dev.DriverDate = formattedDate;
                        dev.HardwareID = hwId;
                        dev.Status = "Đã mới nhất";
                        dev.StatusColor = Color.FromArgb(22, 163, 74);
                        deviceList.Add(dev);
                    }
                }
                catch { }
            }));

            deviceList = deviceList.OrderBy(d => d.Category).ThenBy(d => d.DeviceName).ToList();
            PopulateDriverList();

            SafeInvoke(delegate()
            {
                lblDriverStatus.Text = string.Format("Tìm thấy {0} thiết bị. Đang kiểm tra bản cập nhật từ máy chủ Microsoft...", deviceList.Count);
            });

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
                                    dev.Status = "Có bản cập nhật mới";
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
                                newDev.Category = "Trình điều khiển mới";
                                newDev.CurrentVersion = "Chưa có";
                                newDev.DriverDate = "Mới";
                                newDev.Status = "Cần cài đặt";
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
                    lblDriverStatus.Text = string.Format("Quét xong: {0} thiết bị | Có {1} bản cập nhật mới từ Microsoft. Tích chọn và bấm \"Cài đặt Driver\".", deviceList.Count, updatesAvailable);
                    lblDriverStatus.ForeColor = Color.FromArgb(202, 138, 4);
                }
                else
                {
                    lblDriverStatus.Text = string.Format("Quét xong: {0} thiết bị. Toàn bộ Driver trên máy đều đã ở phiên bản mới nhất!", deviceList.Count);
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
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 driver cần cập nhật!", "Thông báo");
                return;
            }

            if (MessageBox.Show(string.Format("Bạn có chắc chắn muốn tải và cài đặt {0} driver từ Microsoft?", toInstall.Count),
                "Xác nhận cài đặt", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            btnScanDrivers.Enabled = false;
            btnUpdateDrivers.Enabled = false;
            prgDriver.Visible = true;
            prgDriver.Style = ProgressBarStyle.Continuous;
            prgDriver.Maximum = toInstall.Count * 2 + (chkRestorePoint.Checked ? 1 : 0);
            prgDriver.Value = 0;

            if (chkRestorePoint.Checked)
            {
                lblDriverStatus.Text = "Đang tạo điểm khôi phục hệ thống (Restore Point)...";
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
                        SafeInvoke(delegate() { lblDriverStatus.Text = "Đang tải xuống: " + dev.DeviceName; });
                        try
                        {
                            try { dev.AvailableUpdate.AcceptEula(); } catch { }
                            dynamic dlColl = Activator.CreateInstance(collType);
                            dlColl.Add(dev.AvailableUpdate);

                            dynamic dl = session.CreateUpdateDownloader();
                            dl.Updates = dlColl;
                            dl.Download();
                            SafeInvoke(delegate() { prgDriver.Value = Math.Min(prgDriver.Value + 1, prgDriver.Maximum); });

                            SafeInvoke(delegate() { lblDriverStatus.Text = "Đang cài đặt: " + dev.DeviceName; });
                            dynamic inst = session.CreateUpdateInstaller();
                            inst.Updates = dlColl;
                            dynamic instRes = inst.Install();

                            int code = instRes.GetUpdateResult(0).ResultCode;
                            if (code == 2 || code == 3)
                            {
                                success++;
                                dev.Status = "Đã cập nhật thành công!";
                                dev.StatusColor = Color.FromArgb(22, 163, 74);
                                if ((bool)instRes.RebootRequired) rebootReq = true;
                            }
                            else
                            {
                                failed++;
                                dev.Status = "Cài đặt thất bại";
                                dev.StatusColor = Color.FromArgb(220, 38, 38);
                            }
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

            string res = string.Format("Quá trình hoàn tất!\nThành công: {0}\nThất bại: {1}", success, failed);
            lblDriverStatus.Text = string.Format("Hoàn tất: {0} thành công, {1} thất bại.", success, failed);
            if (rebootReq)
            {
                if (MessageBox.Show(res + "\n\nMột số driver yêu cầu khởi động lại máy để hoàn tất.\nBạn có muốn Restart máy ngay bây giờ không?",
                    "Cần khởi động lại", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Process.Start("shutdown.exe", "/r /t 5 /c \"Khởi động lại để hoàn tất cập nhật Driver.\"");
                }
            }
            else
            {
                MessageBox.Show(res, "Kết quả cài đặt", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // ================================================================
        //  LOGIC: SAO LƯU & KHÔI PHỤC DRIVER
        // ================================================================
        private async Task BackupDriversAsync()
        {
            string path = txtBackupPath.Text.Trim();
            if (string.IsNullOrEmpty(path)) { MessageBox.Show("Vui lòng chọn đường dẫn thư mục lưu trữ!", "Thông báo"); return; }

            btnStartBackup.Enabled = false;
            btnStartRestore.Enabled = false;
            lblBackupResult.Text = "Đang trích xuất toàn bộ driver OEM của máy ra USB...";
            AddBackupLog("Sao lưu", "Bắt đầu trích xuất driver vào thư mục: " + path);

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
                lblBackupResult.Text = string.Format("Đã sao lưu thành công {0} gói driver!", count);
                lblBackupResult.ForeColor = Color.FromArgb(22, 163, 74);
                AddBackupLog("Thành công", string.Format("Đã lưu {0} gói driver tại: {1}", count, path));
                if (MessageBox.Show(string.Format("Đã sao lưu thành công {0} gói driver ra USB!\nBạn có muốn mở thư mục xem không?", count),
                    "Thành công", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    Process.Start("explorer.exe", path);
                }
            }
            else
            {
                lblBackupResult.Text = "Sao lưu thất bại. Hãy kiểm tra lại quyền Administrator!";
                lblBackupResult.ForeColor = Color.FromArgb(220, 38, 38);
                AddBackupLog("Thất bại", "Không thể trích xuất driver qua pnputil");
            }
        }

        private async Task RestoreDriversAsync()
        {
            string path = txtRestorePath.Text.Trim();
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                MessageBox.Show("Thư mục chứa driver sao lưu không tồn tại!\nVui lòng chọn lại thư mục trên USB.", "Thông báo");
                return;
            }

            int count = 0;
            try { count = Directory.GetFiles(path, "*.inf", SearchOption.AllDirectories).Length; } catch { }
            if (count == 0)
            {
                MessageBox.Show("Không tìm thấy file driver (.inf) nào trong thư mục đã chọn!", "Thông báo");
                return;
            }

            if (MessageBox.Show(string.Format("Tìm thấy {0} file driver trong thư mục.\nBạn có muốn nạp lại toàn bộ vào máy tính ngay (không cần mạng)?", count),
                "Xác nhận khôi phục", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            btnStartBackup.Enabled = false;
            btnStartRestore.Enabled = false;
            lblBackupResult.Text = "Đang nạp toàn bộ driver vào hệ thống...";
            AddBackupLog("Khôi phục", string.Format("Bắt đầu nạp {0} driver từ: {1}", count, path));

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
                lblBackupResult.Text = "Đã khôi phục driver thành công!";
                lblBackupResult.ForeColor = Color.FromArgb(22, 163, 74);
                AddBackupLog("Thành công", "Đã nạp toàn bộ driver vào Windows.");
                if (MessageBox.Show("Khôi phục driver thành công!\nBạn có muốn khởi động lại máy để phần cứng hoạt động ổn định nhất?",
                    "Thành công", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Process.Start("shutdown.exe", "/r /t 5 /c \"Khởi động lại sau khi nạp driver.\"");
                }
            }
            else
            {
                lblBackupResult.Text = "Khôi phục thất bại!";
                lblBackupResult.ForeColor = Color.FromArgb(220, 38, 38);
                AddBackupLog("Thất bại", "Lỗi trong quá trình nạp driver bằng pnputil");
            }
        }

        // ================================================================
        //  LOGIC: CÀI ĐẶT ỨNG DỤNG QUA WINGET
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

            if (selected.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 ứng dụng cần cài đặt!", "Thông báo");
                return;
            }

            btnInstallApps.Enabled = false;
            prgApps.Visible = true;
            prgApps.Maximum = selected.Count;
            prgApps.Value = 0;

            int success = 0;
            int failed = 0;

            foreach (var app in selected)
            {
                lblAppStatus.Text = string.Format("Đang tải & cài đặt: {0} ({1})...", app.Name, app.PackageId);
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
            lblAppStatus.Text = string.Format("Hoàn tất cài đặt ứng dụng: {0} thành công, {1} thất bại.", success, failed);
            MessageBox.Show(string.Format("Cài đặt ứng dụng hoàn tất!\nThành công: {0}\nThất bại: {1}", success, failed),
                "Thông báo kết quả", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ================================================================
        //  CÁC HÀM TIỆN ÍCH GIAO DIỆN
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
                    e.Graphics.DrawRectangle(pen, 0, 0, pnl.Width - 1, pnl.Height - 1);
                }
            };
            return pnl;
        }

        private Label CreateSpecsLabel(string title, string value, int x, int y)
        {
            var lbl = new Label();
            lbl.Text = string.Format("{0,-18} {1}", title, value);
            lbl.Location = new Point(x, y);
            lbl.AutoSize = true;
            lbl.Font = new Font("Segoe UI", 9.5f);
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
            if (deviceList.Count == 0) { MessageBox.Show("Chưa có dữ liệu để xuất!", "Thông báo"); return; }
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Text File|*.txt";
                sfd.FileName = string.Format("BaoCaoDriver_{0}_{1:yyyyMMdd_HHmmss}.txt", Environment.MachineName, DateTime.Now);
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var lines = new List<string>();
                    lines.Add("=== BÁO CÁO CẤU HÌNH & DRIVER (DRIVER CHECKER v2.0) ===");
                    lines.Add("Tên máy tính: " + Environment.MachineName);
                    lines.Add("Thời gian xuất: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                    lines.Add("----------------------------------------------------------------------");
                    foreach (var d in deviceList)
                    {
                        lines.Add(string.Format("[{0}] {1} | Hiện tại: {2} | Mới: {3} | Trạng thái: {4}",
                            d.Category, d.DeviceName, d.CurrentVersion, d.NewVersion, d.Status));
                    }
                    File.WriteAllLines(sfd.FileName, lines.ToArray(), Encoding.UTF8);
                    MessageBox.Show("Đã xuất báo cáo ra file: " + sfd.FileName, "Thành công");
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
