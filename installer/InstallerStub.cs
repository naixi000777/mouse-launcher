// InstallerStub.cs - 老鼠启动器安装向导 (自解压: exe 尾部附加 payload.zip)
// 用法: 直接运行=向导界面;  -auto <目录> [ns] = 静默安装 (ns=不建快捷方式)
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace MouseLauncherInstaller
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "-auto")
            {
                string dir = args[1];
                bool noShortcut = args.Contains("ns");
                try
                {
                    InstallTo(dir, null, null);
                    if (!noShortcut) CreateShortcut(dir);
                    Console.WriteLine("OK installed to " + dir);
                }
                catch (Exception ex) { Console.WriteLine("FAIL: " + ex.Message); Environment.Exit(2); }
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallForm());
        }

        public static int TotalFiles;
        public static void InstallTo(string dir, ProgressBar bar, Label status)
        {
            using (Stream rs = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
            {
                if (rs == null) throw new Exception("安装包数据缺失 (payload.zip)");
                MemoryStream ms = new MemoryStream();
                rs.CopyTo(ms);
                ms.Position = 0;
                using (ZipArchive zip = new ZipArchive(ms, ZipArchiveMode.Read))
                {
                var entries = zip.Entries.Where(e => e.Name.Length > 0).ToList();
                TotalFiles = entries.Count;
                Directory.CreateDirectory(dir);
                int done = 0;
                foreach (ZipArchiveEntry e in entries)
                {
                    string dest = Path.Combine(dir, e.Name);
                    if (File.Exists(dest))
                    {
                        try { File.Delete(dest); } catch { }
                    }
                    e.ExtractToFile(dest, true);
                    done++;
                    if (bar != null)
                    {
                        int pct = 100 * done / entries.Count;
                        if (bar.InvokeRequired) bar.BeginInvoke((MethodInvoker)delegate { bar.Value = pct; });
                        else bar.Value = pct;
                    }
                    if (status != null)
                    {
                        string s = "正在安装: " + e.Name;
                        if (status.InvokeRequired) status.BeginInvoke((MethodInvoker)delegate { status.Text = s; });
                        else status.Text = s;
                    }
                }
                }
            }
        }

        public static void CreateShortcut(string dir)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            Type shellT = Type.GetTypeFromProgID("WScript.Shell");
            if (shellT == null) return;
            object shell = Activator.CreateInstance(shellT);
            object sc = shellT.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell,
                new object[] { Path.Combine(desktop, "老鼠启动器.lnk") });
            Type scT = sc.GetType();
            string exe = Path.Combine(dir, "老鼠启动器.exe");
            scT.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { exe });
            scT.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { dir });
            scT.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { exe + ",0" });
            scT.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { "老鼠启动器 by Naixi" });
            scT.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
        }
    }

    class InstallForm : Form
    {
        TextBox dirBox;
        Button browseBtn, installBtn;
        CheckBox runChk;
        ProgressBar bar;
        Label statusLab;

        public InstallForm()
        {
            Text = "老鼠启动器 安装向导";
            Width = 520; Height = 300;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Font = new Font("Microsoft YaHei UI", 9.5F);
            BackColor = Color.FromArgb(38, 38, 46);
            ForeColor = Color.FromArgb(235, 235, 240);
            try { Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); } catch { }

            Image logo = null;
            try
            {
                using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream("icon-preview.png"))
                {
                    if (st != null) { MemoryStream ms = new MemoryStream(); st.CopyTo(ms); logo = Image.FromStream(ms); }
                }
            }
            catch { }
            int titleX = 20;
            if (logo != null)
            {
                PictureBox logoBox = new PictureBox();
                logoBox.Image = logo;
                logoBox.SetBounds(20, 14, 32, 32);
                logoBox.SizeMode = PictureBoxSizeMode.Zoom;
                Controls.Add(logoBox);
                titleX = 60;
            }
            Label title = new Label();
            title.Text = "老鼠启动器 by Naixi";
            title.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(86, 156, 255);
            title.SetBounds(titleX, 16, 440, 30);

            Label l1 = new Label();
            l1.Text = "安装到:";
            l1.SetBounds(22, 62, 55, 22);
            dirBox = new TextBox();
            dirBox.SetBounds(80, 59, 330, 26);
            dirBox.BackColor = Color.FromArgb(62, 62, 74);
            dirBox.ForeColor = Color.FromArgb(235, 235, 240);
            dirBox.BorderStyle = BorderStyle.FixedSingle;
            // 不预设安装目录: 由用户自己选择(点「浏览...」或在框内直接输入)
            dirBox.Text = "";
            browseBtn = new Button();
            browseBtn.Text = "浏览...";
            browseBtn.SetBounds(418, 58, 66, 28);
            browseBtn.BackColor = Color.FromArgb(62, 62, 74);
            browseBtn.ForeColor = Color.FromArgb(235, 235, 240);
            browseBtn.FlatStyle = FlatStyle.Flat;
            browseBtn.Click += delegate
            {
                using (FolderBrowserDialog d = new FolderBrowserDialog())
                {
                    d.Description = "选择 老鼠启动器 的安装目录";
                    d.ShowNewFolderButton = true;
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        dirBox.Text = d.SelectedPath;
                        statusLab.Text = "将安装到: " + d.SelectedPath;
                    }
                }
            };

            runChk = new CheckBox();
            runChk.Text = "安装完成后运行 老鼠启动器";
            runChk.Checked = true;
            runChk.SetBounds(22, 100, 300, 24);
            runChk.ForeColor = Color.FromArgb(235, 235, 240);

            bar = new ProgressBar();
            bar.SetBounds(22, 140, 460, 22);

            statusLab = new Label();
            statusLab.Text = "请先选择安装目录 (点「浏览...」或直接输入路径)";
            statusLab.ForeColor = Color.FromArgb(150, 150, 160);
            statusLab.SetBounds(22, 168, 460, 20);

            installBtn = new Button();
            installBtn.Text = "安 装";
            installBtn.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            installBtn.SetBounds(180, 205, 150, 40);
            installBtn.BackColor = Color.FromArgb(67, 181, 106);
            installBtn.ForeColor = Color.White;
            installBtn.FlatStyle = FlatStyle.Flat;
            installBtn.Click += delegate { DoInstall(); };

            Label foot = new Label();
            foot.Text = "绿色软件: 卸载 = 删除安装目录即可 · 数据与 PCL 共用 .minecraft";
            foot.ForeColor = Color.FromArgb(120, 120, 130);
            foot.Font = new Font("Microsoft YaHei UI", 8F);
            foot.SetBounds(22, 252, 460, 20);

            Controls.AddRange(new Control[] { title, l1, dirBox, browseBtn, runChk, bar, statusLab, installBtn, foot });
        }

        void DoInstall()
        {
            string dir = dirBox.Text.Trim();
            if (dir.Length == 0)
            {
                MessageBox.Show(this, "请先选择安装目录 (点「浏览...」或直接输入路径)", "安装向导",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                browseBtn.PerformClick();
                return;
            }
            if (dir.EndsWith(":\\") || dir.EndsWith(":/"))
            {
                MessageBox.Show(this, "不要把启动器直接装到盘符根目录, 请选择一个文件夹", "安装向导",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            dir = dir.TrimEnd('\\');
            installBtn.Enabled = false; browseBtn.Enabled = false; dirBox.Enabled = false;
            bool run = runChk.Checked;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Program.InstallTo(dir, bar, statusLab);
                    Program.CreateShortcut(dir);
                    SetStatus("安装完成 ✓ 共 " + Program.TotalFiles + " 个文件");
                    if (run)
                    {
                        try { Process.Start(Path.Combine(dir, "老鼠启动器.exe")); } catch { }
                    }
                    BeginInvoke((MethodInvoker)delegate
                    {
                        MessageBox.Show(this, "老鼠启动器 安装完成!\n\n目录: " + dir, "安装向导", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Close();
                    });
                }
                catch (Exception ex)
                {
                    SetStatus("安装失败: " + ex.Message);
                    BeginInvoke((MethodInvoker)delegate { installBtn.Enabled = true; browseBtn.Enabled = true; dirBox.Enabled = true; });
                }
            });
        }

        void SetStatus(string s)
        {
            if (statusLab.InvokeRequired) statusLab.BeginInvoke((MethodInvoker)delegate { statusLab.Text = s; });
            else statusLab.Text = s;
        }
    }
}
