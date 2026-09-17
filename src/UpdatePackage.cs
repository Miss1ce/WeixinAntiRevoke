using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WeixinAntiRevoke
{
    // Populated by DataContractJsonSerializer in the standalone helper.
#pragma warning disable 0649
    [DataContract]
    internal sealed class ReleaseManifest
    {
        [DataMember(Name = "schema", IsRequired = true)] public int Schema;
        [DataMember(Name = "version", IsRequired = true)] public string Version;
        [DataMember(Name = "sha256", IsRequired = true)] public string Sha256;
        [DataMember(Name = "size", IsRequired = true)] public long Size;
        [DataMember(Name = "notes")] public string Notes;
        internal System.Version ParsedVersion { get { return PackageRules.ParseVersion(Version); } }
        internal string DownloadUrl { get { return PackageRules.RepositoryUrl + "/releases/download/v" + Version + "/WeixinAntiRevoke.exe"; } }
        internal void Validate()
        {
            if (Schema != 1 || Size < 1024 || Size > PackageRules.MaxPackageBytes || !PackageRules.IsHash(Sha256))
                throw new InvalidDataException("新版信息不完整，请稍后重试。");
            PackageRules.ParseVersion(Version);
            if (Notes != null && Notes.Length > 2000) throw new InvalidDataException("更新说明过长。");
        }
    }

    [DataContract]
    internal sealed class UpdatePlan
    {
        [DataMember] public string Target;
        [DataMember] public string OldHash;
        [DataMember] public ReleaseManifest Release;
        [DataMember] public string[] Arguments;
        [DataMember] public int ParentId;
        [DataMember] public long ParentStartTicks;
    }

    internal static class PackageRules
    {
        internal const string RepositoryUrl = "https://github.com/Miss1ce/WeixinAntiRevoke";
        internal const long MaxPackageBytes = 64 * 1024 * 1024;
        internal static T ReadJson<T>(byte[] bytes)
        {
            if (bytes.Length > 65536) throw new InvalidDataException("更新信息过大。");
            using (MemoryStream input = new MemoryStream(bytes))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(input);
        }
        internal static void WriteJson<T>(string path, T value)
        {
            using (FileStream output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                new DataContractJsonSerializer(typeof(T)).WriteObject(output, value);
        }
        internal static Version ParseVersion(string value)
        {
            if (value == null || !Regex.IsMatch(value, @"\A(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\z"))
                throw new InvalidDataException("更新版本号无效。");
            Version parsed = new Version(value);
            if (parsed.Major > 65535 || parsed.Minor > 65535 || parsed.Build > 65535) throw new InvalidDataException("更新版本号无效。");
            return new Version(parsed.Major, parsed.Minor, parsed.Build, 0);
        }
        internal static bool IsHash(string value) { return value != null && Regex.IsMatch(value, @"\A[a-fA-F0-9]{64}\z"); }
        internal static string Hash(string file)
        {
            using (SHA256 sha = SHA256.Create()) using (FileStream input = File.OpenRead(file))
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
        }
        internal static void Verify(string file, ReleaseManifest release)
        {
            release.Validate();
            if (new FileInfo(file).Length != release.Size || !string.Equals(Hash(file), release.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("新版下载不完整，请重新下载。");
            AssemblyName assembly = AssemblyName.GetAssemblyName(file);
            if (assembly.Name != "WeixinAntiRevoke" || assembly.Version != release.ParsedVersion)
                throw new InvalidDataException("下载的程序与版本信息不一致。");
        }
        // Windows command-line quoting; arguments never pass through a command shell.
        internal static string Quote(string value)
        {
            if (value == null || value.IndexOf('\0') >= 0) throw new ArgumentException("Invalid argument.");
            StringBuilder result = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { result.Append('\\', slashes * 2 + 1); result.Append(c); }
                else { result.Append('\\', slashes); result.Append(c); }
                slashes = 0;
            }
            result.Append('\\', slashes * 2); return result.Append('"').ToString();
        }
        internal static string JoinArguments(string[] values)
        {
            StringBuilder output = new StringBuilder();
            if (values != null) foreach (string value in values) { if (output.Length > 0) output.Append(' '); output.Append(Quote(value)); }
            return output.ToString();
        }
    }
}
