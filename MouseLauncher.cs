// ============================================================
//  老鼠启动器 MouseLauncher v1.0
//  原版 Minecraft 下载 + 启动器 (C# 5 / WinForms / .NET Framework 4.x)
//  与 PCL2 共用同一个 .minecraft 目录
// ============================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using WinTimer = System.Windows.Forms.Timer;

namespace MouseLauncher
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
#if CLI
            if (args.Length < 1)
            {
                Console.WriteLine("用法: mlc -download <版本号|latest> [游戏目录]");
                Console.WriteLine("      mlc -launch <版本号> [玩家名] [内存MB] [javaw路径]");
                Console.WriteLine("      mlc -runtime <组件名>   如 java-runtime-gamma (自动下载对应 Java)");
                Console.WriteLine("      mlc -fabric <MC版本> [fabric版本]  安装 Fabric 版本");
                Console.WriteLine("      mlc -stun               测试公网端点发现");
                Console.WriteLine("      mlc -mp-test [丢包%]    联机传输回环自测");
                Console.WriteLine("      mlc -backup / -crashtest");
                return;
            }
            string cmd = args[0].TrimStart('-').ToLowerInvariant();
            if (args.Length < 2 && cmd != "stun" && cmd != "mp-test" && cmd != "backup" && cmd != "crashtest")
            {
                Console.WriteLine("缺少参数, 见 mlc 无参数说明");
                return;
            }
            if (cmd == "download")
            {
                string want = args[1];
                string mcDir = args.Length > 2 ? args[2] : Settings.McDir;
                VanillaDownloader dl = new VanillaDownloader(mcDir, null,
                    delegate(int pct) { Console.Write("\r进度: {0}%   ", pct); },
                    delegate(string s) { Console.WriteLine(s); });
                Console.WriteLine("获取版本清单...");
                JObject manifest = JObject.Parse(dl.GetManifestJson());
                if (want == "latest") want = (string)manifest["latest"]["release"];
                JToken vt = null;
                foreach (JObject v in (JArray)manifest["versions"]) if ((string)v["id"] == want) { vt = v; break; }
                if (vt == null) { Console.WriteLine("找不到版本: " + want); Environment.Exit(2); }
                Console.WriteLine("下载原版 " + want + " (" + (string)vt["type"] + ")");
                dl.Download(want, (string)vt["url"]);
                Console.WriteLine();
                Console.WriteLine("OK: " + want + " 已可启动");
                Environment.Exit(0);
            }
            if (cmd == "args")
            {
                GameLauncher.PrintArgsOnly = true;
                List<string> f2 = JavaFinder.Find();
                GameLauncher gl2 = new GameLauncher(Settings.McDir, f2.Count > 0 ? f2[0] : "javaw.exe", "naixi", 2048, null);
                gl2.Launch(args[1]);
                Environment.Exit(0);
            }
            if (cmd == "launch")
            {
                string vid = args[1];
                string name = args.Length > 2 ? args[2] : "naixi";
                int mem = args.Length > 3 ? int.Parse(args[3]) : 2048;
                string java = args.Length > 4 ? args[4] : null;
                if (java == null)
                {
                    // 按版本要求: 精确组件没装就自动下载 (老版本用新 Java 会崩, 不凑合)
                    string comp; int need;
                    JavaRuntime.Requirement(Path.Combine(Settings.McDir, "versions", vid, vid + ".json"), out comp, out need);
                    string exact = Path.Combine(JavaRuntime.RuntimeRoot(), comp, "bin", "javaw.exe");
                    if (File.Exists(exact)) java = exact;
                    else
                    {
                        Console.WriteLine("该版本需要 Java " + need + " (" + comp + "), 自动下载运行时...");
                        try { java = JavaRuntime.Ensure(comp, delegate(int pct, string s) { Console.Write("\rJava: {0}% {1}   ", pct, s); }); }
                        catch (Exception jex)
                        {
                            Console.WriteLine("自动下载失败(" + jex.Message + "), 改用已装 Java");
                            List<string> found = JavaFinder.Find();
                            if (found.Count == 0) { Console.WriteLine("未找到 Java"); Environment.Exit(3); }
                            java = found[0];
                        }
                        Console.WriteLine();
                    }
                }
                Console.WriteLine("Java: " + java);
                GameLauncher gl = new GameLauncher(Settings.McDir, java, name, mem, null);
                Process gameProc = gl.Launch(vid);
                Console.WriteLine("已启动, 等待游戏退出...");
                try { gameProc.WaitForExit(); } catch { }
                try
                {
                    Console.WriteLine("游戏已退出, 代码 " + gameProc.ExitCode);
                    if (gameProc.ExitCode != 0)
                    {
                        string why = Util.CrashReason(Settings.McDir);
                        if (why != null) Console.WriteLine("可能原因: " + why.Replace("\n", "  |  "));
                    }
                }
                catch { }
                Environment.Exit(0);
            }
            if (cmd == "fabric")
            {
                string mcVer = args[1];
                string fVer = args.Length > 2 ? args[2] : null;
                if (fVer == null)
                {
                    JArray ls = JArray.Parse(Web.GetText("https://meta.fabricmc.net/v2/versions/loader"));
                    foreach (JObject l in ls) if ((bool)l["stable"]) { fVer = (string)l["version"]; break; }
                    if (fVer == null && ls.Count > 0) fVer = (string)((JObject)ls[0])["version"];
                }
                Console.WriteLine("安装 Fabric " + fVer + " @ MC " + mcVer);
                ModInstaller mi = new ModInstaller(Settings.McDir, null,
                    delegate(int pct) { Console.Write("\r{0}%  ", pct); },
                    delegate(string s) { Console.WriteLine(s); });
                string v = mi.InstallFabric(mcVer, fVer);
                Console.WriteLine();
                Console.WriteLine("OK: " + v);
                Environment.Exit(0);
            }
            if (cmd == "backup")
            {
                string z = Util.BackupSaves(Settings.McDir, Path.Combine(Settings.McDir, "backups"));
                Console.WriteLine(z == null ? "无存档可备份" : "OK: " + z);
                Environment.Exit(0);
            }
            if (cmd == "crashtest")
            {
                string crDir = Path.Combine(Settings.McDir, "crash-reports");
                Directory.CreateDirectory(crDir);
                string fake = Path.Combine(crDir, "crash-" + DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss") + "-client.txt");
                File.WriteAllText(fake, "---- Minecraft Crash Report ----\n// 内存不够啦\n\nDescription: Ticking entity\n\njava.lang.OutOfMemoryError: Java heap space\n\tat net.minecraft.server.level.ServerLevel.tick(ServerLevel.java:512)\n", Encoding.UTF8);
                string old = Path.Combine(crDir, "crash-2000-01-01_00.00.00-client.txt");
                File.WriteAllText(old, "---- Minecraft Crash Report ----\n// 老报告\nDescription: Rendering entity\njava.lang.NullPointerException: old\n", Encoding.UTF8);
                File.SetLastWriteTime(old, DateTime.Now.AddHours(-2));
                GameLauncher.LastLaunchTime = DateTime.Now.AddMinutes(-1);
                string r1 = Util.CrashReason(Settings.McDir) ?? "null";
                Console.WriteLine("1) 应命中新报告(内存不足): " + r1.Replace("\n", "  |  "));
                GameLauncher.LastLaunchTime = DateTime.Now.AddMinutes(1); // 未来: 一律不算
                string r2 = Util.CrashReason(Settings.McDir) ?? "null";
                Console.WriteLine("2) 无本次新报告应为 null: " + r2.Replace("\n", "  |  "));
                File.Delete(fake); File.Delete(old);
                bool pass = r1.Contains("内存") && r2 == "null";
                Console.WriteLine(pass ? "CRASHTEST OK" : "CRASHTEST FAIL");
                Environment.Exit(pass ? 0 : 1);
            }
            if (cmd == "runtime")
            {
                string j = JavaRuntime.Ensure(args[1], delegate(int pct, string s) { Console.Write("\rJava: {0}% {1}   ", pct, s); });
                Console.WriteLine();
                Console.WriteLine("OK: " + j);
                Environment.Exit(0);
            }
            if (cmd == "stun")
            {
                string[] servers = new string[] { "stun.miwifi.com", "stun.qq.com", "stun.chat.bilibili.com", "stun.l.google.com" };
                foreach (string s in servers)
                {
                    using (UdpClient u = new UdpClient(0))
                    {
                        IPEndPoint ep = Stun.Query(u, s, 3478, 3000);
                        Console.WriteLine((ep == null ? "FAIL " : "OK   ") + s + " -> " + (ep == null ? "-" : ep.ToString()));
                    }
                }
                Environment.Exit(0);
            }
            if (cmd == "mp-test")
            {
                int dropPct = args.Length > 1 ? int.Parse(args[1]) : 0;
                UdpSession.SimDropPct = dropPct;
                Console.WriteLine("== 联机传输自测 (模拟丢包 " + dropPct + "%) ==");
                // 1. 本地 echo TCP 服务
                TcpListener echo = new TcpListener(IPAddress.Loopback, 0);
                echo.Start();
                int echoPort = ((IPEndPoint)echo.LocalEndpoint).Port;
                Thread esrv = new Thread(delegate()
                {
                    try
                    {
                        while (true)
                        {
                            TcpClient c = echo.AcceptTcpClient();
                            Thread t = new Thread(delegate()
                            {
                                try
                                {
                                    NetworkStream s = c.GetStream();
                                    byte[] b = new byte[8192]; int n;
                                    while ((n = s.Read(b, 0, b.Length)) > 0) s.Write(b, 0, n);
                                }
                                catch { }
                            });
                            t.IsBackground = true; t.Start();
                        }
                    }
                    catch { }
                });
                esrv.IsBackground = true; esrv.Start();
                // 2. 房主 (打洞端点替换为回环)
                MpHost host = new MpHost();
                host.Create(echoPort, "测试房主", delegate(string s) { });
                JObject o = JObject.Parse(MpHost.Base64UrlDecode(host.InviteCode));
                o["ep"] = "127.0.0.1:" + host.LocalPort;
                string code = MpHost.Base64Url(o.ToString(Newtonsoft.Json.Formatting.None));
                Console.WriteLine("邀请码(回环): " + code);
                // 3. 加入方
                MpJoin join = new MpJoin();
                int readyPort = 0;
                join.Join(code, delegate(string s) { Console.WriteLine("[join] " + s); }, delegate(int p) { readyPort = p; });
                int waited = 0;
                while (readyPort == 0 && waited < 20000) { Thread.Sleep(100); waited += 100; }
                if (readyPort == 0) { Console.WriteLine("FAIL: 打洞/握手失败"); Environment.Exit(1); }
                Console.WriteLine("本地入口: 127.0.0.1:" + readyPort);
                // 4. 走完整链路收发 400KB (带进度/诊断/超时)
                TcpClient cli = new TcpClient();
                cli.Connect(IPAddress.Loopback, readyPort);
                NetworkStream ns = cli.GetStream();
                byte[] payload = new byte[400 * 1024];
                new Random(42).NextBytes(payload);
                DateTime t0 = DateTime.Now;
                byte[] back = new byte[payload.Length];
                int got = 0;
                UdpSession jSess = join.SessionRef;
                Thread killer = new Thread(delegate()
                {
                    Thread.Sleep(45000);
                    Console.WriteLine("TIMEOUT: 传输未完成, got=" + got + "/" + payload.Length + "  join会话: " + (jSess == null ? "-" : jSess.Diag()));
                    Console.WriteLine("  host会话: " + (host.LastSession == null ? "-" : (host.LastSession.Diag() + " closed=" + host.LastSession.Closed)));
                    Console.WriteLine("  host计数: hello=" + host.CntHello + " data=" + host.CntData + " 未知来源=" + host.CntUnknownFrom + " (" + host.LastUnknownFrom + ")  peerKey=" + host.PeerKey);
                    string histo = "";
                    for (int i = 0; i < 16; i++) if (host.Histo[i] > 0) histo += "[" + i + "]x" + host.Histo[i] + " ";
                    Console.WriteLine("  host首字节: " + histo);
                    Console.WriteLine("MP-TEST FAIL");
                    Environment.Exit(2);
                });
                killer.IsBackground = true; killer.Start();
                ns.Write(payload, 0, payload.Length);
                ns.Flush();
                while (got < back.Length)
                {
                    int n = ns.Read(back, got, back.Length - got);
                    if (n <= 0) break;
                    got += n;
                    if (got % (64 * 1024) < n)
                        Console.WriteLine("  进度 " + got / 1024 + "KB / " + ((DateTime.Now - t0).TotalSeconds).ToString("F1") + "s  join会话: " + (jSess == null ? "-" : jSess.Diag()));
                }
                killer.Abort();
                double secs = (DateTime.Now - t0).TotalSeconds;
                bool same = got == payload.Length;
                if (same) for (int i = 0; i < payload.Length; i++) if (payload[i] != back[i]) { same = false; break; }
                Console.WriteLine("回传 " + got + "/" + payload.Length + " 字节, 耗时 " + secs.ToString("F1") + "s (" + (400 / secs).ToString("F0") + " KB/s), 数据一致: " + (same ? "是" : "否"));
                Console.WriteLine(same && got == payload.Length ? "MP-TEST OK" : "MP-TEST FAIL");
                Environment.Exit(same && got == payload.Length ? 0 : 1);
            }
            Console.WriteLine("未知命令: " + cmd);
#else
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainForm form = new MainForm();
            if (args.Length > 0 && args[0] == "-selftest")
            {
                form.Shown += delegate { form.SelfTest(); };
            }
            try { Application.Run(form); }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log"), ex.ToString(), Encoding.UTF8); } catch { }
                MessageBox.Show("启动器出错了: " + ex.Message + "\n\n多为网络波动(镜像限流)或权限问题, 重开一次通常就好。\n详情已写入 error.log。", "老鼠启动器");
            }
#endif
        }
    }

    // ---------- 设置 ----------
    static class Settings
    {
        public static string McDir = DetectMcDir();

        // 游戏目录智能探测: 首次运行 (无 ini) 时按常见位置找, 不再写死开发机路径
        public static string DetectMcDir()
        {
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string[] candidates = new string[] {
                Path.Combine(roaming, ".minecraft"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".minecraft"),
                "D:\\pcl2\\.minecraft", "C:\\pcl2\\.minecraft", "E:\\pcl2\\.minecraft",
                "D:\\.minecraft", "C:\\.minecraft"
            };
            foreach (string c in candidates)
            {
                try { if (Directory.Exists(Path.Combine(c, "versions"))) return c; } catch { }
            }
            return Path.Combine(roaming, ".minecraft"); // 兜底: 下载时会自动创建
        }
        public static string PlayerName = "naixi";
        public static int MemoryMB = 2048;
        public static string WinPos = "";
        public static bool AutoBackup = true;
        static string Path0()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "老鼠启动器.ini");
        }
        public static void Load()
        {
            try
            {
                foreach (string line in File.ReadAllLines(Path0(), Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                    if (k == "Name") PlayerName = v;
                    else if (k == "MemoryMB") { int t; if (int.TryParse(v, out t)) MemoryMB = t; }
                    else if (k == "McDir") McDir = v;
                    else if (k == "AutoBackup") AutoBackup = v == "1" || v.ToLowerInvariant() == "true";
                    else if (k == "WinPos") WinPos = v;
                }
            }
            catch { }
        }
        public static void Save()
        {
            try
            {
                File.WriteAllLines(Path0(), new string[] {
                    "Name=" + PlayerName, "MemoryMB=" + MemoryMB, "McDir=" + McDir,
                    "AutoBackup=" + (AutoBackup ? "1" : "0"), "WinPos=" + WinPos
                }, Encoding.UTF8);
            }
            catch { }
        }
    }

    // ---------- 主题 ----------
    static class Theme
    {
        public static readonly Color[] Accents = {
            Color.FromArgb(86, 156, 255),   // 蓝
            Color.FromArgb(76, 209, 138),   // 绿
            Color.FromArgb(255, 118, 160),  // 粉
            Color.FromArgb(255, 165, 66),   // 橙
        };
        public static Color Accent = Accents[0];
        public static readonly Color Bg = Color.FromArgb(38, 38, 46);
        public static readonly Color Sidebar = Color.FromArgb(30, 30, 36);
        public static readonly Color Card = Color.FromArgb(50, 50, 60);
        public static readonly Color CardLight = Color.FromArgb(62, 62, 74);
        public static readonly Color Text = Color.FromArgb(235, 235, 240);
        public static readonly Color TextDim = Color.FromArgb(150, 150, 160);
        public static readonly Color Console = Color.FromArgb(24, 24, 29);
        public static readonly Color Green = Color.FromArgb(67, 181, 106);
    }

    // ---------- 通用 ----------
    static class Util
    {
        public static void Log(TextBox tb, string msg)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg;
            if (tb == null) { try { Console.WriteLine(line); } catch { } return; }
            if (tb.InvokeRequired)
            {
                try { tb.BeginInvoke((MethodInvoker)delegate { Append(tb, line); }); } catch { }
            }
            else Append(tb, line);
        }
        static void Append(TextBox tb, string line)
        {
            if (tb.Text.Length > 80000) tb.Text = tb.Text.Substring(tb.Text.Length - 40000);
            tb.AppendText(line + Environment.NewLine);
        }
        public static string OfflineUuid(string name)
        {
            byte[] md5 = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" + name));
            md5[6] = (byte)((md5[6] & 0x0F) | 0x30);
            md5[8] = (byte)((md5[8] & 0x3F) | 0x80);
            return BitConverter.ToString(md5).Replace("-", "").ToLowerInvariant();
        }
        public static GraphicsPath Round(RectangleF r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // 启动前备份存档: gameDir\saves -> bakRoot\saves-时间.zip, 保留最近 5 份
        public static string BackupSaves(string gameDir, string bakRoot)
        {
            string saves = Path.Combine(gameDir, "saves");
            if (!Directory.Exists(saves)) return null;
            if (Directory.GetFileSystemEntries(saves).Length == 0) return null;
            string bakDir = bakRoot;
            Directory.CreateDirectory(bakDir);
            string zip = Path.Combine(bakDir, "saves-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip");
            System.IO.Compression.ZipFile.CreateFromDirectory(saves, zip, System.IO.Compression.CompressionLevel.Fastest, false);
            string[] olds = Directory.GetFiles(bakDir, "saves-*.zip");
            if (olds.Length > 5)
            {
                Array.Sort(olds);
                for (int i = 0; i < olds.Length - 5; i++) { try { File.Delete(olds[i]); } catch { } }
            }
            return zip;
        }

        // 游戏异常退出时从本次启动后写出的崩溃报告提取人话原因; 无新报告返回 null
        public static string CrashReason(string mcDir)
        {
            try
            {
                DateTime after = GameLauncher.LastLaunchTime == DateTime.MinValue ? DateTime.Now.AddDays(-1) : GameLauncher.LastLaunchTime;
                // 崩溃报告可能写在工作目录(版本隔离时在版本目录), 两处都查
                List<string> crDirs = new List<string>();
                crDirs.Add(Path.Combine(mcDir, "crash-reports"));
                if (GameLauncher.LastWorkDir != null && !string.Equals(GameLauncher.LastWorkDir, mcDir, StringComparison.OrdinalIgnoreCase))
                    crDirs.Add(Path.Combine(GameLauncher.LastWorkDir, "crash-reports"));
                FileInfo newest = null;
                foreach (string crDir in crDirs)
                {
                    if (!Directory.Exists(crDir)) continue;
                    foreach (string f in Directory.GetFiles(crDir, "crash-*.txt"))
                    {
                        FileInfo fi = new FileInfo(f);
                        if (fi.LastWriteTime < after) continue; // 旧报告不算
                        if (newest == null || fi.LastWriteTime > newest.LastWriteTime) newest = fi;
                    }
                }
                if (newest == null) return null;
                string[] lines = File.ReadAllLines(newest.FullName, Encoding.UTF8);
                string desc = null, exc = null;
                for (int i = 0; i < Math.Min(lines.Length, 80); i++)
                {
                    string l = lines[i].Trim();
                    if (desc == null && l.StartsWith("// ", StringComparison.Ordinal)) desc = l.Substring(3).Trim();
                    if (exc == null && l.StartsWith("Description: ", StringComparison.Ordinal)) desc = l.Substring(13).Trim();
                    if (exc == null && (l.StartsWith("java.lang.", StringComparison.Ordinal) || l.StartsWith("net.fabricmc", StringComparison.Ordinal) || l.StartsWith("Caused by: ", StringComparison.Ordinal)))
                        exc = l;
                    if (desc != null && exc != null) break;
                }
                string reason = desc != null ? desc : "";
                if (exc != null)
                {
                    string e = exc;
                    if (e.Contains("OutOfMemoryError")) reason = "内存不足 (Java 堆溢出), 建议在启动页调大内存";
                    else if (e.Contains("glGetString") || e.Contains("GL_VERSION") || e.Contains("OpenGL")) reason = "显卡/OpenGL 问题, 请更新显卡驱动";
                    else if (e.Contains("NullPointerException")) reason = "程序空指针 (多为 mod 冲突或版本不匹配)";
                    else if (e.Contains("ArrayIndexOutOfBounds") || e.Contains("IndexOutOfBounds")) reason = "数组越界 (多为 mod 与游戏版本不匹配)";
                    else if (e.Contains("FileNotFoundException") || e.Contains("NoSuchFileException")) reason = "缺少文件 (版本文件损坏或 mod 依赖缺失)";
                    else reason = e.Length > 90 ? e.Substring(0, 90) : e;
                }
                if (reason.Length == 0) return null;
                return reason + "\n崩溃报告: " + newest.FullName;
            }
            catch { return null; }
        }
    }

    static class Web
    {
        static string[] official = { "https://launchermeta.mojang.com", "https://piston-meta.mojang.com", "https://piston-data.mojang.com" };
        public const string Mirror = "https://bmclapi2.bangbang93.com";
        static WebClient Make()
        {
            WebClient wc = new WebClient();
            wc.Headers["User-Agent"] = "MouseLauncher/1.0";
            wc.Proxy = null;
            wc.Encoding = Encoding.UTF8;
            return wc;
        }
        static string Mirrorize(string url)
        {
            foreach (string h in official) if (url.StartsWith(h, StringComparison.Ordinal)) return Mirror + url.Substring(h.Length);
            if (url.StartsWith("https://resources.download.minecraft.net/", StringComparison.Ordinal))
                return Mirror + "/assets/" + url.Substring("https://resources.download.minecraft.net/".Length);
            return url;
        }
        public static string GetText(string url)
        {
            // 官方 -> 镜像 -> 镜像重试(503/抖动), 三连败才报错
            Exception last = null;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    if (i == 1) Thread.Sleep(600);
                    return Make().DownloadString(i == 0 ? url : Mirrorize(url));
                }
                catch (Exception ex) { last = ex; }
            }
            throw new Exception("网络请求失败(已自动重试): " + last.Message);
        }
        public static void DownloadFile(string url, string dest)
        {
            Exception last = null;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    if (i == 1) Thread.Sleep(600);
                    Make().DownloadFile(i == 0 ? url : Mirrorize(url), dest);
                    return;
                }
                catch (Exception ex) { last = ex; }
            }
            throw new Exception("下载失败(已自动重试): " + last.Message);
        }
        // 小文件批量场景 (Java 运行时 400+ 文件): 国内走 BMCLAPI 镜像优先, 官方兜底
        public static void DownloadFileMirrorFirst(string url, string dest)
        {
            string m = Mirrorize(url);
            if (m != url) { try { Make().DownloadFile(m, dest); return; } catch { } }
            Make().DownloadFile(url, dest);
        }
    }

    static class Rules
    {
        public static bool Allow(JToken rules)
        {
            if (rules == null) return true;
            bool ok = false;
            foreach (JObject r in rules)
            {
                string action = (string)r["action"];
                JToken os = r["os"];
                if (os != null)
                {
                    string osName = (string)os["name"];
                    if (osName != null && osName != "windows") continue;
                    string arch = (string)os["arch"];
                    if (arch != null && arch == "x86") continue;
                }
                // 功能开关参数(--demo/--width/--quickPlay*)只在启动器启用对应功能时生效, 本启动器一律不启用
                if (r["features"] != null) continue;
                if (action == "allow") ok = true; else if (action == "disallow") ok = false;
            }
            return ok;
        }
    }

    static class JavaFinder
    {
        public static List<string> Find()
        {
            List<string> found = new List<string>();
            try
            {
                string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string runtimeRoot = Path.Combine(roaming, ".minecraft", "runtime");
                if (Directory.Exists(runtimeRoot))
                    foreach (string rt in Directory.GetDirectories(runtimeRoot))
                    {
                        string j = Path.Combine(rt, "bin", "javaw.exe");
                        if (File.Exists(j)) found.Add(j);
                    }
            }
            catch { }
            foreach (string root in new string[] {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
            {
                try
                {
                    foreach (string pat in new string[] { "Java", "Eclipse Adoptium", "Microsoft", "Zulu", "BellSoft", "Amazon Corretto", "Eclipse Foundation" })
                    {
                        string d = Path.Combine(root, pat);
                        if (!Directory.Exists(d)) continue;
                        foreach (string sub in Directory.GetDirectories(d))
                        {
                            string j = Path.Combine(sub, "bin", "javaw.exe");
                            if (File.Exists(j)) found.Add(j);
                        }
                    }
                }
                catch { }
            }
            string jh = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(jh))
            {
                string j = Path.Combine(jh, "bin", "javaw.exe");
                if (File.Exists(j)) found.Add(j);
            }
            return found.Distinct().ToList();
        }
    }

    // Mojang 官方 Java 运行时: 按版本 JSON 的 javaVersion 组件自动下载对应 Java
    static class JavaRuntime
    {
        const string ManifestUrl = "https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";

        public static string RuntimeRoot()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "runtime");
        }

        // 读 javaw.exe 所属 JDK 的主版本号 (release 文件), 失败返回 -1
        public static int MajorOf(string javaw)
        {
            try
            {
                string home = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(javaw)));
                string rel = Path.Combine(home, "release");
                if (!File.Exists(rel)) return -1;
                foreach (string line in File.ReadAllLines(rel))
                    if (line.StartsWith("JAVA_VERSION=\"", StringComparison.Ordinal))
                    {
                        string v = line.Substring(14).TrimEnd('"');
                        string[] p = v.Split('.');
                        int a = int.Parse(p[0]);
                        return a == 1 ? int.Parse(p[1]) : a;
                    }
            }
            catch { }
            return -1;
        }

        // 解析版本 JSON 的 javaVersion; 老版本无该字段则按 jre-legacy / Java 8
        public static void Requirement(string versionJsonPath, out string component, out int major)
        {
            component = "jre-legacy"; major = 8;
            try
            {
                JObject vj = JObject.Parse(File.ReadAllText(versionJsonPath, Encoding.UTF8));
                JToken jv = vj["javaVersion"];
                if (jv != null)
                {
                    string c = (string)jv["component"];
                    if (!string.IsNullOrEmpty(c)) component = c;
                    if (jv["majorVersion"] != null) major = (int)jv["majorVersion"];
                }
            }
            catch { }
        }

        // 确保组件已安装到 %AppData%\.minecraft\runtime\<component>\, 返回 javaw.exe 路径
        public static string Ensure(string component, Action<int, string> progress)
        {
            string dir = Path.Combine(RuntimeRoot(), component);
            string javaw = Path.Combine(dir, "bin", "javaw.exe");
            if (File.Exists(javaw)) return javaw;

            progress(0, "获取 Java 运行时清单...");
            JObject all = JObject.Parse(Web.GetText(ManifestUrl));
            JToken comps = all["windows-x64"];
            if (comps == null || comps[component] == null) throw new Exception("清单中无组件 " + component);
            string manifestUrl = (string)((JArray)comps[component])[0]["manifest"]["url"];

            progress(0, "获取文件列表...");
            JObject pkg = JObject.Parse(Web.GetText(manifestUrl));
            List<JProperty> files = new List<JProperty>();
            foreach (JProperty p in ((JObject)pkg["files"]).Properties()) files.Add(p);
            List<JProperty> dirs = files.FindAll(f => (string)f.Value["type"] == "directory");
            List<JProperty> downloads = files.FindAll(f => (string)f.Value["type"] == "file");
            long totalSize = 0;
            foreach (JProperty f in downloads) totalSize += (long)f.Value["downloads"]["raw"]["size"];

            Directory.CreateDirectory(dir);
            foreach (JProperty f in dirs) Directory.CreateDirectory(Path.Combine(dir, f.Name.Replace('/', Path.DirectorySeparatorChar)));

            // 4 路并行下载 (400+ 小文件, 串行握手太慢); 失败收集后串行重试
            object locker = new object();
            List<JProperty> failed = new List<JProperty>();
            int done = 0; long doneSize = 0;
            ManualResetEvent fin = new ManualResetEvent(false);
            int pending = downloads.Count;
            foreach (JProperty f in downloads)
            {
                JProperty fp = f;
                long size = (long)fp.Value["downloads"]["raw"]["size"];
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        string dest = Path.Combine(dir, fp.Name.Replace('/', Path.DirectorySeparatorChar));
                        if (!(File.Exists(dest) && new FileInfo(dest).Length == size))
                        {
                            string url = (string)fp.Value["downloads"]["raw"]["url"];
                            string part = dest + ".part";
                            Web.DownloadFileMirrorFirst(url, part);
                            if (File.Exists(dest)) File.Delete(dest);
                            File.Move(part, dest);
                        }
                        lock (locker)
                        {
                            done++; doneSize += size;
                            if (done % 8 == 0 || done == downloads.Count)
                                progress((int)(100 * doneSize / Math.Max(1, totalSize)), "Java " + done + "/" + downloads.Count + " 个文件");
                        }
                    }
                    catch { lock (locker) { failed.Add(fp); done++; doneSize += size; } }
                    finally
                    {
                        lock (locker) { pending--; }
                        if (pending == 0) fin.Set();
                    }
                });
            }
            fin.WaitOne();
            foreach (JProperty f in failed)
            {
                string dest = Path.Combine(dir, f.Name.Replace('/', Path.DirectorySeparatorChar));
                string url = (string)f.Value["downloads"]["raw"]["url"];
                string part = dest + ".part";
                Web.DownloadFile(url, part); // 重试用官方源
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(part, dest);
            }
            if (failed.Count > 0) progress(99, "Java 重试完成 " + failed.Count + " 个");
            if (!File.Exists(javaw)) throw new Exception("运行时不完整: 缺少 javaw.exe");
            progress(100, "Java 运行时就绪");
            return javaw;
        }
    }

    // ---------- P2P 打洞联机 ----------

    // 最简 STUN Binding (RFC5389): 用调用方自己的 socket 查公网映射端点 (打洞必须同 socket)
    static class Stun
    {
        public static IPEndPoint Query(UdpClient sock, string host, int port, int timeoutMs)
        {
            try
            {
                byte[] req = new byte[20];
                req[1] = 1; // Binding Request
                req[4] = 0x21; req[5] = 0x12; req[6] = 0xA4; req[7] = 0x42; // magic cookie
                byte[] tid = new byte[12];
                new Random().NextBytes(tid);
                tid.CopyTo(req, 8);
                IPAddress ip = null;
                foreach (IPAddress a in Dns.GetHostEntry(host).AddressList)
                    if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) { ip = a; break; }
                if (ip == null) return null;
                IPEndPoint sv = new IPEndPoint(ip, port);
                sock.Client.ReceiveTimeout = timeoutMs;
                sock.Send(req, req.Length, sv);
                IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                byte[] resp = sock.Receive(ref from);
                if (resp == null || resp.Length < 28) return null;
                for (int i = 20; i + 4 <= resp.Length; )
                {
                    int at = (resp[i] << 8) | resp[i + 1];
                    int al = (resp[i + 2] << 8) | resp[i + 3];
                    if (at == 0x0020 && al >= 8 && i + 4 + 8 <= resp.Length)
                    {
                        int xport = (((resp[i + 5] << 8) | resp[i + 6]) ^ 0x2112) & 0xFFFF;
                        byte[] addr = new byte[4];
                        for (int k = 0; k < 4; k++) addr[k] = (byte)(resp[i + 7 + k] ^ (new byte[] { 0x21, 0x12, 0xA4, 0x42 })[k]);
                        return new IPEndPoint(new IPAddress(addr), xport);
                    }
                    i += 4 + al + ((4 - al % 4) % 4);
                }
            }
            catch { }
            return null;
        }
    }

    // 可靠字节流会话: 在一条 UDP 路径上实现 滑动窗口 + 超时重传 + 累计ACK (承载 MC 的 TCP 流量)
    class UdpSession
    {
        public static int SimDropPct = 0; // 测试用丢包
        const ushort Window = 24;
        const int Mtu = 1100;
        UdpClient sock; IPEndPoint remote;
        object sendLock = new object(); object lockObj = new object();
        volatile bool closed;
        ushort sndNxt, rcvNxt;
        Dictionary<ushort, byte[]> inflight = new Dictionary<ushort, byte[]>();
        Dictionary<ushort, DateTime> sentAt = new Dictionary<ushort, DateTime>();
        Dictionary<ushort, int> tries = new Dictionary<ushort, int>();
        Dictionary<ushort, byte[]> hold = new Dictionary<ushort, byte[]>();
        int rto = 250;
        Thread ticker;
        public Action<byte[]> OnData;
        public Action OnClosed;
        public Action<string> OnStatus;

        static bool SeqLt(ushort a, ushort b)
        {
            int d = ((int)a - (int)b + 65536) % 65536;
            return d != 0 && d < 32768; // 严格小于: 相等必须为 false, 否则按序新包会被当成重复丢弃
        }
        static bool TestDrop() { return SimDropPct > 0 && new Random().Next(100) < SimDropPct; }

        public UdpSession(UdpClient sock, IPEndPoint remote)
        {
            this.sock = sock; this.remote = remote;
            ticker = new Thread(Tick); ticker.IsBackground = true; ticker.Start();
        }

        void Raw(byte[] pkt)
        {
            if (TestDrop()) return;
            try { lock (sendLock) sock.Send(pkt, pkt.Length, remote); } catch { }
        }

        public void Send(byte[] stream)
        {
            int off = 0;
            while (off < stream.Length && !closed)
            {
                lock (lockObj)
                {
                    while (inflight.Count >= Window && !closed) Monitor.Wait(lockObj, 50);
                    if (closed) return;
                }
                int n = Math.Min(Mtu, stream.Length - off);
                byte[] payload = new byte[n];
                Buffer.BlockCopy(stream, off, payload, 0, n);
                off += n;
                ushort seq;
                lock (lockObj)
                {
                    seq = sndNxt; sndNxt++;
                    inflight[seq] = payload; sentAt[seq] = DateTime.Now; tries[seq] = 0;
                }
                Transmit(seq, payload);
            }
        }

        void Transmit(ushort seq, byte[] payload)
        {
            byte[] pkt = new byte[3 + payload.Length];
            pkt[0] = 3;
            pkt[1] = (byte)(seq >> 8); pkt[2] = (byte)(seq & 0xFF);
            Buffer.BlockCopy(payload, 0, pkt, 3, payload.Length);
            Raw(pkt);
        }

        void SendAck()
        {
            ushort ack;
            lock (lockObj) ack = rcvNxt;
            Raw(new byte[] { 4, (byte)(ack >> 8), (byte)(ack & 0xFF) });
        }

        public void SendPing()
        {
            Raw(new byte[] { 5 });
        }

        public void SendFin() { Raw(new byte[] { 7 }); }

        void Tick()
        {
            while (!closed)
            {
                Thread.Sleep(30);
                List<ushort> dead = null;
                List<ushort> resend = null;
                lock (lockObj)
                {
                    foreach (KeyValuePair<ushort, DateTime> kv in sentAt)
                    {
                        if ((DateTime.Now - kv.Value).TotalMilliseconds < rto) continue;
                        if (tries[kv.Key] > 60) { if (dead == null) dead = new List<ushort>(); dead.Add(kv.Key); continue; }
                        if (resend == null) resend = new List<ushort>();
                        resend.Add(kv.Key);
                    }
                    if (resend != null)
                    {
                        foreach (ushort s in resend)
                        {
                            tries[s]++;
                            rto = Math.Min(1500, rto * 2);
                            Transmit(s, inflight[s]);
                            sentAt[s] = DateTime.Now;
                        }
                    }
                }
                if (dead != null) { foreach (ushort s in dead) Close("链路超时"); break; }
            }
        }

        public void Handle(byte[] pkt)
        {
            if (pkt == null || pkt.Length == 0) return;
            byte t = pkt[0];
            if (t == 3 && pkt.Length > 3)
            {
                ushort seq = (ushort)((pkt[1] << 8) | pkt[2]);
                byte[] payload = new byte[pkt.Length - 3];
                Buffer.BlockCopy(pkt, 3, payload, 0, payload.Length);
                List<byte[]> deliver = null;
                lock (lockObj)
                {
                    if (SeqLt(seq, rcvNxt)) { } // 重复包
                    else if (seq == rcvNxt)
                    {
                        rcvNxt++;
                        if (OnData != null) { if (deliver == null) deliver = new List<byte[]>(); deliver.Add(payload); }
                        while (hold.Count > 0)
                        {
                            ushort nx = rcvNxt;
                            if (!hold.ContainsKey(nx)) break;
                            if (OnData != null) deliver.Add(hold[nx]);
                            hold.Remove(nx);
                            rcvNxt++;
                        }
                    }
                    else if (SeqLt(seq, (ushort)(rcvNxt + Window))) hold[seq] = payload;
                }
                // 回调在锁外执行: TCP 写可能阻塞, 不能持锁
                if (deliver != null) foreach (byte[] p in deliver) OnData(p);
                SendAck();
            }
            else if (t == 4 && pkt.Length >= 3)
            {
                ushort ack = (ushort)((pkt[1] << 8) | pkt[2]);
                lock (lockObj)
                {
                    List<ushort> rm = new List<ushort>();
                    foreach (ushort s in inflight.Keys) if (SeqLt(s, ack)) rm.Add(s);
                    foreach (ushort s in rm) { inflight.Remove(s); sentAt.Remove(s); tries.Remove(s); }
                    if (rm.Count > 0) rto = 250;
                    if (inflight.Count < Window) Monitor.Pulse(lockObj);
                }
            }
            else if (t == 5) Raw(new byte[] { 6 });
            else if (t == 7) Close("对方已断开");
        }

        public void Close(string why)
        {
            lock (lockObj)
            {
                if (closed) return;
                closed = true;
                Monitor.PulseAll(lockObj);
            }
            if (OnStatus != null) OnStatus("会话关闭: " + why);
            if (OnClosed != null) OnClosed();
        }

        public bool Closed { get { return closed; } }

        public string Diag()
        {
            lock (lockObj)
                return "sndNxt=" + sndNxt + " rcvNxt=" + rcvNxt + " inflight=" + inflight.Count + " hold=" + hold.Count;
        }
    }

    // 房主: 打洞信标 + 每个穿透成功的朋友一条 TCP 到本机游戏端口
    class MpHost
    {
        UdpClient udp; Thread recv, pinger;
        volatile bool on;
        int gamePort; string secret; string name;
        Dictionary<IPEndPoint, UdpSession> peers = new Dictionary<IPEndPoint, UdpSession>();
        object peerLock = new object();
        public string InviteCode;
        Action<string> status;
        public UdpSession LastSession;
        public int CntHello, CntData, CntUnknownFrom; public string LastUnknownFrom = "", PeerKey = "";
        public int[] Histo = new int[16];

        public int LocalPort { get { return ((IPEndPoint)udp.Client.LocalEndPoint).Port; } }

        public void Create(int gamePort, string name, Action<string> status)
        {
            this.gamePort = gamePort; this.name = string.IsNullOrEmpty(name) ? "房主" : name; this.status = status;
            udp = new UdpClient(0);
            udp.Client.ReceiveBufferSize = 1024 * 1024;
            udp.Client.SendBufferSize = 1024 * 1024;
            IPEndPoint pub = Stun.Query(udp, "stun.miwifi.com", 3478, 2500);
            if (pub == null) pub = Stun.Query(udp, "stun.qq.com", 3478, 2500);
            if (pub == null) pub = Stun.Query(udp, "stun.chat.bilibili.com", 3478, 2500);
            string lanIp = LocalIp();
            byte[] sec = new byte[6]; new Random().NextBytes(sec);
            secret = BitConverter.ToString(sec).Replace("-", "");
            if (pub == null && lanIp == null) throw new Exception("无法获取公网/内网端点 (STUN 全部失败)");
            JObject o = new JObject();
            o["v"] = 1; o["n"] = this.name; o["s"] = secret; o["gp"] = gamePort;
            if (pub != null) o["ep"] = pub.ToString();
            if (lanIp != null) o["lan"] = lanIp;
            InviteCode = Base64Url(o.ToString(Newtonsoft.Json.Formatting.None));
            on = true;
            recv = new Thread(Loop); recv.IsBackground = true; recv.Start();
            pinger = new Thread(PingLoop); pinger.IsBackground = true; pinger.Start();
            status("房间已创建" + (pub != null ? " (公网 " + pub + ")" : " (仅内网)"));
        }

        void PingLoop() { while (on) { Thread.Sleep(3000); lock (peerLock) foreach (UdpSession s in peers.Values) if (!s.Closed) s.SendPing(); } }

        void Loop()
        {
            while (on)
            {
                try
                {
                    IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                    udp.Client.ReceiveTimeout = 1000;
                    byte[] d = udp.Receive(ref from);
                    if (d == null || d.Length == 0) continue;
                    if (d[0] == (byte)'H')
                    {
                        // HELLO|secret (文本信令)
                        string msg = Encoding.UTF8.GetString(d);
                        if (msg != "HELLO|" + secret) continue;
                        UdpSession s;
                        bool fresh = false;
                        lock (peerLock)
                        {
                            if (!peers.TryGetValue(from, out s))
                            {
                                s = new UdpSession(udp, from);
                                peers[from] = s;
                                fresh = true;
                            }
                        }
                        if (fresh)
                        {
                            CntHello++;
                            PeerKey = from.ToString();
                            LastSession = s;
                            Bridge b = new Bridge(s, gamePort);
                            status("玩家已连入: " + from + ", 共 " + peers.Count + " 人");
                        }
                        byte[] w = Encoding.UTF8.GetBytes("WELCOME");
                        try { udp.Send(w, w.Length, from); } catch { }
                    }
                    else
                    {
                        if (d[0] < 16) Histo[d[0]]++;
                        CntData++;
                        UdpSession s;
                        lock (peerLock) if (!peers.TryGetValue(from, out s)) { CntUnknownFrom++; LastUnknownFrom = from.ToString(); continue; }
                        s.Handle(d);
                        List<IPEndPoint> dead = new List<IPEndPoint>();
                        lock (peerLock) foreach (KeyValuePair<IPEndPoint, UdpSession> kv in peers) if (kv.Value.Closed) dead.Add(kv.Key);
                        foreach (IPEndPoint k in dead) lock (peerLock) peers.Remove(k);
                    }
                }
                catch (SocketException) { } // 接收超时, 循环
                catch (ObjectDisposedException) { break; }
                catch { Thread.Sleep(100); }
            }
        }

        public void Stop()
        {
            on = false;
            lock (peerLock) foreach (UdpSession s in peers.Values) s.Close("房主关闭");
            try { udp.Close(); } catch { }
            status("房间已关闭");
        }

        public static string LocalIp()
        {
            try
            {
                using (UdpClient u = new UdpClient())
                {
                    u.Connect("223.5.5.5", 53);
                    IPEndPoint l = (IPEndPoint)u.Client.LocalEndPoint;
                    if (!l.Address.Equals(IPAddress.Any)) return l.Address.ToString();
                }
            }
            catch { }
            return null;
        }

        public static string Base64Url(string s)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }
        public static string Base64UrlDecode(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            while (s.Length % 4 != 0) s += "=";
            return Encoding.UTF8.GetString(Convert.FromBase64String(s));
        }
    }

    // 加入方: 向房主端点打洞, 成功后本地开 TCP 入口, MC 连 127.0.0.1:port
    class MpJoin
    {
        UdpClient udp; Thread punch, recv, pinger;
        volatile bool on, established;
        UdpSession session; Bridge bridge;
        Action<string> status; Action<int> onReady;
        IPEndPoint target;
        string secret;
        int gamePort;

        public void Join(string code, Action<string> status, Action<int> onReady)
        {
            this.status = status; this.onReady = onReady;
            JObject o;
            try { o = JObject.Parse(MpHost.Base64UrlDecode(code.Trim())); }
            catch { throw new Exception("邀请码无法解析"); }
            gamePort = o["gp"] == null ? 25565 : (int)o["gp"];
            secret = (string)o["s"];
            if (o["ep"] == null) throw new Exception("邀请码缺少房主公网端点");
            string ep = (string)o["ep"];
            int c = ep.LastIndexOf(':');
            target = new IPEndPoint(IPAddress.Parse(ep.Substring(0, c)), int.Parse(ep.Substring(c + 1)));
            udp = new UdpClient(0);
            udp.Client.ReceiveBufferSize = 1024 * 1024;
            udp.Client.SendBufferSize = 1024 * 1024;
            on = true;
            status("打洞中...");
            punch = new Thread(PunchLoop); punch.IsBackground = true; punch.Start();
            recv = new Thread(RecvLoop); recv.IsBackground = true; recv.Start();
        }

        void PunchLoop()
        {
            byte[] hello = Encoding.UTF8.GetBytes("HELLO|" + secret);
            int waited = 0;
            while (on && !established)
            {
                try { udp.Send(hello, hello.Length, target); } catch { }
                Thread.Sleep(350);
                waited += 350;
                if (waited % 7000 == 0) status("打洞中... " + (waited / 1000) + "秒 (受 NAT 类型影响可能失败)");
                if (waited > 75000) { status("打洞失败: 请房主重新创建房间再试"); on = false; return; }
            }
        }

        void RecvLoop()
        {
            while (on)
            {
                try
                {
                    IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                    udp.Client.ReceiveTimeout = 1000;
                    byte[] d = udp.Receive(ref from);
                    if (d == null || d.Length == 0) continue;
                    if (!established)
                    {
                        string s = Encoding.UTF8.GetString(d);
                        if (d[0] == 87 && s.StartsWith("WELCOME"))
                        {
                            established = true;
                            session = new UdpSession(udp, from);
                            int localPort = 25565;
                            TcpListener tl = null;
                            while (tl == null)
                            {
                                try { tl = new TcpListener(IPAddress.Loopback, localPort); tl.Start(); }
                                catch
                                {
                                    localPort++;
                                    if (localPort > 25590)
                                    {
                                        tl = new TcpListener(IPAddress.Loopback, 0); tl.Start();
                                        localPort = ((IPEndPoint)tl.LocalEndpoint).Port;
                                        break;
                                    }
                                }
                            }
                            bridge = new Bridge(session, tl);
                            pinger = new Thread(delegate() { while (on && session != null && !session.Closed) { session.SendPing(); Thread.Sleep(3000); } });
                            pinger.IsBackground = true; pinger.Start();
                            status("已连通 ✓  在游戏里「多人游戏→直接连接」输入: 127.0.0.1:" + localPort);
                            if (onReady != null) onReady(localPort);
                            continue;
                        }
                    }
                    if (established && session != null) session.Handle(d);
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { break; }
                catch { Thread.Sleep(100); }
            }
        }

        public void Stop()
        {
            on = false;
            if (session != null) session.SendFin();
            if (session != null) session.Close("主动退出");
            try { udp.Close(); } catch { }
        }

        public UdpSession SessionRef { get { return session; } }
    }

    // 会话 <-> TCP 桥: host 侧 connect 本机游戏端口; join 侧 listen 127.0.0.1 (一条连接, 断开为止)
    class Bridge
    {
        UdpSession sess; int gamePort;

        public Bridge(UdpSession sess, int gamePort)
        {
            this.sess = sess; this.gamePort = gamePort;
            TcpClient tcp = new TcpClient();
            try { tcp.Connect(IPAddress.Loopback, gamePort); }
            catch (Exception ex)
            {
                sess.SendFin();
                sess.Close("本机游戏端口不通: " + ex.Message + " (开「对局域网开放」了吗?)");
                return;
            }
            Attach(tcp);
        }

        public Bridge(UdpSession sess, TcpListener listener)
        {
            this.sess = sess;
            Thread acc = new Thread(delegate()
            {
                try
                {
                    TcpClient tcp = listener.AcceptTcpClient();
                    Attach(tcp);
                }
                catch { sess.Close("本地入口异常"); }
            });
            acc.IsBackground = true; acc.Start();
        }

        void Attach(TcpClient tcp)
        {
            NetworkStream ns = tcp.GetStream();
            // TCP 写走专职队列线程: 会话回调里直接写会在对端慢时反压整个 UDP 接收
            Queue<byte[]> wq = new Queue<byte[]>();
            object wqLock = new object();
            bool done = false;
            sess.OnData = delegate(byte[] chunk)
            {
                lock (wqLock) { wq.Enqueue(chunk); Monitor.Pulse(wqLock); }
            };
            sess.OnClosed = delegate
            {
                lock (wqLock) { done = true; Monitor.Pulse(wqLock); }
                try { tcp.Close(); } catch { }
            };
            Thread writer = new Thread(delegate()
            {
                while (true)
                {
                    byte[] c = null;
                    lock (wqLock)
                    {
                        while (wq.Count == 0 && !done) Monitor.Wait(wqLock, 500);
                        if (wq.Count == 0) break;
                        c = wq.Dequeue();
                    }
                    try { ns.Write(c, 0, c.Length); ns.Flush(); }
                    catch { sess.Close("游戏连接断开"); return; }
                }
            });
            writer.IsBackground = true; writer.Start();
            Thread pump = new Thread(delegate()
            {
                byte[] buf = new byte[4096];
                try
                {
                    int n;
                    while ((n = ns.Read(buf, 0, buf.Length)) > 0)
                    {
                        byte[] piece = new byte[n];
                        Buffer.BlockCopy(buf, 0, piece, 0, n);
                        sess.Send(piece);
                    }
                }
                catch { }
                sess.SendFin();
                sess.Close("游戏连接结束");
                try { tcp.Close(); } catch { }
            });
            pump.IsBackground = true;
            pump.Start();
        }
    }

    // ---------- 下载 ----------
    class VanillaDownloader
    {
        public string McDir;
        TextBox LogBox;
        Action<int> SetProgress;
        Action<string> SetStatus;

        public VanillaDownloader(string mcDir, TextBox logBox, Action<int> setProgress, Action<string> setStatus)
        {
            McDir = mcDir; LogBox = logBox; SetProgress = setProgress; SetStatus = setStatus;
        }

        public string GetManifestJson() { return Web.GetText("https://launchermeta.mojang.com/mc/game/version_manifest.json"); }

        public void Download(string versionId, string versionUrl)
        {
            JObject vj = FetchJsonAndJar(versionId, versionUrl);
            DownloadLibs(vj);
            EnsureAssets(vj);
            Util.Log(LogBox, "版本 " + versionId + " 下载完成!");
        }

        // 确保某个原版版本完整可用 (json + jar + libs + assets), 缺啥补啥
        public void EnsureVersion(string versionId)
        {
            string vJson = Path.Combine(McDir, "versions", versionId, versionId + ".json");
            string vJar = Path.Combine(McDir, "versions", versionId, versionId + ".jar");
            if (!File.Exists(vJson) || !File.Exists(vJar))
            {
                SetStatus("获取原版 " + versionId + " ...");
                JObject m = JObject.Parse(GetManifestJson());
                string url = null;
                foreach (JObject v in (JArray)m["versions"]) if ((string)v["id"] == versionId) { url = (string)v["url"]; break; }
                if (url == null) throw new Exception("版本清单中找不到 " + versionId);
                Download(versionId, url);
                return;
            }
            JObject vj = JObject.Parse(File.ReadAllText(vJson, Encoding.UTF8));
            DownloadLibs(vj);
            EnsureAssets(vj);
        }

        public JObject FetchJsonAndJar(string versionId, string versionUrl)
        {
            string vDir = Path.Combine(McDir, "versions", versionId);
            Directory.CreateDirectory(vDir);
            SetStatus("下载版本信息...");
            string vJsonPath = Path.Combine(vDir, versionId + ".json");
            Fetch(versionUrl, vJsonPath, -1);
            JObject vj = JObject.Parse(File.ReadAllText(vJsonPath, Encoding.UTF8));
            JToken client = vj["downloads"]["client"];
            SetStatus("下载 client.jar ...");
            SetProgress(2);
            Fetch((string)client["url"], Path.Combine(vDir, versionId + ".jar"), (long)client["size"], true);
            Util.Log(LogBox, "client.jar 完成 (" + ((long)client["size"] / 1024 / 1024) + " MB)");
            return vj;
        }

        public List<string> NativeFiles = new List<string>();

        public void DownloadLibs(JObject vj)
        {
            SetStatus("检查支持库...");
            List<string[]> libItems = new List<string[]>(); // {url, 相对路径}
            foreach (JObject lib in (JArray)vj["libraries"])
            {
                if (!Rules.Allow(lib["rules"])) continue;
                JToken dl = lib["downloads"] == null ? null : lib["downloads"]["artifact"];
                if (lib["natives"] != null && lib["natives"]["windows"] != null)
                {
                    string cls = ((string)lib["natives"]["windows"]).Replace("${arch}", "");
                    JToken c = lib["downloads"]["classifiers"][cls];
                    if (c != null) dl = c;
                }
                if (dl != null) libItems.Add(new string[] { (string)dl["url"], "libraries/" + (string)dl["path"] });
                else if (lib["name"] != null && lib["url"] != null)
                {
                    // 老式 maven 库 (Fabric): "组:名:版本" -> 仓库/组/名/版本/名-版本.jar
                    string[] np = ((string)lib["name"]).Split(':');
                    if (np.Length >= 3)
                    {
                        string rel = np[0].Replace('.', '/') + "/" + np[1] + "/" + np[2] + "/" + np[1] + "-" + np[2] + ".jar";
                        libItems.Add(new string[] { ((string)lib["url"]).TrimEnd('/') + "/" + rel, "libraries/" + rel });
                    }
                }
            }
            ParallelFetch(libItems, 2, 20, "支持库");
            Util.Log(LogBox, "支持库完成 (" + libItems.Count + " 项)");
        }

        // 并行批量下载: 8 路 + 镜像优先 + .part 原子落盘 + 限频进度/ETA; 失败收集后官方源串行重试
        void ParallelFetch(List<string[]> items, int progressBase, int progressSpan, string label)
        {
            if (items.Count == 0) return;
            List<string[]> failed = new List<string[]>();
            object locker = new object();
            int done = 0, pending = items.Count;
            ManualResetEvent fin = new ManualResetEvent(false);
            Stopwatch sw = Stopwatch.StartNew();
            Thread reporter = new Thread(delegate()
            {
                while (true)
                {
                    lock (locker)
                    {
                        if (pending == 0) break;
                        int d = done;
                        double rate = d / Math.Max(0.5, sw.Elapsed.TotalSeconds);
                        // 前 8 个完成前样本太少, ETA 会虚高, 只显示计数
                        bool etaOk = d >= 8 && rate > 0.05;
                        int eta = etaOk ? (int)((items.Count - d) / rate) : -1;
                        SetProgress(progressBase + progressSpan * d / items.Count);
                        SetStatus(label + " " + d + " / " + items.Count + (eta > 3 ? "  剩约 " + eta + " 秒" : "  即将完成"));
                    }
                    Thread.Sleep(400);
                }
            });
            reporter.IsBackground = true; reporter.Start();
            foreach (string[] it in items)
            {
                string url = it[0], rel = it[1];
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        string dest = Path.Combine(McDir, rel.Replace('/', '\\'));
                        string dir = Path.GetDirectoryName(dest);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        string part = dest + ".part";
                        Web.DownloadFileMirrorFirst(url, part);
                        if (File.Exists(dest)) File.Delete(dest);
                        File.Move(part, dest);
                    }
                    catch { lock (locker) failed.Add(new string[] { url, rel }); }
                    lock (locker)
                    {
                        done++; pending--;
                        if (pending == 0) fin.Set();
                    }
                });
            }
            fin.WaitOne();
            reporter.Join(1500);
            // 落盘校验: 主循环"成功"但文件缺失的, 记录真实异常再补下 (曾出现 authlib 静默丢失)
            List<string[]> broken = new List<string[]>();
            foreach (string[] it in items)
            {
                string dest = Path.Combine(McDir, it[1].Replace('/', '\\'));
                if (!File.Exists(dest) && !failed.Any(f => f[1] == it[1])) broken.Add(it);
            }
            if (broken.Count > 0)
                Util.Log(LogBox, label + " 校验发现 " + broken.Count + " 个未落盘, 逐个补下...");
            foreach (string[] it in broken)
            {
                string dest = Path.Combine(McDir, it[1].Replace('/', '\\'));
                string dir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                string part = dest + ".part";
                try
                {
                    Web.DownloadFile(it[0], part);
                    if (File.Exists(dest)) File.Delete(dest);
                    File.Move(part, dest);
                    Util.Log(LogBox, label + " 补下成功: " + it[1]);
                }
                catch (Exception ex) { Util.Log(LogBox, label + " 补下失败 " + it[1] + ": " + ex.Message); }
            }
            foreach (string[] it in failed)
            {
                string dest = Path.Combine(McDir, it[1].Replace('/', '\\'));
                string dir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                string part = dest + ".part";
                Web.DownloadFile(it[0], part); // 重试走官方源
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(part, dest);
            }
            if (failed.Count > 0) Util.Log(LogBox, label + "重试成功 " + failed.Count + " 个 (官方源)");
            SetProgress(progressBase + progressSpan);
        }

        public void EnsureAssets(JObject vj)
        {
            JToken ai = vj["assetIndex"];
            if (ai == null || ai["url"] == null) return;
            string indexId = (string)ai["id"];
            string indexPath = Path.Combine(McDir, "assets", "indexes", indexId + ".json");
            Fetch((string)ai["url"], indexPath, -1);
            JObject index = JObject.Parse(File.ReadAllText(indexPath, Encoding.UTF8));
            JObject objects = (JObject)index["objects"];
            int total = objects.Count;
            // 先本地清点, 只下载缺失的
            List<string[]> need = new List<string[]>();
            foreach (JProperty p in objects.Properties())
            {
                string hash = (string)p.Value["hash"];
                long size = (long)p.Value["size"];
                string dest = Path.Combine(McDir, "assets", "objects", hash.Substring(0, 2), hash);
                if (!(File.Exists(dest) && new FileInfo(dest).Length == size))
                    need.Add(new string[] { "https://resources.download.minecraft.net/" + hash.Substring(0, 2) + "/" + hash, "assets/objects/" + hash.Substring(0, 2) + "/" + hash });
            }
            Util.Log(LogBox, "资源文件: 共 " + total + ", 需下载 " + need.Count);
            if (need.Count > 0)
            {
                ParallelFetch(need, 22, 76, "资源文件");
            }
            SetProgress(100);
            SetStatus("完成");
            Util.Log(LogBox, "资源文件完成: 共 " + total + ", 新下载 " + need.Count);
        }

        bool Fetch(string url, string dest, long expectedSize) { return Fetch(url, dest, expectedSize, false); }
        bool Fetch(string url, string dest, long expectedSize, bool mirrorFirst)
        {
            try
            {
                if (File.Exists(dest))
                {
                    if (expectedSize <= 0 || new FileInfo(dest).Length == expectedSize) return false;
                    File.Delete(dest);
                }
                string dir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                string tmp = dest + ".tmp";
                if (mirrorFirst) Web.DownloadFileMirrorFirst(url, tmp);
                else { try { Web.DownloadFile(url, tmp); } catch { Web.DownloadFile(url, tmp); } }
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(tmp, dest);
                return true;
            }
            catch (Exception ex) { throw new Exception("下载失败: " + url + " -> " + ex.Message); }
        }
    }

    // ---------- 启动 ----------
    class GameLauncher
    {
        public string McDir, JavaPath, PlayerName;
        public int MemoryMB;
        TextBox LogBox;
        public static bool PrintArgsOnly; // -args 诊断模式: 打印组装参数不启动
        public static DateTime LastLaunchTime = DateTime.MinValue;
        public static string LastWorkDir;

        // 游戏默认中文: options.txt 的 lang 行强制 zh_cn (无则创建)
        static void EnsureChinese(string gameDir)
        {
            try
            {
                string opt = Path.Combine(gameDir, "options.txt");
                if (File.Exists(opt))
                {
                    string[] lines = File.ReadAllLines(opt);
                    bool found = false;
                    for (int i = 0; i < lines.Length; i++)
                        if (lines[i].StartsWith("lang:", StringComparison.Ordinal)) { lines[i] = "lang:zh_cn"; found = true; break; }
                    if (found) File.WriteAllLines(opt, lines);
                    else File.AppendAllText(opt, "\r\nlang:zh_cn");
                }
                else
                {
                    Directory.CreateDirectory(gameDir);
                    File.WriteAllText(opt, "lang:zh_cn\r\n", Encoding.UTF8);
                }
            }
            catch { }
        }

        public GameLauncher(string mcDir, string javaPath, string playerName, int memoryMB, TextBox logBox)
        {
            McDir = mcDir; JavaPath = javaPath; PlayerName = playerName; MemoryMB = memoryMB; LogBox = logBox;
        }

        public Process Launch(string versionId)
        {
            string vDir = Path.Combine(McDir, "versions", versionId);
            string vJsonPath = Path.Combine(vDir, versionId + ".json");
            if (!File.Exists(vJsonPath)) throw new Exception("找不到版本 json: " + vJsonPath);
            JObject vj = JObject.Parse(File.ReadAllText(vJsonPath, Encoding.UTF8));

            if (vj["inheritsFrom"] != null)
            {
                string parentId = (string)vj["inheritsFrom"];
                string parentPath = Path.Combine(McDir, "versions", parentId, parentId + ".json");
                if (File.Exists(parentPath))
                {
                    JObject pj = JObject.Parse(File.ReadAllText(parentPath, Encoding.UTF8));
                    JArray allLibs = (JArray)pj["libraries"];
                    foreach (JToken l in (JArray)vj["libraries"]) allLibs.Add(l);
                    if (vj["arguments"] == null) vj["arguments"] = pj["arguments"];
                    if (vj["minecraftArguments"] == null && pj["minecraftArguments"] != null) vj["minecraftArguments"] = pj["minecraftArguments"];
                    if (vj["assetIndex"] == null) vj["assetIndex"] = pj["assetIndex"];
                    if (vj["mainClass"] == null) vj["mainClass"] = pj["mainClass"];
                    Util.Log(LogBox, "已合并继承版本: " + parentId);
                }
            }

            // 客户端 jar: 版本目录下没有时, Fabric/整合包版本回退到源原版 (xMouseVanillaJar)
            string jarPath = Path.Combine(vDir, versionId + ".jar");
            if (!File.Exists(jarPath))
            {
                string srcVanilla = (string)vj["xMouseVanillaJar"];
                if (!string.IsNullOrEmpty(srcVanilla))
                {
                    string alt = Path.Combine(McDir, "versions", srcVanilla, srcVanilla + ".jar");
                    if (File.Exists(alt)) jarPath = alt;
                }
            }
            if (!File.Exists(jarPath)) throw new Exception("缺少客户端 jar, 请先下载: " + jarPath);

            List<string> classpath = new List<string>();
            List<string> natives = new List<string>();
            foreach (JObject lib in (JArray)vj["libraries"])
            {
                if (!Rules.Allow(lib["rules"])) continue;
                JToken dl = lib["downloads"] == null ? null : lib["downloads"]["artifact"];
                bool isNative = false;
                if (lib["natives"] != null && lib["natives"]["windows"] != null)
                {
                    string cls = ((string)lib["natives"]["windows"]).Replace("${arch}", "");
                    JToken c = lib["downloads"]["classifiers"][cls];
                    if (c != null) { dl = c; isNative = true; }
                }
                string rel = null;
                if (dl != null) rel = (string)dl["path"];
                else if (lib["name"] != null && lib["url"] != null)
                {
                    // 老式 maven 库 (Fabric)
                    string[] np = ((string)lib["name"]).Split(':');
                    if (np.Length >= 3)
                        rel = np[0].Replace('.', '/') + "/" + np[1] + "/" + np[2] + "/" + np[1] + "-" + np[2] + ".jar";
                }
                if (rel == null) continue;
                string p = Path.Combine(McDir, "libraries", rel.Replace('/', '\\'));
                if (!File.Exists(p)) throw new Exception("缺少支持库: " + rel + " (请先下载)");
                if (isNative) natives.Add(p); else classpath.Add(p);
            }
            classpath.Add(jarPath);

            string nativesDir = Path.Combine(Path.GetTempPath(), "MouseLauncher", versionId, "natives");
            if (Directory.Exists(nativesDir)) Directory.Delete(nativesDir, true);
            Directory.CreateDirectory(nativesDir);
            foreach (string nj in natives)
            {
                try { ZipFile.ExtractToDirectory(nj, nativesDir); } catch (Exception ex) { Util.Log(LogBox, "解压 natives 警告: " + ex.Message); }
            }

            string assetsIndex = vj["assetIndex"] != null ? (string)vj["assetIndex"]["id"] : "legacy";
            // 版本隔离: versions\<id>\mods 存在时工作目录切到版本目录, mod/存档/配置各版本独立
            string workDir = Directory.Exists(Path.Combine(vDir, "mods")) ? vDir : McDir;
            LastWorkDir = workDir;
            EnsureChinese(workDir);
            // 启动前备份该目录存档 (含隔离目录); 备份失败不阻断启动
            if (Settings.AutoBackup)
            {
                try
                {
                    string bz = Util.BackupSaves(workDir, Path.Combine(McDir, "backups"));
                    if (bz != null) Util.Log(LogBox, "存档已备份: " + Path.GetFileName(bz));
                }
                catch (Exception bex) { Util.Log(LogBox, "备份失败(不影响启动): " + bex.Message); }
            }
            Dictionary<string, string> vars = new Dictionary<string, string>();
            vars["auth_player_name"] = PlayerName;
            vars["version_name"] = versionId;
            vars["game_directory"] = workDir;
            vars["assets_root"] = Path.Combine(McDir, "assets");
            vars["assets_index_name"] = assetsIndex;
            vars["auth_uuid"] = Util.OfflineUuid(PlayerName);
            vars["auth_access_token"] = "0";
            vars["auth_session"] = "0";
            vars["clientid"] = "0";
            vars["auth_xuid"] = "0";
            vars["user_type"] = "legacy";
            vars["version_type"] = "release";
            vars["natives_directory"] = nativesDir;
            vars["launcher_name"] = "MouseLauncher";
            vars["launcher_version"] = "1.0";
            vars["classpath"] = string.Join(";", classpath.ToArray());
            vars["classpath_separator"] = ";";
            vars["library_directory"] = Path.Combine(McDir, "libraries");

            List<string> jvmArgs = new List<string>();
            jvmArgs.Add("-Xmx" + MemoryMB + "M");
            if (vj["arguments"] != null && vj["arguments"]["jvm"] != null)
            {
                foreach (JToken t in (JArray)vj["arguments"]["jvm"])
                {
                    if (t.Type == JTokenType.String) jvmArgs.Add(Subst((string)t, vars));
                    else if (Rules.Allow(t["rules"]))
                    {
                        if (t["value"].Type == JTokenType.Array) foreach (string v in (JArray)t["value"]) jvmArgs.Add(Subst(v, vars));
                        else jvmArgs.Add(Subst((string)t["value"], vars));
                    }
                }
            }
            else
            {
                jvmArgs.Add("-Djava.library.path=" + nativesDir);
                jvmArgs.Add("-cp");
                jvmArgs.Add(vars["classpath"]);
            }

            List<string> gameArgs = new List<string>();
            if (vj["arguments"] != null && vj["arguments"]["game"] != null)
            {
                foreach (JToken t in (JArray)vj["arguments"]["game"])
                {
                    if (t.Type == JTokenType.String) gameArgs.Add(Subst((string)t, vars));
                    else if (Rules.Allow(t["rules"]))
                    {
                        if (t["value"].Type == JTokenType.Array) foreach (string v in (JArray)t["value"]) gameArgs.Add(Subst(v, vars));
                        else gameArgs.Add(Subst((string)t["value"], vars));
                    }
                }
            }
            else if (vj["minecraftArguments"] != null)
            {
                foreach (string a in ((string)vj["minecraftArguments"]).Split(' '))
                    if (a.Length > 0) gameArgs.Add(Subst(a, vars));
            }
            gameArgs = FilterArgs(gameArgs, vars);

            string mainClass = (string)vj["mainClass"];

            List<string> all = new List<string>();
            all.AddRange(jvmArgs);
            all.Add(mainClass);
            all.AddRange(gameArgs);
            if (PrintArgsOnly)
            {
                Console.WriteLine("=== JVM 参数 (" + jvmArgs.Count + ") ===");
                foreach (string a in jvmArgs) Console.WriteLine(a.Length > 200 ? a.Substring(0, 200) + "..." : a);
                Console.WriteLine("=== mainClass: " + mainClass);
                Console.WriteLine("=== GAME 参数 (" + gameArgs.Count + ") ===");
                foreach (string a in gameArgs) Console.WriteLine(a);
                Console.WriteLine("=== classpath 项数: " + (vars["classpath"].Split(';').Length));
                foreach (string c in vars["classpath"].Split(';')) Console.WriteLine("  CP: " + c);
                return null;
            }
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = JavaPath;
            psi.Arguments = string.Join(" ", all.Select(a => a.Contains(' ') ? "\"" + a + "\"" : a).ToArray());
            psi.WorkingDirectory = workDir;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            Util.Log(LogBox, "启动: " + Path.GetFileName(JavaPath) + "  版本: " + versionId + "  内存: " + MemoryMB + "MB");
            LastLaunchTime = DateTime.Now;
            Process proc = Process.Start(psi);
            Util.Log(LogBox, "游戏进程 PID: " + proc.Id);
            proc.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) Util.Log(LogBox, e.Data); };
            proc.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) Util.Log(LogBox, "[E] " + e.Data); };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { proc.WaitForExit(); } catch { }
                try { Util.Log(LogBox, "游戏进程已退出 (代码 " + proc.ExitCode + ")"); } catch { }
            });
            return proc;
        }

        static string Subst(string s, Dictionary<string, string> vars)
        {
            foreach (KeyValuePair<string, string> kv in vars) s = s.Replace("${" + kv.Key + "}", kv.Value);
            return s;
        }

        // 丢弃引用了未知变量的 --参数 值 对 (如未使用的 --quickPlay* 选项)
        static List<string> FilterArgs(List<string> args, Dictionary<string, string> vars)
        {
            List<string> outArgs = new List<string>();
            for (int i = 0; i < args.Count; i++)
            {
                string a = args[i];
                if (a.Length > 2 && a[0] == '-' && a[1] == '-' && i + 1 < args.Count)
                {
                    string v = args[i + 1];
                    if (HasUnknownVar(a, vars) || HasUnknownVar(v, vars)) { i++; continue; }
                }
                if (HasUnknownVar(a, vars)) continue;
                outArgs.Add(a);
            }
            return outArgs;
        }
        static bool HasUnknownVar(string s, Dictionary<string, string> vars)
        {
            if (s == null) return false;
            int pos = 0;
            while (true)
            {
                int a = s.IndexOf("${", pos, StringComparison.Ordinal);
                if (a < 0) return false;
                int b = s.IndexOf('}', a);
                if (b < 0) return false;
                string key = s.Substring(a + 2, b - a - 2);
                if (!vars.ContainsKey(key)) return true;
                pos = b + 1;
            }
        }
    }

    // ---------- Modrinth ----------
    static class Modrinth
    {
        public static JArray Search(string query, string ptype)
        {
            string q = Uri.EscapeDataString(query == null ? "" : query);
            string facets = Uri.EscapeDataString("[[\"project_type:" + ptype + "\"]]");
            string url = "https://api.modrinth.com/v2/search?query=" + q + "&facets=" + facets + "&limit=20&index=relevance";
            JObject r = JObject.Parse(Web.GetText(url));
            return (JArray)r["hits"];
        }
        public static JArray Versions(string projectId)
        {
            return JArray.Parse(Web.GetText("https://api.modrinth.com/v2/project/" + Uri.EscapeDataString(projectId) + "/version"));
        }
        public static JObject PrimaryFile(JObject versionEntry)
        {
            JArray files = (JArray)versionEntry["files"];
            foreach (JObject f in files) if ((bool?)f["primary"] == true) return f;
            return files.Count > 0 ? (JObject)files[0] : null;
        }
    }

    // ---------- Mod / 整合包安装 ----------
    class ModInstaller
    {
        public string McDir;
        TextBox LogBox;
        Action<int> SetProgress;
        Action<string> SetStatus;
        VanillaDownloader core;

        public ModInstaller(string mcDir, TextBox logBox, Action<int> setProgress, Action<string> setStatus)
        {
            McDir = mcDir; LogBox = logBox; SetProgress = setProgress; SetStatus = setStatus;
            core = new VanillaDownloader(mcDir, logBox, setProgress, setStatus);
        }

        public bool IsLoaderVersion(string versionId)
        {
            try
            {
                string p = Path.Combine(McDir, "versions", versionId, versionId + ".json");
                if (!File.Exists(p)) return false;
                string js = File.ReadAllText(p, Encoding.UTF8);
                foreach (string key in new string[] { "fabric", "neoforge", "\"forge", "quilt", "liteloader" })
                    if (js.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                return false;
            }
            catch { return false; }
        }

        // 安装单个文件 (Mod / 资源包 / 光影) 到所选版本的子文件夹
        public void InstallFile(JObject versionEntry, string targetVersion, string subfolder)
        {
            JObject file = Modrinth.PrimaryFile(versionEntry);
            if (file == null) throw new Exception("该版本没有可下载文件");
            string url = (string)file["url"];
            string fname = (string)file["filename"];
            string dir = Path.Combine(McDir, "versions", targetVersion, subfolder);
            Directory.CreateDirectory(dir);
            SetStatus("下载 " + fname + " ...");
            string tmp = Path.Combine(dir, fname + ".tmp");
            try { Web.DownloadFile(url, tmp); } catch { Web.DownloadFile(url, tmp); }
            string dest = Path.Combine(dir, fname);
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(tmp, dest);
            Util.Log(LogBox, "已安装: " + subfolder + "/" + fname + "  (版本 " + targetVersion + ")");
            SetProgress(100);
        }

        // 安装 Fabric 到指定原版版本, 生成 <mc>-fabric 版本
        public string InstallFabric(string mcVersion, string fabricVersion)
        {
            SetStatus("确保原版 " + mcVersion + " ...");
            core.EnsureVersion(mcVersion);
            string vName = mcVersion + "-fabric";
            string vDir = Path.Combine(McDir, "versions", vName);
            Directory.CreateDirectory(vDir);
            SetStatus("获取 Fabric 加载器信息...");
            string furl = "https://meta.fabricmc.net/v2/versions/loader/" + mcVersion + "/" + fabricVersion + "/profile/json";
            JObject fabric = JObject.Parse(Web.GetText(furl));
            JObject vanilla = JObject.Parse(File.ReadAllText(Path.Combine(McDir, "versions", mcVersion, mcVersion + ".json"), Encoding.UTF8));
            JObject merged = (JObject)vanilla.DeepClone();
            merged["id"] = vName;
            merged["xMouseVanillaJar"] = mcVersion; // jar 用源原版的
            JArray libs = (JArray)merged["libraries"];
            foreach (JToken fl in (JArray)fabric["libraries"]) libs.Add(fl);
            merged["mainClass"] = fabric["mainClass"];
            if (fabric["arguments"] != null)
            {
                // game 整体替换; jvm 必须拼接保留 vanilla 的 -cp/--libs 路径
                if (fabric["arguments"]["game"] != null) merged["arguments"]["game"] = fabric["arguments"]["game"];
                if (fabric["arguments"]["jvm"] != null)
                {
                    JArray mj = (JArray)(merged["arguments"]["jvm"] ?? new JArray());
                    foreach (JToken j in (JArray)fabric["arguments"]["jvm"]) mj.Add(j);
                    merged["arguments"]["jvm"] = mj;
                }
            }
            File.WriteAllText(Path.Combine(vDir, vName + ".json"), merged.ToString(), Encoding.UTF8);
            core.DownloadLibs(merged);
            core.EnsureAssets(merged);
            Util.Log(LogBox, "Fabric 版本就绪: " + vName + "  (mod 放到 versions\\" + vName + "\\mods)");
            SetProgress(100);
            SetStatus("Fabric 安装完成: " + vName);
            return vName;
        }

        // 一键安装 Modrinth 整合包 (.mrpack)
        public string InstallModpack(JObject versionEntry)
        {
            JObject file = Modrinth.PrimaryFile(versionEntry);
            if (file == null) throw new Exception("该整合包没有可下载文件");
            string mrpack = Path.Combine(Path.GetTempPath(), "MouseLauncher_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".mrpack");
            SetStatus("下载整合包文件...");
            try { Web.DownloadFile((string)file["url"], mrpack); } catch { Web.DownloadFile((string)file["url"], mrpack); }

            string vName = null;
            using (ZipArchive zip = ZipFile.OpenRead(mrpack))
            {
                ZipArchiveEntry indexEntry = zip.GetEntry("modrinth.index.json");
                if (indexEntry == null) { zip.Dispose(); File.Delete(mrpack); throw new Exception("无效的 .mrpack 文件 (缺少 modrinth.index.json)"); }
                JObject idx;
                using (Stream s = indexEntry.Open()) using (StreamReader sr = new StreamReader(s, Encoding.UTF8)) idx = JObject.Parse(sr.ReadToEnd());

                foreach (char c in Path.GetInvalidFileNameChars()) { }
                vName = ((string)idx["name"]) ?? ("pack-" + DateTime.Now.ToString("yyyyMMdd-HHmm"));
                foreach (char c in Path.GetInvalidFileNameChars()) vName = vName.Replace(c, '_');
                if (vName.Trim().Length == 0) vName = "pack";
                string vDir = Path.Combine(McDir, "versions", vName);
                Directory.CreateDirectory(vDir);

                // 1) 依赖: 原版 + fabric
                JObject deps = (JObject)idx["dependencies"];
                string mcDep = deps != null && deps["minecraft"] != null ? (string)deps["minecraft"] : null;
                string fabricDep = deps != null && deps["fabric-loader"] != null ? (string)deps["fabric-loader"] : null;
                if (mcDep == null) throw new Exception("整合包缺少原版版本依赖信息");
                Util.Log(LogBox, "整合包依赖: 原版 " + mcDep + (fabricDep != null ? ", Fabric " + fabricDep : ""));
                core.EnsureVersion(mcDep);

                JObject vanilla = JObject.Parse(File.ReadAllText(Path.Combine(McDir, "versions", mcDep, mcDep + ".json"), Encoding.UTF8));
                JObject merged = (JObject)vanilla.DeepClone();
                merged["id"] = vName;
                merged["xMouseVanillaJar"] = mcDep; // jar 用源原版的
                if (fabricDep != null)
                {
                    SetStatus("获取 Fabric 加载器信息...");
                    string furl = "https://meta.fabricmc.net/v2/versions/loader/" + mcDep + "/" + fabricDep + "/profile/json";
                    string fjson = Web.GetText(furl);
                    JObject fabric = JObject.Parse(fjson);
                    JArray libs = (JArray)merged["libraries"];
                    foreach (JToken fl in (JArray)fabric["libraries"]) libs.Add(fl);
                    merged["mainClass"] = fabric["mainClass"];
                    if (fabric["arguments"] != null)
                    {
                        // game 整体替换; jvm 拼接保留 vanilla 的 -cp/--libs 路径
                        if (fabric["arguments"]["game"] != null) merged["arguments"]["game"] = fabric["arguments"]["game"];
                        if (fabric["arguments"]["jvm"] != null)
                        {
                            JArray mj = (JArray)(merged["arguments"]["jvm"] ?? new JArray());
                            foreach (JToken j in (JArray)fabric["arguments"]["jvm"]) mj.Add(j);
                            merged["arguments"]["jvm"] = mj;
                        }
                    }
                }
                File.WriteAllText(Path.Combine(vDir, vName + ".json"), merged.ToString(), Encoding.UTF8);
                core.DownloadLibs(merged);
                core.EnsureAssets(merged);

                // 2) 包内文件
                JArray files = (JArray)idx["files"];
                int total = files.Count; int done = 0;
                foreach (JObject f in files)
                {
                    JToken env = f["env"];
                    if (env != null && (string)env["client"] == "unsupported") { done++; continue; }
                    string relPath = (string)f["path"];
                    if (string.IsNullOrEmpty(relPath)) { done++; continue; }
                    relPath = relPath.Replace('/', '\\');
                    if (relPath.Contains("..")) { done++; continue; }
                    string dest = Path.Combine(vDir, relPath);
                    if (!File.Exists(dest))
                    {
                        string d = Path.GetDirectoryName(dest);
                        if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
                        JArray urls = (JArray)f["downloads"];
                        string tmp = dest + ".tmp";
                        bool ok = false;
                        foreach (string u in urls)
                        {
                            try { Web.DownloadFile(u, tmp); ok = true; break; } catch { }
                        }
                        if (!ok) throw new Exception("整合包文件下载失败: " + relPath);
                        if (File.Exists(dest)) File.Delete(dest);
                        File.Move(tmp, dest);
                    }
                    done++;
                    SetProgress(30 + 60 * done / Math.Max(1, total));
                    if (done % 10 == 0) SetStatus("整合包文件 " + done + " / " + total);
                }

                // 3) overrides
                int ovCount = 0;
                foreach (ZipArchiveEntry e in zip.Entries)
                {
                    if (!e.FullName.StartsWith("overrides/", StringComparison.Ordinal) || e.FullName == "overrides/") continue;
                    string rel = e.FullName.Substring("overrides/".Length).Replace('/', '\\');
                    if (rel.Length == 0 || rel.Contains("..")) continue;
                    string dest = Path.Combine(vDir, rel);
                    string d = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
                    try { e.ExtractToFile(dest, true); ovCount++; } catch { }
                }
                Util.Log(LogBox, "整合包安装完成: " + vName + " (" + total + " 个文件, " + ovCount + " 个覆盖项)");
            }
            File.Delete(mrpack);
            SetProgress(100);
            SetStatus("整合包安装完成");
            return vName;
        }
    }

    // ---------- UI: 圆角按钮 ----------
    class RButton : Control
    {
        public Color Fill = Theme.Accent;
        public bool UseAccentOnHover = true;
        bool hover, down;
        public RButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Cursor = Cursors.Hand;
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            ForeColor = Color.White;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color c = Fill;
            if (hover) c = ControlPaint.Light(c, 0.12f);
            if (down) c = ControlPaint.Dark(c, 0.05f);
            using (GraphicsPath p = Util.Round(new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), 8))
            {
                using (SolidBrush b = new SolidBrush(c)) e.Graphics.FillPath(b, p);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? ForeColor : Color.FromArgb(160, 160, 160),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    }

    // ---------- UI: 深色滑块 (替代原生 TrackBar) ----------
    class MouseSlider : Control
    {
        public int Minimum = 512, Maximum = 8192;
        int val;
        bool dragging;
        public Action<int> OnValueChanged;

        public int Value
        {
            get { return val; }
            set { val = Math.Max(Minimum, Math.Min(Maximum, value)); Invalidate(); if (OnValueChanged != null) OnValueChanged(val); }
        }

        public MouseSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        int ThumbX()
        {
            float frac = (float)(val - Minimum) / Math.Max(1, Maximum - Minimum);
            return (int)(10 + frac * (Width - 20));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int cy = Height / 2;
            using (Pen p = new Pen(Color.FromArgb(70, 70, 84), 6))
                e.Graphics.DrawLine(p, 10, cy, Width - 10, cy);
            using (Pen p = new Pen(Theme.Accent, 6))
                e.Graphics.DrawLine(p, 10, cy, ThumbX(), cy);
            int tx = ThumbX();
            using (SolidBrush b = new SolidBrush(dragging ? ControlPaint.Light(Theme.Accent, 0.15f) : Theme.Accent))
                e.Graphics.FillEllipse(b, tx - 9, cy - 9, 18, 18);
            using (SolidBrush b = new SolidBrush(Color.White))
                e.Graphics.FillEllipse(b, tx - 4, cy - 4, 8, 8);
        }

        void Apply(MouseEventArgs e)
        {
            float frac = (e.X - 10f) / Math.Max(1, Width - 20);
            frac = Math.Max(0f, Math.Min(1f, frac));
            int nv = Minimum + (int)(frac * (Maximum - Minimum) / 256) * 256; // 256MB 步进
            Value = nv;
        }

        protected override void OnMouseDown(MouseEventArgs e) { dragging = true; Apply(e); Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragging) Apply(e); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; Invalidate(); base.OnMouseUp(e); }
    }

    // ---------- UI: 圆角进度条 (替代原生 ProgressBar) ----------
    class RoundProgress : Control
    {
        int val;
        public int Value
        {
            get { return val; }
            set { val = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }
        public RoundProgress()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath track = Util.Round(new RectangleF(0, 0, Width - 1, Height - 1), Height / 2))
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(46, 46, 56)))
                e.Graphics.FillPath(tb, track);
            int w = (int)((Width - 2) * (val / 100f));
            if (w >= Height / 2 && val > 0)
                using (GraphicsPath fill = Util.Round(new RectangleF(1, 1, Math.Max(Height - 2, w), Height - 2), (Height - 2) / 2))
                using (LinearGradientBrush gb = new LinearGradientBrush(new Rectangle(0, 0, w, Height), Theme.Accent, ControlPaint.Dark(Theme.Accent, 0.12f), 0f))
                    e.Graphics.FillPath(gb, fill);
        }
    }

    // ---------- UI: 侧栏导航项 ----------
    class NavItem : Control
    {
        public bool Active;
        bool hover;
        public string Icon;
        public string Title;
        public NavItem(string icon, string title)
        {
            Icon = icon; Title = title;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
            Height = 46;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (Active)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 40, 40, 54))) e.Graphics.FillRectangle(b, ClientRectangle);
                using (SolidBrush b = new SolidBrush(Theme.Accent)) e.Graphics.FillRectangle(b, 0, 8, 4, Height - 16);
            }
            else if (hover)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(35, 255, 255, 255))) e.Graphics.FillRectangle(b, ClientRectangle);
            }
            TextRenderer.DrawText(e.Graphics, Icon, new Font("Segoe UI Emoji", 13F), new Rectangle(14, 0, 34, Height),
                Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(e.Graphics, Title, Font, new Rectangle(52, 0, Width - 60, Height),
                Active ? Theme.Text : Theme.TextDim, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    }

    // ---------- 主窗口 ----------
    class MainForm : Form
    {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        Panel titleBar, sidebar, pageHost;
        NavItem navLaunch, navDownload, navMod, navSettings, navMp;
        Label titleLabel;
        Image LogoImg;
        // 启动页
        ComboBox versionBox, javaBox;
        TextBox nameBox, logBox;
        MouseSlider memTrack;
        Label memLabel, gameStatLab, javaNeedLab;
        RButton killGameBtn;
        Process gameProc;
        RButton launchBtn, refreshBtn, deleteBtn, javaBrowseBtn;
        // 下载页
        TextBox searchBox;
        ComboBox filterBox;
        ListView verList;
        RoundProgress bar;
        Label statusLab;
        RButton dlBtn, manifestBtn;
        ComboBox fabricBox;
        RButton fabricBtn;
        bool fabricFilled;
        JObject manifest;
        // Mod 页
        Panel modPage;
        TextBox modSearchBox;
        ComboBox modTypeBox, targetVerBox;
        ListView modResultList, modVerList;
        RoundProgress modBar;
        Label modStatusLab;
        RButton modSearchBtn, modInstallBtn;
        JArray curResults;
        // 设置页
        TextBox dirBox;
        RButton dirBrowseBtn, openDirBtn;

        public MainForm()
        {
            Settings.Load();

            Text = "老鼠启动器 by osaka";
            Width = 1120; Height = 640;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1120, 640);
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            try
            {
                using (System.IO.Stream st = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("icon-preview.png"))
                {
                    if (st != null)
                    {
                        System.IO.MemoryStream ms = new System.IO.MemoryStream();
                        st.CopyTo(ms);
                        LogoImg = Image.FromStream(ms);
                    }
                }
            }
            catch { }
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Region = new Region(Util.Round(new RectangleF(0, 0, Width, Height), 14)); // 圆角窗口
            FormClosing += delegate(object s, FormClosingEventArgs e2)
            {
                try { Region.Dispose(); } catch { }
            };
            Font = new Font("Microsoft YaHei UI", 9.5F);
            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.None;

            BuildChrome();
            BuildSidebar();
            pageHost = new Panel();
            pageHost.SetBounds(168, 46, Width - 168, Height - 46);
            pageHost.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pageHost.BackColor = Theme.Bg;
            Controls.Add(pageHost);

            BuildLaunchPage();
            BuildDownloadPage();
            BuildModPage();
            BuildSettingsPage();
            BuildMpPage();
            DarkCombo(javaBox); DarkCombo(versionBox); DarkCombo(filterBox); DarkCombo(fabricBox);
            DarkCombo(modTypeBox); DarkCombo(targetVerBox); DarkCombo(dirPresetBox);
            DarkList(verList); DarkList(modResultList); DarkList(modVerList);
            // 透明链: 页面与容器透明, 极光从窗体透出 (玻璃卡才生效)
            pageHost.BackColor = Color.Transparent;
            foreach (Panel pg in new Panel[] { launchPage, downloadPage, modPage, settingsPage, mpPage })
                pg.BackColor = Color.Transparent;
            StartLanWatcher();

            // 恢复上次窗口位置 (仅当落点在某个屏幕工作区内)
            try
            {
                string[] xy = (Settings.WinPos ?? "").Split(',');
                int sx, sy;
                if (xy.Length == 2 && int.TryParse(xy[0], out sx) && int.TryParse(xy[1], out sy))
                {
                    bool onScreen = false;
                    foreach (System.Windows.Forms.Screen sc in System.Windows.Forms.Screen.AllScreens)
                        if (sc.WorkingArea.Contains(sx, sy)) onScreen = true;
                    if (onScreen) { StartPosition = FormStartPosition.Manual; Location = new Point(sx, sy); }
                }
            }
            catch { }

            ShowPage(navLaunch);
            ScanJava();
            ScanVersions();
            Log("老鼠启动器 v1.1 就绪 by osaka  游戏目录: " + Settings.McDir);
            FormClosed += delegate { Settings.WinPos = Left + "," + Top; Settings.Save(); };
        }

        // 极光背景: 深底渐变 + 三团柔光 (画在窗体最底层, 玻璃卡透出)
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Rectangle r = ClientRectangle;
            if (r.Width <= 0 || r.Height <= 0) return;
            using (LinearGradientBrush g = new LinearGradientBrush(r, Color.FromArgb(22, 22, 34), Color.FromArgb(34, 34, 54), 72f))
                e.Graphics.FillRectangle(g, r);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            PaintGlow(e.Graphics, new Rectangle(560, -160, 720, 460), Theme.Accent, 70);
            PaintGlow(e.Graphics, new Rectangle(-180, 300, 560, 480), Color.FromArgb(150, 90, 220), 55);
            PaintGlow(e.Graphics, new Rectangle(430, 430, 620, 420), Color.FromArgb(60, 190, 170), 45);
            using (Pen p = new Pen(Color.FromArgb(90, 255, 255, 255), 1f))
                e.Graphics.DrawPath(p, Util.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 14));
        }
        void PaintGlow(Graphics g, Rectangle area, Color c, int alpha)
        {
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(area);
                using (PathGradientBrush b = new PathGradientBrush(gp))
                {
                    b.CenterColor = Color.FromArgb(alpha, c);
                    b.SurroundColors = new Color[] { Color.FromArgb(0, c) };
                    g.FillPath(b, gp);
                }
            }
        }

        // ComboBox 下拉列表深色化 (Flat 模式下弹出列表仍是系统白底)
        void DarkCombo(ComboBox cb)
        {
            cb.DrawMode = DrawMode.OwnerDrawFixed;
            cb.ItemHeight = 20;
            cb.DrawItem += delegate(object s, DrawItemEventArgs e)
            {
                e.DrawBackground();
                bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                using (SolidBrush b = new SolidBrush(sel ? Color.FromArgb(60, 74, 96) : Theme.Card)) e.Graphics.FillRectangle(b, e.Bounds);
                ComboBox box = (ComboBox)s;
                string t = e.Index >= 0 && box.Items.Count > e.Index ? (box.Items[e.Index] as string ?? "") : "";
                TextRenderer.DrawText(e.Graphics, t, new Font("Microsoft YaHei UI", 9F),
                    new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height),
                    Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };
        }

        // ListView 表头深色化 (行内容仍走系统默认绘制)
        void DarkList(ListView lv)
        {
            lv.OwnerDraw = true;
            lv.DrawColumnHeader += delegate(object s, DrawListViewColumnHeaderEventArgs e)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(44, 44, 54))) e.Graphics.FillRectangle(b, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                    new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height),
                    Theme.TextDim, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            };
            lv.DrawSubItem += delegate(object s, DrawListViewSubItemEventArgs e) { e.DrawDefault = true; };
            lv.Resize += delegate { PadEnd(lv); };
            lv.HandleCreated += delegate { try { lv.BeginInvoke((MethodInvoker)delegate { PadEnd(lv); }); } catch { } };
        }

        // 末列补满/收敛到剩余宽度 (双向: 垂直滚动条出现后 ClientWidth 变小需回缩)
        void PadEnd(ListView lv)
        {
            if (lv.Columns.Count == 0) return;
            int used = 0;
            foreach (ColumnHeader c in lv.Columns) used += c.Width;
            int diff = lv.ClientSize.Width - used;
            if (diff != 0)
            {
                int last = lv.Columns.Count - 1;
                int nw = lv.Columns[last].Width + diff;
                if (nw > 60) lv.Columns[last].Width = nw;
            }
        }

        // ----- 窗口框架 -----
        void BuildChrome()
        {
            titleBar = new Panel();
            titleBar.Dock = DockStyle.Top;
            titleBar.Height = 46;
            titleBar.BackColor = Color.FromArgb(215, 26, 26, 36);
            titleBar.MouseMove += TitleMove;
            titleBar.MouseDoubleClick += delegate { ToggleMax(); };
            titleBar.Paint += delegate(object s, PaintEventArgs e)
            {
                // 底部 2px 强调色渐变线 (左实右淡)
                using (LinearGradientBrush g = new LinearGradientBrush(new Rectangle(0, titleBar.Height - 2, titleBar.Width, 2), Theme.Accent, Color.FromArgb(30, Theme.Accent), 0f))
                    e.Graphics.FillRectangle(g, 0, titleBar.Height - 2, titleBar.Width, 2);
            };
            Controls.Add(titleBar);

            if (LogoImg != null)
            {
                PictureBox logoS = new PictureBox();
                logoS.Image = LogoImg;
                logoS.SetBounds(186, 11, 24, 24);
                logoS.SizeMode = PictureBoxSizeMode.Zoom;
                logoS.BackColor = Color.Transparent;
                titleBar.Controls.Add(logoS);
            }
            titleLabel = new Label();
            titleLabel.Text = "老鼠启动器 by osaka";
            titleLabel.Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold);
            titleLabel.ForeColor = Theme.Text;
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(218, 11);
            titleLabel.MouseMove += TitleMove;
            titleBar.Controls.Add(titleLabel);

            RButton minBtn = new RButton();
            minBtn.Text = "—"; minBtn.Font = new Font("Microsoft YaHei UI", 9F);
            minBtn.Fill = Color.FromArgb(60, 60, 70);
            minBtn.SetBounds(Width - 92, 9, 38, 28);
            minBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            minBtn.Click += delegate { WindowState = FormWindowState.Minimized; };
            titleBar.Controls.Add(minBtn);

            RButton closeBtn = new RButton();
            closeBtn.Text = "✕"; closeBtn.Font = new Font("Microsoft YaHei UI", 9F);
            closeBtn.Fill = Color.FromArgb(200, 72, 72);
            closeBtn.SetBounds(Width - 48, 9, 38, 28);
            closeBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            closeBtn.Click += delegate { Close(); };
            titleBar.Controls.Add(closeBtn);

            Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Color.FromArgb(20, 20, 24))) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            };
        }
        void TitleMove(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
        }
        void ToggleMax()
        {
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        }

        void BuildSidebar()
        {
            sidebar = new Panel();
            sidebar.Dock = DockStyle.Left;
            sidebar.Width = 168;
            sidebar.BackColor = Color.FromArgb(205, 26, 26, 36);
            Controls.Add(sidebar);

            if (LogoImg != null)
            {
                PictureBox logo = new PictureBox();
                logo.Image = LogoImg;
                logo.SetBounds(0, 12, 168, 56);
                logo.SizeMode = PictureBoxSizeMode.Zoom;
                sidebar.Controls.Add(logo);
            }
            else
            {
                Label logo = new Label();
                logo.Text = "🐭";
                logo.Font = new Font("Segoe UI Emoji", 26F);
                logo.ForeColor = Theme.Text;
                logo.AutoSize = false;
                logo.SetBounds(0, 12, 168, 52);
                logo.TextAlign = ContentAlignment.TopCenter;
                sidebar.Controls.Add(logo);
            }

            navLaunch = new NavItem("🚀", "启动");
            navLaunch.SetBounds(0, 74, 168, 46);
            navLaunch.Click += delegate { ShowPage(navLaunch); };
            navDownload = new NavItem("⬇", "下载");
            navDownload.SetBounds(0, 124, 168, 46);
            navDownload.Click += delegate { ShowPage(navDownload); LoadManifest(); };
            navMod = new NavItem("🧩", "Mod");
            navMod.SetBounds(0, 174, 168, 46);
            navMod.Click += delegate { ShowPage(navMod); FillTargetVersions(); };
            navSettings = new NavItem("⚙", "设置");
            navSettings.SetBounds(0, 224, 168, 46);
            navSettings.Click += delegate { ShowPage(navSettings); };
            navMp = new NavItem("🌐", "联机");
            navMp.SetBounds(0, 274, 168, 46);
            navMp.Click += delegate { ShowPage(navMp); };
            sidebar.Controls.Add(navLaunch);
            sidebar.Controls.Add(navDownload);
            sidebar.Controls.Add(navMod);
            sidebar.Controls.Add(navSettings);
            sidebar.Controls.Add(navMp);

            Label ver = new Label();
            ver.Text = "老鼠启动器 v1.1 by osaka";
            ver.ForeColor = Theme.TextDim;
            ver.Font = new Font("Microsoft YaHei UI", 8.5F);
            ver.AutoSize = false;
            ver.SetBounds(0, 0, 168, 24);
            ver.TextAlign = ContentAlignment.BottomCenter;
            ver.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            sidebar.Controls.Add(ver);
        }

        Panel curPage;
        void ShowPage(NavItem which)
        {
            navLaunch.Active = which == navLaunch;
            navDownload.Active = which == navDownload;
            navMod.Active = which == navMod;
            navSettings.Active = which == navSettings;
            navMp.Active = which == navMp;
            navLaunch.Invalidate(); navDownload.Invalidate(); navMod.Invalidate(); navSettings.Invalidate(); navMp.Invalidate();
            if (curPage != null) curPage.Visible = false;
            Panel target = null;
            if (which == navLaunch) target = launchPage;
            else if (which == navDownload) target = downloadPage;
            else if (which == navMod) target = modPage;
            else if (which == navMp) target = mpPage;
            else target = settingsPage;
            target.Visible = true;
            curPage = target;
        }

        // ----- 启动页 -----
        Panel launchPage;
        List<Panel> pageCards = new List<Panel>();
        Panel Card(Panel parent, string title, int x, int y, int w, int h)
        {
            Panel card = new Panel();
            card.SetBounds(x, y, w, h);
            card.BackColor = Color.FromArgb(165, 40, 40, 54); // 玻璃: 极光背景透出
            card.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath p = Util.Round(new RectangleF(0.5f, 0.5f, card.Width - 1, card.Height - 1), 12))
                {
                    using (Pen pen = new Pen(Color.FromArgb(90, 255, 255, 255))) e.Graphics.DrawPath(pen, p);
                }
                using (SolidBrush dot = new SolidBrush(Theme.Accent)) e.Graphics.FillEllipse(dot, 18, 15, 7, 7);
                TextRenderer.DrawText(e.Graphics, title, new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                    new Rectangle(34, 8, 320, 24), Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            };
            parent.Controls.Add(card);
            pageCards.Add(card);
            return card;
        }

        // 页面控件全部加入后调用: Controls.Add 追加到 z-order 底层,
        // 后加的内容控件会钻到先加的卡片下面, 必须最后统一把卡片压底
        void SinkCards()
        {
            foreach (Panel c in pageCards) c.SendToBack();
            pageCards.Clear();
        }

        void BuildLaunchPage()
        {
            launchPage = new Panel();
            launchPage.Dock = DockStyle.Fill;
            launchPage.BackColor = Theme.Bg;
            pageHost.Controls.Add(launchPage);

            // 玩家卡 (内容挂进卡片, 玻璃透明链才正确)
            Panel cardP = Card(launchPage, "玩家", 20, 14, 420, 96);
            if (LogoImg != null)
            {
                PictureBox avatar = new PictureBox();
                avatar.Image = LogoImg;
                avatar.SetBounds(14, 26, 52, 52);
                avatar.SizeMode = PictureBoxSizeMode.Zoom;
                avatar.BackColor = Color.Transparent;
                cardP.Controls.Add(avatar);
            }
            Label l1 = new Label(); l1.Text = "玩家名:"; l1.ForeColor = Theme.Text; l1.SetBounds(80, 32, 55, 22); l1.BackColor = Color.Transparent;
            nameBox = new TextBox(); nameBox.SetBounds(138, 29, 150, 26); nameBox.Text = Settings.PlayerName;
            nameBox.ForeColor = Theme.Text; nameBox.BackColor = Theme.CardLight; nameBox.BorderStyle = BorderStyle.FixedSingle;
            Label l2 = new Label(); l2.Text = "(离线登录)"; l2.ForeColor = Theme.TextDim; l2.SetBounds(296, 32, 80, 22); l2.BackColor = Color.Transparent;
            cardP.Controls.Add(l1); cardP.Controls.Add(nameBox); cardP.Controls.Add(l2);

            // Java 卡
            Panel cardJ = Card(launchPage, "Java 运行时", 454, 14, 462, 96);
            javaBox = new ComboBox();
            javaBox.SetBounds(16, 26, 340, 26);
            javaBox.DropDownStyle = ComboBoxStyle.DropDownList;
            javaBox.FlatStyle = FlatStyle.Flat;
            javaBox.BackColor = Theme.CardLight; javaBox.ForeColor = Theme.Text;
            javaBrowseBtn = new RButton();
            javaBrowseBtn.Text = "浏览...";
            javaBrowseBtn.Fill = Color.FromArgb(70, 70, 84);
            javaBrowseBtn.SetBounds(364, 25, 84, 30);
            javaBrowseBtn.Font = new Font("Microsoft YaHei UI", 9F);
            javaBrowseBtn.Click += delegate
            {
                using (FolderBrowserDialog d = new FolderBrowserDialog())
                {
                    if (d.ShowDialog(this) == DialogResult.OK)
                        javaBox.Items.Insert(0, Path.Combine(d.SelectedPath, "javaw.exe"));
                    if (javaBox.Items.Count > 0) javaBox.SelectedIndex = 0;
                }
            };
            cardJ.Controls.Add(javaBox); cardJ.Controls.Add(javaBrowseBtn);

            // 该版本对应的 Java 需求提示
            javaNeedLab = new Label();
            javaNeedLab.ForeColor = Theme.Green; javaNeedLab.BackColor = Color.Transparent;
            javaNeedLab.SetBounds(286, 10, 162, 20);
            javaNeedLab.TextAlign = ContentAlignment.MiddleRight;
            javaNeedLab.Font = new Font("Microsoft YaHei UI", 8.5F);
            cardJ.Controls.Add(javaNeedLab);

            // 版本卡
            Panel cardV = Card(launchPage, "游戏版本 (与 PCL 共用)", 20, 122, 620, 96);
            versionBox = new ComboBox();
            versionBox.SetBounds(16, 26, 260, 26);
            versionBox.DropDownStyle = ComboBoxStyle.DropDownList;
            versionBox.FlatStyle = FlatStyle.Flat;
            versionBox.BackColor = Theme.CardLight; versionBox.ForeColor = Theme.Text;
            versionBox.SelectedIndexChanged += delegate { UpdateJavaHint(); };
            refreshBtn = new RButton();
            refreshBtn.Text = "↻ 刷新";
            refreshBtn.Fill = Color.FromArgb(70, 70, 84);
            refreshBtn.Font = new Font("Microsoft YaHei UI", 9F);
            refreshBtn.SetBounds(286, 25, 80, 30);
            refreshBtn.Click += delegate { ScanVersions(); };
            deleteBtn = new RButton();
            deleteBtn.Text = "🗑 删除";
            deleteBtn.Fill = Color.FromArgb(150, 70, 70);
            deleteBtn.Font = new Font("Microsoft YaHei UI", 9F);
            deleteBtn.SetBounds(376, 25, 84, 30);
            deleteBtn.Click += delegate { DeleteVersion(); };
            Label l3 = new Label(); l3.Text = "删除会移除该版本的文件夹(不影响存档)"; l3.ForeColor = Theme.TextDim;
            l3.SetBounds(16, 62, 400, 18); l3.BackColor = Color.Transparent; l3.Font = new Font("Microsoft YaHei UI", 8F);
            cardV.Controls.Add(versionBox); cardV.Controls.Add(refreshBtn); cardV.Controls.Add(deleteBtn); cardV.Controls.Add(l3);

            // 内存卡
            Panel cardM = Card(launchPage, "内存", 654, 122, 262, 96);
            memLabel = new Label();
            memLabel.Text = Settings.MemoryMB + " MB";
            memLabel.ForeColor = Theme.Text;
            memLabel.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            memLabel.SetBounds(212, 40, 70, 24);
            memLabel.BackColor = Color.Transparent;
            memTrack = new MouseSlider();
            memTrack.SetBounds(8, 28, 198, 44);
            memTrack.Minimum = 512; memTrack.Maximum = 8192;
            memTrack.OnValueChanged = delegate(int v)
            {
                memLabel.Text = v + " MB";
                Settings.MemoryMB = v;
            };
            memTrack.Value = Math.Max(512, Math.Min(8192, Settings.MemoryMB));
            cardM.Controls.Add(memTrack); cardM.Controls.Add(memLabel);

            // 启动按钮
            launchBtn = new RButton();
            launchBtn.Text = "▶  启 动 游 戏";
            launchBtn.Fill = Theme.Green;
            launchBtn.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            launchBtn.SetBounds(20, 230, 620, 52);
            launchBtn.Click += delegate { DoLaunch(); };
            launchPage.Controls.Add(launchBtn);

            // 日志卡
            Panel cardL = Card(launchPage, "运行日志", 20, 284, 896, 288);
            gameStatLab = new Label();
            gameStatLab.Text = "○ 未运行";
            gameStatLab.ForeColor = Theme.TextDim;
            gameStatLab.BackColor = Color.Transparent;
            gameStatLab.SetBounds(770, 9, 110, 20);
            gameStatLab.TextAlign = ContentAlignment.MiddleRight;
            cardL.Controls.Add(gameStatLab);
            killGameBtn = new RButton();
            killGameBtn.Text = "⛔ 结束游戏";
            killGameBtn.Fill = Color.FromArgb(150, 70, 70);
            killGameBtn.Font = new Font("Microsoft YaHei UI", 8.5F);
            killGameBtn.SetBounds(652, 7, 110, 24);
            killGameBtn.Visible = false;
            killGameBtn.Click += delegate
            {
                try
                {
                    if (gameProc != null && !gameProc.HasExited) { gameProc.Kill(); Log("已强制结束游戏"); }
                }
                catch { }
            };
            cardL.Controls.Add(killGameBtn);
            logBox = new TextBox();
            logBox.Multiline = true; logBox.ReadOnly = true; logBox.ScrollBars = ScrollBars.Vertical;
            logBox.BackColor = Theme.Console; logBox.ForeColor = Color.FromArgb(210, 230, 210);
            logBox.Font = new Font("Consolas", 9F);
            logBox.SetBounds(10, 36, 876, 240);
            cardL.Controls.Add(logBox);
            SinkCards();
        }

        // ----- 下载页 -----
        Panel downloadPage;
        void BuildDownloadPage()
        {
            downloadPage = new Panel();
            downloadPage.Dock = DockStyle.Fill;
            downloadPage.BackColor = Theme.Bg;
            downloadPage.Visible = false;
            pageHost.Controls.Add(downloadPage);

            Panel cardD = Card(downloadPage, "下载原版", 20, 14, 894, 548);

            searchBox = new TextBox();
            searchBox.SetBounds(16, 38, 240, 28);
            searchBox.BackColor = Theme.Card; searchBox.ForeColor = Theme.Text; searchBox.BorderStyle = BorderStyle.FixedSingle;
            Label ph = new Label(); ph.Text = "🔍 搜索版本号..."; ph.ForeColor = Theme.TextDim; ph.SetBounds(6, 4, 130, 20); ph.BackColor = Color.Transparent;
            searchBox.Controls.Add(ph);
            searchBox.TextChanged += delegate { ph.Visible = string.IsNullOrEmpty(searchBox.Text); FillVerList(); };
            searchBox.GotFocus += delegate { ph.Visible = string.IsNullOrEmpty(searchBox.Text); };

            filterBox = new ComboBox();
            filterBox.SetBounds(266, 38, 130, 28);
            filterBox.DropDownStyle = ComboBoxStyle.DropDownList;
            filterBox.FlatStyle = FlatStyle.Flat;
            filterBox.BackColor = Theme.Card; filterBox.ForeColor = Theme.Text;
            filterBox.Items.AddRange(new object[] { "仅正式版", "正式版+快照", "全部" });
            filterBox.SelectedIndex = 0;
            filterBox.SelectedIndexChanged += delegate { FillVerList(); };

            manifestBtn = new RButton();
            manifestBtn.Text = "↻ 刷新清单";
            manifestBtn.Fill = Color.FromArgb(70, 70, 84);
            manifestBtn.Font = new Font("Microsoft YaHei UI", 9F);
            manifestBtn.SetBounds(406, 37, 100, 30);
            manifestBtn.Click += delegate { LoadManifest(true); };

            Label fLab = new Label(); fLab.Text = "Fabric:"; fLab.ForeColor = Theme.TextDim;
            fLab.BackColor = Color.Transparent; fLab.SetBounds(520, 43, 46, 20);
            fabricBox = new ComboBox();
            fabricBox.SetBounds(568, 38, 110, 28);
            fabricBox.DropDownStyle = ComboBoxStyle.DropDownList;
            fabricBox.FlatStyle = FlatStyle.Flat;
            fabricBox.BackColor = Theme.Card; fabricBox.ForeColor = Theme.Text;
            fabricBox.Items.Add("最新");
            fabricBox.SelectedIndex = 0;
            fabricBtn = new RButton();
            fabricBtn.Text = "🧵 装Fabric";
            fabricBtn.Fill = Color.FromArgb(70, 70, 84);
            fabricBtn.Font = new Font("Microsoft YaHei UI", 9F);
            fabricBtn.SetBounds(684, 37, 104, 30);
            fabricBtn.Click += delegate { InstallFabricClick(); };

            verList = new ListView();
            verList.SetBounds(16, 76, 862, 384);
            verList.View = View.Details; verList.FullRowSelect = true; verList.HideSelection = false;
            verList.BorderStyle = BorderStyle.None;
            verList.BackColor = Theme.Card; verList.ForeColor = Theme.Text;
            verList.Columns.Add("版本", 220);
            verList.Columns.Add("类型", 100);
            verList.Columns.Add("发布时间", 160);
            verList.Columns.Add("状态", 140);
            verList.SelectedIndexChanged += delegate
            {
                if (verList.SelectedItems.Count > 0) statusLab.Text = "已选择: " + verList.SelectedItems[0].Text;
            };
            verList.DoubleClick += delegate { if (verList.SelectedItems.Count > 0) StartDownload(); };

            bar = new RoundProgress();
            bar.SetBounds(16, 476, 500, 16);
            statusLab = new Label();
            statusLab.Text = manifest == null ? "提示: 点击「刷新清单」获取版本列表" : "";
            statusLab.ForeColor = Theme.TextDim;
            statusLab.BackColor = Color.Transparent;
            statusLab.SetBounds(16, 500, 500, 20);

            dlBtn = new RButton();
            dlBtn.Text = "⬇  下载选中版本";
            dlBtn.Fill = Theme.Accent;
            dlBtn.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            dlBtn.SetBounds(676, 462, 202, 46);
            dlBtn.Click += delegate
            {
                if (verList.SelectedItems.Count == 0)
                {
                    statusLab.Text = "请先在上方列表点选一个版本, 再点下载";
                    return;
                }
                StartDownload();
            };

            cardD.Controls.AddRange(new Control[] { searchBox, filterBox, manifestBtn, fLab, fabricBox, fabricBtn, verList, bar, statusLab, dlBtn });
        }

        void LoadManifest() { LoadManifest(false); }
        void LoadManifest(bool force)
        {
            if (manifest != null && !force) { FillVerList(); return; }
            statusLab.Text = "正在获取版本清单...";
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    string json = new VanillaDownloader(Settings.McDir, null, null, null).GetManifestJson();
                    JObject m = JObject.Parse(json);
                    BeginInvoke((MethodInvoker)delegate
                    {
                        manifest = m;
                        statusLab.Text = "清单已获取 ✓  最新正式版: " + (string)manifest["latest"]["release"];
                        FillVerList();
                    });
                }
                catch (Exception ex)
                {
                    BeginInvoke((MethodInvoker)delegate { statusLab.Text = "获取清单失败: " + ex.Message; });
                }
            });
        }

        bool Installed(string id)
        {
            return File.Exists(Path.Combine(Settings.McDir, "versions", id, id + ".json"));
        }

        void FillVerList()
        {
            if (manifest == null) return;
            if (!fabricFilled)
            {
                fabricFilled = true;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        JArray ls = JArray.Parse(Web.GetText("https://meta.fabricmc.net/v2/versions/loader"));
                        int added = 0;
                        foreach (JObject l in ls)
                        {
                            if (!(bool)l["stable"]) continue;
                            BeginInvoke((MethodInvoker)delegate
                            {
                                try { fabricBox.Items.Add((string)l["version"]); } catch { }
                            });
                            if (++added >= 10) break;
                        }
                    }
                    catch { }
                });
            }
            verList.Items.Clear();
            int mode = filterBox.SelectedIndex;
            string q = searchBox.Text == null ? "" : searchBox.Text.Trim().ToLowerInvariant();
            foreach (JObject v in (JArray)manifest["versions"])
            {
                string id = (string)v["id"];
                string type = (string)v["type"];
                if (mode == 0 && type != "release") continue;
                if (mode == 1 && type != "release" && type != "snapshot") continue;
                if (q.Length > 0 && !id.ToLowerInvariant().Contains(q)) continue;
                ListViewItem it = new ListViewItem(id);
                string typeCn = type == "release" ? "正式版" : (type == "snapshot" ? "快照" : type);
                it.SubItems.Add(typeCn);
                string t = (string)v["releaseTime"];
                it.SubItems.Add(t != null && t.Length >= 10 ? t.Substring(0, 10) : (t ?? ""));
                bool ins = Installed(id);
                it.SubItems.Add(ins ? "✓ 已安装" : "可下载");
                it.ForeColor = ins ? Theme.Green : Theme.Text;
                it.Tag = (string)v["url"];
                verList.Items.Add(it);
                if (verList.Items.Count >= 200) break;
            }
            foreach (ColumnHeader ch in verList.Columns) ch.Width = ch.Width == 60 ? -2 : ch.Width;
            PadEnd(verList);
        }

        void InstallFabricClick()
        {
            if (verList.SelectedItems.Count == 0)
            {
                statusLab.Text = "先在列表选中一个原版版本, 再装 Fabric";
                return;
            }
            string mcVer = verList.SelectedItems[0].Text;
            string fVer = fabricBox.SelectedIndex <= 0 ? null : (string)fabricBox.SelectedItem;
            fabricBtn.Enabled = false; dlBtn.Enabled = false; manifestBtn.Enabled = false; verList.Enabled = false;
            Log("为 " + mcVer + " 安装 Fabric" + (fVer != null ? " " + fVer : "") + " ...");
            statusLab.Text = "安装 Fabric...";
            ModInstaller mi = new ModInstaller(Settings.McDir, logBox,
                delegate(int pct) { try { BeginInvoke((MethodInvoker)delegate { bar.Value = Math.Min(100, Math.Max(0, pct)); }); } catch { } },
                delegate(string s) { try { BeginInvoke((MethodInvoker)delegate { statusLab.Text = s; }); } catch { } });
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    if (fVer == null)
                    {
                        JArray ls = JArray.Parse(Web.GetText("https://meta.fabricmc.net/v2/versions/loader"));
                        foreach (JObject l in ls) if ((bool)l["stable"]) { fVer = (string)l["version"]; break; }
                    }
                    string vName = mi.InstallFabric(mcVer, fVer);
                    BeginInvoke((MethodInvoker)delegate
                    {
                        statusLab.Text = "Fabric 安装完成 ✓";
                        bar.Value = 0;
                        fabricBtn.Enabled = true; dlBtn.Enabled = true; manifestBtn.Enabled = true; verList.Enabled = true;
                        ScanVersions();
                        FillVerList();
                        MessageBox.Show(this, "Fabric 版本 " + vName + " 安装完成!\n到「Mod」页选它就能搜 mod 直接安装,\n或回「启动」页选择它启动。", "老鼠启动器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    });
                }
                catch (Exception ex)
                {
                    Log("Fabric 安装失败: " + ex.Message);
                    BeginInvoke((MethodInvoker)delegate
                    {
                        statusLab.Text = "Fabric 安装失败: " + ex.Message;
                        fabricBtn.Enabled = true; dlBtn.Enabled = true; manifestBtn.Enabled = true; verList.Enabled = true;
                    });
                }
            });
        }

        void StartDownload()
        {
            if (verList.SelectedItems.Count == 0) return;
            string id = verList.SelectedItems[0].Text;
            string url = (string)verList.SelectedItems[0].Tag;
            if (url == null) return;
            dlBtn.Enabled = false; manifestBtn.Enabled = false; verList.Enabled = false; searchBox.Enabled = false; filterBox.Enabled = false;
            Log("开始下载原版 " + id + " ...");
            VanillaDownloader dl = new VanillaDownloader(Settings.McDir, logBox,
                delegate(int pct) { try { BeginInvoke((MethodInvoker)delegate { bar.Value = Math.Min(100, Math.Max(0, pct)); }); } catch { } },
                delegate(string s) { try { BeginInvoke((MethodInvoker)delegate { statusLab.Text = s; }); } catch { } });
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    dl.Download(id, url);
                    // 自动准备该版本对应的 Java 运行时
                    try
                    {
                        string comp; int mj;
                        JavaRuntime.Requirement(Path.Combine(Settings.McDir, "versions", id, id + ".json"), out comp, out mj);
                        if (!File.Exists(Path.Combine(JavaRuntime.RuntimeRoot(), comp, "bin", "javaw.exe")))
                        {
                            Log("该版本需要 Java " + mj + ", 自动下载官方运行时 (" + comp + ")...");
                            JavaRuntime.Ensure(comp, delegate(int pct, string s)
                            {
                                try { BeginInvoke((MethodInvoker)delegate { bar.Value = Math.Min(100, Math.Max(0, pct)); statusLab.Text = s; }); } catch { }
                            });
                            Log("Java 运行时就绪 (" + comp + ")");
                        }
                    }
                    catch (Exception jex) { Log("Java 自动下载失败: " + jex.Message); }
                    BeginInvoke((MethodInvoker)delegate
                    {
                        MessageBox.Show(this, "版本 " + id + " 下载完成!\n回到「启动」页选择该版本即可游玩。", "老鼠启动器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        bar.Value = 0;
                        statusLab.Text = "下载完成 ✓";
                        dlBtn.Enabled = true; manifestBtn.Enabled = true; verList.Enabled = true; searchBox.Enabled = true; filterBox.Enabled = true;
                        FillVerList();
                        ScanVersions();
                    });
                }
                catch (Exception ex)
                {
                    Log("下载失败: " + ex.Message);
                    BeginInvoke((MethodInvoker)delegate
                    {
                        statusLab.Text = "下载失败: " + ex.Message;
                        dlBtn.Enabled = true; manifestBtn.Enabled = true; verList.Enabled = true; searchBox.Enabled = true; filterBox.Enabled = true;
                    });
                }
            });
        }

        // ----- Mod / 整合包页 -----
        void BuildModPage()
        {
            modPage = new Panel();
            modPage.Dock = DockStyle.Fill;
            modPage.BackColor = Theme.Bg;
            modPage.Visible = false;
            pageHost.Controls.Add(modPage);

            Panel cardM = Card(modPage, "Mod / 整合包 / 资源包 / 光影", 20, 14, 894, 548);

            modTypeBox = new ComboBox();
            modTypeBox.SetBounds(16, 38, 110, 28);
            modTypeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            modTypeBox.FlatStyle = FlatStyle.Flat;
            modTypeBox.BackColor = Theme.Card; modTypeBox.ForeColor = Theme.Text;
            modTypeBox.Items.AddRange(new object[] { "Mod", "整合包", "资源包", "光影" });
            modTypeBox.SelectedIndex = 0;
            modTypeBox.SelectedIndexChanged += delegate
            {
                if (modResultList == null || modVerList == null) return;
                modResultList.Items.Clear(); modVerList.Items.Clear(); curResults = null;
            };

            modSearchBox = new TextBox();
            modSearchBox.SetBounds(136, 38, 300, 28);
            modSearchBox.BackColor = Theme.Card; modSearchBox.ForeColor = Theme.Text; modSearchBox.BorderStyle = BorderStyle.FixedSingle;
            modSearchBox.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ModSearch(); } };

            modSearchBtn = new RButton();
            modSearchBtn.Text = "🔍 搜索";
            modSearchBtn.Font = new Font("Microsoft YaHei UI", 9F);
            modSearchBtn.Fill = Theme.Accent;
            modSearchBtn.SetBounds(446, 37, 90, 30);
            modSearchBtn.Click += delegate { ModSearch(); };

            Label tLab = new Label(); tLab.Text = "安装到:"; tLab.ForeColor = Theme.TextDim; tLab.SetBounds(546, 43, 52, 20); tLab.BackColor = Color.Transparent;
            targetVerBox = new ComboBox();
            targetVerBox.SetBounds(600, 38, 278, 28);
            targetVerBox.DropDownStyle = ComboBoxStyle.DropDownList;
            targetVerBox.FlatStyle = FlatStyle.Flat;
            targetVerBox.BackColor = Theme.Card; targetVerBox.ForeColor = Theme.Text;

            modResultList = new ListView();
            modResultList.SetBounds(16, 76, 460, 384);
            modResultList.View = View.Details; modResultList.FullRowSelect = true; modResultList.HideSelection = false;
            modResultList.BorderStyle = BorderStyle.None;
            modResultList.BackColor = Theme.Card; modResultList.ForeColor = Theme.Text;
            modResultList.Columns.Add("名称", 170);
            modResultList.Columns.Add("作者", 100);
            modResultList.Columns.Add("下载量", 80);
            modResultList.ItemSelectionChanged += delegate { if (modResultList.SelectedItems.Count > 0) LoadModVersions(); };

            modVerList = new ListView();
            modVerList.SetBounds(486, 76, 392, 384);
            modVerList.View = View.Details; modVerList.FullRowSelect = true; modVerList.HideSelection = false;
            modVerList.BorderStyle = BorderStyle.None;
            modVerList.BackColor = Theme.Card; modVerList.ForeColor = Theme.Text;
            modVerList.Columns.Add("版本", 170);
            modVerList.Columns.Add("支持MC", 120);
            modVerList.Columns.Add("加载器", 90);
            modVerList.SelectedIndexChanged += delegate { modInstallBtn.Enabled = modVerList.SelectedItems.Count > 0; };

            modBar = new RoundProgress();
            modBar.SetBounds(16, 476, 400, 16);
            modStatusLab = new Label();
            modStatusLab.Text = "来自 Modrinth · 搜索后选择工程, 再选择文件版本";
            modStatusLab.ForeColor = Theme.TextDim;
            modStatusLab.SetBounds(16, 500, 560, 20);
            modStatusLab.BackColor = Color.Transparent;

            modInstallBtn = new RButton();
            modInstallBtn.Text = "⬇  安装";
            modInstallBtn.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            modInstallBtn.Fill = Theme.Accent;
            modInstallBtn.SetBounds(676, 462, 202, 46);
            modInstallBtn.Enabled = false;
            modInstallBtn.Click += delegate { InstallSelected(); };

            cardM.Controls.AddRange(new Control[] { modTypeBox, modSearchBox, modSearchBtn, tLab, targetVerBox,
                modResultList, modVerList, modBar, modStatusLab, modInstallBtn });
        }

        void FillTargetVersions()
        {
            string old = targetVerBox.SelectedItem as string;
            targetVerBox.Items.Clear();
            List<string> loaders = new List<string>(), plain = new List<string>();
            try
            {
                foreach (string dir in Directory.GetDirectories(Path.Combine(Settings.McDir, "versions")))
                {
                    string name = Path.GetFileName(dir);
                    string jp = Path.Combine(dir, name + ".json");
                    if (!File.Exists(jp)) continue;
                    // 加载器版本排在前面, mod 才有得装
                    string js = File.ReadAllText(jp, Encoding.UTF8);
                    bool isLoader = js.IndexOf("fabric", StringComparison.OrdinalIgnoreCase) >= 0 || js.IndexOf("forge", StringComparison.OrdinalIgnoreCase) >= 0 || js.IndexOf("neoforge", StringComparison.OrdinalIgnoreCase) >= 0;
                    (isLoader ? loaders : plain).Add(name);
                }
            }
            catch { }
            foreach (string n in loaders) targetVerBox.Items.Add(n);
            foreach (string n in plain) targetVerBox.Items.Add(n);
            if (old != null && targetVerBox.Items.Contains(old)) targetVerBox.SelectedItem = old;
            else if (loaders.Count > 0) targetVerBox.SelectedIndex = 0;
            else if (targetVerBox.Items.Count > 0) targetVerBox.SelectedIndex = 0;
        }

        void ModSearch()
        {
            string ptype = "mod";
            if (modTypeBox.SelectedIndex == 1) ptype = "modpack";
            else if (modTypeBox.SelectedIndex == 2) ptype = "resourcepack";
            else if (modTypeBox.SelectedIndex == 3) ptype = "shader";
            modStatusLab.Text = "搜索中...";
            modSearchBtn.Enabled = false;
            string q = modSearchBox.Text;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    JArray hits = Modrinth.Search(q, ptype);
                    BeginInvoke((MethodInvoker)delegate
                    {
                        curResults = hits;
                        modResultList.Items.Clear();
                        modVerList.Items.Clear();
                        foreach (JObject h in hits)
                        {
                            ListViewItem it = new ListViewItem((string)h["title"]);
                            it.SubItems.Add((string)h["author"]);
                            long dl = h["downloads"] == null ? 0 : (long)h["downloads"];
                            it.SubItems.Add(dl >= 1000000 ? (dl / 1000000.0).ToString("0.#") + "M" : (dl >= 1000 ? (dl / 1000.0).ToString("0.#") + "K" : dl.ToString()));
                            it.Tag = (string)h["project_id"];
                            modResultList.Items.Add(it);
                        }
                        modStatusLab.Text = "找到 " + hits.Count + " 个结果 (来自 Modrinth)";
                        modSearchBtn.Enabled = true;
                    });
                }
                catch (Exception ex)
                {
                    BeginInvoke((MethodInvoker)delegate { modStatusLab.Text = "搜索失败: " + ex.Message; modSearchBtn.Enabled = true; });
                }
            });
        }

        void LoadModVersions()
        {
            if (modResultList.SelectedItems.Count == 0) return;
            string pid = (string)modResultList.SelectedItems[0].Tag;
            modStatusLab.Text = "获取工程版本列表...";
            modVerList.Items.Clear();
            modInstallBtn.Enabled = false;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    JArray vs = Modrinth.Versions(pid);
                    BeginInvoke((MethodInvoker)delegate
                    {
                        foreach (JObject v in vs)
                        {
                            if (modVerList.Items.Count >= 40) break;
                            ListViewItem it = new ListViewItem((string)v["version_number"]);
                            JArray gv = (JArray)v["game_versions"];
                            string gvs = gv.Count > 0 ? string.Join(",", gv.Take(3).Select(g => (string)g).ToArray()) + (gv.Count > 3 ? "…" : "") : "";
                            it.SubItems.Add(gvs);
                            JArray ld = (JArray)v["loaders"];
                            it.SubItems.Add(ld.Count > 0 ? string.Join(",", ld.Select(l => (string)l).ToArray()) : "-");
                            it.Tag = v;
                            modVerList.Items.Add(it);
                        }
                        modStatusLab.Text = "共 " + modVerList.Items.Count + " 个版本, 选择一个安装";
                    });
                }
                catch (Exception ex)
                {
                    BeginInvoke((MethodInvoker)delegate { modStatusLab.Text = "获取版本失败: " + ex.Message; });
                }
            });
        }

        void InstallSelected()
        {
            if (modVerList.SelectedItems.Count == 0) return;
            JObject versionEntry = (JObject)modVerList.SelectedItems[0].Tag;
            string title = modResultList.SelectedItems.Count > 0 ? (string)modResultList.SelectedItems[0].Text : versionEntry["version_number"].ToString();
            bool isPack = modTypeBox.SelectedIndex == 1;
            string target = targetVerBox.SelectedItem as string;
            if (!isPack && string.IsNullOrEmpty(target))
            {
                MessageBox.Show(this, "请先选择要安装到的版本"); return;
            }
            if (!isPack && modTypeBox.SelectedIndex == 0)
            {
                ModInstaller mi0 = new ModInstaller(Settings.McDir, logBox, null, null);
                if (!mi0.IsLoaderVersion(target))
                {
                    MessageBox.Show(this, "所选版本 \"" + target + "\" 是原版, 无法加载 Mod。\n请先安装带 Fabric/NeoForge 的版本, 或去下载页安装整合包。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            modInstallBtn.Enabled = false; modSearchBtn.Enabled = false;
            string subfolder = "mods";
            if (modTypeBox.SelectedIndex == 2) subfolder = "resourcepacks";
            else if (modTypeBox.SelectedIndex == 3) subfolder = "shaderpacks";
            Log("开始安装: " + title + (isPack ? " (整合包)" : " → " + target + "/" + subfolder));
            ModInstaller mi = new ModInstaller(Settings.McDir, logBox,
                delegate(int pct) { try { BeginInvoke((MethodInvoker)delegate { modBar.Value = Math.Min(100, Math.Max(0, pct)); }); } catch { } },
                delegate(string s) { try { BeginInvoke((MethodInvoker)delegate { modStatusLab.Text = s; }); } catch { } });
            bool pack = isPack;
            string tgt = target;
            string subf = subfolder;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    if (pack)
                    {
                        string newVer = mi.InstallModpack(versionEntry);
                        BeginInvoke((MethodInvoker)delegate
                        {
                            MessageBox.Show(this, "整合包安装完成!\n新版本: " + newVer + "\n去「启动」页选择它即可游玩。", "老鼠启动器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            ScanVersions();
                            FillTargetVersions();
                        });
                    }
                    else
                    {
                        mi.InstallFile(versionEntry, tgt, subf);
                        BeginInvoke((MethodInvoker)delegate
                        {
                            MessageBox.Show(this, "安装完成!\n" + title + " → " + tgt + "\\" + subf, "老鼠启动器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log("安装失败: " + ex.Message);
                    BeginInvoke((MethodInvoker)delegate { modStatusLab.Text = "安装失败: " + ex.Message; });
                }
                BeginInvoke((MethodInvoker)delegate
                {
                    modInstallBtn.Enabled = true; modSearchBtn.Enabled = true;
                    modStatusLab.Text = pack ? "整合包安装结束" : "安装结束";
                });
            });
        }

        // ----- 设置页 -----
        Panel settingsPage;
        // ----- 联机页 -----
        Panel mpPage;
        MpHost mpHost; MpJoin mpJoin;
        TextBox mpPortBox, mpCodeOut, mpCodeIn;
        RButton mpHostBtn, mpCopyBtn, mpJoinBtn;
        Label mpHostStat, mpJoinStat;
        UdpClient lanWatch;

        void BuildMpPage()
        {
            mpPage = new Panel();
            mpPage.Dock = DockStyle.Fill;
            mpPage.BackColor = Theme.Bg;
            mpPage.Visible = false;
            pageHost.Controls.Add(mpPage);

            Panel cardH = Card(mpPage, "创建房间 (你是房主)", 20, 14, 440, 300);
            Label h1 = new Label(); h1.Text = "1. 进入单人世界按 Esc → 「对局域网开放」\n2. 游戏端口会自动识别 (也可手动填)\n3. 点创建, 把邀请码发给朋友";
            h1.ForeColor = Theme.TextDim; h1.BackColor = Color.Transparent; h1.SetBounds(16, 26, 400, 54);
            Label h2 = new Label(); h2.Text = "游戏端口:"; h2.ForeColor = Theme.Text; h2.BackColor = Color.Transparent; h2.SetBounds(16, 90, 66, 22);
            mpPortBox = new TextBox(); mpPortBox.SetBounds(86, 87, 90, 24); mpPortBox.Text = "25565";
            mpPortBox.ForeColor = Theme.Text; mpPortBox.BackColor = Theme.CardLight; mpPortBox.BorderStyle = BorderStyle.FixedSingle;
            Label h3 = new Label(); h3.Text = "(开「对局域网开放」后自动填入)"; h3.ForeColor = Theme.TextDim; h3.BackColor = Color.Transparent; h3.SetBounds(184, 90, 240, 22);
            mpHostBtn = new RButton(); mpHostBtn.Text = "🌐 创建房间"; mpHostBtn.Fill = Theme.Green;
            mpHostBtn.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            mpHostBtn.SetBounds(16, 124, 130, 34);
            mpHostBtn.Click += delegate { ToggleHost(); };
            mpCodeOut = new TextBox(); mpCodeOut.Multiline = true; mpCodeOut.ReadOnly = true;
            mpCodeOut.SetBounds(16, 168, 408, 56); mpCodeOut.Font = new Font("Consolas", 8.5F);
            mpCodeOut.ForeColor = Theme.Text; mpCodeOut.BackColor = Theme.CardLight; mpCodeOut.BorderStyle = BorderStyle.FixedSingle;
            mpCodeOut.Text = "(邀请码生成后显示在这里)";
            mpCopyBtn = new RButton(); mpCopyBtn.Text = "📋 复制邀请码"; mpCopyBtn.Fill = Color.FromArgb(70, 70, 84);
            mpCopyBtn.Font = new Font("Microsoft YaHei UI", 9F);
            mpCopyBtn.SetBounds(156, 124, 116, 34);
            mpCopyBtn.Click += delegate
            {
                if (mpHost == null || string.IsNullOrEmpty(mpHost.InviteCode)) return;
                try { Clipboard.SetText(mpHost.InviteCode); mpHostStat.Text = "邀请码已复制到剪贴板 ✓"; } catch { }
            };
            mpHostStat = new Label(); mpHostStat.Text = "未创建"; mpHostStat.ForeColor = Theme.TextDim;
            mpHostStat.BackColor = Color.Transparent; mpHostStat.SetBounds(16, 230, 408, 60);

            Panel cardJ2 = Card(mpPage, "加入房间 (你是客人)", 480, 14, 434, 300);
            Label j1 = new Label(); j1.Text = "把房主发来的邀请码粘到这里, 加入后照常进游戏:";
            j1.ForeColor = Theme.TextDim; j1.BackColor = Color.Transparent; j1.SetBounds(16, 26, 400, 20);
            mpCodeIn = new TextBox(); mpCodeIn.Multiline = true; mpCodeIn.SetBounds(16, 52, 400, 84);
            mpCodeIn.Font = new Font("Consolas", 8.5F);
            mpCodeIn.ForeColor = Theme.Text; mpCodeIn.BackColor = Theme.CardLight; mpCodeIn.BorderStyle = BorderStyle.FixedSingle;
            mpJoinBtn = new RButton(); mpJoinBtn.Text = "🚪 加入房间"; mpJoinBtn.Fill = Theme.Accent;
            mpJoinBtn.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            mpJoinBtn.SetBounds(16, 144, 130, 34);
            mpJoinBtn.Click += delegate { ToggleJoin(); };
            mpJoinStat = new Label(); mpJoinStat.Text = ""; mpJoinStat.ForeColor = Theme.TextDim;
            mpJoinStat.BackColor = Color.Transparent; mpJoinStat.SetBounds(16, 188, 400, 100);

            Panel cardN = Card(mpPage, "说明", 20, 330, 894, 150);
            Label note = new Label();
            note.Text = "· P2P 打洞直连: 不经任何服务器, 延迟取决于双方网络; 无需注册账号。\n" +
                "· 双方都用老鼠启动器: 房主「创建房间」, 朋友粘贴邀请码「加入房间」, 然后在 MC 多人游戏里直接连接提示的地址。\n" +
                "· 若长时间打洞失败: 多为运营商 NAT 限制, 换房主/换网络重建房间再试; Windows 防火弹窗请点允许。\n" +
                "· 同一个房间邀请码可发给多人, 房主端会显示已连入人数。";
            note.ForeColor = Theme.TextDim; note.BackColor = Color.Transparent; note.SetBounds(16, 16, 860, 120);

            cardH.Controls.Add(h1); cardH.Controls.Add(h2); cardH.Controls.Add(mpPortBox); cardH.Controls.Add(h3);
            cardH.Controls.Add(mpHostBtn); cardH.Controls.Add(mpCodeOut); cardH.Controls.Add(mpCopyBtn); cardH.Controls.Add(mpHostStat);
            cardJ2.Controls.Add(j1); cardJ2.Controls.Add(mpCodeIn); cardJ2.Controls.Add(mpJoinBtn); cardJ2.Controls.Add(mpJoinStat);
            cardN.Controls.Add(note);
            SinkCards();
        }

        void ToggleHost()
        {
            if (mpHost != null)
            {
                mpHost.Stop(); mpHost = null;
                mpHostBtn.Text = "🌐 创建房间"; mpHostBtn.Fill = Theme.Green;
                mpCodeOut.Text = "(邀请码生成后显示在这里)";
                return;
            }
            int port;
            if (!int.TryParse(mpPortBox.Text.Trim(), out port) || port < 1 || port > 65535) { mpHostStat.Text = "游戏端口不合法"; return; }
            mpHostBtn.Enabled = false;
            MpHost h = new MpHost();
            Action<string> st = delegate(string s)
            {
                try { BeginInvoke((MethodInvoker)delegate { mpHostStat.Text = s; Log("[联机] " + s); }); } catch { }
            };
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    h.Create(port, Settings.PlayerName, st);
                    mpHost = h;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            mpCodeOut.Text = h.InviteCode;
                            mpHostBtn.Text = "⛔ 关闭房间"; mpHostBtn.Fill = Color.FromArgb(150, 70, 70);
                            mpHostBtn.Enabled = true;
                            try { Clipboard.SetText(h.InviteCode); mpHostStat.Text = "房间已创建, 邀请码已复制到剪贴板 ✓"; } catch { }
                        });
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    try { BeginInvoke((MethodInvoker)delegate { mpHostStat.Text = "创建失败: " + ex.Message; mpHostBtn.Enabled = true; }); } catch { }
                }
            });
        }

        void ToggleJoin()
        {
            if (mpJoin != null)
            {
                mpJoin.Stop(); mpJoin = null;
                mpJoinBtn.Text = "🚪 加入房间"; mpJoinBtn.Fill = Theme.Accent;
                mpJoinStat.Text = "";
                return;
            }
            string code = mpCodeIn.Text.Trim();
            if (code.Length < 20) { mpJoinStat.Text = "请先粘贴房主的邀请码"; return; }
            MpJoin j = new MpJoin();
            mpJoin = j;
            mpJoinBtn.Text = "⛔ 断开"; mpJoinBtn.Fill = Color.FromArgb(150, 70, 70);
            Action<string> st = delegate(string s)
            {
                try { BeginInvoke((MethodInvoker)delegate { mpJoinStat.Text = s; Log("[联机] " + s); }); } catch { }
            };
            try { j.Join(code, st, delegate(int p) { }); }
            catch (Exception ex)
            {
                mpJoin = null; mpJoinBtn.Text = "🚪 加入房间"; mpJoinBtn.Fill = Theme.Accent;
                mpJoinStat.Text = "加入失败: " + ex.Message;
            }
        }

        // 监听 MC 局域网世界广播 (UDP 组播 224.0.2.60:4445), 自动填入游戏端口
        void StartLanWatcher()
        {
            try
            {
                lanWatch = new UdpClient();
                lanWatch.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                lanWatch.Client.Bind(new IPEndPoint(IPAddress.Any, 4445));
                lanWatch.JoinMulticastGroup(IPAddress.Parse("224.0.2.60"));
                Thread t = new Thread(delegate()
                {
                    while (true)
                    {
                        try
                        {
                            IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                            lanWatch.Client.ReceiveTimeout = 2000;
                            byte[] d = lanWatch.Receive(ref from);
                            string s = Encoding.UTF8.GetString(d);
                            int a = s.IndexOf("[AD]"), b = s.IndexOf("[/AD]");
                            if (a < 0 || b <= a + 4) continue;
                            string ps = s.Substring(a + 4, b - a - 4);
                            int port;
                            if (!int.TryParse(ps.Trim(), out port)) continue;
                            try
                            {
                                BeginInvoke((MethodInvoker)delegate
                                {
                                    if (mpHost == null && mpPortBox.Text.Trim() != port.ToString())
                                    {
                                        mpPortBox.Text = port.ToString();
                                        Log("[联机] 检测到局域网世界, 游戏端口 " + port);
                                    }
                                });
                            }
                            catch { }
                        }
                        catch (SocketException) { }
                        catch { Thread.Sleep(500); }
                    }
                });
                t.IsBackground = true; t.Start();
            }
            catch { }
        }

        void BuildSettingsPage()
        {
            settingsPage = new Panel();
            settingsPage.Dock = DockStyle.Fill;
            settingsPage.BackColor = Theme.Bg;
            settingsPage.Visible = false;
            pageHost.Controls.Add(settingsPage);

            Panel cardDir = Card(settingsPage, "游戏目录 (.minecraft)", 20, 14, 896, 138);
            dirBox = new TextBox();
            dirBox.SetBounds(16, 30, 560, 26);
            dirBox.Text = Settings.McDir;
            dirBox.ReadOnly = true;
            dirBox.BackColor = Theme.CardLight; dirBox.ForeColor = Theme.Text; dirBox.BorderStyle = BorderStyle.FixedSingle;
            dirCountLab = new Label();
            dirCountLab.ForeColor = Theme.Green; dirCountLab.BackColor = Color.Transparent;
            dirCountLab.SetBounds(586, 34, 100, 20);
            dirCountLab.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            cardDir.Controls.Add(dirCountLab);
            dirBrowseBtn = new RButton();
            dirBrowseBtn.Text = "更改...";
            dirBrowseBtn.Fill = Color.FromArgb(70, 70, 84);
            dirBrowseBtn.Font = new Font("Microsoft YaHei UI", 9F);
            dirBrowseBtn.SetBounds(694, 29, 80, 30);
            dirBrowseBtn.Click += delegate { PickMcDir(); };
            openDirBtn = new RButton();
            openDirBtn.Text = "打开文件夹";
            openDirBtn.Fill = Color.FromArgb(70, 70, 84);
            openDirBtn.Font = new Font("Microsoft YaHei UI", 9F);
            openDirBtn.SetBounds(782, 29, 96, 30);
            openDirBtn.Click += delegate { try { Process.Start(Settings.McDir); } catch { } };
            cardDir.Controls.Add(dirBrowseBtn); cardDir.Controls.Add(openDirBtn);

            // 快速选择常见游戏目录 + 自动备份开关
            Label presetLab = new Label(); presetLab.Text = "快速选择:"; presetLab.ForeColor = Theme.TextDim;
            presetLab.BackColor = Color.Transparent; presetLab.SetBounds(16, 72, 66, 20);
            cardDir.Controls.Add(presetLab);
            dirPresetBox = new ComboBox();
            dirPresetBox.SetBounds(86, 68, 250, 26);
            dirPresetBox.DropDownStyle = ComboBoxStyle.DropDownList;
            dirPresetBox.FlatStyle = FlatStyle.Flat;
            dirPresetBox.BackColor = Theme.CardLight; dirPresetBox.ForeColor = Theme.Text;
            dirPresetBox.SelectedIndexChanged += delegate
            {
                int i = dirPresetBox.SelectedIndex;
                if (i < 0 || i >= presetPaths.Count) return;
                string p = presetPaths[i];
                if (p == "__browse__") PickMcDir();
                else if (p != Settings.McDir) ApplyMcDir(p);
            };
            cardDir.Controls.Add(dirPresetBox);
            FillDirPresets();

            CheckBox backupChk = new CheckBox();
            backupChk.Text = "启动游戏前自动备份存档 (保留最近 5 份, 存到 backups\\)";
            backupChk.ForeColor = Theme.Text;
            backupChk.BackColor = Color.Transparent;
            backupChk.AutoSize = false;
            backupChk.SetBounds(354, 70, 480, 26);
            backupChk.Checked = Settings.AutoBackup;
            backupChk.CheckedChanged += delegate { Settings.AutoBackup = backupChk.Checked; Settings.Save(); };
            cardDir.Controls.Add(backupChk);

            // 快捷打开子目录
            string[][] quickDirs = new string[][] {
                new string[] { "🗺 存档", "saves" }, new string[] { "🧩 mods", "mods" },
                new string[] { "📷 截图", "screenshots" }, new string[] { "🗂 备份", "backups" }, new string[] { "📦 版本", "versions" } };
            for (int i = 0; i < quickDirs.Length; i++)
            {
                string sub = quickDirs[i][1];
                RButton qb = new RButton();
                qb.Text = quickDirs[i][0];
                qb.Fill = Color.FromArgb(70, 70, 84);
                qb.Font = new Font("Microsoft YaHei UI", 8.5F);
                qb.SetBounds(16 + i * 96, 102, 88, 26);
                qb.Click += delegate
                {
                    string d = Path.Combine(Settings.McDir, sub);
                    if (sub == "mods" || sub == "backups") Directory.CreateDirectory(d);
                    if (Directory.Exists(d)) { try { Process.Start(d); } catch { } }
                    else MessageBox.Show(this, "目录还不存在: " + sub + "\n(玩过游戏/装过mod后自动生成)", "老鼠启动器");
                };
                cardDir.Controls.Add(qb);
            }
            cardDir.Controls.Add(dirBox);
            UpdateDirCount();

            Panel cardAbout = Card(settingsPage, "关于", 20, 164, 896, 120);
            if (LogoImg != null)
            {
                PictureBox aboutLogo = new PictureBox();
                aboutLogo.Image = LogoImg;
                aboutLogo.SetBounds(16, 30, 44, 44);
                aboutLogo.SizeMode = PictureBoxSizeMode.Zoom;
                aboutLogo.BackColor = Color.Transparent;
                cardAbout.Controls.Add(aboutLogo);
            }
            Label about = new Label();
            about.Text = "老鼠启动器 v1.1  by osaka\n" +
                "原版下载 + Fabric + Mod/整合包 + P2P 联机 + 自动装 Java + 存档备份, 深色主题。\n" +
                "与 PCL2 共用同一个 .minecraft 目录, 双方互相可见对方安装的版本。\n" +
                "数据源: Mojang 官方 (launchermeta / resources.download), 失败自动切换 BMCLAPI 镜像。";
            about.ForeColor = Theme.TextDim;
            about.BackColor = Color.Transparent;
            about.SetBounds(76, 32, 800, 80);
            cardAbout.Controls.Add(about);
            SinkCards();
        }

        // ----- 游戏目录管理 -----
        ComboBox dirPresetBox;
        Label dirCountLab;
        List<string> presetPaths = new List<string>();

        void ApplyMcDir(string dir)
        {
            Settings.McDir = dir;
            Settings.Save();
            dirBox.Text = dir;
            UpdateDirCount();
            ScanVersions();
            Log("游戏目录已切换: " + dir);
        }

        void PickMcDir()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "选择 .minecraft 目录 (需包含 versions 子目录)";
                if (d.ShowDialog(this) == DialogResult.OK) ApplyMcDir(d.SelectedPath);
            }
        }

        // 常见位置一键切换: 只列实际存在的; 另有浏览自定义项
        void FillDirPresets()
        {
            dirPresetBox.Items.Clear();
            presetPaths.Clear();
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string[][] presets = new string[][] {
                new string[] { "官方默认 (AppData)", Path.Combine(roaming, ".minecraft") },
                new string[] { "启动器同目录", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".minecraft") },
                new string[] { "PCL (D 盘)", "D:\\pcl2\\.minecraft" },
                new string[] { "PCL (C 盘)", "C:\\pcl2\\.minecraft" }
            };
            foreach (string[] p in presets)
            {
                if (Directory.Exists(p[1]))
                {
                    dirPresetBox.Items.Add(p[0]);
                    presetPaths.Add(p[1]);
                }
            }
            dirPresetBox.Items.Add("浏览其他目录...");
            presetPaths.Add("__browse__");
            int cur = presetPaths.IndexOf(Settings.McDir);
            if (cur >= 0) dirPresetBox.SelectedIndex = cur;
        }

        void UpdateDirCount()
        {
            try
            {
                int n = 0;
                string vdir = Path.Combine(Settings.McDir, "versions");
                if (Directory.Exists(vdir))
                    foreach (string d in Directory.GetDirectories(vdir))
                        if (File.Exists(Path.Combine(d, Path.GetFileName(d) + ".json"))) n++;
                if (n > 0) { dirCountLab.Text = "✓ " + n + " 个版本"; dirCountLab.ForeColor = Theme.Green; }
                else { dirCountLab.Text = "无版本"; dirCountLab.ForeColor = Color.FromArgb(230, 130, 90); }
                FillDirPresets();
            }
            catch { }
        }

        // ----- 业务 -----
        void Log(string msg) { Util.Log(logBox, msg); }

        // 选中版本对应的 Java 需求与就绪状态
        void UpdateJavaHint()
        {
            try
            {
                if (versionBox.SelectedItem == null) { javaNeedLab.Text = ""; return; }
                string vid = (string)versionBox.SelectedItem;
                string comp; int major;
                JavaRuntime.Requirement(Path.Combine(Settings.McDir, "versions", vid, vid + ".json"), out comp, out major);
                bool ready = File.Exists(Path.Combine(JavaRuntime.RuntimeRoot(), comp, "bin", "javaw.exe"));
                javaNeedLab.Text = ready ? "此版本需 Java " + major + " ✓ 已就绪" : "此版本需 Java " + major + " · 将自动下载";
                javaNeedLab.ForeColor = ready ? Theme.Green : Theme.Accent;
            }
            catch { javaNeedLab.Text = ""; }
        }

        void ScanJava()
        {
            javaBox.Items.Clear();
            foreach (string f in JavaFinder.Find()) javaBox.Items.Add(f);
            if (javaBox.Items.Count > 0) javaBox.SelectedIndex = 0;
            else Log("警告: 未找到 Java, 请在启动页手动选择 javaw.exe");
        }

        void ScanVersions()
        {
            versionBox.Items.Clear();
            try
            {
                foreach (string dir in Directory.GetDirectories(Path.Combine(Settings.McDir, "versions")).OrderBy(s => s))
                {
                    string name = Path.GetFileName(dir);
                    if (File.Exists(Path.Combine(dir, name + ".json"))) versionBox.Items.Add(name);
                }
            }
            catch { }
            if (versionBox.Items.Count > 0) versionBox.SelectedIndex = 0;
            if (targetVerBox != null) FillTargetVersions();
            if (versionBox.Items.Count == 0)
                Log("未扫到版本。当前游戏目录: " + Settings.McDir + "\n若游戏装在别处, 请到「设置」页点「更改...」选择; 也可直接用下载页下载新版本");
            else
                Log("已扫描到 " + versionBox.Items.Count + " 个版本 (目录: " + Settings.McDir + ")");
        }

        void DeleteVersion()
        {
            if (versionBox.SelectedItem == null) return;
            string vid = (string)versionBox.SelectedItem;
            if (MessageBox.Show(this, "确定删除版本 " + vid + " 吗?\n(仅删除 versions\\" + vid + " 文件夹, 不影响其他数据)",
                "删除版本", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                Directory.Delete(Path.Combine(Settings.McDir, "versions", vid), true);
                Log("已删除版本: " + vid);
                ScanVersions();
            }
            catch (Exception ex) { MessageBox.Show(this, "删除失败: " + ex.Message); }
        }

        // 自测: 程序化走完 资源包 搜索->选择->安装 流程
        public void SelfTest()
        {
            Log("[自测] 开始: 资源包搜索 -> 安装到 26.3");
            ShowPage(navMod);
            modTypeBox.SelectedIndex = 2;
            ModSearch();
            WinTimer t1 = new WinTimer();
            int ticks = 0;
            t1.Tick += delegate
            {
                ticks++;
                if (curResults != null && curResults.Count > 0)
                {
                    t1.Stop();
                    modResultList.Items[0].Selected = true;
                    WinTimer t2 = new WinTimer();
                    int t2n = 0;
                    t2.Tick += delegate
                    {
                        t2n++;
                        if (modVerList.Items.Count > 0)
                        {
                            t2.Stop();
                            modVerList.Items[0].Selected = true;
                            WinTimer t3 = new WinTimer();
                            t3.Interval = 600;
                            t3.Tick += delegate
                            {
                                t3.Stop();
                                InstallSelected();
                                WinTimer t4 = new WinTimer();
                                int t4n = 0;
                                t4.Tick += delegate
                                {
                                    t4n++;
                                    string dir = Path.Combine(Settings.McDir, "versions", (string)targetVerBox.SelectedItem, "resourcepacks");
                                    bool has = Directory.Exists(dir) && Directory.GetFiles(dir).Length > 0;
                                    if (has || t4n > 40)
                                    {
                                        t4.Stop();
                                        Log("[自测] " + (has ? "通过 ✓ 已安装: " + Directory.GetFiles(dir)[0] : "失败: 超时未出现文件"));
                                    }
                                };
                                t4.Start();
                            };
                            t3.Start();
                        }
                        else if (t2n > 40) { t2.Stop(); Log("[自测] 失败: 版本列表未加载"); }
                    };
                    t2.Start();
                }
                else if (ticks > 40) { t1.Stop(); Log("[自测] 失败: 搜索无结果"); }
            };
            t1.Start();
        }

        void DoLaunch()
        {
            if (versionBox.SelectedItem == null) { MessageBox.Show(this, "请先选择版本"); return; }
            string java = javaBox.SelectedItem != null ? (string)javaBox.SelectedItem : javaBox.Text;
            string vid = (string)versionBox.SelectedItem;
            string name = string.IsNullOrEmpty(nameBox.Text.Trim()) ? "Player" : nameBox.Text.Trim();
            Settings.PlayerName = name;
            Settings.MemoryMB = memTrack.Value;
            Settings.Save();
            launchBtn.Enabled = false;
            Log("正在启动 " + vid + " ...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                // 解析该版本对应的 Java (javaVersion): 精确组件没装就自动下载 Mojang 官方运行时
                string useJava = java;
                try
                {
                    string vjson = Path.Combine(Settings.McDir, "versions", vid, vid + ".json");
                    string comp; int need;
                    JavaRuntime.Requirement(vjson, out comp, out need);
                    string exact = Path.Combine(JavaRuntime.RuntimeRoot(), comp, "bin", "javaw.exe");
                    if (File.Exists(exact))
                    {
                        useJava = exact;
                        Log("使用对应 Java (" + comp + "): " + useJava);
                    }
                    else
                    {
                        Log("该版本需要 Java " + need + " (" + comp + "), 自动下载官方运行时...");
                        useJava = JavaRuntime.Ensure(comp, delegate(int pct, string s) { Util.Log(logBox, "  " + pct + "% " + s); });
                        try { BeginInvoke((MethodInvoker)delegate { ScanJava(); }); } catch { }
                        Log("Java 就绪: " + useJava);
                    }
                }
                catch (Exception ex)
                {
                    Log("Java 自动下载失败: " + ex.Message + ", 尝试用已选 Java 继续");
                }
                if (string.IsNullOrEmpty(useJava) || !File.Exists(useJava))
                {
                    Util.Log(logBox, "启动失败: 无可用 Java, 请在启动页手动选择");
                    try { BeginInvoke((MethodInvoker)delegate { launchBtn.Enabled = true; }); } catch { }
                    return;
                }
                try
                {
                    GameLauncher gl = new GameLauncher(Settings.McDir, useJava, name, memTrack.Value, logBox);
                    Process gp = gl.Launch(vid);
                    gameProc = gp;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            launchBtn.Enabled = true;
                            launchBtn.Text = "⛏ 游戏运行中...";
                            gameStatLab.Text = "● 运行中";
                            gameStatLab.ForeColor = Theme.Green;
                            killGameBtn.Visible = true;
                            WindowState = FormWindowState.Minimized;
                        });
                    }
                    catch { }
                    gp.WaitForExit();
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            launchBtn.Text = "▶  启 动 游 戏";
                            gameStatLab.Text = "○ 未运行";
                            gameStatLab.ForeColor = Theme.TextDim;
                            killGameBtn.Visible = false;
                        });
                    }
                    catch { }
                    if (gp.ExitCode != 0)
                    {
                        Log("游戏异常退出 (代码 " + gp.ExitCode + ")");
                        string why = Util.CrashReason(Settings.McDir);
                        if (why != null) Log("可能原因: " + why);
                    }
                    else Log("游戏已正常退出");
                }
                catch (Exception ex) { Util.Log(logBox, "启动失败: " + ex.Message); }
                try { BeginInvoke((MethodInvoker)delegate { launchBtn.Enabled = true; }); } catch { }
            });
        }
    }
}
