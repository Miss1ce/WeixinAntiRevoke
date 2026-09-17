using System;
using System.IO;
using System.Reflection;
using System.Threading;
#if OLD
[assembly: AssemblyVersion("3.1.1.0")]
#else
[assembly: AssemblyVersion("3.2.0.0")]
#endif
internal static class UpdateFixture
{
    private static int Main(string[] args)
    {
        File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixture-args.txt"), args);
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == "--update-ready") using (EventWaitHandle ready = EventWaitHandle.OpenExisting(args[i + 1])) ready.Set();
        return 0;
    }
}
