using System;
using System.Windows.Forms;
using System.Threading;
using System.Text.RegularExpressions;

namespace WeixinAntiRevoke
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            EmbeddedDependencies.Initialize();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string path = null;
            bool prompt = true;
            string readyEvent = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--path" && i + 1 < args.Length) path = args[++i];
                else if (args[i] == "--quiet") prompt = false;
                else if (args[i] == "--update-ready" && i + 1 < args.Length) readyEvent = args[++i];
            }
            using (MainForm form = new MainForm(path, prompt, true))
            {
                form.Shown += delegate
                {
                    if (readyEvent != null && Regex.IsMatch(readyEvent, @"\ALocal\\WeixinAntiRevoke-[a-f0-9]{32}\z"))
                        try { using (EventWaitHandle ready = EventWaitHandle.OpenExisting(readyEvent)) ready.Set(); } catch (WaitHandleCannotBeOpenedException) {} catch (UnauthorizedAccessException) {}
                };
                Application.Run(form);
            }
        }
    }
}
