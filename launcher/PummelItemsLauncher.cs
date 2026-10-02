// PummelItems launcher for Windows: asks how to start Pummel Party - with the mod, without
// it, or not at all - in the style of Steam's own launch-option menu, the same menu the Steam
// Deck gets from deck/picker.py.
//
// Steam runs it from the game's launch options:
//
//     "<game folder>\PummelItemsLauncher.exe" %command%
//
// so it receives the game's own command line, starts it once the choice is made, and waits for
// the game to exit - Steam counts the game as running, and syncs the cloud, by this process.
//
//   with the mod     MelonLoader's off switch in UserData\Loader.cfg is set back to on
//   without the mod  the game gets MelonLoader's own --no-mods, which stops it before it loads
//                    anything; nothing on disk changes
//
// Testing options, given before the game's command: --pi-choose=modded|vanilla|cancel answers
// without showing the menu, --pi-wait=N sets the countdown, --pi-preview=out.png draws the
// menu into a picture and exits.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("PummelItems launcher")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace PummelItemsLauncher
{
    internal static class Program
    {
        private const string GameExe = "PummelParty.exe";
        private const string AppId = "880940";

        [STAThread]
        private static int Main(string[] argv)
        {
            string here = AppDomain.CurrentDomain.BaseDirectory;
            Log log = new Log(Path.Combine(here, @"UserData\PummelCustomItems\launcher.log"));

            // Our own options come first; everything after them is the game's command line.
            string forced = null, preview = null;
            int wait = 12;
            int first = 0;
            for (; first < argv.Length && argv[first].StartsWith("--pi-", StringComparison.Ordinal); first++)
            {
                string a = argv[first];
                if (a.StartsWith("--pi-choose=", StringComparison.Ordinal)) forced = a.Substring(12);
                else if (a.StartsWith("--pi-wait=", StringComparison.Ordinal)) int.TryParse(a.Substring(10), out wait);
                else if (a.StartsWith("--pi-preview=", StringComparison.Ordinal)) preview = a.Substring(13);
            }
            List<string> command = new List<string>();
            for (int i = first; i < argv.Length; i++) command.Add(argv[i]);

            string modeFile = Path.Combine(here, @"UserData\PummelCustomItems\launcher-mode.txt");
            Choice last = ReadMode(modeFile);
            Wording text = Wording.ForCurrentLanguage();

            if (preview != null)
            {
                Dpi.Aware();
                Picker.Preview(preview, text, last, wait);
                return 0;
            }

            Choice choice;
            if (forced == "modded") choice = Choice.Modded;
            else if (forced == "vanilla") choice = Choice.Vanilla;
            else if (forced == "cancel") choice = Choice.Cancel;
            else
            {
                Dpi.Aware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Picker picker = new Picker(text, last, wait);
                Application.Run(picker);
                choice = picker.Result ?? last;
                log.Write("menu: " + (picker.Result == null ? "timed out, last choice " : "chose ") + choice);
            }

            if (choice == Choice.Cancel)
            {
                log.Write("cancelled - the game is not started");
                return 0;
            }
            WriteMode(modeFile, choice, log);

            // Started by hand rather than by Steam: run the game next to us, and tell the
            // Steam API which game it is, as Steam would.
            if (command.Count == 0) command.Add(Path.Combine(here, GameExe));

            if (choice == Choice.Modded) LoaderSwitch(Path.Combine(here, @"UserData\Loader.cfg"), log);
            else command.Add("--no-mods");

            return Run(command, here, log);
        }

        // ------------------------------------------------------------------ the choice

        private static Choice ReadMode(string path)
        {
            try
            {
                if (File.Exists(path) && File.ReadAllText(path).Trim() == "vanilla") return Choice.Vanilla;
            }
            catch { }
            return Choice.Modded;
        }

        private static void WriteMode(string path, Choice c, Log log)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, c == Choice.Vanilla ? "vanilla" : "modded");
            }
            catch (Exception e)
            {
                log.Write("could not remember the choice: " + e.Message);
            }
        }

        /// <summary>
        /// An old switch (Play Vanilla.bat) may have left MelonLoader turned off in its config;
        /// with the mod chosen it is turned back on. The pattern is anchored: the same file has
        /// disable_start_screen and other keys that start with "disable".
        /// </summary>
        private static void LoaderSwitch(string cfg, Log log)
        {
            try
            {
                if (!File.Exists(cfg)) return;      // first start: MelonLoader writes it, switched on
                string s = File.ReadAllText(cfg);
                Regex r = new Regex(@"^(\s*disable\s*=\s*)true\b", RegexOptions.Multiline);
                if (!r.IsMatch(s)) return;
                File.WriteAllText(cfg, r.Replace(s, "${1}false", 1));
                log.Write("MelonLoader was switched off in Loader.cfg - switched back on");
            }
            catch (Exception e)
            {
                log.Write("could not check Loader.cfg: " + e.Message);
            }
        }

        // ------------------------------------------------------------------ the game

        private static int Run(List<string> command, string here, Log log)
        {
            ProcessStartInfo psi = new ProcessStartInfo(command[0], JoinArgs(command, 1))
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(command[0])) ?? here,
            };
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SteamAppId")))
                psi.EnvironmentVariables["SteamAppId"] = AppId;

            log.Write("start: " + command[0] + " " + psi.Arguments);
            try
            {
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit();
                    log.Write("game exited with " + p.ExitCode);
                    return p.ExitCode;
                }
            }
            catch (Exception e)
            {
                log.Write("could not start the game: " + e.Message);
                MessageBox.Show(e.Message, "Pummel Party", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        /// <summary>Rebuilds a command line the way the C runtime will split it again.</summary>
        private static string JoinArgs(List<string> args, int from)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = from; i < args.Count; i++)
            {
                if (sb.Length > 0) sb.Append(' ');
                string a = args[i];
                if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
                {
                    sb.Append(a);
                    continue;
                }
                sb.Append('"');
                int slashes = 0;
                foreach (char c in a)
                {
                    if (c == '\\') { slashes++; continue; }
                    if (c == '"') sb.Append('\\', slashes * 2 + 1);
                    else sb.Append('\\', slashes);
                    slashes = 0;
                    sb.Append(c);
                }
                sb.Append('\\', slashes * 2);
                sb.Append('"');
            }
            return sb.ToString();
        }
    }

    internal enum Choice { Modded, Vanilla, Cancel }

    internal sealed class Log
    {
        private readonly string m_path;
        public Log(string path) { m_path = path; }

        public void Write(string line)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(m_path));
                if (File.Exists(m_path) && new FileInfo(m_path).Length > 200000) File.Delete(m_path);
                File.AppendAllText(m_path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + line + Environment.NewLine);
            }
            catch { }
        }
    }

    // ---------------------------------------------------------------------- wording

    internal sealed class Wording
    {
        public string Title, Modded, Vanilla, Cancel, Select, Back, HintModded, HintVanilla;

        public static Wording ForCurrentLanguage()
        {
            // Russian if Windows is in Russian, or set to Russian formats; English otherwise.
            if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ||
                CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "ru")
                return new Wording
                {
                    Title = "Выберите вариант запуска Pummel Party",
                    Modded = "Играть с модом", Vanilla = "Играть без мода", Cancel = "Отмена",
                    Select = "ВЫБРАТЬ", Back = "НАЗАД",
                    HintModded = "Без выбора через {0} с — запуск с модом, как в прошлый раз",
                    HintVanilla = "Без выбора через {0} с — запуск без мода, как в прошлый раз",
                };
            return new Wording
            {
                Title = "Choose how to launch Pummel Party",
                Modded = "Play with the mod", Vanilla = "Play without the mod", Cancel = "Cancel",
                Select = "SELECT", Back = "BACK",
                HintModded = "Starting with the mod in {0} s, like last time",
                HintVanilla = "Starting without the mod in {0} s, like last time",
            };
        }
    }

    internal static class Dpi
    {
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();

        /// <summary>Draw at the screen's real resolution rather than be stretched by Windows.</summary>
        public static void Aware()
        {
            try { SetProcessDPIAware(); } catch { }
        }
    }

    // ---------------------------------------------------------------------- the menu

    internal sealed class Picker : Form
    {
        // Layout in 96-dpi units, the same as the Deck's menu.
        private const int W = 680, H = 440, RowW = 520, RowH = 62, RowStep = 66;

        private static readonly Color Bg = Color.FromArgb(16, 22, 30);
        private static readonly Color RowBg = Color.FromArgb(43, 58, 75), RowText = Color.FromArgb(220, 227, 234);
        private static readonly Color OnBg = Color.FromArgb(223, 227, 232), OnText = Color.FromArgb(27, 38, 51);
        private static readonly Color Hint = Color.FromArgb(139, 146, 154), Foot = Color.FromArgb(220, 222, 223);

        private readonly Wording m_text;
        private readonly Choice m_last;
        private readonly DateTime m_deadline;
        private readonly Timer m_timer = new Timer { Interval = 50 };
        private readonly Gamepads m_pads = new Gamepads();
        private int m_selected;
        private bool m_counting = true;
        private float m_scale = 1f;

        public Choice? Result { get; private set; }

        public Picker(Wording text, Choice last, int wait)
        {
            m_text = text;
            m_last = last;
            m_selected = last == Choice.Vanilla ? 1 : 0;
            m_deadline = DateTime.UtcNow.AddSeconds(Math.Max(wait, 1));

            Text = "Pummel Party";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            ShowInTaskbar = true;
            BackColor = Bg;
            KeyPreview = true;
            DoubleBuffered = true;

            using (Graphics g = CreateGraphics()) m_scale = g.DpiX / 96f;
            ClientSize = new Size((int)(W * m_scale), (int)(H * m_scale));

            m_timer.Tick += (s, e) => Tick();
            m_timer.Start();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
        }

        private void Tick()
        {
            if (m_counting && DateTime.UtcNow >= m_deadline)
            {
                Finish(null);
                return;
            }
            foreach (string a in m_pads.Poll()) Act(a);
            if (m_counting) Invalidate();
        }

        private void Act(string what)
        {
            m_counting = false;
            if (what == "up") m_selected = (m_selected + 2) % 3;
            else if (what == "down") m_selected = (m_selected + 1) % 3;
            else if (what == "ok") { Finish((Choice)m_selected); return; }
            else if (what == "back") { Finish(Choice.Cancel); return; }
            Invalidate();
        }

        private void Finish(Choice? c)
        {
            m_timer.Stop();
            Result = c;
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Up: Act("up"); break;
                case Keys.Down: case Keys.Tab: Act("down"); break;
                case Keys.Enter: case Keys.Space: Act("ok"); break;
                case Keys.Escape: Act("back"); break;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = Hit(e.Location);
            if (i >= 0 && i != m_selected)
            {
                m_counting = false;
                m_selected = i;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int i = Hit(e.Location);
            if (e.Button == MouseButtons.Left && i >= 0) Finish((Choice)i);
        }

        private int Hit(Point p)
        {
            float x = p.X / m_scale, y = p.Y / m_scale;
            MenuLayout lay = new MenuLayout(W, H);
            for (int i = 0; i < 3; i++)
                if (x >= lay.RowX && x < lay.RowX + RowW && y >= lay.RowY[i] && y < lay.RowY[i] + RowH) return i;
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            int secs = m_counting ? Math.Max(0, (int)Math.Ceiling((m_deadline - DateTime.UtcNow).TotalSeconds)) : -1;
            Draw(e.Graphics, m_scale, m_text, m_selected, secs, m_last);
        }

        private sealed class MenuLayout
        {
            public readonly int TitleY, RowX, HintY;
            public readonly int[] RowY = new int[3];

            public MenuLayout(int w, int h)
            {
                int total = 40 + 16 + RowStep * 3 + 12 + 24;
                TitleY = Math.Max((h - total) / 2 - 20, 8);
                RowX = (w - RowW) / 2;
                for (int i = 0; i < 3; i++) RowY[i] = TitleY + 40 + 16 + i * RowStep;
                HintY = TitleY + 40 + 16 + RowStep * 3 + 12;
            }
        }

        /// <summary>The whole menu, in 96-dpi units scaled to the screen.</summary>
        private static void Draw(Graphics g, float scale, Wording t, int selected, int secs, Choice last)
        {
            g.ScaleTransform(scale, scale);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Bg);

            MenuLayout lay = new MenuLayout(W, H);
            using (Font title = new Font("Segoe UI Semibold", 26f, GraphicsUnit.Pixel))
            using (Font row = new Font("Segoe UI", 22f, GraphicsUnit.Pixel))
            using (Font small = new Font("Segoe UI", 16f, GraphicsUnit.Pixel))
            using (Font foot = new Font("Segoe UI Semibold", 16f, GraphicsUnit.Pixel))
            using (StringFormat centre = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (StringFormat left = new StringFormat { LineAlignment = StringAlignment.Center })
            {
                using (Brush b = new SolidBrush(Color.White))
                    g.DrawString(t.Title, title, b, new RectangleF(0, lay.TitleY, W, 40), centre);

                string[] labels = { t.Modded, t.Vanilla, t.Cancel };
                for (int i = 0; i < 3; i++)
                {
                    bool on = i == selected;
                    RectangleF r = new RectangleF(lay.RowX, lay.RowY[i], RowW, RowH);
                    using (Brush bg = new SolidBrush(on ? OnBg : RowBg)) g.FillRectangle(bg, r);
                    using (Brush fg = new SolidBrush(on ? OnText : RowText))
                        g.DrawString(labels[i], row, fg, new RectangleF(r.X + 22, r.Y, r.Width - 60, r.Height), left);
                    if (i < 2)
                    {
                        // The chevron Steam puts on rows that launch something.
                        using (Pen p = new Pen(on ? OnText : RowText, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                        {
                            float cx = r.Right - 30, cy = r.Y + RowH / 2f;
                            g.DrawLines(p, new[] { new PointF(cx - 4, cy - 8), new PointF(cx + 4, cy), new PointF(cx - 4, cy + 8) });
                        }
                    }
                }

                if (secs >= 0)
                    using (Brush b = new SolidBrush(Hint))
                        g.DrawString(string.Format(last == Choice.Vanilla ? t.HintVanilla : t.HintModded, secs),
                                     small, b, new RectangleF(0, lay.HintY, W, 24), centre);

                // "(A) SELECT   (B) BACK" at the bottom right, white discs with the letter cut out.
                SizeF s1 = g.MeasureString(t.Select, foot), s2 = g.MeasureString(t.Back, foot);
                float x = W - 36 - (26 + 6 + s1.Width + 22 + 26 + 6 + s2.Width), y = H - 28 - 30;
                x = DrawButton(g, x, y, "A", t.Select, foot, s1);
                DrawButton(g, x + 22, y, "B", t.Back, foot, s2);
            }
        }

        private static float DrawButton(Graphics g, float x, float y, string letter, string word, Font font, SizeF size)
        {
            using (Brush disc = new SolidBrush(Color.White))
            using (Brush hole = new SolidBrush(Bg))
            using (Brush fg = new SolidBrush(Foot))
            using (Font l = new Font("Segoe UI Semibold", 15f, GraphicsUnit.Pixel))
            using (StringFormat centre = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                RectangleF r = new RectangleF(x, y + 2, 26, 26);
                g.FillEllipse(disc, r);
                g.DrawString(letter, l, hole, new RectangleF(r.X, r.Y + 0.5f, r.Width, r.Height), centre);
                g.DrawString(word, font, fg, x + 26 + 6, y + (30 - size.Height) / 2f);
            }
            return x + 26 + 6 + size.Width;
        }

        /// <summary>Draws the menu into a PNG, for checking how it looks without a person to click it.</summary>
        public static void Preview(string path, Wording t, Choice last, int wait)
        {
            float scale = 1f;
            using (Bitmap bmp = new Bitmap((int)(W * scale), (int)(H * scale)))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                Draw(g, scale, t, last == Choice.Vanilla ? 1 : 0, wait, last);
                bmp.Save(path, ImageFormat.Png);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) m_timer.Dispose();
            base.Dispose(disposing);
        }
    }

    // ---------------------------------------------------------------------- controllers

    /// <summary>XInput controllers: D-pad or left stick to move, A to choose, B to back out.</summary>
    internal sealed class Gamepads
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct XInputState
        {
            public uint Packet;
            public ushort Buttons;
            public byte LeftTrigger, RightTrigger;
            public short LX, LY, RX, RY;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")] private static extern int Get14(int i, out XInputState s);
        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")] private static extern int Get910(int i, out XInputState s);

        private const ushort Up = 0x0001, Down = 0x0002, A = 0x1000, B = 0x2000;
        private readonly ushort[] m_buttons = new ushort[4];
        private readonly int[] m_stick = new int[4];
        private readonly bool[] m_seen = new bool[4];
        private bool m_use910, m_none;
        private readonly DateTime m_start = DateTime.UtcNow;

        public IEnumerable<string> Poll()
        {
            List<string> outp = new List<string>();
            if (m_none) return outp;
            for (int i = 0; i < 4; i++)
            {
                XInputState s;
                int rc;
                try { rc = m_use910 ? Get910(i, out s) : Get14(i, out s); }
                catch (DllNotFoundException)
                {
                    if (m_use910) { m_none = true; return outp; }
                    m_use910 = true;
                    i--;
                    continue;
                }
                catch (EntryPointNotFoundException) { m_none = true; return outp; }
                if (rc != 0) { m_seen[i] = false; continue; }

                ushort pressed = (ushort)(s.Buttons & ~m_buttons[i]);
                int stick = s.LY > 16000 ? -1 : (s.LY < -16000 ? 1 : 0);
                // The first reading only records what is already held - the A that started the
                // game from Steam must not count as a choice.
                bool fresh = m_seen[i] && (DateTime.UtcNow - m_start).TotalMilliseconds > 400;
                if (fresh)
                {
                    if ((pressed & Up) != 0) outp.Add("up");
                    if ((pressed & Down) != 0) outp.Add("down");
                    if ((pressed & A) != 0) outp.Add("ok");
                    if ((pressed & B) != 0) outp.Add("back");
                    if (stick != 0 && stick != m_stick[i]) outp.Add(stick < 0 ? "up" : "down");
                }
                m_buttons[i] = s.Buttons;
                m_stick[i] = stick;
                m_seen[i] = true;
            }
            return outp;
        }
    }
}
