using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace WeixinAntiRevoke
{
    internal static class UpdateService
    {
        internal const string ManifestUrl = PackageRules.RepositoryUrl + "/releases/latest/download/update.json";
        internal static Version CurrentVersion { get { return typeof(UpdateService).Assembly.GetName().Version; } }
        internal static string DisplayVersion { get { return CurrentVersion.ToString(3); } }

        internal static Task<ReleaseManifest> CheckAsync(CancellationToken cancel)
        {
            return Task.Run(delegate
            {
                try
                {
                    using (MemoryStream data = new MemoryStream())
                    {
                        Fetch(new Uri(ManifestUrl), data, 65536, TimeSpan.FromSeconds(15), null, cancel);
                        ReleaseManifest release = PackageRules.ReadJson<ReleaseManifest>(data.ToArray());
                        release.Validate(); return release.ParsedVersion > CurrentVersion ? release : null;
                    }
                }
                catch (WebException ex)
                {
                    using (HttpWebResponse response = ex.Response as HttpWebResponse)
                        if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                            throw new InvalidDataException("还没有发布可下载的新版，请稍后再试。", ex);
                    throw;
                }
            }, cancel);
        }

        internal static bool AllowedUri(Uri uri)
        {
            if (uri == null || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0) return false;
            if (uri.Host == "github.com") return uri.AbsolutePath.StartsWith("/Miss1ce/WeixinAntiRevoke/releases/", StringComparison.Ordinal);
            return uri.Host == "release-assets.githubusercontent.com" || uri.Host == "objects.githubusercontent.com";
        }

        internal static void Fetch(Uri source, Stream output, long limit, TimeSpan timeout, Action<long> progress, CancellationToken cancel)
        {
            // Keep normal Windows proxy settings, certificate checks, and TLS validation.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (CancellationTokenSource timed = CancellationTokenSource.CreateLinkedTokenSource(cancel))
            {
                timed.CancelAfter(timeout);
                for (int redirect = 0; redirect < 6; redirect++)
                {
                    if (!AllowedUri(source)) throw new InvalidDataException("更新下载地址不属于本项目。");
                    timed.Token.ThrowIfCancellationRequested();
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(source);
                    request.UserAgent = "WeixinAntiRevoke/" + DisplayVersion;
                    request.AllowAutoRedirect = false; request.Timeout = 12000; request.ReadWriteTimeout = 15000;
                    request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    using (timed.Token.Register(request.Abort))
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        int code = (int)response.StatusCode;
                        if (code == 301 || code == 302 || code == 303 || code == 307 || code == 308)
                        {
                            string location = response.Headers[HttpResponseHeader.Location];
                            if (string.IsNullOrEmpty(location)) throw new InvalidDataException("更新地址无效。");
                            source = new Uri(source, location); continue;
                        }
                        if (code != 200 || response.ContentLength > limit) throw new InvalidDataException("下载内容大小或状态不正确。");
                        using (Stream input = response.GetResponseStream())
                        {
                            byte[] buffer = new byte[65536]; long total = 0; int count;
                            while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                timed.Token.ThrowIfCancellationRequested(); total += count;
                                if (total > limit) throw new InvalidDataException("下载文件超出了预期大小。");
                                output.Write(buffer, 0, count); if (progress != null) progress(total);
                            }
                        }
                        return;
                    }
                }
                throw new InvalidDataException("更新地址跳转次数过多。");
            }
        }

        internal static Task<string> PrepareAsync(ReleaseManifest release, string target, string[] arguments, IProgress<int> progress, CancellationToken cancel)
        {
            return Task.Run(delegate
            {
                release.Validate();
                if (release.ParsedVersion <= CurrentVersion) throw new InvalidDataException("无需安装旧版本。");
                string stage = Path.Combine(Path.GetTempPath(), "WeixinAntiRevoke-update-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(stage);
                try
                {
                    string package = Path.Combine(stage, "package.exe");
                    using (FileStream file = new FileStream(package, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        Fetch(new Uri(release.DownloadUrl), file, release.Size, TimeSpan.FromMinutes(3), delegate(long bytes) { if (progress != null) progress.Report((int)(bytes * 100 / release.Size)); }, cancel);
                    cancel.ThrowIfCancellationRequested(); PackageRules.Verify(package, release);
                    using (Stream helper = typeof(UpdateService).Assembly.GetManifestResourceStream("WeixinAntiRevoke.UpdateHelper.exe"))
                    using (FileStream file = new FileStream(Path.Combine(stage, "UpdateHelper.exe"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        if (helper == null) throw new InvalidDataException("工具缺少更新组件，请重新下载工具。");
                        helper.CopyTo(file);
                    }
                    using (Process parent = Process.GetCurrentProcess())
                        PackageRules.WriteJson(Path.Combine(stage, "plan.json"), new UpdatePlan { Target = Path.GetFullPath(target), OldHash = PackageRules.Hash(target), Release = release, Arguments = arguments, ParentId = parent.Id, ParentStartTicks = parent.StartTime.ToUniversalTime().Ticks });
                    cancel.ThrowIfCancellationRequested(); return stage;
                }
                catch { Cleanup(stage); throw; }
            }, cancel);
        }

        internal static void StartHelper(string stage)
        {
            ProcessStartInfo start = new ProcessStartInfo(Path.Combine(stage, "UpdateHelper.exe"), PackageRules.Quote(Path.Combine(stage, "plan.json")));
            start.UseShellExecute = false; start.CreateNoWindow = true; start.WorkingDirectory = stage;
            using (Process helper = Process.Start(start)) { if (helper == null) throw new IOException("未能启动更新，请稍后重试。"); }
        }
        internal static void Cleanup(string stage)
        {
            if (string.IsNullOrEmpty(stage)) return;
            foreach (string file in new[] { "package.exe", "UpdateHelper.exe", "plan.json" })
                try { File.Delete(Path.Combine(stage, file)); } catch (IOException) {} catch (UnauthorizedAccessException) {}
            try { Directory.Delete(stage, false); } catch (IOException) {} catch (UnauthorizedAccessException) {}
        }
    }
}
