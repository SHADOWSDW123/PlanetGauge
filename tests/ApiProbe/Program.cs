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
            VerifyGaugeValueColor(assembly);
            Console.WriteLine("PASS: required API and gauge value color settings/priority checks.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    private static void VerifyGaugeValueColor(Assembly assembly)
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.Public | BindingFlags.NonPublic;
        Type settingsType = assembly.GetType("PlanetGauge.PlanetGaugeSettings", true);
        object settings = Activator.CreateInstance(settingsType);
        settingsType.GetField("MainGaugeValueColorR", all).SetValue(settings, -7);
        settingsType.GetField("MainGaugeValueColorG", all).SetValue(settings, 300);
        settingsType.GetField("MainGaugeValueColorB", all).SetValue(settings, 128);
        settingsType.GetMethod("Sanitize", all).Invoke(settings, null);
        object userColor = settingsType.GetMethod("GetMainGaugeValueColor", all).Invoke(settings, null);
        ExpectColor(userColor, 0, 255, 128, "sanitized user text color");

        Type channelType = assembly.GetType("PlanetGauge.PlanetGaugeRateChannel", true);
        object disabledChannel = channelType.GetProperty("Disabled", all).GetValue(null, null);
        Type eventType = assembly.GetType("PlanetGauge.PlanetGaugeEventSettings", true);
        ConstructorInfo constructor = eventType.GetConstructors(all)[0];
        object blockedRecovery = constructor.Invoke(new[]
        {
            (object)true, disabledChannel, disabledChannel,
            100f, 100f, 100f, false, true, false, 100f, false
        });
        MethodInfo resolver = assembly.GetType("PlanetGauge.MainGaugeHud", true)
            .GetMethod("ResolveGaugeValueColor", all);
        object dependent = resolver.Invoke(null, new[] { blockedRecovery, userColor, (object)false });
        object independent = resolver.Invoke(null, new[] { blockedRecovery, userColor, (object)true });
        ExpectColor(dependent, 176, 32, 32, "SetPlanetGauge color when dependent");
        ExpectColor(independent, 0, 255, 128, "user text color when independent");
    }

    private static void ExpectColor(object color, byte r, byte g, byte b, string name)
    {
        Type type = color.GetType();
        byte actualR = (byte)type.GetField("r").GetValue(color);
        byte actualG = (byte)type.GetField("g").GetValue(color);
        byte actualB = (byte)type.GetField("b").GetValue(color);
        if (actualR != r || actualG != g || actualB != b)
            throw new InvalidOperationException(name + " failed: " + actualR + "," + actualG + "," + actualB);
    }
}
