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
            VerifyXPlanetGaugePriority(assembly);
            VerifyAutoTileRecoverySettingRemoved(assembly);
            VerifySightreadRates(assembly);
            VerifySightreadSelectionLock(assembly);
            VerifySightreadKeyLabels(assembly);
            VerifyShortcutResetGroups(assembly);
            VerifyXPlanetGaugeShortcutToggle(assembly);
            Console.WriteLine("PASS: required API, gauge value color, X-PlanetGauge priority, fixed auto-recovery, sightread rates, selection lock, key labels, and X shortcut toggle.");
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

    private static void VerifyXPlanetGaugePriority(Assembly assembly)
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.Public | BindingFlags.NonPublic;
        Type mainType = assembly.GetType("PlanetGauge.Main", true);
        Type settingsType = assembly.GetType("PlanetGauge.PlanetGaugeSettings", true);
        Type runtimeType = assembly.GetType("PlanetGauge.GaugeRuntime", true);
        Type commandType = assembly.GetType("PlanetGauge.PlanetGaugeEventCommand", true);
        Type modeType = assembly.GetType("PlanetGauge.PlanetGaugeAttributeMode", true);
        object settings = Activator.CreateInstance(settingsType);
        PropertyInfo mainSettings = mainType.GetProperty("Settings", all);
        object previousSettings = mainSettings.GetValue(null, null);
        PropertyInfo eventSettings = runtimeType.GetProperty("EventSettings", all);
        object previousEventSettings = eventSettings.GetValue(null, null);
        try
        {
            mainSettings.SetValue(null, settings, null);
            runtimeType.GetMethod("Reset", all).Invoke(null, null);
            PropertyInfo active = runtimeType.GetProperty("IsXPlanetGaugeActive", all);
            if ((bool)active.GetValue(null, null)) throw new InvalidOperationException("X-PG must start off");

            object command = Activator.CreateInstance(commandType);
            commandType.GetField("ApplyAttributeMode", all).SetValue(command, true);
            commandType.GetField("AttributeMode", all).SetValue(command, Enum.Parse(modeType, "XPlanetGauge"));
            commandType.GetField("AttributeEnabled", all).SetValue(command, true);
            MethodInfo apply = runtimeType.GetMethod("ApplyEventSettings", all);
            apply.Invoke(null, new[] { command, (object)-1 });
            if (!(bool)active.GetValue(null, null)) throw new InvalidOperationException("X-PG event ON failed");

            object blockRecovery = Activator.CreateInstance(commandType);
            commandType.GetField("ApplyAttributeMode", all).SetValue(blockRecovery, true);
            commandType.GetField("AttributeMode", all).SetValue(blockRecovery, Enum.Parse(modeType, "BlockRecovery"));
            commandType.GetField("AttributeEnabled", all).SetValue(blockRecovery, true);
            apply.Invoke(null, new[] { blockRecovery, (object)-1 });
            float blockedPositiveDelta = (float)runtimeType.GetMethod("TransformDelta", all)
                .Invoke(null, new object[] { 0.1f });
            if (blockedPositiveDelta != 0f)
                throw new InvalidOperationException("BlockRecovery did not suppress automatic recovery delta");

            commandType.GetField("AttributeEnabled", all).SetValue(command, false);
            apply.Invoke(null, new[] { command, (object)-1 });
            if ((bool)active.GetValue(null, null)) throw new InvalidOperationException("X-PG event OFF failed");
            object afterXOff = eventSettings.GetValue(null, null);
            if (!(bool)afterXOff.GetType().GetProperty("RecoveryBlocked", all).GetValue(afterXOff, null))
                throw new InvalidOperationException("X-PG event OFF changed another attribute");

            settingsType.GetField("XPlanetGaugeMode", all).SetValue(settings, true);
            if (!(bool)active.GetValue(null, null)) throw new InvalidOperationException("UMM X-PG ON failed");
            apply.Invoke(null, new[] { command, (object)-1 });
            if (!(bool)active.GetValue(null, null)) throw new InvalidOperationException("event OFF overrode UMM X-PG");
        }
        finally
        {
            eventSettings.SetValue(null, previousEventSettings, null);
            mainSettings.SetValue(null, previousSettings, null);
        }
    }

    private static void VerifyAutoTileRecoverySettingRemoved(Assembly assembly)
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.Public | BindingFlags.NonPublic;
        Type eventSettingsType = assembly.GetType("PlanetGauge.PlanetGaugeEventSettings", true);
        Type commandType = assembly.GetType("PlanetGauge.PlanetGaugeEventCommand", true);
        Type registryType = assembly.GetType("PlanetGauge.PlanetGaugeLevelEventRegistry", true);
        Type runtimeType = assembly.GetType("PlanetGauge.GaugeRuntime", true);
        if (eventSettingsType.GetProperty("AutoTileRecovery", all) != null
            || commandType.GetField("ApplyAutoTileRecovery", all) != null
            || commandType.GetField("AutoTileRecovery", all) != null
            || registryType.GetField("AutoTileRecoveryKey", all) != null
            || runtimeType.GetMethod("ApplyAutomaticRecovery", all) == null)
        {
            throw new InvalidOperationException("fixed auto-recovery contract is inconsistent");
        }
    }

    private static void VerifySightreadRates(Assembly assembly)
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.Public | BindingFlags.NonPublic;
        Type mainType = assembly.GetType("PlanetGauge.Main", true);
        Type runtimeType = assembly.GetType("PlanetGauge.GaugeRuntime", true);
        Type settingsType = assembly.GetType("PlanetGauge.PlanetGaugeEventSettings", true);
        Type channelType = assembly.GetType("PlanetGauge.PlanetGaugeRateChannel", true);
        Type sourceType = assembly.GetType("PlanetGauge.PlanetGaugeRateSource", true);
        PropertyInfo eventSettings = runtimeType.GetProperty("EventSettings", all);
        PropertyInfo mode = mainType.GetProperty("SightreadMode", all);
        PropertyInfo lastMode = mainType.GetProperty("LastSightreadMode", all);
        object previousSettings = eventSettings.GetValue(null, null);
        int previousMode = (int)mode.GetValue(null, null);
        MethodInfo setMode = mainType.GetMethod("SetSightreadMode", all);
        MethodInfo toggleMode = mainType.GetMethod("ToggleSightreadMode", all);
        MethodInfo transform = runtimeType.GetMethod("TransformDelta", all);
        try
        {
            object recovery = Activator.CreateInstance(channelType, all, null,
                new[] { (object)true, 400f, Enum.Parse(sourceType, "Increase") }, null);
            object damage = Activator.CreateInstance(channelType, all, null,
                new[] { (object)true, 20f, Enum.Parse(sourceType, "Decrease") }, null);
            object levelRates = Activator.CreateInstance(settingsType, all, null,
                new[] { (object)false, recovery, damage, 400f, 20f, 100f,
                    false, true, false, 100f, false }, null);
            eventSettings.SetValue(null, levelRates, null);
            float[] positive = { 0.4f, 0.2f, 0.2f, 0.3f };
            float[] negative = { -0.2f, -1f, -0.5f, -0.5f };
            for (int i = 0; i <= 3; i++)
            {
                setMode.Invoke(null, new object[] { i });
                float actualPositive = (float)transform.Invoke(null, new object[] { 0.1f });
                float actualNegative = (float)transform.Invoke(null, new object[] { -1f });
                if (Math.Abs(actualPositive - positive[i]) > 0.00001f
                    || Math.Abs(actualNegative - negative[i]) > 0.00001f)
                    throw new InvalidOperationException("SR-" + i + " rate precedence failed");
            }
            toggleMode.Invoke(null, new object[] { 3 });
            if ((int)mode.GetValue(null, null) != 0)
                throw new InvalidOperationException("Repeated SR shortcut must turn the mode off");
            if ((int)lastMode.GetValue(null, null) != 3)
                throw new InvalidOperationException("Turning SR off must remember the last mode");
            setMode.Invoke(null, new object[] { 1 });
            setMode.Invoke(null, new object[] { 2 });
            if ((int)mode.GetValue(null, null) != 2)
                throw new InvalidOperationException("Sightread modes must be exclusive");
            setMode.Invoke(null, new object[] { 0 });
            setMode.Invoke(null, new object[] { (int)lastMode.GetValue(null, null) });
            if ((int)mode.GetValue(null, null) != 2)
                throw new InvalidOperationException("SR toggle must restore the last selected mode");
        }
        finally
        {
            setMode.Invoke(null, new object[] { previousMode });
            eventSettings.SetValue(null, previousSettings, null);
        }
    }

    private static void VerifySightreadKeyLabels(Assembly assembly)
    {
        const BindingFlags all = BindingFlags.Static | BindingFlags.NonPublic;
        MethodInfo format = assembly.GetType("PlanetGauge.PlanetGaugeSettings", true)
            .GetMethod("FormatSightreadKey", all);
        Type keyCodeType = format.GetParameters()[0].ParameterType;
        for (int i = 1; i <= 3; i++)
        {
            object key = Enum.Parse(keyCodeType, "Alpha" + i);
            string label = (string)format.Invoke(null, new[] { key });
            if (label != i.ToString())
                throw new InvalidOperationException("Alpha key label must use its digit");
        }
    }

    private static void VerifyShortcutResetGroups(Assembly assembly)
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type settingsType = assembly.GetType("PlanetGauge.PlanetGaugeSettings", true);
        object settings = Activator.CreateInstance(settingsType);
        FieldInfo[] keys = new FieldInfo[5];
        for (int i = 0; i < keys.Length; i++)
            keys[i] = settingsType.GetField("SightreadKey" + (i + 1), all);
        Type keyType = keys[0].FieldType;
        keys[0].SetValue(settings, Enum.Parse(keyType, "F4"));
        keys[1].SetValue(settings, Enum.Parse(keyType, "F5"));
        keys[2].SetValue(settings, Enum.Parse(keyType, "F6"));
        settingsType.GetMethod("ResetCommonKeys", all).Invoke(settings, null);
        if (keys[0].GetValue(settings).ToString() != "LeftShift"
            || keys[1].GetValue(settings).ToString() != "P"
            || keys[2].GetValue(settings).ToString() != "F6")
            throw new InvalidOperationException("Shared key reset changed SR action keys");

        keys[0].SetValue(settings, Enum.Parse(keyType, "F4"));
        keys[2].SetValue(settings, Enum.Parse(keyType, "F7"));
        settingsType.GetMethod("ResetSightreadActionKeys", all).Invoke(settings, null);
        if (keys[0].GetValue(settings).ToString() != "F4"
            || keys[2].GetValue(settings).ToString() != "Alpha1")
            throw new InvalidOperationException("SR action reset changed shared keys");

        keys[0].SetValue(settings, Enum.Parse(keyType, "F4"));
        keys[2].SetValue(settings, Enum.Parse(keyType, "LeftShift"));
        settingsType.GetMethod("ResetCommonKeys", all).Invoke(settings, null);
        if (keys[0].GetValue(settings).ToString() != "LeftShift"
            || keys[2].GetValue(settings).ToString() == "LeftShift")
            throw new InvalidOperationException("Shared key reset left a shortcut conflict");
    }

    private static void VerifySightreadSelectionLock(Assembly assembly)
    {
        const BindingFlags all = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        Type mainType = assembly.GetType("PlanetGauge.Main", true);
        PropertyInfo mode = mainType.GetProperty("SightreadMode", all);
        PropertyInfo locked = mainType.GetProperty("IsSightreadSelectionLocked", all);
        MethodInfo setMode = mainType.GetMethod("SetSightreadMode", all);
        MethodInfo setLock = mainType.GetMethod("SetSightreadGameplayLock", all);
        int previousMode = (int)mode.GetValue(null, null);
        bool previousLock = (bool)locked.GetValue(null, null);
        try
        {
            setLock.Invoke(null, new object[] { false });
            setMode.Invoke(null, new object[] { 1 });
            setLock.Invoke(null, new object[] { true });
            setMode.Invoke(null, new object[] { 2 });
            if ((int)mode.GetValue(null, null) != 1)
                throw new InvalidOperationException("Gameplay changed sightread mode");
            setLock.Invoke(null, new object[] { false });
            setMode.Invoke(null, new object[] { 2 });
            if ((int)mode.GetValue(null, null) != 2)
                throw new InvalidOperationException("Editor could not change sightread mode");
        }
        finally
        {
            setLock.Invoke(null, new object[] { false });
            setMode.Invoke(null, new object[] { previousMode });
            setLock.Invoke(null, new object[] { previousLock });
        }
    }

    private static void VerifyXPlanetGaugeShortcutToggle(Assembly assembly)
    {
        const BindingFlags all = BindingFlags.Static | BindingFlags.Instance
            | BindingFlags.Public | BindingFlags.NonPublic;
        Type mainType = assembly.GetType("PlanetGauge.Main", true);
        Type settingsType = assembly.GetType("PlanetGauge.PlanetGaugeSettings", true);
        PropertyInfo mainSettings = mainType.GetProperty("Settings", all);
        PropertyInfo locked = mainType.GetProperty("IsSightreadSelectionLocked", all);
        MethodInfo setLock = mainType.GetMethod("SetSightreadGameplayLock", all);
        MethodInfo toggleX = mainType.GetMethod("ToggleXPlanetGaugeMode", all);
        object previousSettings = mainSettings.GetValue(null, null);
        bool previousLock = (bool)locked.GetValue(null, null);
        object settings = Activator.CreateInstance(settingsType);
        FieldInfo xMode = settingsType.GetField("XPlanetGaugeMode", all);
        try
        {
            mainSettings.SetValue(null, settings, null);
            setLock.Invoke(null, new object[] { false });
            toggleX.Invoke(null, null);
            if (!(bool)xMode.GetValue(settings))
                throw new InvalidOperationException("X shortcut did not enable the mode");
            setLock.Invoke(null, new object[] { true });
            toggleX.Invoke(null, null);
            if (!(bool)xMode.GetValue(settings))
                throw new InvalidOperationException("X shortcut changed the mode during gameplay");
            setLock.Invoke(null, new object[] { false });
            toggleX.Invoke(null, null);
            if ((bool)xMode.GetValue(settings))
                throw new InvalidOperationException("X shortcut did not disable the mode");
        }
        finally
        {
            setLock.Invoke(null, new object[] { previousLock });
            mainSettings.SetValue(null, previousSettings, null);
        }
    }
}
