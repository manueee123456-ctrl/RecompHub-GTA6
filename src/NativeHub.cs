using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal sealed class ProjectHub : Form
{
    const string SiteUrl = "https://gta6recomp.com";
    static readonly Color Background = Color.FromArgb(17, 17, 24);
    static readonly Color Sidebar = Color.FromArgb(24, 23, 33);
    static readonly Color Surface = Color.FromArgb(31, 30, 43);
    static readonly Color Border = Color.FromArgb(54, 51, 70);
    static readonly Color Ink = Color.FromArgb(246, 243, 253);
    static readonly Color Accent = Color.FromArgb(196, 174, 255);
    static readonly Color Muted = Color.FromArgb(175, 169, 194);
    static readonly Font BodyFont = new Font("Segoe UI", 10);
    static readonly Font StrongFont = new Font("Segoe UI Semibold", 11);
    static readonly Font SmallFont = new Font("Segoe UI", 9);
    static readonly Font ArticleTitleFont = new Font("Segoe UI Semibold", 22);
    static readonly Font ArticleBodyFont = new Font("Segoe UI", 11);
    static readonly Bitmap BrandIcon = LoadBrand();
    static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    readonly ListBox news = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None,
        BackColor = Surface, ForeColor = Ink, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 86 };
    readonly RichTextBox article = Reader();
    readonly Label newsStatus = LabelFor("Loading news…");
    readonly Label releaseStatus = LabelFor("Checking release status…");
    readonly CheckBox alert = new CheckBox { Text = "Notify me if the status changes while the app is open", AutoSize = true };
    readonly Timer refresh = new Timer { Interval = 300000 };
    string lastRelease;
    int articleRequest;
    bool updatingNews;

    [STAThread]
    static void Main()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        Client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new ProjectHub());
    }

    ProjectHub()
    {
        Text = "Recomp Hub";
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("app-icon"))
        using (var icon = new Icon(stream))
            Icon = (Icon)icon.Clone();
        ClientSize = new Size(1170, 750);
        MinimumSize = new Size(920, 620);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Background;
        ForeColor = Ink;
        Font = BodyFont;

        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 202));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var sidebar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, BackColor = Sidebar, Padding = new Padding(16, 22, 16, 0), Margin = Padding.Empty };
        var brand = new Panel { Size = new Size(168, 66), Margin = new Padding(0, 0, 0, 22) };
        brand.Paint += DrawBrand;
        sidebar.Controls.Add(brand);
        var workspace = new Panel { Dock = DockStyle.Fill, Padding = new Padding(26, 22, 26, 24), Margin = Padding.Empty };
        var header = new Label { Dock = DockStyle.Top, Height = 70, Font = new Font("Segoe UI Semibold", 27),
            ForeColor = Ink, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0, 4, 0, 0) };
        var content = new Panel { Dock = DockStyle.Fill };
        workspace.Controls.Add(content);
        workspace.Controls.Add(header);
        shell.Controls.Add(sidebar, 0, 0);
        shell.Controls.Add(workspace, 1, 0);
        Controls.Add(shell);

        var newsPage = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        newsPage.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 267));
        newsPage.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        newsPage.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
        newsPage.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        newsStatus.Dock = DockStyle.Fill;
        newsStatus.ForeColor = Muted;
        newsStatus.Font = SmallFont;
        newsStatus.Padding = new Padding(0);
        newsPage.Controls.Add(newsStatus, 0, 0);
        newsPage.SetColumnSpan(newsStatus, 2);
        var listCard = Card(news, new Padding(6));
        listCard.Margin = new Padding(0, 0, 13, 0);
        newsPage.Controls.Add(listCard, 0, 1);
        article.BackColor = Surface;
        newsPage.Controls.Add(Card(article, new Padding(24)), 1, 1);
        news.DrawItem += DrawPostRow;
        news.SelectedIndexChanged += async (sender, args) =>
        {
            if (!updatingNews) await ReadPost();
        };

        var projects = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
            BackColor = Surface, ForeColor = Ink, BorderStyle = BorderStyle.None };
        projects.Columns.Add("Project", 290);
        projects.Columns.Add("Reported progress", 194);
        foreach (var line in Resource("repositories").Split('\n'))
        {
            var fields = line.Split('	');
            projects.Items.Add(new ListViewItem(new[] { fields[0], fields[1].TrimEnd() }));
        }
        var repos = new Panel { Dock = DockStyle.Fill };
        var snapshot = LabelFor("gta6recomp.com snapshot from November 24, 2026");
        snapshot.Dock = DockStyle.Top;
        snapshot.ForeColor = Muted;
        repos.Controls.Add(Card(projects, new Padding(14)));
        repos.Controls.Add(snapshot);

        var releaseLayout = Stack();
        releaseStatus.Font = new Font("Segoe UI Semibold", 18);
        releaseStatus.ForeColor = Accent;
        releaseLayout.Controls.Add(releaseStatus);
        releaseLayout.Controls.Add(alert);
        var faq = Reader();
        faq.BackColor = Surface;
        RenderFaq(faq);

        var pages = new Control[] { newsPage, Hardware(), repos, Card(releaseLayout, new Padding(24)),
            Card(faq, new Padding(24)) };
        var names = new[] { "News", "PC Compatibility", "Projects", "Release", "FAQ" };
        var symbols = new[] { "▤", "◈", "◇", "↓", "?" };
        var navigation = new Button[names.Length];
        Action<int> showPage = index =>
        {
            header.Text = names[index];
            for (int i = 0; i < pages.Length; i++)
            {
                bool active = i == index;
                pages[i].Visible = active;
                navigation[i].BackColor = active ? Color.FromArgb(51, 43, 72) : Sidebar;
                navigation[i].ForeColor = active ? Accent : Muted;
            }
        };
        for (int i = 0; i < pages.Length; i++)
        {
            int index = i;
            var button = new Button { Text = symbols[i] + "   " + names[i], AccessibleName = names[i],
                Size = new Size(169, 45), FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(11, 0, 0, 0), Margin = new Padding(0, 0, 0, 5),
                Font = StrongFont, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            RoundControl(button, 12);
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Surface;
            button.Click += (sender, args) => showPage(index);
            navigation[i] = button;
            sidebar.Controls.Add(button);
            pages[i].Dock = DockStyle.Fill;
            content.Controls.Add(pages[i]);
        }
        showPage(0);
        Shown += async (sender, args) => await UpdateFeeds();
        refresh.Tick += async (sender, args) => await UpdateFeeds();
        FormClosed += (sender, args) => refresh.Dispose();
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    protected override void OnHandleCreated(EventArgs args)
    {
        base.OnHandleCreated(args);
        int dark = 1;
        if (DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)) != 0)
            DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
    }

    static Bitmap LoadBrand()
    {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("brand"))
        using (var image = Image.FromStream(stream))
            return new Bitmap(image);
    }

    static void DrawBrand(object sender, PaintEventArgs args)
    {
        args.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        args.Graphics.DrawImage(BrandIcon, new Rectangle(0, 3, 47, 47));
        TextRenderer.DrawText(args.Graphics, "Recomp Hub", StrongFont, new Point(53, 17), Ink);
    }

    static GraphicsPath Rounded(Rectangle area, int radius)
    {
        var path = new GraphicsPath();
        int diameter = Math.Min(radius * 2, Math.Min(area.Width, area.Height));
        if (diameter <= 0) return path;
        path.AddArc(area.Left, area.Top, diameter, diameter, 180, 90);
        path.AddArc(area.Right - diameter, area.Top, diameter, diameter, 270, 90);
        path.AddArc(area.Right - diameter, area.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(area.Left, area.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    static void RoundControl(Control control, int radius)
    {
        Action update = () =>
        {
            if (control.Width <= 0 || control.Height <= 0) return;
            using (var path = Rounded(control.ClientRectangle, radius))
            {
                var previous = control.Region;
                control.Region = new Region(path);
                if (previous != null) previous.Dispose();
            }
        };
        control.SizeChanged += (sender, args) => update();
        update();
    }

    static Panel Card(Control child, Padding padding)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = padding, Margin = Padding.Empty };
        RoundControl(panel, 16);
        panel.Controls.Add(child);
        return panel;
    }

    static FlowLayoutPanel Stack()
    {
        return new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true };
    }

    void DrawPostRow(object sender, DrawItemEventArgs args)
    {
        if (args.Index < 0 || args.Index >= news.Items.Count) return;
        var post = (Post)news.Items[args.Index];
        bool current = (args.State & DrawItemState.Selected) != 0;
        using (var fill = new SolidBrush(current ? Color.FromArgb(52, 43, 73) : Surface))
            args.Graphics.FillRectangle(fill, args.Bounds);
        if (current)
            using (var indicator = new SolidBrush(Accent))
                args.Graphics.FillRectangle(indicator, args.Bounds.X, args.Bounds.Top + 9, 3, args.Bounds.Height - 18);
        var date = FormatPostDate(post);
        TextRenderer.DrawText(args.Graphics, date, SmallFont,
            new Rectangle(args.Bounds.X + 13, args.Bounds.Y + 9, args.Bounds.Width - 26, 19), current ? Accent : Muted,
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(args.Graphics, post.ToString(), StrongFont,
            new Rectangle(args.Bounds.X + 13, args.Bounds.Y + 30, args.Bounds.Width - 26, 45), Ink,
            TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.TextBoxControl);
        using (var divider = new Pen(Border))
            args.Graphics.DrawLine(divider, args.Bounds.Left + 12, args.Bounds.Bottom - 1, args.Bounds.Right - 12, args.Bounds.Bottom - 1);
        args.DrawFocusRectangle();
    }

    static string FormatPostDate(Post post)
    {
        DateTimeOffset date;
        return DateTimeOffset.TryParse(post.date, out date) ? date.ToLocalTime().ToString("MMM d, yyyy",
            System.Globalization.CultureInfo.GetCultureInfo("en-US")) : "";
    }

    static void RenderFaq(RichTextBox faq)
    {
        foreach (var line in Resource("faq").Split('\n'))
        {
            bool question = line.TrimEnd().EndsWith("?");
            faq.SelectionFont = question ? StrongFont : ArticleBodyFont;
            faq.SelectionColor = question ? Accent : Ink;
            faq.AppendText(line.TrimEnd() + "\n");
        }
        faq.SelectionStart = 0;
    }

    static Label LabelFor(string text)
    {
        return new Label { Text = text, AutoSize = true, Padding = new Padding(3, 4, 3, 4) };
    }

    static RichTextBox Reader()
    {
        return new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None,
            BackColor = Background, ForeColor = Ink, DetectUrls = false };
    }

    static string Resource(string name)
    {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        using (var reader = new StreamReader(stream))
            return reader.ReadToEnd();
    }

    static Control Hardware()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true,
            Padding = new Padding(0, 3, 1, 0), Margin = Padding.Empty };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var target = new ComboBox { FlatStyle = FlatStyle.Flat, DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        target.Items.AddRange(new object[] { "1080p Low", "Xbox Series S", "1080p High", "Xbox Series X",
            "1440p High", "4K Ultra" });
        target.SelectedIndex = 0;
        var gpu = Picker("gpu", true);
        var cpu = Picker("cpu", true);
        var ram = new NumericUpDown { Minimum = 2, Maximum = 1024, Value = 16, Width = 100 };
        var ramType = Picker("ram-type", false);
        var storage = new ComboBox { FlatStyle = FlatStyle.Flat, DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill };
        storage.Items.AddRange(new object[] { "HDD", "SSD", "NVMe SSD" });
        var drives = new ComboBox { FlatStyle = FlatStyle.Flat, DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill, DropDownWidth = 340 };
        drives.Items.Add("Enter details manually");
        drives.SelectedIndex = 0;
        var detect = new Button { Text = "Detect hardware", AutoSize = true, FlatStyle = FlatStyle.Flat,
            BackColor = Accent, ForeColor = Background, Padding = new Padding(9, 3, 9, 3),
            Margin = new Padding(3, 0, 3, 6), MinimumSize = new Size(150, 34) };
        detect.FlatAppearance.BorderSize = 0;
        RoundControl(detect, 12);
        var detectionStatus = LabelFor("Detect your PC hardware or enter the details below.");
        detectionStatus.ForeColor = Muted;
        detectionStatus.MaximumSize = new Size(340, 0);
        var setupTitle = LabelFor("PC setup");
        setupTitle.Font = new Font("Segoe UI Semibold", 17);
        setupTitle.Margin = new Padding(0, 0, 0, 11);
        AddWideRow(grid, setupTitle);
        AddWideRow(grid, detect);
        AddWideRow(grid, detectionStatus);
        AddRow(grid, "Target", target);
        AddRow(grid, "Graphics card", gpu);
        AddRow(grid, "Processor", cpu);
        AddRow(grid, "RAM (GB)", ram);
        AddRow(grid, "RAM type", ramType);
        var storageTitle = LabelFor("Storage");
        storageTitle.Font = StrongFont;
        storageTitle.Margin = new Padding(0, 12, 0, 5);
        AddWideRow(grid, storageTitle);
        AddRow(grid, "Drive", drives);
        var diskDetails = LabelFor("");
        diskDetails.ForeColor = Muted;
        diskDetails.MaximumSize = new Size(330, 0);
        int detailRow = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(diskDetails, 1, detailRow);

        var manualLabel = LabelFor("Drive type");
        int manualRow = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        storage.BackColor = Sidebar;
        storage.ForeColor = Ink;
        grid.Controls.Add(manualLabel, 0, manualRow);
        grid.Controls.Add(storage, 1, manualRow);

        var spaceLabel = LabelFor("Free space (GB)");
        var freeSpace = new NumericUpDown { Minimum = 0, Maximum = 1000000, Width = 115,
            BackColor = Sidebar, ForeColor = Ink };
        int spaceRow = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(spaceLabel, 0, spaceRow);
        grid.Controls.Add(freeSpace, 1, spaceRow);

        Action updateStorage = () =>
        {
            var selected = drives.SelectedItem as PcHardware.DriveProfile;
            bool manualType = selected == null || selected.Kind == DiskKind.Unknown;
            manualLabel.Visible = manualType;
            storage.Visible = manualType;
            spaceLabel.Visible = selected == null;
            freeSpace.Visible = selected == null;
            storage.SelectedIndex = selected == null ? -1 : (int)selected.Kind - 1;
            if (selected == null)
                diskDetails.Text = "Enter the free space in GB or click Detect hardware.";
            else
            {
                diskDetails.Text = selected.Media + " · " + selected.FreeSpace + " free";
                if (selected.Kind == DiskKind.Hdd)
                    diskDetails.Text += "\nThis estimate requires an SSD.";
                if (selected.FreeGb < 250)
                    diskDetails.Text += "\nNot enough free space. This estimate requires at least 250 GB.";
                if (selected.Kind == DiskKind.Unknown)
                    diskDetails.Text += "\nSelect this drive’s type manually.";
            }
        };
        drives.SelectedIndexChanged += (sender, args) => updateStorage();
        updateStorage();

        var results = Stack();
        var resultTitle = LabelFor("Compatibility estimate");
        resultTitle.Font = StrongFont;
        var score = LabelFor("—");
        score.Font = new Font("Segoe UI Semibold", 56);
        score.Margin = new Padding(0, 8, 0, 0);
        var result = LabelFor("");
        result.MaximumSize = new Size(250, 0);
        result.Font = StrongFont;
        result.Margin = new Padding(0, 0, 0, 19);
        var metrics = new[] { new MetricBar("GPU"), new MetricBar("CPU"),
            new MetricBar("RAM"), new MetricBar("Drive type"), new MetricBar("Free space") };
        var vramLabel = LabelFor("");
        vramLabel.ForeColor = Muted;
        vramLabel.Margin = new Padding(0, 12, 0, 0);
        results.Controls.Add(resultTitle);
        results.Controls.Add(score);
        results.Controls.Add(result);
        results.Controls.AddRange(metrics);
        results.Controls.Add(vramLabel);
        var comparison = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Margin = Padding.Empty };
        comparison.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        comparison.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 295));
        var controls = Card(grid, new Padding(19));
        controls.Margin = new Padding(0, 0, 14, 0);
        comparison.Controls.Add(controls, 0, 0);
        comparison.Controls.Add(Card(results, new Padding(19)), 1, 0);

        var requirements = new[,] { {44,60,16,6}, {38,50,16,6}, {60,65,16,8},
            {70,70,32,10}, {90,70,32,12}, {150,85,32,16} };
        Action calculate = () =>
        {
            var graphics = (Option)gpu.SelectedItem;
            var processor = (Option)cpu.SelectedItem;
            if (graphics == null || processor == null || storage.SelectedIndex < 0)
            {
                result.Text = "Select a CPU, graphics card and drive type to see your estimate.";
                result.ForeColor = Muted;
                score.Text = "—";
                score.ForeColor = Muted;
                foreach (var metric in metrics) metric.SetRatio(-1);
                vramLabel.Text = "";
                return;
            }
            int t = target.SelectedIndex;
            var values = graphics.Value.Split('|');
            int vram = int.Parse(values[1]);
            double graphicsRatio = double.Parse(values[0]) / requirements[t, 0];
            double cpuRatio = double.Parse(processor.Value.Split('|')[0]) / requirements[t, 1];
            double ramRatio = (double)ram.Value / requirements[t, 2];
            var selectedDrive = drives.SelectedItem as PcHardware.DriveProfile;
            var diskKind = (DiskKind)(storage.SelectedIndex + 1);
            double availableGb = selectedDrive == null ? (double)freeSpace.Value : selectedDrive.FreeBytes / 1000000000.0;
            double diskRatio = DiskRatio(diskKind);
            double spaceRatio = availableGb / 350;
            int requiredVram = requirements[t, 3];
            bool below = graphicsRatio < .8 || cpuRatio < .8 || ram.Value < 16 || diskKind == DiskKind.Hdd || availableGb < 250 ||
                vram < Math.Min(6, requiredVram) || vram <= requiredVram / 2.0;
            bool borderline = graphicsRatio < 1 || cpuRatio < 1 || ramRatio < 1 || spaceRatio < 1 ||
                vram < requiredVram;
            bool headroom = graphicsRatio >= 1.3 && ram.Value >= 32 && diskKind == DiskKind.Nvme;
            string verdict = below ? "Below minimum requirements" : borderline ? "Borderline" :
                headroom ? "Exceeds target" : "Meets target";
            result.ForeColor = below ? Color.FromArgb(255, 169, 171) :
                borderline ? Color.FromArgb(255, 208, 135) : Accent;
            double overall = new[] { graphicsRatio, cpuRatio, ramRatio, diskRatio, spaceRatio }.Min();
            score.Text = overall >= 2 ? "200%+" : Math.Round(overall * 100) + "%";
            score.ForeColor = result.ForeColor;
            result.Text = verdict;
            var ratios = new[] { graphicsRatio, cpuRatio, ramRatio, diskRatio, spaceRatio };
            for (int i = 0; i < metrics.Length; i++) metrics[i].SetRatio(ratios[i]);
            vramLabel.Text = String.Format("VRAM: {0} GB / {1} GB required", vram, requiredVram);
        };
        foreach (var field in new[] { target, gpu, cpu, ramType, storage })
            field.SelectedIndexChanged += (sender, args) => calculate();
        ram.ValueChanged += (sender, args) => calculate();
        freeSpace.ValueChanged += (sender, args) => calculate();
        drives.SelectedIndexChanged += (sender, args) => calculate();
        detect.Click += async (sender, args) =>
        {
            if (!RequestHardwareConsent(grid.FindForm())) return;
            detect.Enabled = false;
            detectionStatus.Text = "Detecting hardware…";
            try
            {
                var pc = await Task.Run(() => PcHardware.Read());
                if (grid.IsDisposed) return;
                gpu.SelectedIndex = BestMatch(gpu, pc.Graphics);
                cpu.SelectedIndex = BestMatch(cpu, pc.Processors);
                if (pc.MemoryGb > 0) ram.Value = Math.Max(ram.Minimum, Math.Min(ram.Maximum, pc.MemoryGb));
                ramType.SelectedIndex = pc.RamProfile;
                var previousDrive = drives.SelectedItem as PcHardware.DriveProfile;
                drives.BeginUpdate();
                try
                {
                    drives.Items.Clear();
                    drives.Items.Add("Enter details manually");
                    drives.Items.AddRange(pc.Drives);
                    string preferred = previousDrive == null ? pc.SystemDrive : previousDrive.Letter;
                    int index = Array.FindIndex(pc.Drives, item => item.Letter == preferred);
                    drives.SelectedIndex = index < 0 && pc.Drives.Length > 0 ? 1 : index + 1;
                }
                finally { drives.EndUpdate(); }
                if (pc.Drives.Length == 0) diskDetails.Text = "Windows did not report any available drives. Enter the details manually.";
                var summary = new System.Collections.Generic.List<string>();
                if (pc.Processors.Length > 0) summary.Add(String.Join(", ", pc.Processors));
                if (pc.Graphics.Length > 0) summary.Add(String.Join(", ", pc.Graphics));
                if (pc.MemoryGb > 0) summary.Add(pc.MemoryGb + " GB RAM");

                detectionStatus.Text = summary.Count > 0 ? String.Join(" · ", summary) :
                    "Windows did not report any hardware details.";
                if (gpu.SelectedIndex < 0 || cpu.SelectedIndex < 0 || storage.SelectedIndex < 0 ||
                    pc.MemoryGb == 0 || pc.RamProfile < 0)
                    detectionStatus.Text += "\nCheck and fill in any details Windows could not detect.";
                calculate();
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is InvalidOperationException ||
                    ex is System.Management.ManagementException || ex is UnauthorizedAccessException)) throw;
                if (!grid.IsDisposed) detectionStatus.Text = "Hardware details are unavailable. You can enter them manually.";
            }
            finally
            {
                if (!grid.IsDisposed) detect.Enabled = true;
            }
        };
        calculate();
        return comparison;
    }

    static bool RequestHardwareConsent(Form owner)
    {
        using (var dialog = new Form
        {
            Text = "Detect hardware", StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(430, 161), FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false,
            BackColor = Surface, ForeColor = Ink, Font = BodyFont
        })
        {
            var description = new Label { Text = "Allow Recomp Hub to read your CPU, graphics cards, RAM and free drive space? These details stay on this PC.",
                Location = new Point(20, 19), Size = new Size(390, 74) };
            var allow = new Button { Text = "Allow", DialogResult = DialogResult.OK, Location = new Point(221, 102),
                Size = new Size(88, 34), FlatStyle = FlatStyle.Flat, BackColor = Accent, ForeColor = Background };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(319, 102),
                Size = new Size(88, 34), FlatStyle = FlatStyle.Flat, BackColor = Sidebar, ForeColor = Ink };
            RoundControl(allow, 9);
            RoundControl(cancel, 9);
            dialog.Controls.AddRange(new Control[] { description, allow, cancel });
            dialog.AcceptButton = cancel;
            dialog.CancelButton = cancel;
            dialog.ActiveControl = cancel;
            return dialog.ShowDialog(owner) == DialogResult.OK;
        }
    }

    static double DiskRatio(DiskKind kind)
    {
        if (kind == DiskKind.Nvme) return 1.30;
        if (kind == DiskKind.Ssd) return 1.05;
        return 0.40;
    }

    static int BestMatch(ComboBox catalog, string[] hardwareNames)
    {
        int bestIndex = -1;
        int bestScore = -1;
        foreach (var device in hardwareNames)
        {
            string hardware = PcHardware.NormalizeModel(device);
            bool mobile = Regex.IsMatch(hardware, @"\b(?:laptop|mobile|max q)\b");
            var matches = catalog.Items.Cast<Option>().Select((option, index) =>
            {
                var title = Regex.Replace(option.Title, @"\([^)]*\)|\b\d+\s*gb\b", "",
                    RegexOptions.IgnoreCase);
                string model = PcHardware.NormalizeModel(title.Split('/')[0]);
                bool mobileOption = Regex.IsMatch(model, @"\b(?:laptop|mobile|max q)\b");
                bool found = model.Length > 0 && mobile == mobileOption &&
                    Regex.IsMatch(hardware, @"\b" + Regex.Escape(model) + @"\b");
                return new { Index = index, ModelLength = found ? model.Length : 0, Option = option };
            }).Where(item => item.ModelLength > 0).OrderByDescending(item => item.ModelLength).ToArray();
            if (matches.Length == 0 || (matches.Length > 1 &&
                matches[0].ModelLength == matches[1].ModelLength)) continue;
            int score = Int32.Parse(matches[0].Option.Value.Split('|')[0]);
            if (score <= bestScore) continue;
            bestScore = score;
            bestIndex = matches[0].Index;
        }
        return bestIndex;
    }

    static void AddWideRow(TableLayoutPanel grid, Control field)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(field, 0, row);
        grid.SetColumnSpan(field, 2);
    }

    static void AddRow(TableLayoutPanel grid, string title, Control field)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 41));
        field.BackColor = Sidebar;
        field.ForeColor = Ink;
        grid.Controls.Add(LabelFor(title), 0, row);
        grid.Controls.Add(field, 1, row);
    }

    static ComboBox Picker(string name, bool searchable)
    {
        var box = new ComboBox { FlatStyle = FlatStyle.Flat, DropDownStyle = searchable ? ComboBoxStyle.DropDown :
            ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        int selected = 0;
        foreach (var line in Resource(name).Split('\n'))
        {
            var fields = line.Split('\t');
            box.Items.Add(new Option { Title = fields[0], Value = fields[1] });
            if (fields[2].TrimEnd() == "1") selected = box.Items.Count - 1;
        }
        if (searchable)
        {
            box.AutoCompleteSource = AutoCompleteSource.ListItems;
            box.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            box.Validating += (sender, args) =>
            {
                int match = box.FindStringExact(box.Text);
                if (match >= 0) box.SelectedIndex = match;
            };
        }
        box.SelectedIndex = selected;
        return box;
    }

    async Task UpdateFeeds()
    {
        refresh.Stop();
        try { await Task.WhenAll(UpdateNews(), UpdateRelease()); }
        finally { if (!IsDisposed) refresh.Start(); }
    }

    async Task UpdateNews()
    {
        try
        {
            var json = await Client.GetStringAsync(SiteUrl + "/data/posts.json");
            var posts = new JavaScriptSerializer().Deserialize<Post[]>(json)
                .Where(post => !post.draft && !String.IsNullOrWhiteSpace(post.slug))
                .OrderByDescending(post => post.date, StringComparer.Ordinal).ToArray();
            if (IsDisposed) return;
            var selected = news.SelectedItem as Post;
            int nextSelection = selected == null ? -1 : Array.FindIndex(posts, post => post.slug == selected.slug);
            updatingNews = true;
            news.BeginUpdate();
            try
            {
                int top = news.TopIndex;
                news.Items.Clear();
                news.Items.AddRange(posts);
                if (posts.Length > 0)
                {
                    news.SelectedIndex = Math.Max(0, nextSelection);
                    if (nextSelection >= 0) news.TopIndex = Math.Min(top, posts.Length - 1);
                }
            }
            finally
            {
                news.EndUpdate();
                updatingNews = false;
            }
            if (nextSelection < 0) await ReadPost();
            if (IsDisposed) return;
            newsStatus.Text = "gta6recomp.com  ·  Updated at " + DateTime.Now.ToShortTimeString() +
                "  ·  Refreshes every 5 minutes";
        }
        catch (Exception ex)
        {
            if (!(ex is HttpRequestException || ex is TaskCanceledException || ex is ArgumentException || ex is InvalidOperationException)) throw;
            if (!IsDisposed) newsStatus.Text = "Unable to refresh. Retrying in five minutes.";
        }
    }

    async Task UpdateRelease()
    {
        try
        {
            var page = await Client.GetStringAsync(SiteUrl + "/download");
            var match = Regex.Match(page, @"<h2\b[^>]*>(.*?)</h2>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!match.Success) throw new InvalidOperationException();
            string current = WebUtility.HtmlDecode(Regex.Replace(match.Groups[1].Value, "<[^>]+>", "")).Trim();
            if (IsDisposed) return;
            releaseStatus.Text = current;
            bool changed = lastRelease != null && current != lastRelease;
            lastRelease = current;
            if (alert.Checked && changed) MessageBox.Show(this, current, "Release status changed");
        }
        catch (Exception ex)
        {
            if (!(ex is HttpRequestException || ex is TaskCanceledException || ex is InvalidOperationException)) throw;
            if (!IsDisposed) releaseStatus.Text = "Release status is unavailable.";
        }
    }

    async Task ReadPost()
    {
        var post = news.SelectedItem as Post;
        int request = ++articleRequest;
        if (post == null)
        {
            article.Clear();
            return;
        }
        article.Text = post.title + "\n\n" + post.excerpt;
        try
        {
            var markdown = await Client.GetStringAsync(SiteUrl + "/posts/" + Uri.EscapeDataString(post.slug) + ".md");
            if (!IsDisposed && request == articleRequest)
                {
                    article.Text = "";
                    article.SelectionFont = ArticleTitleFont;
                    article.SelectionColor = Muted;
                    article.SelectionFont = SmallFont;
                    article.AppendText(FormatPostDate(post) + "\n\n");
                    article.SelectionColor = Ink;
                    article.SelectionFont = ArticleTitleFont;
                    article.AppendText(post.title + "\n\n");
                    foreach (var line in markdown.Split('\n'))
                    {
                        var current = line.TrimEnd();
                        if (current == "---") continue;
                        bool heading = current.StartsWith("#");
                        article.SelectionFont = heading ? StrongFont : ArticleBodyFont;
                        article.SelectionColor = heading ? Accent : Ink;
                        current = Regex.Replace(current, @"^#+\s+", "");
                        current = Regex.Replace(current, @"!?\[([^\]]+)\]\([^)]+\)", "$1");
                        article.AppendText(current.Replace("**", "") + "\n");
                    }
                    article.SelectionStart = 0;
                    article.ScrollToCaret();
                }
        }
        catch (Exception ex)
        {
            if (!(ex is HttpRequestException || ex is TaskCanceledException)) throw;
            if (!IsDisposed && request == articleRequest) article.AppendText("\n\nFull article unavailable.");
        }
    }

    sealed class MetricBar : Control
    {
        readonly string metric;
        double ratio = -1;

        internal MetricBar(string name)
        {
            metric = name;
            Size = new Size(238, 43);
            Margin = new Padding(3, 0, 0, 3);
            DoubleBuffered = true;
            BackColor = Surface;
        }

        internal void SetRatio(double value)
        {
            ratio = value;
            AccessibleName = ratio < 0 ? metric : metric + " " + ratio.ToString("P0");
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);
            TextRenderer.DrawText(args.Graphics, metric, SmallFont, new Point(0, 0), Ink);
            string label = ratio < 0 ? "—" : Math.Round(ratio * 100) + "%";
            TextRenderer.DrawText(args.Graphics, label, SmallFont, new Rectangle(0, 0, Width, 24), Muted,
                TextFormatFlags.Right | TextFormatFlags.NoPadding);
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var track = new SolidBrush(Border))
            using (var shape = Rounded(new Rectangle(0, 24, Width, 8), 4))
                args.Graphics.FillPath(track, shape);
            if (ratio > 0)
                using (var fill = new SolidBrush(ratio >= 1 ? Accent : ratio >= 0.8 ?
                    Color.FromArgb(255, 195, 131) : Color.FromArgb(250, 143, 168)))
                using (var shape = Rounded(new Rectangle(0, 24, Math.Max(1, (int)(Width * Math.Min(ratio, 1))), 8), 4))
                    args.Graphics.FillPath(fill, shape);
        }
    }

    sealed class Option
    {
        public string Title;
        public string Value;
        public override string ToString() { return Title; }
    }

    public sealed class Post
    {
        public string slug { get; set; }
        public string title { get; set; }
        public string excerpt { get; set; }
        public string date { get; set; }
        public bool draft { get; set; }
        public override string ToString() { return title ?? slug; }
    }
}
