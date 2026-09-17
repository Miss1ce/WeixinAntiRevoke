using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace WeixinAntiRevoke
{
    internal static class UpdateHelper
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length != 1) return 2;
            try
            {
                string planPath = Path.GetFullPath(args[0]);
                UpdatePlan plan = PackageRules.ReadJson<UpdatePlan>(File.ReadAllBytes(planPath));
                WaitForParent(plan);
                string backup = Apply(plan, Path.GetDirectoryName(planPath), Launch);
                // Leave the previous executable beside the new one for manual recovery.
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("这次更新没有完成。旧版文件会保留，请稍后重试。\r\n\r\n" + ex.Message, "微信防撤回 · 更新", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 1;
            }
        }
        internal static void WaitForParent(UpdatePlan plan)
        {
            if (plan.ParentId <= 0) throw new InvalidDataException("更新等待信息无效。");
            try
            {
                using (Process parent = Process.GetProcessById(plan.ParentId))
                {
                    if (parent.StartTime.ToUniversalTime().Ticks != plan.ParentStartTicks) return;
                    if (!parent.WaitForExit(30000)) throw new IOException("请先关闭其他正在运行的本工具窗口。");
                }
            }
            catch (ArgumentException) { /* The old process has already exited. */ }
        }
        internal static string Apply(UpdatePlan plan, string stage, Func<string, string[], bool> launch)
        {
            if (plan == null || plan.Release == null || !PackageRules.IsHash(plan.OldHash)) throw new InvalidDataException("更新信息无效。");
            string target = Path.GetFullPath(plan.Target);
            if (!Path.IsPathRooted(plan.Target) || !string.Equals(Path.GetExtension(target), ".exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("更新目标不是工具程序。");
            string package = Path.Combine(Path.GetFullPath(stage), "package.exe");
            if (string.Equals(package, target, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("更新文件位置不正确。");
            AssemblyName old = AssemblyName.GetAssemblyName(target);
            if (old.Name != "WeixinAntiRevoke" || plan.Release.ParsedVersion <= old.Version) throw new InvalidDataException("更新目标或版本不正确。");
            if (!string.Equals(PackageRules.Hash(target), plan.OldHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("工具文件刚发生变化，请重新打开后检查更新。");
            ValidateArguments(plan.Arguments);
            PackageRules.Verify(package, plan.Release);
            string unique = Guid.NewGuid().ToString("N");
            string candidate = target + ".update-" + unique;
            string backup = Path.Combine(Path.GetDirectoryName(target), Path.GetFileNameWithoutExtension(target) + ".previous-" + old.Version.ToString(3) + "-" + unique.Substring(0, 8) + ".exe");
            bool replaced = false;
            try
            {
                File.Copy(package, candidate, false); PackageRules.Verify(candidate, plan.Release);
                // Recheck immediately before atomic replacement; File.Replace keeps a recovery copy.
                if (!string.Equals(PackageRules.Hash(target), plan.OldHash, StringComparison.OrdinalIgnoreCase)) throw new IOException("工具文件发生变化，请重新检查。");
                File.Replace(candidate, target, backup, true); replaced = true;
                PackageRules.Verify(target, plan.Release);
                if (!launch(target, plan.Arguments)) throw new IOException("新版未能正常打开，已尝试恢复旧版。");
                return backup;
            }
            catch (Exception failure)
            {
                if (replaced)
                {
                    try
                    {
                        if (!string.Equals(PackageRules.Hash(backup), plan.OldHash, StringComparison.OrdinalIgnoreCase)) throw new IOException("旧版备份检查未通过。");
                        File.Replace(backup, target, target + ".failed-" + unique, true);
                    }
                    catch (Exception rollback) { throw new IOException("自动恢复没有完成，旧版保留在：" + backup, new AggregateException(failure, rollback)); }
                    try { launch(target, plan.Arguments); } catch { /* Original bytes are restored even when launching fails. */ }
                }
                throw;
            }
            finally { try { File.Delete(candidate); } catch (IOException) {} catch (UnauthorizedAccessException) {} }
        }
        private static void ValidateArguments(string[] arguments)
        {
            if (arguments == null || arguments.Length > 3) throw new InvalidDataException("启动选项无效。");
            for (int i = 0; i < arguments.Length; i++)
            {
                if (arguments[i] == "--quiet") continue;
                if (arguments[i] == "--path" && ++i < arguments.Length && !string.IsNullOrWhiteSpace(arguments[i])) continue;
                throw new InvalidDataException("启动选项无效。");
            }
        }
        internal static bool Launch(string target, string[] args)
        {
            string eventName = "Local\\WeixinAntiRevoke-" + Guid.NewGuid().ToString("N");
            using (EventWaitHandle ready = new EventWaitHandle(false, EventResetMode.ManualReset, eventName))
            {
                string arguments = PackageRules.JoinArguments(args) + " --update-ready " + PackageRules.Quote(eventName);
                using (Process process = Process.Start(new ProcessStartInfo(target, arguments) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(target) }))
                {
                    if (process == null) return false;
                    if (ready.WaitOne(30000)) return true;
                    // Stop only the new tool process that this helper started; never touch Weixin.
                    if (!process.HasExited) { process.Kill(); process.WaitForExit(5000); }
                    return false;
                }
            }
        }
    }
}
