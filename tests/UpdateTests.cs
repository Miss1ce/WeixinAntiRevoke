using System;
using System.IO;
using System.Text;
using System.Threading;
using WeixinAntiRevoke;

internal static class UpdateTests
{
    private static string root, oldFixture, newFixture;
    private static int passed;
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
    private static void Reject(Action action, string message)
    {
        bool rejected = false; try { action(); } catch { rejected = true; }
        Assert(rejected, message);
    }
    private static UpdatePlan Plan(string name, out string stage)
    {
        stage = Path.Combine(root, name); Directory.CreateDirectory(stage);
        string target = Path.Combine(stage, "微信工具 & 'old'.exe");
        File.Copy(oldFixture, target); File.Copy(newFixture, Path.Combine(stage, "package.exe"));
        return new UpdatePlan { Target = target, OldHash = PackageRules.Hash(target), Release = new ReleaseManifest { Schema = 1, Version = "3.2.0", Size = new FileInfo(newFixture).Length, Sha256 = PackageRules.Hash(newFixture), Notes = "更新检查" }, Arguments = new[] { "--path", "C:\\微信 & 测试\\Weixin.dll", "--quiet" } };
    }
    private static int Main(string[] args)
    {
        try
        {
            root = args[0]; oldFixture = args[1]; newFixture = args[2];
            Directory.CreateDirectory(root);
            string stage; UpdatePlan plan = Plan("success", out stage);
            string backup = UpdateHelper.Apply(plan, stage, UpdateHelper.Launch);
            Assert(PackageRules.Hash(plan.Target) == plan.Release.Sha256, "new executable installed");
            Assert(PackageRules.Hash(backup) == plan.OldHash, "previous executable preserved");
            string[] received = File.ReadAllLines(Path.Combine(stage, "fixture-args.txt"));
            Assert(received[0] == "--path" && received[1] == plan.Arguments[1] && received[2] == "--quiet", "Unicode and metacharacter arguments preserved without a shell");

            plan = Plan("rollback", out stage); int attempts = 0;
            Reject(delegate { UpdateHelper.Apply(plan, stage, delegate(string exe, string[] launchArgs) { attempts++; return attempts > 1; }); }, "failed startup rejects update");
            Assert(PackageRules.Hash(plan.Target) == plan.OldHash && attempts == 2, "failed startup restores and reopens original");

            plan = Plan("tamper", out stage); File.AppendAllText(Path.Combine(stage, "package.exe"), "modified");
            Reject(delegate { UpdateHelper.Apply(plan, stage, delegate { throw new Exception("must not launch"); }); }, "tampered package rejected");
            Assert(PackageRules.Hash(plan.Target) == plan.OldHash, "tampering leaves original unchanged");

            plan = Plan("hash", out stage); plan.Release.Sha256 = new string('0', 64);
            Reject(delegate { UpdateHelper.Apply(plan, stage, delegate { return true; }); }, "wrong checksum rejected");
            Assert(PackageRules.Hash(plan.Target) == plan.OldHash, "checksum rejection leaves original unchanged");

            plan = Plan("target-change", out stage); File.AppendAllText(plan.Target, "changed by another process");
            string changed = PackageRules.Hash(plan.Target);
            Reject(delegate { UpdateHelper.Apply(plan, stage, delegate { return true; }); }, "concurrent target change rejected");
            Assert(PackageRules.Hash(plan.Target) == changed, "concurrent target contents preserved");

            plan = Plan("locked", out stage);
            using (FileStream locked = new FileStream(plan.Target, FileMode.Open, FileAccess.Read, FileShare.Read))
                Reject(delegate { UpdateHelper.Apply(plan, stage, delegate { return true; }); }, "locked target fails safely");
            Assert(PackageRules.Hash(plan.Target) == plan.OldHash, "locked target leaves original unchanged");

            plan = Plan("downgrade", out stage); plan.Release.Version = "3.0.0";
            Reject(delegate { UpdateHelper.Apply(plan, stage, delegate { return true; }); }, "downgrade rejected");
            plan = Plan("assembly-version", out stage); plan.Release.Version = "3.3.0";
            Reject(delegate { UpdateHelper.Apply(plan, stage, delegate { return true; }); }, "manifest and executable version mismatch rejected");
            plan = Plan("arguments", out stage); plan.Arguments = new[] { "--run-command", "bad" };
            Reject(delegate { UpdateHelper.Apply(plan, stage, delegate { return true; }); }, "unexpected restart options rejected");

            foreach (string bad in new[] { "3.2", "../3.2.0", "3.2.0-beta", "3.2.0.1", "03.2.0", "99999.2.0" })
                Reject(delegate { PackageRules.ParseVersion(bad); }, "reject invalid version " + bad);
            Assert(PackageRules.ParseVersion("3.10.0") > PackageRules.ParseVersion("3.9.9"), "numeric version comparison");
            Reject(delegate { PackageRules.ReadJson<ReleaseManifest>(Encoding.UTF8.GetBytes("{\"version\":\"3.2.0\"}")); }, "missing manifest fields rejected");
            Reject(delegate { PackageRules.ReadJson<ReleaseManifest>(new byte[65537]); }, "oversized manifest rejected");
            Assert(UpdateService.AllowedUri(new Uri("https://github.com/Miss1ce/WeixinAntiRevoke/releases/latest/download/update.json")), "own release URL allowed");
            Assert(UpdateService.AllowedUri(new Uri("https://release-assets.githubusercontent.com/github-production-release-asset/asset")), "GitHub asset redirect allowed");
            foreach (string bad in new[] { "http://github.com/Miss1ce/WeixinAntiRevoke/releases/a", "https://evil.example/a", "https://github.com/other/repo/releases/a", "https://github.com:444/Miss1ce/WeixinAntiRevoke/releases/a", "https://user@github.com/Miss1ce/WeixinAntiRevoke/releases/a" })
                Assert(!UpdateService.AllowedUri(new Uri(bad)), "reject foreign or insecure download URL");
            using (CancellationTokenSource cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                Reject(delegate { UpdateService.CheckAsync(cancelled.Token).GetAwaiter().GetResult(); }, "cancelled check exits without network");
            }
            Console.WriteLine("ALL " + passed + " UPDATE CHECKS PASS; sandbox files only"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
