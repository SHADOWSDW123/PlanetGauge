using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using ADOFAI;
using HarmonyLib;
using UnityFileDialog;

namespace PlanetGauge
{
    internal enum MapSettingsAvailabilityReason
    {
        Available = 0,
        ModDisabled = 1,
        NotInEditor = 2,
        EditorInPlayMode = 3
    }

    internal enum MapSettingsImportFailureKind
    {
        None = 0,
        FileNotFound = 1,
        InvalidExtension = 2,
        InvalidJsonOrSchema = 3,
        InvalidVersion = 4,
        VersionTooNew = 5,
        NoEvents = 6,
        TileCountInsufficient = 7,
        FileTooLarge = 8,
        TooManyEvents = 9
    }

    internal sealed class ParsedMapSettingsImport
    {
        internal string FileVersion { get; set; }
        internal string LevelName { get; set; }
        internal int TotalFloors { get; set; }
        internal int MaxFloor { get; set; }
        internal List<LevelEvent> Events { get; set; }
        internal bool MapMismatch { get; set; }
    }

    /// <summary>
    /// 레벨 에디터에 열려 있는 맵의 SetPlanetGauge 이벤트를 .plgg(JSON) 파일로 내보내거나
    /// .plgg 파일을 현재 열린 에디터 씬에 교체 적용한다.
    /// SetPlanetGauge 이벤트 스키마/로직이 크게 변경될 경우 이 클래스의 직렬화·역직렬화 및 버전 호환성을 함께 검토해야 한다.
    /// </summary>
    internal static class PlanetGaugeMapSettingsTransfer
    {
        internal const string FileExtension = ".plgg";
        internal const string FileExtensionNoDot = "plgg";
        internal const int MaxImportFileMegabytes = 4;
        internal const long MaxImportFileBytes = MaxImportFileMegabytes * 1024L * 1024L;
        internal const int MaxImportEventCount = PlanetGaugeJson.MaxArrayElements;

        internal const string PlanetGaugeVersionKey = "planetGaugeVersion";
        internal const string LevelNameKey = "levelName";
        internal const string TotalFloorsKey = "totalFloors";
        internal const string MaxFloorKey = "maxFloor";
        internal const string EventsKey = "events";

        private const string FloorKey = "floor";
        private const string EventTypeKey = "eventType";
        private const string ActiveKey = "active";
        private const string VisibleKey = "visible";
        private const string LockedKey = "locked";
        private const string AngleOffsetKey = "angleOffset";
        private const string RemovedAutoTileRecoveryKey = "autoTileRecovery";
        private const string DefaultFallbackFileName = "level";
        private const int MaxVersionStringLength = 32;
        private const int MaxVersionSuffixLength = 16;
        private const int MaxVersionSegmentDigits = 6;
        private const int MaxStatusFragmentLength = 120;
        private const int MaxEnumErrorSnippetLength = 48;

        private enum StatusKind
        {
            None = 0,
            ModDisabled = 1,
            NotInEditor = 2,
            EditorInPlayMode = 3,
            ExportNoEvents = 4,
            ExportSucceeded = 5,
            ExportFailed = 6,
            ImportSucceeded = 7,
            ImportFailed = 8,
            ImportFileNotFound = 9,
            ImportInvalidExtension = 10,
            ImportInvalidFormat = 11,
            ImportVersionInvalid = 12,
            ImportVersionTooNew = 13,
            ImportNoEvents = 14,
            ImportTileCountInsufficient = 15,
            ImportFileTooLarge = 16,
            ImportTooManyEvents = 17
        }

        private static readonly string[] PlggExtensions = { FileExtensionNoDot };
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private static StatusKind lastStatusKind;
        private static object[] lastStatusArguments;
        private static bool lastMapMismatchWarning;
        private static string lastUsedDirectory;

        internal static string LastMessage
        {
            get { return FormatStatusMessage(lastStatusKind, lastStatusArguments); }
        }

        internal static string LastWarning
        {
            get
            {
                return lastMapMismatchWarning
                    ? LocalizedStrings.MapSettingsImportMapMismatchWarning
                    : string.Empty;
            }
        }

        internal static void ClearMessages()
        {
            lastStatusKind = StatusKind.None;
            lastStatusArguments = null;
            lastMapMismatchWarning = false;
        }

        internal static void ValidateRequiredGameApi()
        {
            if (AccessTools.Method(typeof(scnEditor), nameof(scnEditor.ApplyEventsToFloors), Type.EmptyTypes) == null)
            {
                throw new MissingMethodException("호환성에 필요한 scnEditor.ApplyEventsToFloors() 메서드를 찾을 수 없습니다.");
            }

            if (AccessTools.Method(typeof(scnEditor), nameof(scnEditor.DecideInspectorTabsAtSelected), Type.EmptyTypes) == null)
            {
                throw new MissingMethodException("호환성에 필요한 scnEditor.DecideInspectorTabsAtSelected() 메서드를 찾을 수 없습니다.");
            }

            if (AccessTools.Method(typeof(scnEditor), nameof(scnEditor.ShowEventIndicators), new[] { typeof(scrFloor) }) == null)
            {
                throw new MissingMethodException("호환성에 필요한 scnEditor.ShowEventIndicators(scrFloor) 메서드를 찾을 수 없습니다.");
            }

            if (AccessTools.Method(typeof(InspectorPanel), nameof(InspectorPanel.HideAllInspectorTabs), Type.EmptyTypes) == null)
            {
                throw new MissingMethodException("호환성에 필요한 InspectorPanel.HideAllInspectorTabs() 메서드를 찾을 수 없습니다.");
            }

            if (AccessTools.Constructor(
                    typeof(SaveStateScope),
                    new[] { typeof(scnEditor), typeof(bool), typeof(bool), typeof(bool) }) == null)
            {
                throw new MissingMethodException("호환성에 필요한 SaveStateScope(scnEditor, bool, bool, bool) 생성자를 찾을 수 없습니다.");
            }
        }

        internal static string GetCurrentModVersion()
        {
            Assembly assembly = typeof(Main).Assembly;
            object[] attributes = assembly.GetCustomAttributes(
                typeof(AssemblyInformationalVersionAttribute),
                false);
            if (attributes != null && attributes.Length > 0)
            {
                AssemblyInformationalVersionAttribute infoAttr =
                    attributes[0] as AssemblyInformationalVersionAttribute;
                if (infoAttr != null && !string.IsNullOrWhiteSpace(infoAttr.InformationalVersion))
                {
                    return infoAttr.InformationalVersion.Trim();
                }
            }

            Version asmVersion = assembly.GetName().Version;
            if (asmVersion != null)
            {
                return asmVersion.Major.ToString(CultureInfo.InvariantCulture)
                    + "." + asmVersion.Minor.ToString(CultureInfo.InvariantCulture)
                    + "." + Math.Max(0, asmVersion.Build).ToString(CultureInfo.InvariantCulture);
            }

            return "0.5.0";
        }

        internal static bool CanOperateInEditor(out MapSettingsAvailabilityReason reason)
        {
            if (!Main.IsEnabled)
            {
                reason = MapSettingsAvailabilityReason.ModDisabled;
                return false;
            }

            scnEditor editor = scnEditor.instance;
            if (editor == null
                || editor.customLevel == null
                || editor.levelData == null
                || editor.events == null
                || editor.floors == null
                || editor.floors.Count == 0)
            {
                reason = MapSettingsAvailabilityReason.NotInEditor;
                return false;
            }

            if (editor.playMode)
            {
                reason = MapSettingsAvailabilityReason.EditorInPlayMode;
                return false;
            }

            reason = MapSettingsAvailabilityReason.Available;
            return true;
        }

        internal static string GetAvailabilityHint(MapSettingsAvailabilityReason reason)
        {
            switch (reason)
            {
                case MapSettingsAvailabilityReason.ModDisabled:
                    return LocalizedStrings.MapSettingsTransferModDisabled;
                case MapSettingsAvailabilityReason.NotInEditor:
                    return LocalizedStrings.MapSettingsTransferEditorOnly;
                case MapSettingsAvailabilityReason.EditorInPlayMode:
                    return LocalizedStrings.MapSettingsTransferPlayModeBlocked;
                case MapSettingsAvailabilityReason.Available:
                default:
                    return string.Empty;
            }
        }

        internal static bool ExportCurrentMap()
        {
            MapSettingsAvailabilityReason availability;
            if (!CanOperateInEditor(out availability))
            {
                SetAvailabilityStatus(availability);
                return false;
            }

            scnEditor editor = scnEditor.instance;
            List<LevelEvent> gaugeEvents = CollectSetPlanetGaugeEvents(editor.events);
            if (gaugeEvents.Count == 0)
            {
                SetStatus(StatusKind.ExportNoEvents, false);
                return false;
            }

            string levelPath = editor.customLevel != null ? editor.customLevel.levelPath : null;
            string initialDirectory = ResolveInitialDirectory(levelPath);
            string defaultFileName = ResolveDefaultExportFileName(levelPath);

            string selectedPath;
            try
            {
                selectedPath = FileBrowser.SaveFile(
                    initialDirectory,
                    defaultFileName,
                    LocalizedStrings.PlggFileFilter,
                    PlggExtensions,
                    LocalizedStrings.ExportMapSettingsDialog);
            }
            catch (Exception exception)
            {
                Main.LogException("PlanetGauge 맵 설정 내보내기 파일 선택에 실패했습니다.", exception);
                SetStatus(StatusKind.ExportFailed, false, exception.Message);
                return false;
            }

            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return false;
            }

            try
            {
                selectedPath = EnsurePlggExtension(Path.GetFullPath(selectedPath));
                string levelName = editor.levelData.song ?? string.Empty;
                int totalFloors = editor.floors.Count;
                string json = BuildExportJson(
                    GetCurrentModVersion(),
                    levelName,
                    totalFloors,
                    gaugeEvents);

                string directory = Path.GetDirectoryName(selectedPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(selectedPath, json, Utf8NoBom);
                if (!string.IsNullOrEmpty(directory))
                {
                    lastUsedDirectory = directory;
                }

                SetStatus(
                    StatusKind.ExportSucceeded,
                    false,
                    Path.GetFileName(selectedPath),
                    gaugeEvents.Count);
                return true;
            }
            catch (Exception exception)
            {
                Main.LogException("PlanetGauge 맵 설정 내보내기에 실패했습니다.", exception);
                SetStatus(StatusKind.ExportFailed, false, exception.Message);
                return false;
            }
        }

        internal static bool ImportIntoCurrentMap()
        {
            MapSettingsAvailabilityReason availability;
            if (!CanOperateInEditor(out availability))
            {
                SetAvailabilityStatus(availability);
                return false;
            }

            scnEditor editor = scnEditor.instance;
            string levelPath = editor.customLevel != null ? editor.customLevel.levelPath : null;
            string initialDirectory = ResolveInitialDirectory(levelPath);

            string selectedPath;
            try
            {
                selectedPath = FileBrowser.PickFile(
                    initialDirectory,
                    LocalizedStrings.PlggFileFilter,
                    PlggExtensions,
                    LocalizedStrings.ImportMapSettingsDialog);
            }
            catch (Exception exception)
            {
                Main.LogException("PlanetGauge 맵 설정 가져오기 파일 선택에 실패했습니다.", exception);
                SetStatus(StatusKind.ImportFailed, false, exception.Message);
                return false;
            }

            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return false;
            }

            try
            {
                selectedPath = Path.GetFullPath(selectedPath);
                if (!File.Exists(selectedPath))
                {
                    SetStatus(StatusKind.ImportFileNotFound, false);
                    return false;
                }

                if (!HasPlggExtension(selectedPath))
                {
                    SetStatus(StatusKind.ImportInvalidExtension, false);
                    return false;
                }

                // 다이얼로그가 열려 있는 동안 씬 상태가 바뀌었을 수 있으므로 다시 검사한다.
                if (!CanOperateInEditor(out availability))
                {
                    SetAvailabilityStatus(availability);
                    return false;
                }

                if (!PlanetGaugeLevelEventRegistry.IsRegistered)
                {
                    PlanetGaugeLevelEventRegistry.TryRegister();
                }

                editor = scnEditor.instance;
                string jsonText;
                MapSettingsImportFailureKind failureKind;
                object[] failureArguments;
                if (!TryReadBoundedImportFile(
                    selectedPath,
                    out jsonText,
                    out failureKind,
                    out failureArguments))
                {
                    SetImportFailureStatus(failureKind, failureArguments);
                    return false;
                }

                string currentVersion = GetCurrentModVersion();
                int currentTotalFloors = editor.floors.Count;
                string currentLevelName = editor.levelData.song ?? string.Empty;

                ParsedMapSettingsImport parsed;
                if (!TryParseAndValidateImport(
                    jsonText,
                    currentVersion,
                    currentTotalFloors,
                    currentLevelName,
                    out parsed,
                    out failureKind,
                    out failureArguments))
                {
                    SetImportFailureStatus(failureKind, failureArguments);
                    return false;
                }

                ApplyImportedEventsToEditor(editor, parsed.Events);

                string directory = Path.GetDirectoryName(selectedPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    lastUsedDirectory = directory;
                }

                SetStatus(
                    StatusKind.ImportSucceeded,
                    parsed.MapMismatch,
                    Path.GetFileName(selectedPath),
                    parsed.Events.Count);
                return true;
            }
            catch (Exception exception)
            {
                Main.LogException("PlanetGauge 맵 설정 가져오기에 실패했습니다.", exception);
                SetStatus(StatusKind.ImportFailed, false, exception.Message);
                return false;
            }
        }

        internal static string ResolveDefaultExportFileName(string levelPath)
        {
            string baseName = null;
            if (!string.IsNullOrWhiteSpace(levelPath))
            {
                try
                {
                    baseName = Path.GetFileNameWithoutExtension(levelPath);
                }
                catch (ArgumentException)
                {
                    baseName = null;
                }
            }

            if (!string.IsNullOrWhiteSpace(baseName))
            {
                baseName = SanitizeFileName(baseName);
            }

            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = DefaultFallbackFileName;
            }

            return baseName + FileExtension;
        }

        internal static bool HasPlggExtension(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    Path.GetExtension(path),
                    FileExtension,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        internal static string EnsurePlggExtension(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || HasPlggExtension(path))
            {
                return path;
            }

            return path + FileExtension;
        }

        internal static bool TryReadBoundedImportFile(
            string fullPath,
            out string jsonText,
            out MapSettingsImportFailureKind failureKind,
            out object[] failureArguments)
        {
            jsonText = null;
            failureKind = MapSettingsImportFailureKind.None;
            failureArguments = null;

            if (string.IsNullOrWhiteSpace(fullPath))
            {
                failureKind = MapSettingsImportFailureKind.FileNotFound;
                return false;
            }

            FileInfo fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists)
            {
                failureKind = MapSettingsImportFailureKind.FileNotFound;
                return false;
            }

            if (fileInfo.Length > MaxImportFileBytes)
            {
                failureKind = MapSettingsImportFailureKind.FileTooLarge;
                failureArguments = new object[] { MaxImportFileMegabytes };
                return false;
            }

            using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.CanSeek && stream.Length > MaxImportFileBytes)
                {
                    failureKind = MapSettingsImportFailureKind.FileTooLarge;
                    failureArguments = new object[] { MaxImportFileMegabytes };
                    return false;
                }

                int initialCapacity = stream.CanSeek && stream.Length > 0
                    ? (int)Math.Min(stream.Length, MaxImportFileBytes)
                    : 4096;
                using (MemoryStream memory = new MemoryStream(initialCapacity))
                {
                    byte[] buffer = new byte[8192];
                    long totalBytes = 0;
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        totalBytes += read;
                        if (totalBytes > MaxImportFileBytes)
                        {
                            failureKind = MapSettingsImportFailureKind.FileTooLarge;
                            failureArguments = new object[] { MaxImportFileMegabytes };
                            return false;
                        }

                        memory.Write(buffer, 0, read);
                    }

                    jsonText = Encoding.UTF8.GetString(memory.GetBuffer(), 0, (int)memory.Length);
                    return true;
                }
            }
        }

        internal static bool TryParseComparableVersion(string raw, out Version parsed)
        {
            parsed = null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string trimmed = raw.Trim();
            if (trimmed.Length > MaxVersionStringLength)
            {
                return false;
            }

            if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(1);
            }

            string[] rawParts = trimmed.Split('.');
            if (rawParts.Length < 2 || rawParts.Length > 4)
            {
                return false;
            }

            int[] numbers = new int[4];
            for (int i = 0; i < rawParts.Length; i++)
            {
                string part = rawParts[i];
                if (string.IsNullOrEmpty(part))
                {
                    return false;
                }

                // 마지막 세그먼트는 0.3.6M, 0.5.0-beta 같은 안전한 버전 접미사만 허용한다.
                int digitLength = 0;
                while (digitLength < part.Length
                    && part[digitLength] >= '0'
                    && part[digitLength] <= '9')
                {
                    digitLength++;
                }

                if (digitLength == 0 || digitLength > MaxVersionSegmentDigits)
                {
                    return false;
                }

                if (digitLength < part.Length)
                {
                    if (i != rawParts.Length - 1)
                    {
                        return false;
                    }

                    int suffixLength = part.Length - digitLength;
                    if (suffixLength > MaxVersionSuffixLength)
                    {
                        return false;
                    }

                    for (int s = digitLength; s < part.Length; s++)
                    {
                        if (!IsAllowedVersionSuffixChar(part[s]))
                        {
                            return false;
                        }
                    }
                }

                int value;
                if (!int.TryParse(
                    part.Substring(0, digitLength),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out value))
                {
                    return false;
                }

                numbers[i] = value;
            }

            parsed = new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
            return true;
        }

        private static bool IsAllowedVersionSuffixChar(char ch)
        {
            return (ch >= '0' && ch <= '9')
                || (ch >= 'A' && ch <= 'Z')
                || (ch >= 'a' && ch <= 'z')
                || ch == '-'
                || ch == '_'
                || ch == '+';
        }

        internal static List<LevelEvent> CollectSetPlanetGaugeEvents(IList<LevelEvent> sourceEvents)
        {
            List<KeyValuePair<int, LevelEvent>> indexed = new List<KeyValuePair<int, LevelEvent>>();
            if (sourceEvents != null)
            {
                for (int i = 0; i < sourceEvents.Count; i++)
                {
                    LevelEvent evnt = sourceEvents[i];
                    if (evnt != null && evnt.eventType == PlanetGaugeLevelEventRegistry.EventType)
                    {
                        indexed.Add(new KeyValuePair<int, LevelEvent>(i, evnt));
                    }
                }
            }

            // 같은 타일 내 이벤트 순서는 유지하면서 타일 번호 오름차순으로 정렬한다.
            indexed.Sort((left, right) =>
            {
                int floorCompare = left.Value.floor.CompareTo(right.Value.floor);
                return floorCompare != 0 ? floorCompare : left.Key.CompareTo(right.Key);
            });

            List<LevelEvent> result = new List<LevelEvent>(indexed.Count);
            for (int i = 0; i < indexed.Count; i++)
            {
                result.Add(indexed[i].Value);
            }

            return result;
        }

        internal static string BuildExportJson(
            string modVersion,
            string levelName,
            int totalFloors,
            IList<LevelEvent> events)
        {
            if (events == null || events.Count == 0)
            {
                throw new InvalidOperationException("No SetPlanetGauge events to export.");
            }

            List<LevelEvent> ordered = CollectSetPlanetGaugeEvents(events);
            if (ordered.Count == 0)
            {
                throw new InvalidOperationException("No SetPlanetGauge events to export.");
            }

            List<object> encodedEvents = new List<object>(ordered.Count);
            int maxFloor = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                LevelEvent evnt = ordered[i];
                if (evnt.floor > maxFloor)
                {
                    maxFloor = evnt.floor;
                }

                encodedEvents.Add(EncodeSetPlanetGaugeEvent(evnt));
            }

            Dictionary<string, object> root = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { PlanetGaugeVersionKey, modVersion ?? GetCurrentModVersion() },
                { LevelNameKey, levelName ?? string.Empty },
                { TotalFloorsKey, Math.Max(0, totalFloors) },
                { MaxFloorKey, maxFloor },
                { EventsKey, encodedEvents }
            };

            return PlanetGaugeJson.Serialize(root);
        }

        internal static Dictionary<string, object> EncodeSetPlanetGaugeEvent(LevelEvent levelEvent)
        {
            if (levelEvent == null)
            {
                throw new ArgumentNullException(nameof(levelEvent));
            }

            Dictionary<string, object> encoded = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { FloorKey, levelEvent.floor },
                { EventTypeKey, PlanetGaugeLevelEventRegistry.EventName }
            };

            if (!levelEvent.active)
            {
                encoded[ActiveKey] = false;
            }

            if (!levelEvent.visible)
            {
                encoded[VisibleKey] = false;
            }

            if (levelEvent.locked)
            {
                encoded[LockedKey] = true;
            }

            Dictionary<string, object> eventData = levelEvent.GetData();
            LevelEventInfo info = PlanetGaugeLevelEventRegistry.EventInfo;
            List<ADOFAI.PropertyInfo> orderedProperties = GetOrderedProperties(info);
            for (int i = 0; i < orderedProperties.Count; i++)
            {
                ADOFAI.PropertyInfo property = orderedProperties[i];
                if (property == null || !property.encode)
                {
                    continue;
                }

                string key = property.name;
                if (string.IsNullOrEmpty(key)
                    || string.Equals(key, RemovedAutoTileRecoveryKey, StringComparison.Ordinal))
                {
                    continue;
                }

                if (property.canBeDisabled && IsPropertyDisabled(levelEvent, key))
                {
                    continue;
                }

                object value = null;
                if (eventData == null || !eventData.TryGetValue(key, out value) || value == null)
                {
                    value = property.value_default;
                }

                encoded[key] = ConvertPropertyForJson(property, key, value);
            }

            // 커스텀 편집된 .adofai에서 angleOffset을 가진 경우 손실 없이 보존한다.
            if (eventData != null)
            {
                object rawAngleOffset;
                float angleOffset;
                if (eventData.TryGetValue(AngleOffsetKey, out rawAngleOffset)
                    && TryConvertToFiniteFloat(rawAngleOffset, out angleOffset)
                    && Math.Abs(angleOffset) > 0f)
                {
                    encoded[AngleOffsetKey] = angleOffset;
                }
            }

            return encoded;
        }

        internal static bool TryParseAndValidateImport(
            string jsonText,
            string currentModVersion,
            int currentTotalFloors,
            string currentLevelName,
            out ParsedMapSettingsImport result,
            out MapSettingsImportFailureKind failureKind,
            out object[] failureArguments)
        {
            result = null;
            failureKind = MapSettingsImportFailureKind.None;
            failureArguments = null;

            if (jsonText != null && jsonText.Length > PlanetGaugeJson.MaxInputCharacters)
            {
                failureKind = MapSettingsImportFailureKind.FileTooLarge;
                failureArguments = new object[] { MaxImportFileMegabytes };
                return false;
            }

            object parsedRoot;
            try
            {
                parsedRoot = PlanetGaugeJson.Deserialize(jsonText);
            }
            catch (PlanetGaugeJsonLimitException limitException)
            {
                if (limitException.LimitKind == PlanetGaugeJsonLimitKind.InputTooLarge)
                {
                    failureKind = MapSettingsImportFailureKind.FileTooLarge;
                    failureArguments = new object[] { MaxImportFileMegabytes };
                    return false;
                }

                if (limitException.LimitKind == PlanetGaugeJsonLimitKind.TooManyArrayElements)
                {
                    failureKind = MapSettingsImportFailureKind.TooManyEvents;
                    failureArguments = new object[] { MaxImportEventCount };
                    return false;
                }

                failureKind = MapSettingsImportFailureKind.InvalidJsonOrSchema;
                failureArguments = new object[] { SanitizeMessageFragment(limitException.Message, MaxStatusFragmentLength) };
                return false;
            }
            catch (Exception exception)
            {
                failureKind = MapSettingsImportFailureKind.InvalidJsonOrSchema;
                failureArguments = new object[] { SanitizeMessageFragment(exception.Message, MaxStatusFragmentLength) };
                return false;
            }

            Dictionary<string, object> root = parsedRoot as Dictionary<string, object>;
            if (root == null)
            {
                failureKind = MapSettingsImportFailureKind.InvalidJsonOrSchema;
                failureArguments = new object[] { "Root value must be a JSON object." };
                return false;
            }

            object rawVersion;
            string fileVersionText = root.TryGetValue(PlanetGaugeVersionKey, out rawVersion)
                ? rawVersion as string
                : null;
            Version fileVersion;
            if (!TryParseComparableVersion(fileVersionText, out fileVersion))
            {
                failureKind = MapSettingsImportFailureKind.InvalidVersion;
                return false;
            }

            Version currentVersion;
            if (!TryParseComparableVersion(currentModVersion, out currentVersion))
            {
                currentVersion = new Version(0, 5, 0, 0);
            }

            if (fileVersion > currentVersion)
            {
                failureKind = MapSettingsImportFailureKind.VersionTooNew;
                failureArguments = new object[]
                {
                    SanitizeMessageFragment(fileVersionText.Trim(), MaxVersionStringLength),
                    SanitizeMessageFragment(currentModVersion, MaxVersionStringLength)
                };
                return false;
            }

            object rawEventsObj;
            if (!root.TryGetValue(EventsKey, out rawEventsObj))
            {
                failureKind = MapSettingsImportFailureKind.InvalidJsonOrSchema;
                failureArguments = new object[] { "Missing 'events' array." };
                return false;
            }

            List<object> rawEvents = rawEventsObj as List<object>;
            if (rawEvents == null)
            {
                failureKind = MapSettingsImportFailureKind.InvalidJsonOrSchema;
                failureArguments = new object[] { "'events' must be a JSON array." };
                return false;
            }

            if (rawEvents.Count == 0)
            {
                failureKind = MapSettingsImportFailureKind.NoEvents;
                return false;
            }

            if (rawEvents.Count > MaxImportEventCount)
            {
                failureKind = MapSettingsImportFailureKind.TooManyEvents;
                failureArguments = new object[] { MaxImportEventCount };
                return false;
            }

            List<LevelEvent> decodedEvents = new List<LevelEvent>(rawEvents.Count);
            int maxEventFloor = -1;
            for (int i = 0; i < rawEvents.Count; i++)
            {
                Dictionary<string, object> eventDict = rawEvents[i] as Dictionary<string, object>;
                if (eventDict == null)
                {
                    failureKind = MapSettingsImportFailureKind.InvalidJsonOrSchema;
                    failureArguments = new object[] { "Event #" + i + " must be a JSON object." };
                    return false;
                }

                LevelEvent decodedEvent;
                string eventError;
                if (!TryDecodeSetPlanetGaugeEvent(eventDict, i, out decodedEvent, out eventError))
                {
                    failureKind = MapSettingsImportFailureKind.InvalidJsonOrSchema;
                    failureArguments = new object[] { eventError };
                    return false;
                }

                if (decodedEvent.floor > maxEventFloor)
                {
                    maxEventFloor = decodedEvent.floor;
                }

                decodedEvents.Add(decodedEvent);
            }

            if (currentTotalFloors <= 0 || maxEventFloor >= currentTotalFloors)
            {
                failureKind = MapSettingsImportFailureKind.TileCountInsufficient;
                failureArguments = new object[]
                {
                    maxEventFloor,
                    Math.Max(0, currentTotalFloors - 1),
                    Math.Max(0, currentTotalFloors)
                };
                return false;
            }

            string fileLevelName = string.Empty;
            object rawLevelName;
            if (root.TryGetValue(LevelNameKey, out rawLevelName) && rawLevelName != null)
            {
                string levelNameString = rawLevelName as string;
                if (levelNameString == null)
                {
                    failureKind = MapSettingsImportFailureKind.InvalidJsonOrSchema;
                    failureArguments = new object[] { "'levelName' must be a string." };
                    return false;
                }

                fileLevelName = levelNameString;
            }

            int fileTotalFloors = -1;
            object rawTotalFloors;
            if (root.TryGetValue(TotalFloorsKey, out rawTotalFloors) && rawTotalFloors != null)
            {
                int parsedTotalFloors;
                if (!TryConvertToExactInt(rawTotalFloors, out parsedTotalFloors) || parsedTotalFloors < 0)
                {
                    failureKind = MapSettingsImportFailureKind.InvalidJsonOrSchema;
                    failureArguments = new object[] { "'totalFloors' must be a non-negative integer." };
                    return false;
                }

                fileTotalFloors = parsedTotalFloors;
            }

            bool songMismatch = !string.Equals(
                fileLevelName,
                currentLevelName ?? string.Empty,
                StringComparison.Ordinal);
            bool totalFloorsMismatch = fileTotalFloors >= 0 && fileTotalFloors != currentTotalFloors;

            result = new ParsedMapSettingsImport
            {
                FileVersion = fileVersionText.Trim(),
                LevelName = fileLevelName,
                TotalFloors = fileTotalFloors,
                MaxFloor = maxEventFloor,
                Events = decodedEvents,
                MapMismatch = songMismatch || totalFloorsMismatch
            };
            return true;
        }

        internal static bool TryDecodeSetPlanetGaugeEvent(
            Dictionary<string, object> dict,
            int eventIndex,
            out LevelEvent levelEvent,
            out string error)
        {
            levelEvent = null;
            error = null;

            object rawEventType;
            string eventTypeName = dict.TryGetValue(EventTypeKey, out rawEventType)
                ? rawEventType as string
                : null;
            if (!string.Equals(
                eventTypeName,
                PlanetGaugeLevelEventRegistry.EventName,
                StringComparison.OrdinalIgnoreCase))
            {
                error = "Event #" + eventIndex + " must have eventType 'SetPlanetGauge'.";
                return false;
            }

            object rawFloor;
            int floor;
            if (!dict.TryGetValue(FloorKey, out rawFloor)
                || !TryConvertToExactInt(rawFloor, out floor)
                || floor < 0)
            {
                error = "Event #" + eventIndex + " has an invalid floor value.";
                return false;
            }

            bool active = true;
            object rawActive;
            if (dict.TryGetValue(ActiveKey, out rawActive) && rawActive != null)
            {
                if (!TryConvertToBool(rawActive, out active))
                {
                    error = "Event #" + eventIndex + " has an invalid 'active' value.";
                    return false;
                }
            }

            bool visible = true;
            object rawVisible;
            if (dict.TryGetValue(VisibleKey, out rawVisible) && rawVisible != null)
            {
                if (!TryConvertToBool(rawVisible, out visible))
                {
                    error = "Event #" + eventIndex + " has an invalid 'visible' value.";
                    return false;
                }
            }

            bool locked = false;
            object rawLocked;
            if (dict.TryGetValue(LockedKey, out rawLocked) && rawLocked != null)
            {
                if (!TryConvertToBool(rawLocked, out locked))
                {
                    error = "Event #" + eventIndex + " has an invalid 'locked' value.";
                    return false;
                }
            }

            LevelEventInfo info = PlanetGaugeLevelEventRegistry.EventInfo;
            Dictionary<string, object> data = new Dictionary<string, object>(StringComparer.Ordinal);
            Dictionary<string, bool> disabled = new Dictionary<string, bool>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, ADOFAI.PropertyInfo> pair in info.propertiesInfo)
            {
                string key = pair.Key;
                ADOFAI.PropertyInfo property = pair.Value;
                if (property == null)
                {
                    continue;
                }

                if (!property.encode)
                {
                    data[key] = property.value_default;
                    disabled[key] = !property.startEnabled;
                    continue;
                }

                object rawValue;
                if (!dict.TryGetValue(key, out rawValue) || rawValue == null)
                {
                    data[key] = property.value_default;
                    disabled[key] = property.canBeDisabled;
                    continue;
                }

                object convertedValue;
                string valueError;
                if (!TryConvertJsonToProperty(property, key, rawValue, out convertedValue, out valueError))
                {
                    error = "Event #" + eventIndex + " property '" + key + "': " + valueError;
                    return false;
                }

                data[key] = convertedValue;
                disabled[key] = false;
            }

            object rawAngleOffset;
            if (dict.TryGetValue(AngleOffsetKey, out rawAngleOffset) && rawAngleOffset != null)
            {
                float angleOffset;
                if (!TryConvertToFiniteFloat(rawAngleOffset, out angleOffset))
                {
                    error = "Event #" + eventIndex + " has an invalid 'angleOffset' value.";
                    return false;
                }

                data[AngleOffsetKey] = angleOffset;
            }

            levelEvent = new LevelEvent(
                floor,
                PlanetGaugeLevelEventRegistry.EventType,
                info,
                data,
                disabled,
                active,
                visible,
                locked);
            return true;
        }

        private static void ApplyImportedEventsToEditor(
            scnEditor editor,
            List<LevelEvent> importedEvents)
        {
            using (new SaveStateScope(editor, true, true, false))
            {
                for (int i = editor.events.Count - 1; i >= 0; i--)
                {
                    LevelEvent existing = editor.events[i];
                    if (existing != null
                        && existing.eventType == PlanetGaugeLevelEventRegistry.EventType)
                    {
                        editor.events.RemoveAt(i);
                    }
                }

                for (int i = 0; i < importedEvents.Count; i++)
                {
                    editor.events.Add(importedEvents[i]);
                }

                editor.ApplyEventsToFloors();
                RefreshEditorInspector(editor);
            }
        }

        private static void RefreshEditorInspector(scnEditor editor)
        {
            if (editor.SelectionIsSingle()
                && editor.selectedFloors != null
                && editor.selectedFloors.Count == 1
                && editor.selectedFloors[0] != null)
            {
                editor.DecideInspectorTabsAtSelected();
                editor.ShowEventIndicators(editor.selectedFloors[0]);
            }
            else if (editor.levelEventsPanel != null)
            {
                editor.levelEventsPanel.HideAllInspectorTabs();
            }
        }

        private static List<ADOFAI.PropertyInfo> GetOrderedProperties(LevelEventInfo info)
        {
            List<ADOFAI.PropertyInfo> result = new List<ADOFAI.PropertyInfo>();
            if (info == null || info.propertiesInfo == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, ADOFAI.PropertyInfo> pair in info.propertiesInfo)
            {
                if (pair.Value != null)
                {
                    result.Add(pair.Value);
                }
            }

            result.Sort((left, right) =>
            {
                int orderCompare = left.order.CompareTo(right.order);
                return orderCompare != 0
                    ? orderCompare
                    : string.CompareOrdinal(left.name, right.name);
            });
            return result;
        }

        private static bool IsPropertyDisabled(LevelEvent levelEvent, string key)
        {
            bool disabled;
            return levelEvent.disabled != null
                && levelEvent.disabled.TryGetValue(key, out disabled)
                && disabled;
        }

        private static object ConvertPropertyForJson(
            ADOFAI.PropertyInfo property,
            string key,
            object value)
        {
            switch (property.type)
            {
                case PropertyType.Bool:
                    bool boolVal;
                    return TryConvertToBool(value, out boolVal)
                        ? (object)boolVal
                        : property.value_default is bool && (bool)property.value_default;
                case PropertyType.Float:
                    float floatVal;
                    if (!TryConvertToFiniteFloat(value, out floatVal))
                    {
                        floatVal = property.value_default is float ? (float)property.value_default : 0f;
                    }
                    return SanitizeFloatProperty(key, floatVal);
                case PropertyType.Enum:
                    if (value != null && property.enumType != null && Enum.IsDefined(property.enumType, value))
                    {
                        return value.ToString();
                    }
                    return property.value_default != null ? property.value_default.ToString() : string.Empty;
                default:
                    return value;
            }
        }

        private static bool TryConvertJsonToProperty(
            ADOFAI.PropertyInfo property,
            string key,
            object rawValue,
            out object convertedValue,
            out string error)
        {
            convertedValue = null;
            error = null;

            switch (property.type)
            {
                case PropertyType.Bool:
                    bool boolVal;
                    if (!TryConvertToBool(rawValue, out boolVal))
                    {
                        error = "expected boolean.";
                        return false;
                    }

                    convertedValue = boolVal;
                    return true;

                case PropertyType.Float:
                    float floatVal;
                    if (!TryConvertToFiniteFloat(rawValue, out floatVal))
                    {
                        error = "expected finite number.";
                        return false;
                    }

                    convertedValue = SanitizeFloatProperty(key, floatVal);
                    return true;

                case PropertyType.Enum:
                    if (property.enumType == null)
                    {
                        error = "enum type is undefined.";
                        return false;
                    }

                    string enumText = rawValue as string;
                    if (enumText != null)
                    {
                        try
                        {
                            object parsedEnum = Enum.Parse(property.enumType, enumText, true);
                            if (Enum.IsDefined(property.enumType, parsedEnum))
                            {
                                convertedValue = parsedEnum;
                                return true;
                            }
                        }
                        catch (ArgumentException)
                        {
                        }

                        error = "unknown enum value '" + SanitizeMessageFragment(enumText, MaxEnumErrorSnippetLength) + "'.";
                        return false;
                    }

                    int enumInt;
                    if (TryConvertToExactInt(rawValue, out enumInt)
                        && Enum.IsDefined(property.enumType, enumInt))
                    {
                        convertedValue = Enum.ToObject(property.enumType, enumInt);
                        return true;
                    }

                    error = "invalid enum value.";
                    return false;

                default:
                    convertedValue = rawValue;
                    return true;
            }
        }

        private static float SanitizeFloatProperty(string key, float value)
        {
            if (string.Equals(key, PlanetGaugeLevelEventRegistry.MultiplierPercentKey, StringComparison.Ordinal))
            {
                return PlanetGaugeValueRules.SanitizeMultiplier(value);
            }

            if (string.Equals(key, PlanetGaugeLevelEventRegistry.RecoveryAmountPercentKey, StringComparison.Ordinal))
            {
                return PlanetGaugeValueRules.SanitizeRecoveryAmount(value);
            }

            if (string.Equals(key, PlanetGaugeLevelEventRegistry.WarningOffsetAngleKey, StringComparison.Ordinal))
            {
                return PlanetGaugeValueRules.SanitizeWarningOffsetAngle(value);
            }

            if (string.Equals(key, PlanetGaugeLevelEventRegistry.WarningPulseBeatsKey, StringComparison.Ordinal))
            {
                return PlanetGaugeValueRules.SanitizeWarningPulseBeats(value);
            }

            if (string.Equals(key, PlanetGaugeLevelEventRegistry.RecoveryCapPercentKey, StringComparison.Ordinal))
            {
                return PlanetGaugeValueRules.SanitizeRecoveryCap(value);
            }

            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        private static bool TryConvertToBool(object value, out bool result)
        {
            if (value is bool)
            {
                result = (bool)value;
                return true;
            }

            string text = value as string;
            if (text != null)
            {
                if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(text, "Enabled", StringComparison.OrdinalIgnoreCase))
                {
                    result = true;
                    return true;
                }

                if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(text, "Disabled", StringComparison.OrdinalIgnoreCase))
                {
                    result = false;
                    return true;
                }
            }

            result = false;
            return false;
        }

        private static bool TryConvertToExactInt(object value, out int result)
        {
            if (value is int)
            {
                result = (int)value;
                return true;
            }

            if (value is long)
            {
                long l = (long)value;
                if (l >= int.MinValue && l <= int.MaxValue)
                {
                    result = (int)l;
                    return true;
                }
            }

            if (value is double)
            {
                double d = (double)value;
                if (!double.IsNaN(d)
                    && !double.IsInfinity(d)
                    && Math.Floor(d) == d
                    && d >= int.MinValue
                    && d <= int.MaxValue)
                {
                    result = (int)d;
                    return true;
                }
            }

            result = 0;
            return false;
        }

        private static bool TryConvertToFiniteFloat(object value, out float result)
        {
            if (value is float)
            {
                float f = (float)value;
                if (!float.IsNaN(f) && !float.IsInfinity(f))
                {
                    result = f;
                    return true;
                }

                result = 0f;
                return false;
            }

            if (value is double)
            {
                double d = (double)value;
                if (!double.IsNaN(d) && !double.IsInfinity(d))
                {
                    float f = (float)d;
                    if (!float.IsNaN(f) && !float.IsInfinity(f))
                    {
                        result = f;
                        return true;
                    }
                }

                result = 0f;
                return false;
            }

            if (value is long)
            {
                result = (long)value;
                return true;
            }

            if (value is int)
            {
                result = (int)value;
                return true;
            }

            if (value is decimal)
            {
                result = (float)(decimal)value;
                return !float.IsNaN(result) && !float.IsInfinity(result);
            }

            result = 0f;
            return false;
        }

        private static string ResolveInitialDirectory(string levelPath)
        {
            if (!string.IsNullOrWhiteSpace(levelPath))
            {
                try
                {
                    string levelDirectory = Path.GetDirectoryName(levelPath);
                    if (!string.IsNullOrWhiteSpace(levelDirectory) && Directory.Exists(levelDirectory))
                    {
                        return levelDirectory;
                    }
                }
                catch (ArgumentException)
                {
                }
            }

            if (!string.IsNullOrWhiteSpace(lastUsedDirectory) && Directory.Exists(lastUsedDirectory))
            {
                return lastUsedDirectory;
            }

            if (!string.IsNullOrWhiteSpace(Main.ModDirectory) && Directory.Exists(Main.ModDirectory))
            {
                return Main.ModDirectory;
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private static string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char ch = name[i];
                bool isInvalid = false;
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (ch == invalid[j])
                    {
                        isInvalid = true;
                        break;
                    }
                }

                if (!isInvalid)
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString().Trim();
        }

        private static void SetAvailabilityStatus(MapSettingsAvailabilityReason reason)
        {
            switch (reason)
            {
                case MapSettingsAvailabilityReason.ModDisabled:
                    SetStatus(StatusKind.ModDisabled, false);
                    break;
                case MapSettingsAvailabilityReason.EditorInPlayMode:
                    SetStatus(StatusKind.EditorInPlayMode, false);
                    break;
                case MapSettingsAvailabilityReason.NotInEditor:
                default:
                    SetStatus(StatusKind.NotInEditor, false);
                    break;
            }
        }

        private static void SetImportFailureStatus(
            MapSettingsImportFailureKind failureKind,
            object[] arguments)
        {
            switch (failureKind)
            {
                case MapSettingsImportFailureKind.FileNotFound:
                    SetStatus(StatusKind.ImportFileNotFound, false);
                    break;
                case MapSettingsImportFailureKind.InvalidExtension:
                    SetStatus(StatusKind.ImportInvalidExtension, false);
                    break;
                case MapSettingsImportFailureKind.InvalidVersion:
                    SetStatus(StatusKind.ImportVersionInvalid, false);
                    break;
                case MapSettingsImportFailureKind.VersionTooNew:
                    SetStatus(StatusKind.ImportVersionTooNew, false, arguments);
                    break;
                case MapSettingsImportFailureKind.NoEvents:
                    SetStatus(StatusKind.ImportNoEvents, false);
                    break;
                case MapSettingsImportFailureKind.TileCountInsufficient:
                    SetStatus(StatusKind.ImportTileCountInsufficient, false, arguments);
                    break;
                case MapSettingsImportFailureKind.FileTooLarge:
                    SetStatus(StatusKind.ImportFileTooLarge, false, arguments);
                    break;
                case MapSettingsImportFailureKind.TooManyEvents:
                    SetStatus(StatusKind.ImportTooManyEvents, false, arguments);
                    break;
                case MapSettingsImportFailureKind.InvalidJsonOrSchema:
                default:
                    SetStatus(StatusKind.ImportInvalidFormat, false, arguments);
                    break;
            }
        }

        private static void SetStatus(StatusKind kind, bool mapMismatchWarning, params object[] arguments)
        {
            lastStatusKind = kind;
            lastStatusArguments = SanitizeStatusArguments(arguments);
            lastMapMismatchWarning = mapMismatchWarning;
        }

        private static object[] SanitizeStatusArguments(object[] arguments)
        {
            if (arguments == null || arguments.Length == 0)
            {
                return arguments;
            }

            object[] sanitized = new object[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
            {
                string text = arguments[i] as string;
                sanitized[i] = text != null
                    ? (object)SanitizeMessageFragment(text, MaxStatusFragmentLength)
                    : arguments[i];
            }

            return sanitized;
        }

        /// <summary>
        /// 외부 입력·파일명·예외 문자열이 UMM IMGUI 상태 메시지에 삽입될 때
        /// Rich-Text 태그, 줄바꿈/제어문자, 양방향(Bidi) 스푸핑 문자 및 과도한 길이를 무력화한다.
        /// (레벨 데이터의 곡명 비교에는 사용하지 않으므로 ADOFAI 곡명의 &lt;color&gt; 태그에는 영향을 주지 않는다.)
        /// </summary>
        internal static string SanitizeMessageFragment(string raw, int maxLength)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            int safeMax = Math.Max(8, maxLength);
            StringBuilder builder = new StringBuilder(Math.Min(raw.Length, safeMax + 4));
            bool previousWasSpace = false;
            bool truncated = false;

            for (int i = 0; i < raw.Length; i++)
            {
                char ch = raw[i];
                if (IsBidiControlChar(ch))
                {
                    continue;
                }

                if (ch == '\r' || ch == '\n' || ch == '\t' || char.IsControl(ch))
                {
                    if (!previousWasSpace && builder.Length > 0)
                    {
                        if (builder.Length >= safeMax)
                        {
                            truncated = true;
                            break;
                        }

                        builder.Append(' ');
                        previousWasSpace = true;
                    }

                    continue;
                }

                previousWasSpace = ch == ' ';
                if (builder.Length >= safeMax)
                {
                    truncated = true;
                    break;
                }

                if (ch == '<')
                {
                    builder.Append('\u2039'); // ‹
                }
                else if (ch == '>')
                {
                    builder.Append('\u203A'); // ›
                }
                else
                {
                    builder.Append(ch);
                }
            }

            string cleaned = builder.ToString().Trim();
            return truncated ? cleaned + "..." : cleaned;
        }

        private static bool IsBidiControlChar(char ch)
        {
            return ch == '\u061C'
                || ch == '\u200E'
                || ch == '\u200F'
                || (ch >= '\u202A' && ch <= '\u202E')
                || (ch >= '\u2066' && ch <= '\u2069');
        }

        private static string FormatStatusMessage(StatusKind kind, object[] args)
        {
            switch (kind)
            {
                case StatusKind.None:
                    return string.Empty;
                case StatusKind.ModDisabled:
                    return LocalizedStrings.MapSettingsTransferModDisabled;
                case StatusKind.NotInEditor:
                    return LocalizedStrings.MapSettingsTransferEditorOnly;
                case StatusKind.EditorInPlayMode:
                    return LocalizedStrings.MapSettingsTransferPlayModeBlocked;
                case StatusKind.ExportNoEvents:
                    return LocalizedStrings.MapSettingsExportNoEvents;
                case StatusKind.ExportSucceeded:
                    return LocalizedStrings.Format(LocalizedStrings.MapSettingsExportSucceeded, args ?? new object[0]);
                case StatusKind.ExportFailed:
                    return LocalizedStrings.Format(LocalizedStrings.MapSettingsExportFailed, args ?? new object[0]);
                case StatusKind.ImportSucceeded:
                    return LocalizedStrings.Format(LocalizedStrings.MapSettingsImportSucceeded, args ?? new object[0]);
                case StatusKind.ImportFailed:
                    return LocalizedStrings.Format(LocalizedStrings.MapSettingsImportFailed, args ?? new object[0]);
                case StatusKind.ImportFileNotFound:
                    return LocalizedStrings.MapSettingsImportFileNotFound;
                case StatusKind.ImportInvalidExtension:
                    return LocalizedStrings.MapSettingsImportInvalidExtension;
                case StatusKind.ImportInvalidFormat:
                    return LocalizedStrings.Format(LocalizedStrings.MapSettingsImportInvalidFormat, args ?? new object[0]);
                case StatusKind.ImportVersionInvalid:
                    return LocalizedStrings.MapSettingsImportVersionInvalid;
                case StatusKind.ImportVersionTooNew:
                    return LocalizedStrings.Format(LocalizedStrings.MapSettingsImportVersionTooNew, args ?? new object[0]);
                case StatusKind.ImportNoEvents:
                    return LocalizedStrings.MapSettingsImportNoEvents;
                case StatusKind.ImportTileCountInsufficient:
                    return LocalizedStrings.Format(LocalizedStrings.MapSettingsImportTileCountInsufficient, args ?? new object[0]);
                case StatusKind.ImportFileTooLarge:
                    return LocalizedStrings.Format(LocalizedStrings.MapSettingsImportFileTooLarge, args ?? new object[0]);
                case StatusKind.ImportTooManyEvents:
                    return LocalizedStrings.Format(LocalizedStrings.MapSettingsImportTooManyEvents, args ?? new object[0]);
                default:
                    return string.Empty;
            }
        }
    }
}
