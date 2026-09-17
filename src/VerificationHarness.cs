using System;
using System.IO;

namespace WeixinAntiRevoke
{
    internal static class VerificationHarness
    {
        private static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: VerificationHarness <source Weixin.dll> <sandbox directory>");
                return 2;
            }

            InspectionResult before = PatchEngine.Inspect(args[0]);
            Console.WriteLine(
                "before={0};version={1};sha256={2};offset=0x{3:X}",
                before.State,
                before.Version,
                before.Sha256,
                before.MatchOffset);
            if (before.State != PatchState.Ready)
            {
                Console.Error.WriteLine(before.Message);
                return 3;
            }

            byte[] patched =
                PatchEngine.BuildPromptPatchedBytesForVerification(args[0]);
            Console.WriteLine("prompt_patched_sha256=" + PatchEngine.ComputeSha256(patched));

            Directory.CreateDirectory(args[1]);
            string sandboxDll = Path.Combine(args[1], "Weixin.dll");
            File.Copy(args[0], sandboxDll, true);

            InspectionResult installed =
                PatchEngine.InstallPromptForVerification(sandboxDll);
            Console.WriteLine(
                "installed={0};sha256={1};offset=0x{2:X}",
                installed.State,
                installed.Sha256,
                installed.MatchOffset);
            if (installed.State != PatchState.PromptPatched)
            {
                return 4;
            }

            InspectionResult restored =
                PatchEngine.RestoreForVerification(sandboxDll);
            Console.WriteLine(
                "restored={0};sha256={1};backup={2}",
                restored.State,
                restored.Sha256,
                File.Exists(sandboxDll + PatchEngine.BackupSuffix));
            if (restored.State != PatchState.Ready)
            {
                return 5;
            }

            string migrationDirectory = Path.Combine(args[1], "migration");
            Directory.CreateDirectory(migrationDirectory);
            string migrationDll = Path.Combine(migrationDirectory, "Weixin.dll");
            File.WriteAllBytes(
                migrationDll,
                PatchEngine.BuildLegacyPatchedBytesForVerification(args[0]));
            File.Copy(args[0], migrationDll + PatchEngine.BackupSuffix, true);

            InspectionResult legacy = PatchEngine.Inspect(migrationDll);
            Console.WriteLine(
                "legacy={0};sha256={1}",
                legacy.State,
                legacy.Sha256);
            if (legacy.State != PatchState.LegacyPatched)
            {
                return 6;
            }

            InspectionResult migrated =
                PatchEngine.InstallPromptForVerification(migrationDll);
            Console.WriteLine(
                "migrated={0};sha256={1}",
                migrated.State,
                migrated.Sha256);
            if (migrated.State != PatchState.PromptPatched)
            {
                return 7;
            }

            InspectionResult converted =
                PatchEngine.InstallLegacyForVerification(migrationDll);
            Console.WriteLine(
                "converted={0};sha256={1}",
                converted.State,
                converted.Sha256);
            if (converted.State != PatchState.LegacyPatched)
            {
                return 8;
            }

            return 0;
        }
    }
}
