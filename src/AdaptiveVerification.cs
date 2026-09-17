using System;
using System.IO;

namespace WeixinAntiRevoke
{
    internal static class AdaptiveVerification
    {
        private static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); }
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 2) return 2;
                string source = Path.GetFullPath(args[0]), sandbox = Path.GetFullPath(args[1]);
                Assert(sandbox.IndexOf("test", StringComparison.OrdinalIgnoreCase) >= 0 && !sandbox.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase), "sandbox location");
                Directory.CreateDirectory(sandbox);
                string target = Path.Combine(sandbox, "Weixin.dll");
                Assert(!File.Exists(target), "fresh sandbox");
                File.Copy(source, target);
                InspectionResult inspected = PatchEngine.Inspect(target);
                Console.WriteLine(inspected.Message);
                Assert(inspected.State == PatchState.Ready && inspected.Adaptive != null, "new version analyzed without catalog entry");
                Assert(inspected.Adaptive.PromptEdits != null, "prompt mode derived");
                File.WriteAllText(Path.Combine(sandbox, "analysis.txt"), inspected.Adaptive.Report, System.Text.Encoding.UTF8);
                Console.WriteLine(inspected.Adaptive.Report);
                byte[] original = File.ReadAllBytes(target);
                AdaptivePlan plan = inspected.Adaptive;
                byte[] prompt = plan.Build(original, true);
                Assert(PatchEngine.ComputeSha256(prompt) == plan.PromptHash, "generated prompt hash");
                int changes = 0;
                for (int n = 0; n < original.Length; n++)
                {
                    if (original[n] == prompt[n]) continue;
                    changes++;
                    bool allowed = false;
                    foreach (PatchEdit e in plan.PromptEdits) if (n >= e.Offset && n < e.Offset + e.After.Length) allowed = true;
                    AssertQuiet(allowed, "unintended change");
                }
                Assert(changes > 0 && changes <= 9 && plan.PromptEdits.Length == 2, "only selected handler and ID flag modified");
                InspectionResult installed = PatchEngine.InstallPromptForVerification(target);
                Assert(installed.State == PatchState.PromptPatched && installed.Sha256 == plan.PromptHash, "prompt installation + recognition from signed backup");
                Assert(PatchEngine.ComputeFileSha256(target + PatchEngine.BackupSuffix) == plan.OriginalHash, "original backup exact");
                InspectionResult legacy = PatchEngine.InstallLegacyForVerification(target);
                Assert(legacy.State == PatchState.LegacyPatched && legacy.Sha256 == plan.LegacyHash, "switch prompt to no-prompt");
                InspectionResult switched = PatchEngine.InstallPromptForVerification(target);
                Assert(switched.State == PatchState.PromptPatched, "switch no-prompt to prompt");
                InspectionResult restored = PatchEngine.RestoreForVerification(target);
                Assert(restored.State == PatchState.Ready && restored.Sha256 == plan.OriginalHash, "restore byte-for-byte");
                byte[] changed = (byte[])original.Clone();
                changed[plan.PromptEdits[0].Offset] ^= 1;
                bool refused = false;
                try { plan.Build(changed, true); } catch (InvalidOperationException) { refused = true; }
                Assert(refused, "stale analysis cannot patch changed DLL");
                File.WriteAllBytes(target, changed);
                InspectionResult bad = PatchEngine.Inspect(target);
                Assert(bad.State != PatchState.Ready && bad.State != PatchState.PromptPatched && bad.State != PatchState.LegacyPatched, "modified DLL rejected even with valid backup");
                refused = false;
                try { PatchEngine.InstallPromptForVerification(target); } catch (InvalidOperationException) { refused = true; }
                Assert(refused, "install refuses altered DLL");
                File.WriteAllBytes(target, original);
                string backup = target + PatchEngine.BackupSuffix;
                File.WriteAllBytes(backup, changed);
                refused = false;
                try { PatchEngine.InstallPromptForVerification(target); } catch (InvalidOperationException) { refused = true; }
                Assert(refused && PatchEngine.ComputeFileSha256(target) == plan.OriginalHash, "bad backup blocks write without touching DLL");
                File.WriteAllBytes(backup, original);
                File.WriteAllBytes(target, prompt);
                File.Move(backup, backup + ".saved");
                Assert(PatchEngine.Inspect(target).State == PatchState.Unsupported, "patched DLL without signed original rejected");
                File.Move(backup + ".saved", backup);
                Assert(PatchEngine.RestoreForVerification(target).State == PatchState.Ready, "restore after restart-style reanalysis");
                // A changed architecture cannot be accepted merely by matching a byte signature.
                byte[] wrongArch = (byte[])original.Clone();
                int header = BitConverter.ToInt32(wrongArch, 0x3C); wrongArch[header + 4] = 0x64; wrongArch[header + 5] = 0xAA;
                refused = false;
                try { AdaptiveAnalyzer.Analyze(wrongArch); } catch (InvalidOperationException) { refused = true; }
                Assert(refused, "ARM64 rejected");
                byte[] missingId = (byte[])original.Clone();
                missingId[plan.PromptEdits[1].Offset] = 0x90;
                AdaptivePlan limited = AdaptiveAnalyzer.Analyze(missingId);
                Assert(limited.PromptEdits == null && limited.PromptFailure != null && limited.LegacyEdits != null, "missing prompt evidence disables prompt independently");
                byte[] duplicate = new byte[original.Length + 64];
                Buffer.BlockCopy(original, 0, duplicate, 0, original.Length);
                Buffer.BlockCopy(original, plan.LegacyEdits[0].Offset - 21, duplicate, original.Length, 32);
                refused = false;
                try { AdaptiveAnalyzer.Analyze(duplicate); } catch (InvalidOperationException) { refused = true; }
                Assert(refused, "duplicate parser signature rejected");
                Assert(PatchEngine.ComputeFileSha256(source) == plan.OriginalHash, "installed Weixin DLL unchanged");
                Console.WriteLine("ALL PASS; actual message behavior not tested");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        private static void AssertQuiet(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
