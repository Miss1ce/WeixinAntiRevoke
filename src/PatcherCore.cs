using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace WeixinAntiRevoke
{
    internal enum PatchState
    {
        Missing,
        Ready,
        LegacyPatched,
        PromptPatched,
        Unsupported,
        Ambiguous
    }

    internal sealed class PatchProfile
    {
        public string Version;
        public string RuleVersion;
        public string OriginalSha256;
        public string LegacyPatchedSha256;
        public string PromptPatchedSha256;
        public int[] LegacyOriginalPattern;
        public int[] LegacyPatchedPattern;
        public int LegacyPatchIndex;
        public int[] PromptMessageOriginalPattern;
        public int[] PromptMessagePatchedPattern;
        public int[] PromptIdOriginalPattern;
        public int[] PromptIdPatchedPattern;
        public byte[] PromptMessageReplacement;
        public int PromptMessageExpectedMatches;
        public int PromptIdExpectedMatches;
    }

    internal sealed class InspectionResult
    {
        public PatchState State;
        public PatchProfile Profile;
        public AdaptivePlan Adaptive;
        public string Path;
        public string Version;
        public string Sha256;
        public int LegacyOriginalMatches;
        public int LegacyPatchedMatches;
        public int PromptMessageOriginalMatches;
        public int PromptMessagePatchedMatches;
        public int PromptIdOriginalMatches;
        public int PromptIdPatchedMatches;
        public long MatchOffset = -1;
        public string Message;
    }

    internal static class PatchCatalog
    {
        public static readonly PatchProfile[] Profiles =
        {
            Create411155(),
            Create411226(),
            Create411255(),
            Create411312()
        };

        public static string SupportedVersionsText
        {
            get
            {
                string[] versions = new string[Profiles.Length];
                for (int i = 0; i < Profiles.Length; i++)
                {
                    versions[i] = Profiles[i].Version;
                }
                return string.Join("、", versions);
            }
        }

        public static string RuleVersionsText
        {
            get
            {
                string[] rules = new string[Profiles.Length];
                for (int i = 0; i < Profiles.Length; i++)
                {
                    rules[i] = Profiles[i].Version + " / 规则 " + Profiles[i].RuleVersion;
                }
                return string.Join("；", rules);
            }
        }

        public static PatchProfile FindByVersion(string version)
        {
            foreach (PatchProfile profile in Profiles)
            {
                if (string.Equals(
                    profile.Version,
                    version,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }
            return null;
        }

        private static PatchProfile Create411155()
        {
            return new PatchProfile
            {
                Version = "4.1.11.55",
                RuleVersion = "0.28.4",
                OriginalSha256 =
                    "AB925B9428239DEF44B252D970C337034D75E66B27EB5529633DC10669FC796A",
                LegacyPatchedSha256 =
                    "052F2F6990DE2F1155B6F33D0BE3A8134F362E8BA0E9BA38765B1BCBBE90B952",
                PromptPatchedSha256 =
                    "ACF7E34E7E7CBAC4A7B896236DB2DA7B6DB8010CEA2115E60437C7E7A92D9AD1",
                LegacyOriginalPattern = ParsePattern(
                    "90488986C80100004C89AD080200004C8D05"),
                LegacyPatchedPattern = ParsePattern(
                    "90482986C80100004C89AD080200004C8D05"),
                LegacyPatchIndex = 2,
                PromptMessageOriginalPattern = ParsePattern(
                    "488D55??4531C0E8????????90488B9D????00004885DB"),
                PromptMessagePatchedPattern = ParsePattern(
                    "488D55??488385F00300000190488B9D????00004885DB"),
                PromptIdOriginalPattern = ParsePattern(
                    "48C1E0204909??C644242801C644242000"),
                PromptIdPatchedPattern = ParsePattern(
                    "48C1E0204909??C644242801C644242001"),
                PromptMessageReplacement = BytesFromHex("488385F003000001"),
                PromptMessageExpectedMatches = 2,
                PromptIdExpectedMatches = 1
            };
        }

        private static PatchProfile Create411226()
        {
            return new PatchProfile
            {
                Version = "4.1.12.26",
                RuleVersion = "0.28.4",
                OriginalSha256 =
                    "4914A621A810ECBC0A132B6FF8F612658CFCE323D3989B3E5FE32D4FF343BA46",
                LegacyPatchedSha256 =
                    "AA7F7B7C0A2F3421548D0EFA7519EDB133AF9C70249A81127BDE75C2E6DFB533",
                PromptPatchedSha256 =
                    "C19F99CE25E826C3D65C6853C4AB4F04D330495F0E58D6080E0140167FF9530F",
                // BetterWX-UI revoke2 base instruction for this build. It is
                // retained as a migration signature for old no-prompt patches.
                LegacyOriginalPattern = ParsePattern(
                    "84C00F85????FFFF488B8D????0000E8????????90488986????????4C"),
                LegacyPatchedPattern = ParsePattern(
                    "84C00F85????FFFF488B8D????0000E8????????90482986????????4C"),
                LegacyPatchIndex = 22,
                PromptMessageOriginalPattern = ParsePattern(
                    "488D55??4531C0E8????????90488B9D????00004885DB"),
                PromptMessagePatchedPattern = ParsePattern(
                    "488D55??488385F80300000190488B9D????00004885DB"),
                PromptIdOriginalPattern = ParsePattern(
                    "48C1E0204909??C644242801C644242000"),
                PromptIdPatchedPattern = ParsePattern(
                    "48C1E0204909??C644242801C644242001"),
                PromptMessageReplacement = BytesFromHex("488385F803000001"),
                PromptMessageExpectedMatches = 2,
                PromptIdExpectedMatches = 1
            };
        }

        private static PatchProfile Create411255()
        {
            return new PatchProfile
            {
                Version = "4.1.12.55",
                RuleVersion = "0.28.5",
                OriginalSha256 =
                    "7AD9753D11C2BAF5C900AAC50DDF56A8170AA85C46129D661325FF88505BEFB1",
                LegacyPatchedSha256 =
                    "2E5EC0FDA673972ABF21DCD03C1AEF60227AF8E5AFD6CB92E74467838B04D4B6",
                PromptPatchedSha256 =
                    "3CFED28140F45744F3BF64CF0954972C89941849F6E361780A826E691E10143A",
                // This build retains the 4.1.12 revoke2 instruction layout.
                // The full original and patched file hashes still gate every write.
                LegacyOriginalPattern = ParsePattern(
                    "84C00F85????FFFF488B8D????0000E8????????90488986????????4C"),
                LegacyPatchedPattern = ParsePattern(
                    "84C00F85????FFFF488B8D????0000E8????????90482986????????4C"),
                LegacyPatchIndex = 22,
                PromptMessageOriginalPattern = ParsePattern(
                    "488D55??4531C0E8????????90488B9D????00004885DB"),
                PromptMessagePatchedPattern = ParsePattern(
                    "488D55??488385F80300000190488B9D????00004885DB"),
                PromptIdOriginalPattern = ParsePattern(
                    "48C1E0204909??C644242801C644242000"),
                PromptIdPatchedPattern = ParsePattern(
                    "48C1E0204909??C644242801C644242001"),
                PromptMessageReplacement = BytesFromHex("488385F803000001"),
                PromptMessageExpectedMatches = 2,
                PromptIdExpectedMatches = 1
            };
        }

        private static PatchProfile Create411312()
        {
            return new PatchProfile
            {
                Version = "4.1.13.12",
                RuleVersion = "0.28.6",
                OriginalSha256 =
                    "E3240BF8A4D00593A4B3E6CE6C8B6AC26897622C27F410F6655C4EEE17CB3B6D",
                LegacyPatchedSha256 =
                    "52B907967D05552CAFA8BFF4CAE5D5394B9F370EA27680953E52A935A007B0CD",
                PromptPatchedSha256 =
                    "FC57F46B4448F9A267D475DEE494BE3B329DE419864DFC80998DF09910471399",
                // This build retains the 4.1.12 revoke2 instruction layout.
                LegacyOriginalPattern = ParsePattern(
                    "84C00F85????FFFF488B8D????0000E8????????90488986????????4C"),
                LegacyPatchedPattern = ParsePattern(
                    "84C00F85????FFFF488B8D????0000E8????????90482986????????4C"),
                LegacyPatchIndex = 22,
                PromptMessageOriginalPattern = ParsePattern(
                    "488D55??4531C0E8????????90488B9D????00004885DB"),
                PromptMessagePatchedPattern = ParsePattern(
                    "488D55??488385F80300000190488B9D????00004885DB"),
                PromptIdOriginalPattern = ParsePattern(
                    "48C1E0204909??C644242801C644242000"),
                PromptIdPatchedPattern = ParsePattern(
                    "48C1E0204909??C644242801C644242001"),
                PromptMessageReplacement = BytesFromHex("488385F803000001"),
                PromptMessageExpectedMatches = 2,
                PromptIdExpectedMatches = 1
            };
        }

        private static int[] ParsePattern(string text)
        {
            int[] result = new int[text.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                string token = text.Substring(i * 2, 2);
                result[i] = token == "??"
                    ? -1
                    : int.Parse(
                        token,
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture);
            }
            return result;
        }

        private static byte[] BytesFromHex(string text)
        {
            int[] parsed = ParsePattern(text);
            byte[] result = new byte[parsed.Length];
            for (int i = 0; i < parsed.Length; i++)
            {
                result[i] = (byte)parsed[i];
            }
            return result;
        }
    }

    internal static class BytePattern
    {
        public static List<long> FindAll(byte[] data, int[] pattern)
        {
            List<long> matches = new List<long>();
            if (data == null || pattern == null || pattern.Length == 0 ||
                data.Length < pattern.Length)
            {
                return matches;
            }

            int firstFixed = -1;
            for (int i = 0; i < pattern.Length; i++)
            {
                if (pattern[i] >= 0)
                {
                    firstFixed = i;
                    break;
                }
            }

            if (firstFixed < 0)
            {
                return matches;
            }

            int candidateStart = 0;
            int lastStart = data.Length - pattern.Length;
            while (candidateStart <= lastStart)
            {
                int found = Array.IndexOf(
                    data,
                    checked((byte)pattern[firstFixed]),
                    candidateStart + firstFixed);
                if (found < 0)
                {
                    break;
                }

                int offset = found - firstFixed;
                if (offset > lastStart)
                {
                    break;
                }

                bool matched = true;
                for (int i = 0; i < pattern.Length; i++)
                {
                    if (pattern[i] >= 0 && data[offset + i] != (byte)pattern[i])
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    matches.Add(offset);
                }
                candidateStart = offset + 1;
            }
            return matches;
        }
    }

    internal static class PatchEngine
    {
        public const string BackupSuffix = ".codex-antirevoke.bak";
        public const string LegacySafetyBackupSuffix = ".codex-antirevoke-v1.bak";
        private const string TemporarySuffix = ".codex-antirevoke.tmp";
        private const string RollbackSuffix = ".codex-antirevoke.rollback";

        public static string DetectDefaultTarget()
        {
            List<string> bases = new List<string>();
            AddBase(bases, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            AddBase(bases, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
            AddBase(bases, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

            string bestPath = null;
            Version bestVersion = new Version(0, 0, 0, 0);
            foreach (string basePath in bases)
            {
                string root = Path.Combine(basePath, "Tencent", "Weixin");
                if (!Directory.Exists(root))
                {
                    continue;
                }

                ConsiderCandidate(Path.Combine(root, "Weixin.dll"), ref bestPath, ref bestVersion);
                string[] directories;
                try
                {
                    directories = Directory.GetDirectories(root);
                }
                catch
                {
                    continue;
                }

                foreach (string directory in directories)
                {
                    ConsiderCandidate(
                        Path.Combine(directory, "Weixin.dll"),
                        ref bestPath,
                        ref bestVersion);
                }
            }
            return bestPath;
        }

        public static InspectionResult Inspect(string path)
        {
            InspectionResult result = new InspectionResult();
            result.Path = path ?? string.Empty;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                result.State = PatchState.Missing;
                result.Message = "自动校验失败：没有找到 Weixin.dll。";
                return result;
            }

            if (!string.Equals(
                Path.GetFileName(path),
                "Weixin.dll",
                StringComparison.OrdinalIgnoreCase))
            {
                result.State = PatchState.Unsupported;
                result.Message = "自动校验失败：请选择名为 Weixin.dll 的文件。";
                return result;
            }

            byte[] data;
            try
            {
                if (new FileInfo(path).Length > 512L * 1024 * 1024)
                    throw new InvalidOperationException("DLL 超出当前 512 MB 分析范围。");
                result.Version =
                    FileVersionInfo.GetVersionInfo(path).FileVersion ?? string.Empty;
                data = File.ReadAllBytes(path);
                result.Sha256 = ComputeSha256(data);
            }
            catch (Exception ex)
            {
                result.State = PatchState.Unsupported;
                result.Message = "自动校验失败：无法读取文件：" + ex.Message;
                return result;
            }

            result.Profile = PatchCatalog.FindByVersion(result.Version);
            if (result.Profile == null ||
                (result.Sha256 != result.Profile.OriginalSha256 &&
                 result.Sha256 != result.Profile.LegacyPatchedSha256 &&
                 result.Sha256 != result.Profile.PromptPatchedSha256))
            {
                return AdaptiveEngine.Inspect(path, data, result.Version);
            }

            PatchProfile profile = result.Profile;
            List<long> legacyOriginal =
                BytePattern.FindAll(data, profile.LegacyOriginalPattern);
            List<long> legacyPatched =
                BytePattern.FindAll(data, profile.LegacyPatchedPattern);
            List<long> messageOriginal =
                BytePattern.FindAll(data, profile.PromptMessageOriginalPattern);
            List<long> messagePatched =
                BytePattern.FindAll(data, profile.PromptMessagePatchedPattern);
            List<long> idOriginal =
                BytePattern.FindAll(data, profile.PromptIdOriginalPattern);
            List<long> idPatched =
                BytePattern.FindAll(data, profile.PromptIdPatchedPattern);

            result.LegacyOriginalMatches = legacyOriginal.Count;
            result.LegacyPatchedMatches = legacyPatched.Count;
            result.PromptMessageOriginalMatches = messageOriginal.Count;
            result.PromptMessagePatchedMatches = messagePatched.Count;
            result.PromptIdOriginalMatches = idOriginal.Count;
            result.PromptIdPatchedMatches = idPatched.Count;

            if (IsOriginalState(result))
            {
                result.State = PatchState.Ready;
                result.MatchOffset = messageOriginal[0];
                result.Message = string.Format(
                    CultureInfo.InvariantCulture,
                    "自动校验通过：微信 {0} 原版，可安装带提示防撤回。",
                    profile.Version);
                return result;
            }

            if (IsLegacyState(result))
            {
                result.State = PatchState.LegacyPatched;
                result.MatchOffset = legacyPatched[0];
                result.Message = string.Format(
                    CultureInfo.InvariantCulture,
                    "自动校验通过：检测到微信 {0} 旧版无提示补丁，可安全升级。",
                    profile.Version);
                return result;
            }

            if (IsPromptState(result))
            {
                result.State = PatchState.PromptPatched;
                result.MatchOffset = messagePatched[0];
                result.Message = string.Format(
                    CultureInfo.InvariantCulture,
                    "自动校验通过：微信 {0} 带提示防撤回已安装；提示可能需切换会话刷新。",
                    profile.Version);
                return result;
            }

            if (HasMixedOrExcessivePatterns(result))
            {
                result.State = PatchState.Ambiguous;
                result.Message =
                    "校验发现混合或非唯一补丁特征，已拒绝修改。可使用可信原始备份还原。";
                return result;
            }

            result.State = PatchState.Unsupported;
            result.Message = string.Format(
                CultureInfo.InvariantCulture,
                "微信 {0} 的完整哈希或机器码特征不在已验证目录中；已拒绝修改。",
                profile.Version);
            return result;
        }

        public static InspectionResult InstallPrompt(string targetPath)
        {
            return InstallModeInternal(targetPath, true, true);
        }

        internal static InspectionResult InstallPromptForVerification(string targetPath)
        {
            return InstallModeInternal(targetPath, true, false);
        }

        public static InspectionResult InstallLegacy(string targetPath)
        {
            return InstallModeInternal(targetPath, false, true);
        }

        internal static InspectionResult InstallLegacyForVerification(string targetPath)
        {
            return InstallModeInternal(targetPath, false, false);
        }

        private static InspectionResult InstallModeInternal(
            string targetPath,
            bool promptMode,
            bool requireWeixinClosed)
        {
            if (requireWeixinClosed)
            {
                EnsureWeixinClosed();
            }

            InspectionResult before = Inspect(targetPath);
            if (promptMode && before.State == PatchState.PromptPatched)
            {
                return before;
            }
            if (!promptMode && before.State == PatchState.LegacyPatched)
            {
                return before;
            }
            if (before.State != PatchState.Ready &&
                before.State != PatchState.LegacyPatched &&
                before.State != PatchState.PromptPatched)
            {
                throw new InvalidOperationException(before.Message);
            }

            if (before.Adaptive != null)
                return AdaptiveEngine.Install(targetPath, promptMode, before);

            PatchProfile profile = before.Profile;
            string originalBackupPath = targetPath + BackupSuffix;
            byte[] originalData;
            if (before.State == PatchState.Ready)
            {
                EnsureTrustedOriginalBackup(
                    targetPath,
                    originalBackupPath,
                    true,
                    profile);
                originalData = File.ReadAllBytes(targetPath);
            }
            else
            {
                EnsureTrustedOriginalBackup(
                    targetPath,
                    originalBackupPath,
                    false,
                    profile);
                if (before.State == PatchState.LegacyPatched)
                {
                    EnsureLegacySafetyBackup(targetPath, profile);
                }
                originalData = File.ReadAllBytes(originalBackupPath);
            }

            byte[] replacement = promptMode
                ? BuildPromptPatchedBytes(profile, originalData)
                : BuildLegacyPatchedBytes(profile, originalData);
            ReplaceTargetSafely(
                targetPath,
                replacement,
                promptMode
                    ? profile.PromptPatchedSha256
                    : profile.LegacyPatchedSha256,
                promptMode ? PatchState.PromptPatched : PatchState.LegacyPatched);
            return Inspect(targetPath);
        }

        public static InspectionResult Restore(string targetPath)
        {
            return RestoreInternal(targetPath, true);
        }

        internal static InspectionResult RestoreForVerification(string targetPath)
        {
            return RestoreInternal(targetPath, false);
        }

        private static InspectionResult RestoreInternal(
            string targetPath,
            bool requireWeixinClosed)
        {
            if (requireWeixinClosed)
            {
                EnsureWeixinClosed();
            }

            InspectionResult current = Inspect(targetPath);
            if (current.Adaptive != null)
                return AdaptiveEngine.Restore(targetPath, current);
            PatchProfile profile = current.Profile;
            if (profile == null)
            {
                throw new InvalidOperationException(current.Message);
            }

            string backupPath = targetPath + BackupSuffix;
            if (!File.Exists(backupPath))
            {
                throw new FileNotFoundException(
                    "没有找到本工具创建的原始备份文件。",
                    backupPath);
            }

            byte[] backupData = File.ReadAllBytes(backupPath);
            if (!string.Equals(
                ComputeSha256(backupData),
                profile.OriginalSha256,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "原始备份哈希不正确，为避免损坏微信已拒绝还原。");
            }

            ReplaceTargetSafely(
                targetPath,
                backupData,
                profile.OriginalSha256,
                PatchState.Ready);
            return Inspect(targetPath);
        }

        public static byte[] BuildPromptPatchedBytesForVerification(string sourcePath)
        {
            InspectionResult inspection = Inspect(sourcePath);
            if (inspection.State != PatchState.Ready)
            {
                throw new InvalidOperationException(inspection.Message);
            }
            if (inspection.Adaptive != null)
                return inspection.Adaptive.Build(File.ReadAllBytes(sourcePath), true);
            return BuildPromptPatchedBytes(
                inspection.Profile,
                File.ReadAllBytes(sourcePath));
        }

        internal static byte[] BuildLegacyPatchedBytesForVerification(string sourcePath)
        {
            InspectionResult inspection = Inspect(sourcePath);
            if (inspection.State != PatchState.Ready)
            {
                throw new InvalidOperationException(inspection.Message);
            }
            if (inspection.Adaptive != null)
                return inspection.Adaptive.Build(File.ReadAllBytes(sourcePath), false);
            return BuildLegacyPatchedBytes(
                inspection.Profile,
                File.ReadAllBytes(sourcePath));
        }

        public static bool HasOriginalBackup(string targetPath)
        {
            return !string.IsNullOrWhiteSpace(targetPath) &&
                File.Exists(targetPath + BackupSuffix);
        }

        public static string ComputeFileSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(stream));
            }
        }

        public static string ComputeSha256(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(data));
            }
        }

        private static byte[] BuildLegacyPatchedBytes(
            PatchProfile profile,
            byte[] source)
        {
            if (string.IsNullOrEmpty(profile.LegacyPatchedSha256) ||
                !string.Equals(
                    ComputeSha256(source),
                    profile.OriginalSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "不带提示补丁只能从已验证的原始 DLL 生成。");
            }

            byte[] data = (byte[])source.Clone();
            List<long> matches =
                BytePattern.FindAll(data, profile.LegacyOriginalPattern);
            if (matches.Count != 1)
            {
                throw new InvalidOperationException(
                    "不带提示规则复核失败：原始特征必须唯一匹配。");
            }
            data[checked((int)matches[0] + profile.LegacyPatchIndex)] = 0x29;

            string newHash = ComputeSha256(data);
            if (!string.Equals(
                newHash,
                profile.LegacyPatchedSha256,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "生成的不带提示补丁文件哈希与内置校验值不一致，已停止。");
            }
            return data;
        }

        private static byte[] BuildPromptPatchedBytes(
            PatchProfile profile,
            byte[] source)
        {
            if (!string.Equals(
                ComputeSha256(source),
                profile.OriginalSha256,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "带提示补丁只能从已验证的原始 DLL 生成。");
            }

            byte[] data = (byte[])source.Clone();
            List<long> messageOffsets =
                BytePattern.FindAll(data, profile.PromptMessageOriginalPattern);
            List<long> idOffsets =
                BytePattern.FindAll(data, profile.PromptIdOriginalPattern);
            if (messageOffsets.Count != profile.PromptMessageExpectedMatches ||
                idOffsets.Count != profile.PromptIdExpectedMatches)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "带提示规则复核失败：消息特征 {0} 处，ID 特征 {1} 处。",
                        messageOffsets.Count,
                        idOffsets.Count));
            }

            foreach (long messageOffset in messageOffsets)
            {
                Buffer.BlockCopy(
                    profile.PromptMessageReplacement,
                    0,
                    data,
                    checked((int)messageOffset + 4),
                    profile.PromptMessageReplacement.Length);
            }
            data[checked((int)idOffsets[0] + 16)] = 0x01;

            string newHash = ComputeSha256(data);
            if (!string.Equals(
                newHash,
                profile.PromptPatchedSha256,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "生成的带提示补丁文件哈希与内置校验值不一致，已停止。");
            }
            return data;
        }

        private static void EnsureTrustedOriginalBackup(
            string targetPath,
            string backupPath,
            bool mayCreate,
            PatchProfile profile)
        {
            if (File.Exists(backupPath))
            {
                string existingHash = ComputeFileSha256(backupPath);
                if (!string.Equals(
                    existingHash,
                    profile.OriginalSha256,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "同名原始备份已经存在但哈希不正确，已拒绝覆盖：" + backupPath);
                }
                return;
            }

            if (!mayCreate)
            {
                throw new InvalidOperationException(
                    "检测到旧版无提示补丁，但没有找到可信原始备份，无法安全升级。");
            }

            File.Copy(targetPath, backupPath, false);
            if (!string.Equals(
                ComputeFileSha256(backupPath),
                profile.OriginalSha256,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("备份完成后的哈希校验失败。");
            }
        }

        private static void EnsureLegacySafetyBackup(
            string targetPath,
            PatchProfile profile)
        {
            if (string.IsNullOrEmpty(profile.LegacyPatchedSha256))
            {
                throw new InvalidOperationException(
                    "该版本没有登记旧版补丁哈希，无法进行旧版迁移。");
            }

            string safetyPath = targetPath + LegacySafetyBackupSuffix;
            if (File.Exists(safetyPath))
            {
                if (!string.Equals(
                    ComputeFileSha256(safetyPath),
                    profile.LegacyPatchedSha256,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "旧版补丁安全副本已存在但哈希不正确，已拒绝覆盖：" + safetyPath);
                }
                return;
            }

            File.Copy(targetPath, safetyPath, false);
            if (!string.Equals(
                ComputeFileSha256(safetyPath),
                profile.LegacyPatchedSha256,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("旧版补丁安全副本校验失败。");
            }
        }

        private static void ReplaceTargetSafely(
            string targetPath,
            byte[] replacement,
            string expectedHash,
            PatchState expectedState)
        {
            string temporaryPath = targetPath + TemporarySuffix;
            string rollbackPath = targetPath + RollbackSuffix;
            DeleteOwnTemporaryFile(temporaryPath);
            DeleteOwnTemporaryFile(rollbackPath);

            bool replaced = false;
            bool verified = false;
            try
            {
                File.WriteAllBytes(temporaryPath, replacement);
                if (!string.Equals(
                    ComputeFileSha256(temporaryPath),
                    expectedHash,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("临时文件哈希校验失败。");
                }

                File.Replace(temporaryPath, targetPath, rollbackPath, true);
                replaced = true;
                InspectionResult after = Inspect(targetPath);
                if (after.State != expectedState)
                {
                    throw new InvalidOperationException(
                        "写入后的版本、哈希或机器码复核失败。");
                }
                verified = true;
                DeleteOwnTemporaryFile(rollbackPath);
            }
            catch
            {
                if (replaced && File.Exists(rollbackPath))
                {
                    try
                    {
                        File.Copy(rollbackPath, targetPath, true);
                    }
                    catch
                    {
                        // Retain rollback when recovery itself fails.
                    }
                }
                throw;
            }
            finally
            {
                DeleteOwnTemporaryFile(temporaryPath);
                if (verified)
                {
                    DeleteOwnTemporaryFile(rollbackPath);
                }
            }
        }

        private static bool IsOriginalState(InspectionResult result)
        {
            PatchProfile profile = result.Profile;
            return string.Equals(
                    result.Sha256,
                    profile.OriginalSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                result.LegacyOriginalMatches == 1 &&
                result.LegacyPatchedMatches == 0 &&
                result.PromptMessageOriginalMatches ==
                    profile.PromptMessageExpectedMatches &&
                result.PromptMessagePatchedMatches == 0 &&
                result.PromptIdOriginalMatches == profile.PromptIdExpectedMatches &&
                result.PromptIdPatchedMatches == 0;
        }

        private static bool IsLegacyState(InspectionResult result)
        {
            PatchProfile profile = result.Profile;
            return !string.IsNullOrEmpty(profile.LegacyPatchedSha256) &&
                string.Equals(
                    result.Sha256,
                    profile.LegacyPatchedSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                result.LegacyOriginalMatches == 0 &&
                result.LegacyPatchedMatches == 1 &&
                result.PromptMessageOriginalMatches ==
                    profile.PromptMessageExpectedMatches &&
                result.PromptMessagePatchedMatches == 0 &&
                result.PromptIdOriginalMatches == profile.PromptIdExpectedMatches &&
                result.PromptIdPatchedMatches == 0;
        }

        private static bool IsPromptState(InspectionResult result)
        {
            PatchProfile profile = result.Profile;
            return string.Equals(
                    result.Sha256,
                    profile.PromptPatchedSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                result.LegacyOriginalMatches == 1 &&
                result.LegacyPatchedMatches == 0 &&
                result.PromptMessageOriginalMatches == 0 &&
                result.PromptMessagePatchedMatches ==
                    profile.PromptMessageExpectedMatches &&
                result.PromptIdOriginalMatches == 0 &&
                result.PromptIdPatchedMatches == profile.PromptIdExpectedMatches;
        }

        private static bool HasMixedOrExcessivePatterns(InspectionResult result)
        {
            PatchProfile profile = result.Profile;
            return result.LegacyOriginalMatches > 1 ||
                result.LegacyPatchedMatches > 1 ||
                result.PromptMessageOriginalMatches >
                    profile.PromptMessageExpectedMatches ||
                result.PromptMessagePatchedMatches >
                    profile.PromptMessageExpectedMatches ||
                result.PromptIdOriginalMatches > profile.PromptIdExpectedMatches ||
                result.PromptIdPatchedMatches > profile.PromptIdExpectedMatches ||
                (result.LegacyOriginalMatches > 0 &&
                    result.LegacyPatchedMatches > 0) ||
                (result.PromptMessageOriginalMatches > 0 &&
                    result.PromptMessagePatchedMatches > 0) ||
                (result.PromptIdOriginalMatches > 0 &&
                    result.PromptIdPatchedMatches > 0);
        }

        private static void DeleteOwnTemporaryFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void EnsureWeixinClosed()
        {
            Process[] processes = Process.GetProcessesByName("Weixin");
            try
            {
                if (processes.Length > 0)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "检测到 {0} 个微信进程。请完全退出微信后再操作。",
                            processes.Length));
                }
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }
        }

        private static void AddBase(List<string> bases, string basePath)
        {
            if (!string.IsNullOrWhiteSpace(basePath) && !bases.Contains(basePath))
            {
                bases.Add(basePath);
            }
        }

        private static void ConsiderCandidate(
            string path,
            ref string bestPath,
            ref Version bestVersion)
        {
            if (!File.Exists(path))
            {
                return;
            }

            string versionText;
            try
            {
                versionText = FileVersionInfo.GetVersionInfo(path).FileVersion;
            }
            catch
            {
                return;
            }

            Version version;
            if (!Version.TryParse(versionText, out version))
            {
                return;
            }

            if (bestPath == null || version > bestVersion)
            {
                bestPath = path;
                bestVersion = version;
            }
        }

        private static string ToHex(byte[] bytes)
        {
            char[] chars = new char[bytes.Length * 2];
            const string alphabet = "0123456789ABCDEF";
            for (int i = 0; i < bytes.Length; i++)
            {
                chars[i * 2] = alphabet[bytes[i] >> 4];
                chars[i * 2 + 1] = alphabet[bytes[i] & 0x0F];
            }
            return new string(chars);
        }
    }
}
