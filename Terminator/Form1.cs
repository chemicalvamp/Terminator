using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Media;
using System.Security.Principal;

namespace Terminator
{
    public partial class Form1 : Form
    {
        static string commandFile;
        MenuItem sDenial;
        static string denialFile;
        public Thread denialThread;
        string[] denials;
        MenuItem sAlive;
        public SoundPlayer player;
        public string soundFile;
        public Thread soundThread;
        public uint myPID;
        public GlobalHotkey killHK;
        public GlobalHotkey minHK;
        public List<GlobalHotkey> hotkeys = new List<GlobalHotkey>();
        public List<Thread> threads = new List<Thread>();

        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        public static bool IsAdministrator()
        {
            return (new WindowsPrincipal(WindowsIdentity.GetCurrent()))
                      .IsInRole(WindowsBuiltInRole.Administrator);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Constants.WM_HOTKEY_MSG_ID)
                HandleHotkey(m);
            base.WndProc(ref m);
        }

        public void HandleHotkey(Message m)
        {
            EventArgs e = new EventArgs();
            if (m.LParam.ToInt32() == minHK.lParam)
                MinWindow(this, e);

            if (m.LParam.ToInt32() == killHK.lParam)
                KillWindow(this, e);
        }

        public Form1()
        {
            InitializeComponent();
            GetWindowThreadProcessId(this.Handle, out myPID);
            soundFile = Environment.CurrentDirectory + @"\Sound.wav";
            soundThread = new Thread(SoundLoop);
            threads.Add(soundThread);
            player = new SoundPlayer(soundFile);
            denialFile = Environment.CurrentDirectory + @"\Denials.txt";
            if (!File.Exists(denialFile))
                File.Create(denialFile);
            commandFile = Environment.CurrentDirectory + @"\Commands.txt";
            if (!File.Exists(commandFile))
                File.Create(commandFile);
            denialThread = new Thread(DenialLoop);
            threads.Add(denialThread);
            SysTrayApp();
        }

        public static string OutputFullPath
        {
            get { return Environment.CurrentDirectory + @"\Output.txt"; }
        }

        public void SysTrayApp()
        {
            trayMenu = new ContextMenu();
            sDenial = trayMenu.MenuItems.Add("App Denial", AppDenial);
            sAlive = trayMenu.MenuItems.Add("Sound Alive", SoundAlive);
            trayMenu.MenuItems.Add("Kill", KillWindow);
            trayMenu.MenuItems.Add("Minimize", MinWindow);
            trayMenu.MenuItems.Add("Show", ShowWindow);
            trayMenu.MenuItems.Add("Exit", OnExit);

            trayIcon = new NotifyIcon
            {
                Text = "Terminator",
                Icon = new Icon(Environment.CurrentDirectory + @"\Tray.ico", 64, 64),

                // Add menu to tray icon and show it.
                ContextMenu = trayMenu,
                Visible = true
            };
            sAlive.Checked = false;
            sDenial.Checked = false;
            AppDenial(this, new EventArgs());
            SoundAlive(this, new EventArgs());
        }
        private void AppDenial(object sender, EventArgs e)
        {
            if ((denialThread.ThreadState == System.Threading.ThreadState.Running) || (denialThread.ThreadState == System.Threading.ThreadState.WaitSleepJoin))
            {
                sDenial.Checked = false;
                denialThread.Abort();
                threads.Remove(denialThread);
            }
            else if (denialThread.ThreadState == System.Threading.ThreadState.Aborted)
            {
                sDenial.Checked = true;
                denials = File.ReadAllLines(denialFile);
                denialThread = new Thread(DenialLoop);
                threads.Add(denialThread);
                denialThread.Start();
            }
            else
            {
                sDenial.Checked = true;
                threads.Add(denialThread);
                denials = File.ReadAllLines(denialFile);
                denialThread.Start();
            }
        }
        private void SoundAlive(object sender, EventArgs e)
        {
            if ((soundThread.ThreadState == System.Threading.ThreadState.Running) || (soundThread.ThreadState == System.Threading.ThreadState.WaitSleepJoin))
            {
                sAlive.Checked = false;
                soundThread.Abort();
                threads.Remove(soundThread);
            }
            else if (soundThread.ThreadState == System.Threading.ThreadState.Aborted)
            {
                sAlive.Checked = true;
                soundThread = new Thread(SoundLoop);
                soundThread.Start();
            }
            else
            {
                sAlive.Checked = true;
                soundThread.Start();
            }
        }

        private void SoundLoop()
        {
            while (true)
            {
                player.PlaySync();
                Thread.Sleep(2 * 60000);
            }
        }
        private void DenialLoop()
        {
            while (true)
            {
                Process[] processes = Process.GetProcesses();
                foreach (Process process in processes)
                {
                    foreach (string kill in denials)
                    {
                        if (process.ProcessName == kill)
                            process.Kill();
                    }
                }
                Thread.Sleep(100);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            Visible = false; // Hide form window.
            ShowInTaskbar = false; // Remove from taskbar.

            killHK = new GlobalHotkey(Constants.CTRL + Constants.ALT, Keys.Insert, this);
            hotkeys.Add(killHK);
            killHK.Register();
            minHK = new GlobalHotkey(Constants.CTRL + Constants.ALT, Keys.Home, this);
            hotkeys.Add(minHK);
            minHK.Register();
            base.OnLoad(e);
        }

        private void OnExit(object sender, EventArgs e)
        {
            foreach (GlobalHotkey hotkey in hotkeys)
                hotkey.Unregister();
            for (int i = 0; i < threads.Count; i++)
            {
                threads[i].Abort();
                threads[i] = null;
            }
            trayMenu.Dispose();
            player.Dispose();
            Application.Exit();
            this.Dispose();
        }

        private void ShowWindow(object sender, EventArgs e)
        {
            while (true)
            {
                uint current = 0;
                IntPtr currentHwnd = GetForegroundWindow();
                GetWindowThreadProcessId(currentHwnd, out current);

                if ((current != 0) && (current != myPID))
                {
                    MessageBox.Show("PID: " + myPID.ToString() + " is me. PID: " + current.ToString() + " is on top.");
                    break;
                }
                Thread.Sleep(100);
            }
        }

        public void MinWindow(object sender, EventArgs e)
        {
            while (true)
            {
                uint currentPID = 0;
                IntPtr currentHwnd = GetForegroundWindow();
                GetWindowThreadProcessId(currentHwnd, out currentPID);

                if ((currentPID != 0) && (currentPID != myPID))
                {
                    Process minThis = Process.GetProcessById((int)currentPID);
                    try
                    {
                        ShowWindowAsync(minThis.Handle, Constants.SHOWMINIMIZED);
                        ShowWindowAsync(minThis.MainWindowHandle, Constants.SHOWMINIMIZED);
                    }
                    catch
                    {
                        ShowWindowAsync(minThis.MainWindowHandle, Constants.SHOWMINIMIZED);
                    }
                    break;
                }
                Thread.Sleep(100);
            }
        }

        private void KillWindow(object sender, EventArgs e)
        {
            // These strings will show "Access Denied" if the Thread cannot access the target's data and overwrite them.
            string fileName = "Access Denied.";
            string mainWindowTitle = "Access Denied.";
            string processName = "Access Denied.";
            string hasExited = "Access Denied.";

            while (true)
            {
                uint currentPID = 0;
                IntPtr currentHwnd = GetForegroundWindow();
                GetWindowThreadProcessId(currentHwnd, out currentPID);
                using (StreamWriter sw = File.AppendText(OutputFullPath))
                    if ((currentPID != 0) && (currentPID != myPID))
                    {
                        Process killThis = Process.GetProcessById((int)currentPID);
                        try
                        {
                            mainWindowTitle = killThis.MainWindowTitle.ToString();
                            processName = killThis.ProcessName.ToString();
                            fileName = killThis.MainModule.FileName.ToString();
                        }
                        catch { }
                        try
                        {
                            killThis.Kill();
                        }
                        catch { }
                        try
                        {
                            hasExited = killThis.HasExited.ToString();
                        }
                        catch { }
                        sw.WriteLine(DateTime.Now.ToString() + " | Path:" + fileName + ". Window Title:" + mainWindowTitle + ". Process Name:" + processName + ". Exited:" + hasExited);
                        sw.Close();
                        break;
                    }
                Thread.Sleep(100);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            using (StreamWriter sw = File.AppendText(OutputFullPath))
            {
                sw.WriteLine(DateTime.Now + " Admin privlige: " + IsAdministrator().ToString());
                string[] commands = File.ReadAllLines(commandFile);
                foreach (string command in commands)
                {
                    sw.WriteLine(DateTime.Now + " Running command: " + command);
                    Process process = new Process();
                    ProcessStartInfo startInfo = new ProcessStartInfo();

                    startInfo.WorkingDirectory = @"C:\Windows\System32";
                    startInfo.FileName = "cmd.exe";
                    startInfo.Arguments = "/user:Administrator \"cmd /C " + command + "\"";
                    startInfo.CreateNoWindow = true;
                    startInfo.ErrorDialog = false;
                    startInfo.UseShellExecute = false;
                    startInfo.RedirectStandardError = true;
                    startInfo.RedirectStandardOutput = true;
                    process.StartInfo = startInfo;
                    process.Start();
                    process.WaitForExit();
                    string sOutput = process.StandardOutput.ReadToEnd();
                    string sError = process.StandardError.ReadToEnd();
                    if (sError == "")
                        sw.WriteLine(CutNullChar(sOutput));
                    else
                        sw.WriteLine(CutNullChar(sError));
                }
                sw.WriteLine(DateTime.Now + " Done running commands.");
                sw.Close();
            }
        }

        private string CutNullChar(string input)
        {
            string output = "";
            foreach (char character in input)
                if (character != 0x00)
                    output += character;
            return output;
        }
        
        public static class Constants
        {
            //modifiers are binary nothing is 0000 alt is 0001, ctrl is 0010, ctrl + alt + shift + win is 1111. Modifier combonations are added together.
            public const int NOMOD = 0x0000;    //0000
            public const int ALT = 0x0001;      //0001
            public const int CTRL = 0x0002;     //0010
            public const int SHIFT = 0x0004;    //0100
            public const int WIN = 0x0008;      //1000
                                                //windows message id for hotkey
            public const int WM_HOTKEY_MSG_ID = 0x0312;
            public const int SHOWNORMAL = 1;
            public const int SHOWMINIMIZED = 2;
            public const int SHOWMAXIMIZED = 3;
        }

        public class GlobalHotkey
        {
            [DllImport("user32.dll")]
            private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
            [DllImport("user32.dll")]
            private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

            public int lParam;
            private int modifier;
            private int key;
            private IntPtr hWnd;
            private int id;

            private int MakeLParam(int LoWord, int HiWord)
            {
                return ((HiWord << 16) | (LoWord & 0xffff));
            }

            public GlobalHotkey(int modifier, Keys key, Form form)
            {
                this.modifier = modifier;
                this.key = (int)key;
                this.hWnd = form.Handle;
                id = this.GetHashCode();
                lParam = MakeLParam(modifier, (int)key);
            }

            public override int GetHashCode()
            {
                return modifier ^ key ^ hWnd.ToInt32();
            }

            public bool Register()
            {
                return RegisterHotKey(hWnd, id, modifier, key);
            }

            public bool Unregister()
            {
                return UnregisterHotKey(hWnd, id);
            }
        }
    }
}
