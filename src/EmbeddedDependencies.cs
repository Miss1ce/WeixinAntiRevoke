using System;
using System.IO;
using System.Reflection;

namespace WeixinAntiRevoke
{
    internal static class EmbeddedDependencies
    {
        internal static void Initialize()
        {
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs args)
            {
                if (new AssemblyName(args.Name).Name != "Iced") return null;
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WeixinAntiRevoke.Iced.dll"))
                {
                    if (stream == null) return null;
                    byte[] bytes = new byte[checked((int)stream.Length)];
                    int offset = 0;
                    while (offset < bytes.Length) { int count = stream.Read(bytes, offset, bytes.Length - offset); if (count == 0) throw new EndOfStreamException(); offset += count; }
                    return Assembly.Load(bytes);
                }
            };
        }
    }
}
