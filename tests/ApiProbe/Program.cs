using System;
using System.IO;
using System.Reflection;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("ApiProbe <Managed directory> <PlanetGauge.dll>");
        AppDomain.CurrentDomain.AssemblyResolve += (_, request) =>
        {
            string name = new AssemblyName(request.Name).Name;
            if (name.EndsWith(".resources", StringComparison.Ordinal)) return null;
            foreach (string folder in new[] { args[0], Path.Combine(args[0], "UnityModManager") })
            {
                string path = Path.Combine(folder, name + ".dll");
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        try
        {
            Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(args[1]));
            assembly.GetType("PlanetGauge.Main", true)
                .GetMethod("ValidateRequiredGameApi", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null);
            Console.WriteLine("PASS: built PlanetGauge.ValidateRequiredGameApi against installed Managed directory.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }
}
