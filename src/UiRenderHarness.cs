using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace WeixinAntiRevoke
{
    internal static class UiRenderHarness
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                return 2;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            EmbeddedDependencies.Initialize();
            using (MainForm form = new MainForm())
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-32000, -32000);
                form.ShowInTaskbar = false;
                form.Show();
                Application.DoEvents();
                DateTime deadline = DateTime.UtcNow.AddSeconds(90);
                while (form.IsAnalysisBusy && DateTime.UtcNow < deadline) { Application.DoEvents(); System.Threading.Thread.Sleep(20); }
                if (form.IsAnalysisBusy) return 3;
                using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                {
                form.DrawToBitmap(
                    bitmap,
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                bitmap.Save(args[0], ImageFormat.Png);
                Console.WriteLine(
                    "rendered={0}x{1};controls={2}",
                    bitmap.Width,
                    bitmap.Height,
                    form.Controls.Count);
                }
                form.Hide();
            }

            return 0;
        }
    }
}
