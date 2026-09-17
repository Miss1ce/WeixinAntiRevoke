using System;
using System.IO;

namespace WeixinAntiRevoke
{
    internal static class AdaptiveEngine
    {
        internal static InspectionResult Inspect(string path, byte[] current, string version)
        {
            InspectionResult result = new InspectionResult { Path = path, Version = version, Sha256 = PatchEngine.ComputeSha256(current), State = PatchState.Unsupported };
            try
            {
                byte[] original;
                string backup = path + PatchEngine.BackupSuffix;
                try
                {
                    original = Authenticode.ReadTrusted(path);
                    PeImage.Need(PatchEngine.ComputeSha256(original) == result.Sha256, "目标在读取期间发生变化，请重新分析");
                }
                catch (InvalidOperationException)
                {
                    if (!File.Exists(backup)) throw;
                    original = Authenticode.ReadTrusted(backup);
                    PeImage.Need(System.Diagnostics.FileVersionInfo.GetVersionInfo(backup).FileVersion == version, "原始备份与当前 DLL 版本不同");
                }
                AdaptivePlan plan = AdaptiveAnalyzer.Analyze(original);
                result.Adaptive = plan;
                if (result.Sha256 == plan.OriginalHash) result.State = PatchState.Ready;
                else if (result.Sha256 == plan.LegacyHash) result.State = PatchState.LegacyPatched;
                else if (plan.PromptHash != null && result.Sha256 == plan.PromptHash) result.State = PatchState.PromptPatched;
                else { result.State = PatchState.Ambiguous; result.Message = "当前文件与原版及本工具推导的补丁均不一致，已拒绝安装。"; return result; }
                result.Message = result.State == PatchState.Ready
                    ? "本地结构分析通过（实验性）；" + (plan.PromptEdits != null ? "两种模式均可试用。" : "仅不带提示模式可试用。")
                    : result.State == PatchState.PromptPatched ? "已安装本地推导的带提示补丁（实验性）。" : "已安装本地推导的不带提示补丁（实验性）。";
                result.MatchOffset = plan.LegacyEdits[0].Offset;
            }
            catch (Exception ex) { result.Message = "自动分析未通过：" + ex.Message; }
            return result;
        }

        internal static InspectionResult Install(string path, bool prompt, InspectionResult before)
        {
            AdaptivePlan plan = before.Adaptive;
            PeImage.Need(plan != null, "没有可用的分析结果");
            string backup = path + PatchEngine.BackupSuffix;
            byte[] original = Authenticode.ReadTrusted(before.State == PatchState.Ready ? path : backup);
            PeImage.Need(PatchEngine.ComputeSha256(original) == plan.OriginalHash, "原文件或备份已改变，请重新分析");
            byte[] patched = plan.Build(original, prompt);
            string expected = prompt ? plan.PromptHash : plan.LegacyHash;
            PeImage.Need(PatchEngine.ComputeSha256(patched) == expected, "副本生成结果不一致");
            if (File.Exists(backup))
                PeImage.Need(PatchEngine.ComputeFileSha256(backup) == plan.OriginalHash, "已有备份与当前原文件不同，已拒绝覆盖");
            else
            {
                PeImage.Need(before.State == PatchState.Ready, "缺少原始备份");
                using (FileStream output = new FileStream(backup, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { output.Write(original, 0, original.Length); output.Flush(true); }
                PeImage.Need(PatchEngine.ComputeFileSha256(backup) == plan.OriginalHash, "原始备份写入校验失败");
            }
            return Replace(path, patched, before.Sha256, expected, prompt ? PatchState.PromptPatched : PatchState.LegacyPatched);
        }

        internal static InspectionResult Restore(string path, InspectionResult current)
        {
            PeImage.Need(current.Adaptive != null && (current.State == PatchState.Ready || current.State == PatchState.LegacyPatched || current.State == PatchState.PromptPatched), "当前文件状态无法确认，已停止自动还原");
            byte[] backup = Authenticode.ReadTrusted(path + PatchEngine.BackupSuffix);
            PeImage.Need(PatchEngine.ComputeSha256(backup) == current.Adaptive.OriginalHash, "原始备份已改变");
            return Replace(path, backup, current.Sha256, current.Adaptive.OriginalHash, PatchState.Ready);
        }

        private static InspectionResult Replace(string path, byte[] replacement, string beforeHash, string afterHash, PatchState expectedState)
        {
            string temporary = path + ".codex-adaptive-" + Guid.NewGuid().ToString("N") + ".tmp";
            string rollback = temporary + ".rollback";
            bool replaced = false;
            try
            {
                using (FileStream output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { output.Write(replacement, 0, replacement.Length); output.Flush(true); }
                PeImage.Need(PatchEngine.ComputeFileSha256(temporary) == afterHash, "写入临时文件校验失败");
                PeImage.Need(PatchEngine.ComputeFileSha256(path) == beforeHash, "目标文件已改变，请重新分析");
                File.Replace(temporary, path, rollback, true); replaced = true;
                PeImage.Need(PatchEngine.ComputeFileSha256(path) == afterHash, "替换后文件校验失败");
                InspectionResult inspection = PatchEngine.Inspect(path);
                PeImage.Need(inspection.State == expectedState, "替换后状态复核失败：" + inspection.Message);
                File.Delete(rollback);
                return inspection;
            }
            catch
            {
                if (replaced && File.Exists(rollback)) { try { File.Copy(rollback, path, true); } catch {} }
                throw;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
