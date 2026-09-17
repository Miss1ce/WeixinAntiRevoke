using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WeixinAntiRevoke
{
    internal sealed class MainForm : Form
    {
        private Label brand, tagline, badge, steps, statusTag, stateValue, stateDescription, versionValue, modeTitle, modeHint, actionHint, footer;
        private StatusCard hero;
        private ModernButton autoCheckButton, installButton, restoreButton, browseButton, helpButton, updateButton;
        private EffectCard promptCard, quietCard;
        private InspectionResult lastInspection;
        private bool operationBusy, promptMode = true, manualTarget;
        private string targetPath, lastFailure;
        private readonly StringBuilder activity = new StringBuilder();
        private readonly System.Windows.Forms.Timer animation = new System.Windows.Forms.Timer();
        private readonly Icon applicationIcon;
        private readonly CancellationTokenSource updateLifetime = new CancellationTokenSource();
        private CancellationTokenSource downloadCancellation;
        private ReleaseManifest pendingRelease;
        private bool checkingUpdate, downloadingUpdate;
        internal bool IsAnalysisBusy { get { return operationBusy; } }

        internal MainForm() : this(null, true) { }
        internal MainForm(string path, bool prompt) : this(path, prompt, false) { }
        internal MainForm(string path, bool prompt, bool checkUpdates)
        {
            targetPath = path; manualTarget = !string.IsNullOrWhiteSpace(path); promptMode = prompt;
            Text = "微信防撤回 · " + UpdateService.DisplayVersion;
            applicationIcon = new Icon(typeof(MainForm), "AppIcon.ico"); Icon = applicationIcon;
            AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(900, 640); MinimumSize = new Size(916, 679);
            StartPosition = FormStartPosition.CenterScreen; BackColor = Visual.Background;
            Font = new Font("Microsoft YaHei UI", 10F); ForeColor = Visual.Text;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            brand = NewLabel("微信防撤回", 17F, true, Visual.Text, this);
            tagline = NewLabel("让收到的消息留在眼前", 9F, false, Visual.Muted, this);
            badge = NewLabel(UpdateService.DisplayVersion + "  /  体验版", 9F, false, Visual.Accent, this);
            badge.TextAlign = ContentAlignment.MiddleRight;
            steps = NewLabel("01  检查微信     ─────     02  选择效果     ─────     03  开启使用", 9F, false, Visual.Muted, this);

            hero = new StatusCard(); Controls.Add(hero);
            statusTag = NewLabel("正在准备", 9F, true, Visual.Accent, hero);
            stateValue = NewLabel("正在查找你的微信…", 23F, true, Visual.Text, hero);
            stateDescription = NewLabel("稍等一下，检查完成后就可以设置。", 10F, false, Visual.Muted, hero);
            versionValue = NewLabel("自动查找 · 无需手动设置", 9F, false, Color.FromArgb(128, 156, 182), hero);

            modeTitle = NewLabel("你想怎样保留消息？", 13F, true, Visual.Text, this);
            modeHint = NewLabel("选一个喜欢的方式就好", 9F, false, Visual.Muted, this);
            modeHint.TextAlign = ContentAlignment.MiddleRight;
            promptCard = new EffectCard { Title = "保留消息 + 撤回提醒", Description = "消息留下，同时告诉你对方撤回过。", Footnote = "提醒可能需要切换聊天后显示", AccessibleName = "保留消息并显示撤回提醒", TabIndex = 1 };
            quietCard = new EffectCard { Title = "只保留消息", Description = "消息安静地留下，不显示撤回提醒。", Footnote = "喜欢简单，可以选这个", AccessibleName = "只保留消息，不显示提醒", TabIndex = 2 };
            Controls.Add(promptCard); Controls.Add(quietCard);
            promptCard.Click += delegate { SetSelection(true); };
            quietCard.Click += delegate { SetSelection(false); };

            installButton = NewButton("开启防撤回", true, false, 3); installButton.Enabled = false;
            restoreButton = NewButton("关闭防撤回", false, false, 4); restoreButton.Enabled = false;
            autoCheckButton = NewButton("重新检查", false, true, 0);
            browseButton = NewButton("手动查找微信", false, true, 6);
            helpButton = NewButton("使用帮助", false, true, 5);
            autoCheckButton.Font = browseButton.Font = helpButton.Font = new Font("Microsoft YaHei UI", 9F);
            autoCheckButton.Click += delegate { AutoDetectAndValidate(); };
            installButton.Click += delegate { Install(); };
            restoreButton.Click += delegate { if (downloadingUpdate) { downloadCancellation.Cancel(); restoreButton.Enabled = false; } else Restore(); };
            browseButton.Click += delegate { Browse(); };
            helpButton.Click += delegate { ShowHelp(); };
            updateButton = NewButton("检查软件更新", false, true, 7);
            updateButton.Font = new Font("Microsoft YaHei UI", 9F);
            updateButton.Click += delegate { if (pendingRelease == null) CheckForUpdates(true); else DownloadUpdate(); };
            actionHint = NewLabel("开启前请完全退出微信；首次使用，建议用一条测试消息确认效果。", 9F, false, Visual.Muted, this);
            footer = NewLabel("不读取聊天记录  ·  更新由 GitHub 提供", 8.5F, false, Color.FromArgb(109, 132, 158), this);
            animation.Interval = 50;
            animation.Tick += delegate { hero.Phase = (hero.Phase + 4) % 360; hero.Invalidate(); };
            SetSelection(promptMode);
            Shown += delegate { AutoDetectAndValidate(); if (checkUpdates) CheckForUpdates(false); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (operationBusy) { e.Cancel = true; actionHint.Text = "正在处理，请稍候再关闭窗口。"; } };
            FormClosed += delegate { updateLifetime.Cancel(); animation.Dispose(); applicationIcon.Dispose(); };
            LayoutContent();
        }

        private Label NewLabel(string text, float size, bool bold, Color color, Control parent)
        {
            Label label = new Label { Text = text, Font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = color, BackColor = Color.Transparent, AutoSize = false, AutoEllipsis = true };
            parent.Controls.Add(label); return label;
        }
        private ModernButton NewButton(string text, bool primary, bool quiet, int tab)
        {
            ModernButton b = new ModernButton { Text = text, Primary = primary, Quiet = quiet, TabIndex = tab, AccessibleName = text };
            Controls.Add(b); return b;
        }
        private int S(float value) { return (int)Math.Round(value * DeviceDpi / 96F); }
        private void Place(Control c, float x, float y, float width, float height) { c.SetBounds(S(x), S(y), S(width), S(height)); }
        protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); if (footer != null) LayoutContent(); }
        private void LayoutContent()
        {
            float width = ClientSize.Width * 96F / DeviceDpi, height = ClientSize.Height * 96F / DeviceDpi;
            float inside = width - 64, card = (inside - 16) / 2;
            Place(brand, 80, 23, 280, 33); Place(tagline, 82, 60, 350, 22); Place(badge, width - 200, 29, 164, 26);
            Place(updateButton, width - 222, 64, 190, 29);
            Place(steps, 34, 99, inside - 10, 23);
            Place(hero, 32, 140, inside, 162);
            Place(statusTag, 26, 20, inside - 210, 21); Place(stateValue, 24, 47, inside - 210, 45);
            Place(stateDescription, 26, 101, inside - 210, 24); Place(versionValue, 26, 132, inside - 210, 19);
            Place(modeTitle, 34, 325, 350, 30); Place(modeHint, width - 390, 329, 354, 24);
            Place(promptCard, 32, 368, card, 112); Place(quietCard, 48 + card, 368, card, 112);
            Place(installButton, 32, 504, inside - 208, 54); Place(restoreButton, width - 224, 504, 192, 54);
            Place(actionHint, 34, 571, inside - 4, 30);
            Place(footer, 34, height - 30, inside - 386, 23);
            Place(autoCheckButton, width - 378, height - 36, 104, 28);
            Place(browseButton, width - 273, height - 36, 136, 28); Place(helpButton, width - 137, height - 36, 106, 28);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            Visual.Shield(g, S(31), S(28), S(34), Visual.Accent);
            using (Pen p = new Pen(Color.FromArgb(27, 43, 61))) g.DrawLine(p, S(32), ClientSize.Height - S(47), ClientSize.Width - S(32), ClientSize.Height - S(47));
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int enabled = 1; DwmSetWindowAttribute(Handle, 20, ref enabled, 4); } catch (DllNotFoundException) {} catch (EntryPointNotFoundException) {}
        }
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        private void AutoDetectAndValidate()
        {
            if (operationBusy) return;
            lastFailure = null;
            if (!manualTarget) targetPath = PatchEngine.DetectDefaultTarget();
            string path = targetPath;
            BeginWork("正在检查你的微信…", "稍等一下，检查完成后就可以设置。", delegate { return PatchEngine.Inspect(path); }, null);
        }

        private void SetSelection(bool prompt)
        {
            if (operationBusy || (prompt && promptCard != null && !promptCard.Available)) return;
            promptMode = prompt;
            promptCard.Selected = prompt; quietCard.Selected = !prompt;
            promptCard.Invalidate(); quietCard.Invalidate();
            if (lastInspection != null) ApplyInspection(lastInspection);
        }

        private void ApplyInspection(InspectionResult result)
        {
            versionValue.Text = string.IsNullOrWhiteSpace(result.Version) ? "自动查找 · 无需手动设置" : "当前微信  " + result.Version;
            bool active = result.State == PatchState.PromptPatched || result.State == PatchState.LegacyPatched;
            bool usable = result.State == PatchState.Ready || active;
            promptCard.Available = result.Adaptive == null || result.Adaptive.PromptEdits != null;
            if (!promptCard.Available) promptMode = false;
            promptCard.Selected = promptMode; quietCard.Selected = !promptMode;
            promptCard.Enabled = usable && promptCard.Available && !operationBusy;
            quietCard.Enabled = usable && !operationBusy;
            promptCard.Invalidate(); quietCard.Invalidate();
            modeHint.Text = !promptCard.Available ? "已为你选好当前可用的方式" : "选一个喜欢的方式就好";
            hero.Signal = usable ? Visual.Accent : Color.FromArgb(247, 190, 111);
            statusTag.ForeColor = hero.Signal;
            bool sameMode = (promptMode && result.State == PatchState.PromptPatched) || (!promptMode && result.State == PatchState.LegacyPatched);
            if (active)
            {
                statusTag.Text = "已经开启"; stateValue.Text = "防撤回已开启";
                stateDescription.Text = result.State == PatchState.PromptPatched ? "当前设置：保留消息，并显示撤回提醒。" : "当前设置：保留消息，不显示撤回提醒。";
                installButton.Text = sameMode ? "已开启，去微信试试" : "切换到这个效果";
                actionHint.Text = "首次开启后，建议用一条测试消息确认效果；需要时可随时关闭。";
            }
            else if (result.State == PatchState.Ready)
            {
                statusTag.Text = "检查完成"; stateValue.Text = "微信已找到，准备就绪";
                stateDescription.Text = "选好下方的效果，退出微信后就可以开启。";
                installButton.Text = "开启防撤回";
                actionHint.Text = "开启前请完全退出微信；首次使用，建议用一条测试消息确认效果。";
            }
            else if (result.State == PatchState.Missing)
            {
                statusTag.Text = "需要你的帮助"; stateValue.Text = "还没找到电脑上的微信";
                stateDescription.Text = "请先安装微信，或点击下方「手动查找微信」。";
                installButton.Text = "找到微信后即可开启"; actionHint.Text = "手动查找时，选择平时用来打开微信的程序即可。";
            }
            else
            {
                statusTag.Text = "暂时无法开启";
                stateValue.Text = result.State == PatchState.Ambiguous ? "微信文件有变化，先暂停一下" : "这版微信暂时无法开启";
                stateDescription.Text = "可以更新本工具后重试，或查看「使用帮助」。";
                installButton.Text = "暂时无法开启";
                actionHint.Text = "检查未通过时不会更改微信。遇到问题，可在帮助中复制检查信息。";
            }
            installButton.Enabled = usable && !sameMode && !operationBusy;
            restoreButton.Enabled = active && PatchEngine.HasOriginalBackup(targetPath) && !operationBusy;
            hero.Invalidate();
        }

        private void Install()
        {
            if (operationBusy || lastInspection == null || !installButton.Enabled) return;
            if (!EnsureWeixinClosed()) return;
            if (!EnsurePermission()) return;
            bool prompt = promptMode; string path = targetPath;
            BeginWork("正在为你开启…", "会自动保留备份，请稍候。", delegate { return prompt ? PatchEngine.InstallPrompt(path) : PatchEngine.InstallLegacy(path); }, "已完成。打开微信后，可用一条测试消息确认效果。");
        }

        private void Restore()
        {
            if (operationBusy || lastInspection == null || !restoreButton.Enabled) return;
            if (!EnsureWeixinClosed()) return;
            if (ShowDialogBox("关闭防撤回？", "关闭后，新收到的消息会按微信默认方式处理。\r\n以后想用时，可以再次开启。", "确认关闭", "继续保留", false) != DialogResult.OK) return;
            if (!EnsurePermission()) return;
            string path = targetPath;
            BeginWork("正在为你关闭…", "正在恢复微信原来的设置，请稍候。", delegate { return PatchEngine.Restore(path); }, "已关闭。重新打开微信即可正常使用。");
        }

        private bool EnsureWeixinClosed()
        {
            Process[] processes = Process.GetProcessesByName("Weixin");
            bool open = processes.Length > 0;
            foreach (Process p in processes) p.Dispose();
            if (!open) return true;
            ShowDialogBox("请先退出微信", "请在屏幕右下角找到微信图标，右键选择「退出」。\r\n只关掉聊天窗口还不够。\r\n退出后，回来再点一次就好。", "知道了", null, false);
            return false;
        }

        private bool EnsurePermission()
        {
            if (IsAdministrator()) return true;
            try
            {
                string args = string.IsNullOrWhiteSpace(targetPath) ? "" : "--path \"" + targetPath + "\"";
                if (!promptMode) args += " --quiet";
                Process.Start(new ProcessStartInfo(Application.ExecutablePath, args) { UseShellExecute = true, Verb = "runas" });
                Close();
            }
            catch (Win32Exception ex)
            {
                Log(ex.ToString()); actionHint.Text = ex.NativeErrorCode == 1223 ? "你取消了 Windows 的授权。准备好后，再点击要执行的操作即可。" : "暂时没能获得 Windows 授权，请稍后重试。";
            }
            return false;
        }

        private void Browse()
        {
            if (operationBusy) return;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "找到平时用来打开微信的程序";
                dialog.Filter = "微信程序 (Weixin.exe)|Weixin.exe";
                dialog.CheckFileExists = true;
                if (!string.IsNullOrWhiteSpace(targetPath) && Directory.Exists(Path.GetDirectoryName(targetPath))) dialog.InitialDirectory = Path.GetDirectoryName(targetPath);
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string root = Path.GetDirectoryName(dialog.FileName);
                    string best = null; Version bestVersion = new Version(0, 0);
                    string[] folders = Directory.GetDirectories(root);
                    string[] candidates = new string[folders.Length + 1]; candidates[0] = root; Array.Copy(folders, 0, candidates, 1, folders.Length);
                    foreach (string folder in candidates)
                    {
                        string candidate = Path.Combine(folder, "Weixin.dll");
                        if (!File.Exists(candidate)) continue;
                        Version version;
                        if (Version.TryParse(FileVersionInfo.GetVersionInfo(candidate).FileVersion, out version) && (best == null || version > bestVersion)) { best = candidate; bestVersion = version; }
                    }
                    if (best == null) { ShowDialogBox("这个位置没有找到微信", "请选择平时用来打开微信的程序，然后再试一次。", "知道了", null, false); return; }
                    targetPath = best; manualTarget = true; lastInspection = null;
                    AutoDetectAndValidate();
                }
                catch (Exception ex) { Log(ex.ToString()); ShowDialogBox("暂时无法打开这个位置", "可以先重新打开微信，再回来点击「重新检查」。", "知道了", null, false); }
            }
        }

        private void BeginWork(string title, string description, Func<InspectionResult> work, string success)
        {
            if (operationBusy) return;
            SetBusy(true); statusTag.Text = "请稍候"; stateValue.Text = title; stateDescription.Text = description;
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = work(); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                if (e.Error != null)
                {
                    lastFailure = e.Error.ToString(); Log(lastFailure); lastInspection = null;
                    statusTag.Text = "没有完成"; stateValue.Text = "这次没有完成，请重试";
                    stateDescription.Text = FriendlyFailure(e.Error.Message);
                    actionHint.Text = "先退出微信，再点击「重新检查」。仍有问题可查看使用帮助。";
                    hero.Signal = Color.FromArgb(247, 190, 111); statusTag.ForeColor = hero.Signal;
                }
                else
                {
                    lastFailure = null; lastInspection = (InspectionResult)e.Result; Log(lastInspection.Message);
                }
                SetBusy(false);
                if (success != null && e.Error == null && lastInspection != null && (lastInspection.State == PatchState.Ready || lastInspection.State == PatchState.PromptPatched || lastInspection.State == PatchState.LegacyPatched)) actionHint.Text = success;
                worker.Dispose();
            };
            worker.RunWorkerAsync();
        }
        private string FriendlyFailure(string message)
        {
            if (message.Contains("进程") || message.Contains("退出微信")) return "微信还在运行，请完全退出后再试。";
            if (message.Contains("备份")) return "需要的备份暂时不可用。请保留现有文件，并查看使用帮助。";
            if (message.Contains("改变") || message.Contains("变化")) return "微信刚刚发生变化，请重新检查后再试。";
            if (message.Contains("拒绝访问") || message.Contains("denied")) return "Windows 暂时没有允许这次操作，请重新打开工具再试。";
            return "没有完成这次设置。你可以重新检查，或在帮助中复制检查信息。";
        }
        private void SetBusy(bool busy)
        {
            operationBusy = busy; UseWaitCursor = busy; hero.Working = busy;
            autoCheckButton.Enabled = browseButton.Enabled = helpButton.Enabled = !busy;
            RefreshUpdateButton();
            if (busy)
            {
                installButton.Enabled = restoreButton.Enabled = promptCard.Enabled = quietCard.Enabled = false;
                installButton.Text = "请稍候…"; hero.Signal = Visual.Accent; statusTag.ForeColor = hero.Signal; animation.Start();
            }
            else
            {
                animation.Stop();
                if (lastInspection != null) ApplyInspection(lastInspection);
                else { installButton.Enabled = restoreButton.Enabled = promptCard.Enabled = quietCard.Enabled = false; installButton.Text = "重新检查后再试"; }
            }
            hero.Invalidate();
        }
        private void ShowHelp()
        {
            DialogResult choice = ShowDialogBox("使用其实很简单", "① 等待检查完成，选择喜欢的效果。\r\n② 完全退出微信，再点击开启。\r\n③ 授权时选择「是」，工具重开后再点一次。\r\n\r\n首次开启后，建议用一条测试消息确认效果。\r\n微信更新后，回来重新检查，支持时即可开启。", "知道了", null, true);
            if (choice == DialogResult.Retry)
            {
                try { Clipboard.SetText(BuildReport()); actionHint.Text = "检查信息已复制，可以粘贴给帮你解决问题的人。"; }
                catch (Exception ex) { Log(ex.ToString()); actionHint.Text = "复制暂时没有成功，请稍后再试。"; }
            }
        }
        private string BuildReport()
        {
            return "微信防撤回 " + UpdateService.DisplayVersion + "\r\n" + targetPath + "\r\n" + (lastInspection == null ? "" : lastInspection.Version + "\r\n" + lastInspection.Message + "\r\n" + lastInspection.Sha256 + "\r\n" + (lastInspection.Adaptive == null ? "" : lastInspection.Adaptive.Report)) + "\r\n" + lastFailure + "\r\n" + activity;
        }
        private void Log(string text) { activity.AppendLine(DateTime.Now.ToString("HH:mm:ss") + " " + text); }
        private void RefreshUpdateButton()
        {
            updateButton.Enabled = !checkingUpdate && !operationBusy;
            updateButton.Primary = pendingRelease != null;
            updateButton.Quiet = pendingRelease == null;
            updateButton.Text = checkingUpdate ? "正在检查新版…" : pendingRelease != null ? "发现新版 · 点击更新" : "检查软件更新";
            updateButton.AccessibleName = updateButton.Text; updateButton.Invalidate();
        }
        private async void CheckForUpdates(bool manual)
        {
            if (checkingUpdate || downloadingUpdate) return;
            checkingUpdate = true; RefreshUpdateButton();
            try
            {
                pendingRelease = await UpdateService.CheckAsync(updateLifetime.Token);
                if (IsDisposed || Disposing) return;
                if (manual && pendingRelease == null) ShowDialogBox("已经是最新版本", "当前版本 " + UpdateService.DisplayVersion + "，可以放心继续使用。", "知道了", null, false);
            }
            catch (Exception ex)
            {
                if (IsDisposed || Disposing || updateLifetime.IsCancellationRequested) return;
                Log(ex.ToString());
                if (manual) ShowDialogBox("暂时没能检查更新", "可能暂时连接不上，或新版还未发布完成。\r\n你可以继续使用当前版本，稍后再试。", "知道了", null, false);
            }
            finally { checkingUpdate = false; if (!IsDisposed && !Disposing) RefreshUpdateButton(); }
        }
        private async void DownloadUpdate()
        {
            if (operationBusy || pendingRelease == null) return;
            ReleaseManifest release = pendingRelease;
            string notes = string.IsNullOrWhiteSpace(release.Notes) ? "包含最新改进与修复。" : release.Notes.Replace("\r", " ").Replace("\n", " ");
            if (notes.Length > 80) notes = notes.Substring(0, 80) + "…";
            if (ShowDialogBox("发现新版本 " + release.Version, notes + "\r\n\r\n更新后会重新打开工具，并为你保留旧版。", "立即更新", "稍后再说", false) != DialogResult.OK) return;
            downloadingUpdate = true;
            downloadCancellation = CancellationTokenSource.CreateLinkedTokenSource(updateLifetime.Token);
            SetBusy(true); stateValue.Text = "正在下载新版…"; stateDescription.Text = "正在连接，请稍候。"; statusTag.Text = "更新软件";
            restoreButton.Text = "取消更新"; restoreButton.Enabled = true;
            actionHint.Text = "下载完成后工具会自动重新打开。微信可以继续使用。";
            string stage = null;
            try
            {
                System.Collections.Generic.List<string> args = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(targetPath)) { args.Add("--path"); args.Add(targetPath); }
                if (!promptMode) args.Add("--quiet");
                Progress<int> progress = new Progress<int>(delegate(int value) { if (!IsDisposed && downloadingUpdate) stateDescription.Text = "已下载 " + value + "%"; });
                stage = await UpdateService.PrepareAsync(release, Application.ExecutablePath, args.ToArray(), progress, downloadCancellation.Token);
                downloadCancellation.Token.ThrowIfCancellationRequested();
                restoreButton.Enabled = false;
                UpdateService.StartHelper(stage);
                stage = null; downloadingUpdate = false; SetBusy(false); Close();
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                bool cancelled = downloadCancellation.IsCancellationRequested;
                downloadingUpdate = false; SetBusy(false);
                actionHint.Text = cancelled ? "已取消更新，可以继续使用当前版本。" : "新版暂时没有下载成功，旧版可继续使用。请稍后再试。";
            }
            finally
            {
                if (stage != null) UpdateService.Cleanup(stage);
                downloadCancellation.Dispose(); downloadCancellation = null;
                if (!IsDisposed) { restoreButton.Text = "关闭防撤回"; RefreshUpdateButton(); }
            }
        }
        private DialogResult ShowDialogBox(string title, string body, string positive, string negative, bool copy)
        {
            using (Form dialog = new Form())
            {
                dialog.Text = "微信防撤回"; dialog.Icon = applicationIcon; dialog.AutoScaleDimensions = new SizeF(96, 96); dialog.AutoScaleMode = AutoScaleMode.Dpi;
                dialog.ClientSize = new Size(540, copy ? 320 : 260); dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = dialog.MinimizeBox = false; dialog.StartPosition = FormStartPosition.CenterParent; dialog.BackColor = Visual.Background;
                Label heading = NewLabel(title, 18F, true, Visual.Text, dialog); heading.SetBounds(28, 25, 484, 40);
                Label contents = NewLabel(body, 10F, false, Visual.Muted, dialog); contents.SetBounds(30, 82, 480, copy ? 156 : 102); contents.AutoEllipsis = false;
                ModernButton yes = new ModernButton { Text = positive, Primary = true, DialogResult = DialogResult.OK }; yes.SetBounds(338, dialog.ClientSize.Height - 61, 172, 40); dialog.Controls.Add(yes); dialog.AcceptButton = yes;
                if (negative != null || copy)
                {
                    ModernButton no = new ModernButton { Text = copy ? "复制检查信息" : negative, DialogResult = copy ? DialogResult.Retry : DialogResult.Cancel };
                    no.SetBounds(30, dialog.ClientSize.Height - 61, 194, 40); dialog.Controls.Add(no); if (!copy) dialog.CancelButton = no;
                }
                return dialog.ShowDialog(this);
            }
        }
        private static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent()) return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
