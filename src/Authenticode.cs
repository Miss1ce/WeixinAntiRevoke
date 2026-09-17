using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace WeixinAntiRevoke
{
    internal static class Authenticode
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct FileInfo { public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public IntPtr Handle, KnownSubject; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TrustData
        {
            public uint Size; public IntPtr Policy, Sip;
            public uint UI, Revocation, UnionChoice; public IntPtr File;
            public uint StateAction; public IntPtr StateData, Url;
            public uint Flags, Context; public IntPtr SignatureSettings;
        }
        [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int WinVerifyTrust(IntPtr window, [In] ref Guid action, ref TrustData data);

        internal static byte[] ReadTrusted(string path)
        {
            // Keep the file stable while verifying its signature and reading the bytes.
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                PeImage.Need(stream.Length >= 256 && stream.Length <= 512L * 1024 * 1024, "DLL 文件大小超出分析范围");
                Guid action = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
                FileInfo file = new FileInfo { Size = (uint)Marshal.SizeOf(typeof(FileInfo)), Path = System.IO.Path.GetFullPath(path) };
                IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(FileInfo)));
                Marshal.StructureToPtr(file, ptr, false);
                TrustData trust = new TrustData { Size = (uint)Marshal.SizeOf(typeof(TrustData)), UI = 2, UnionChoice = 1, File = ptr, StateAction = 1, Flags = 0x1000 };
                try
                {
                    int result = WinVerifyTrust(new IntPtr(-1), ref action, ref trust);
                    PeImage.Need(result == 0, string.Format("原文件的 Windows 数字签名校验未通过（0x{0:X8}）；需要腾讯原始 DLL 或有效原始备份", result));
                    using (X509Certificate2 certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path)))
                    {
                        string publisher = certificate.GetNameInfo(X509NameType.SimpleName, false);
                        PeImage.Need(string.Equals(publisher, "Tencent Technology (Shenzhen) Company Limited", StringComparison.OrdinalIgnoreCase), "文件签名发布者不是预期的腾讯发布者");
                    }
                    byte[] data = new byte[checked((int)stream.Length)];
                    int read = 0;
                    while (read < data.Length)
                    { int count = stream.Read(data, read, data.Length - read); if (count == 0) throw new EndOfStreamException(); read += count; }
                    return data;
                }
                finally
                {
                    trust.StateAction = 2;
                    WinVerifyTrust(new IntPtr(-1), ref action, ref trust);
                    Marshal.DestroyStructure(ptr, typeof(FileInfo));
                    Marshal.FreeHGlobal(ptr);
                }
            }
        }
    }
}
