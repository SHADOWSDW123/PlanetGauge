using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ADOFAI;
using ADOFAI.LevelEditor.Controls;
using HarmonyLib;
using UnityEngine;

namespace PlanetGauge
{
    /// <summary>
    /// 레벨 이벤트의 "속성 설정" 드롭다운에 저장되는 값이다.
    /// 실제 판정값 반영은 <see cref="GaugeRuntime"/>가 담당한다.
    /// </summary>
    internal enum PlanetGaugeAttributeMode
    {
        Normal,
        BlockRecovery,
        AmplifyDecrease,
        AmplifyIncrease,
        AmplifyBoth,
        Blindfold,
        ForceRecovery
    }

    internal enum PlanetGaugeRateSource
    {
        None,
        Increase,
        Decrease,
        Both
    }

    internal struct PlanetGaugeRateChannel
    {
        internal PlanetGaugeRateChannel(bool enabled, float percent, PlanetGaugeRateSource source)
        {
            Enabled = enabled;
            Percent = PlanetGaugeValueRules.SanitizeMultiplier(percent);
            Source = enabled ? source : PlanetGaugeRateSource.None;
        }

        internal bool Enabled { get; }
        internal float Percent { get; }
        internal PlanetGaugeRateSource Source { get; }

        internal static PlanetGaugeRateChannel Disabled
        {
            get { return new PlanetGaugeRateChannel(false, 100f, PlanetGaugeRateSource.None); }
        }
    }

    internal struct PlanetGaugeEventSettings
    {
        internal static readonly PlanetGaugeEventSettings Default = new PlanetGaugeEventSettings(
            false,
            PlanetGaugeRateChannel.Disabled,
            PlanetGaugeRateChannel.Disabled,
            100f,
            100f,
            100f,
            false,
            true,
            false,
            100f,
            false);

        internal PlanetGaugeEventSettings(
            bool recoveryBlocked,
            PlanetGaugeRateChannel recoveryRate,
            PlanetGaugeRateChannel damageRate,
            float configuredIncreasePercent,
            float configuredDecreasePercent,
            float configuredBothPercent,
            bool blindfoldEnabled,
            bool failureProtection,
            bool recoveryCapEnabled,
            float recoveryCapPercent,
            bool autoTileRecovery)
        {
            RecoveryBlocked = recoveryBlocked;
            RecoveryRate = recoveryRate;
            DamageRate = damageRate;
            ConfiguredIncreasePercent = PlanetGaugeValueRules.SanitizeMultiplier(configuredIncreasePercent);
            ConfiguredDecreasePercent = PlanetGaugeValueRules.SanitizeMultiplier(configuredDecreasePercent);
            ConfiguredBothPercent = PlanetGaugeValueRules.SanitizeMultiplier(configuredBothPercent);
            BlindfoldEnabled = blindfoldEnabled;
            FailureProtection = failureProtection;
            RecoveryCapEnabled = recoveryCapEnabled;
            RecoveryCapPercent = recoveryCapPercent;
            AutoTileRecovery = autoTileRecovery;
        }

        internal bool RecoveryBlocked { get; }
        internal PlanetGaugeRateChannel RecoveryRate { get; }
        internal PlanetGaugeRateChannel DamageRate { get; }
        internal float ConfiguredIncreasePercent { get; }
        internal float ConfiguredDecreasePercent { get; }
        internal float ConfiguredBothPercent { get; }

        internal bool BlindfoldEnabled { get; }

        internal bool FailureProtection { get; }

        internal bool RecoveryCapEnabled { get; }

        internal float RecoveryCapPercent { get; }
        internal bool AutoTileRecovery { get; }
    }

    /// <summary>
    /// 이벤트 하나가 현재 런타임 설정 중 어떤 값만 바꿀지 나타낸다.
    /// PropertyInfo의 O/X 상태는 LevelEvent.disabled에 저장되며 여기서 한 번만 해석한다.
    /// </summary>
    internal struct PlanetGaugeEventCommand
    {
        internal bool ApplyAttributeMode;
        internal PlanetGaugeAttributeMode AttributeMode;
        internal bool AttributeEnabled;
        internal bool DisableOtherAttributes;
        internal bool ApplyMultiplier;
        internal float MultiplierPercent;
        internal float RecoveryAmountPercent;
        internal float WarningOffsetAngle;
        internal float WarningPulseBeats;
        internal bool ApplyFailureProtection;
        internal bool FailureProtection;
        internal bool ApplyRecoveryCap;
        internal bool RecoveryCapEnabled;
        internal float RecoveryCapPercent;
        internal bool ForceRecoveryCap;
        internal bool ApplyAutoTileRecovery;
        internal bool AutoTileRecovery;
    }

    internal static class PlanetGaugeValueRules
    {
        internal static float SanitizeMultiplier(float value)
        {
            return SanitizePercent(value, 100f, 0f, 1000f);
        }

        internal static float SanitizeRecoveryCap(float value)
        {
            return SanitizePercent(value, 100f, 0.1f, 1000f);
        }

        internal static float SanitizeRecoveryAmount(float value)
        {
            return SanitizePercent(value, 0f, -1000f, 1000f);
        }

        internal static float SanitizeWarningOffsetAngle(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return Mathf.Min(0f, value);
        }

        internal static float SanitizeWarningPulseBeats(float value)
        {
            return SanitizePercent(value, 0.5f, 0.125f, 16f);
        }

        internal static bool IsAmplificationMode(PlanetGaugeAttributeMode mode)
        {
            return mode == PlanetGaugeAttributeMode.AmplifyDecrease
                || mode == PlanetGaugeAttributeMode.AmplifyIncrease
                || mode == PlanetGaugeAttributeMode.AmplifyBoth;
        }

        private static float SanitizePercent(
            float value,
            float fallback,
            float minimum,
            float maximum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }

            return Mathf.Clamp(value, minimum, maximum);
        }
    }

    /// <summary>
    /// Assembly-CSharp의 LevelEventInfo/PropertyInfo 계약에 PlanetGauge 이벤트를 등록한다.
    /// 네이티브 enum을 수정할 수 없으므로 충돌 가능성이 낮은 고정 숫자 ID를 사용한다.
    /// </summary>
    internal static class PlanetGaugeLevelEventRegistry
    {
        internal const int NumericEventId = 0x5047;
        internal const string EventName = "SetPlanetGauge";

        internal const string AttributeModeKey = "attributeMode";
        internal const string AttributeEnabledKey = "attributeEnabled";
        internal const string DisableOtherAttributesKey = "disableOtherAttributes";
        internal const string MultiplierPercentKey = "multiplierPercent";
        internal const string RecoveryAmountPercentKey = "recoveryAmountPercent";
        internal const string WarningOffsetAngleKey = "warningOffsetAngle";
        internal const string WarningPulseBeatsKey = "warningPulseBeats";
        internal const string FailureProtectionKey = "failureProtection";
        internal const string RecoveryCapEnabledKey = "recoveryCapEnabled";
        internal const string RecoveryCapPercentKey = "recoveryCapPercent";
        internal const string ForceRecoveryCapKey = "forceRecoveryCap";
        internal const string AutoTileRecoveryKey = "autoTileRecovery";
        internal const string MultiplierReuseNoteKey = "multiplierReuseNote";
        internal const string MultiplierReuseLocalizationKey = "planetGauge.multiplierReuseNote";
        internal const string RecoveryAmountNoteKey = "recoveryAmountNote";
        internal const string RecoveryAmountLocalizationKey = "planetGauge.recoveryAmountNote";

        private const string IconRelativePath = "Assets/Gaugeline.png";
        private const float IconPixelsPerUnit = 128f;

        internal static readonly LevelEventType EventType = (LevelEventType)NumericEventId;

        private static LevelEventInfo eventInfo;
        private static Sprite registeredIcon;
        private static Sprite customIcon;
        private static Texture2D customIconTexture;
        private static bool customIconLoadAttempted;

        internal static LevelEventInfo EventInfo
        {
            get
            {
                if (eventInfo == null)
                {
                    eventInfo = CreateEventInfo();
                }

                return eventInfo;
            }
        }

        internal static bool IsRegistered
        {
            get
            {
                if (GCS.levelEventsInfo == null || GCS.levelEventTypeString == null)
                {
                    return false;
                }

                LevelEventInfo registeredInfo;
                string registeredName;
                return GCS.levelEventsInfo.TryGetValue(NumericEventId.ToString(), out registeredInfo)
                    && ReferenceEquals(registeredInfo, eventInfo)
                    && GCS.levelEventTypeString.TryGetValue(EventType, out registeredName)
                    && string.Equals(registeredName, EventName, StringComparison.Ordinal);
            }
        }

        internal static bool TryRegister()
        {
            if (!AreGameRegistriesReady())
            {
                return false;
            }

            LevelEventInfo existingInfo;
            if (GCS.levelEventsInfo.TryGetValue(NumericEventId.ToString(), out existingInfo)
                && !ReferenceEquals(existingInfo, EventInfo))
            {
                throw new InvalidOperationException(
                    "커스텀 이벤트 ID " + NumericEventId + "가 다른 이벤트에서 이미 사용 중입니다.");
            }

            string existingName;
            if (GCS.levelEventTypeString.TryGetValue(EventType, out existingName)
                && !string.Equals(existingName, EventName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "커스텀 이벤트 ID " + NumericEventId + "가 " + existingName + " 이벤트와 충돌합니다.");
            }

            // 편집기는 levelEventsInfo.Values를 순회한다. 이름/숫자 키를 둘 다 넣으면 버튼이 중복되므로
            // undefined enum의 ToString() 결과인 숫자 키 하나만 등록한다.
            GCS.levelEventsInfo[NumericEventId.ToString()] = EventInfo;
            GCS.levelEventTypeString[EventType] = EventName;
            EnsureIcon();
            return true;
        }

        private static bool AreGameRegistriesReady()
        {
            if (GCS.levelEventsInfo == null || GCS.levelEventTypeString == null)
            {
                return false;
            }

            LevelEventInfo nativeInfo;
            string nativeName;
            return GCS.levelEventsInfo.TryGetValue(nameof(LevelEventType.SetSpeed), out nativeInfo)
                && nativeInfo != null
                && GCS.levelEventTypeString.TryGetValue(LevelEventType.SetSpeed, out nativeName)
                && string.Equals(nativeName, nameof(LevelEventType.SetSpeed), StringComparison.Ordinal);
        }

        internal static void RollbackRegistration()
        {
            if (GCS.levelEventsInfo != null)
            {
                LevelEventInfo registeredInfo;
                if (GCS.levelEventsInfo.TryGetValue(NumericEventId.ToString(), out registeredInfo)
                    && ReferenceEquals(registeredInfo, eventInfo))
                {
                    GCS.levelEventsInfo.Remove(NumericEventId.ToString());
                }
            }

            if (GCS.levelEventTypeString != null)
            {
                string registeredName;
                if (GCS.levelEventTypeString.TryGetValue(EventType, out registeredName)
                    && string.Equals(registeredName, EventName, StringComparison.Ordinal))
                {
                    GCS.levelEventTypeString.Remove(EventType);
                }
            }

            if (GCS.levelEventIcons != null && registeredIcon != null)
            {
                Sprite currentIcon;
                if (GCS.levelEventIcons.TryGetValue(EventType, out currentIcon)
                    && ReferenceEquals(currentIcon, registeredIcon))
                {
                    GCS.levelEventIcons.Remove(EventType);
                }
            }

            registeredIcon = null;
            DestroyCustomIcon();
            customIconLoadAttempted = false;
        }

        internal static void EnsureIcon()
        {
            if (GCS.levelEventIcons == null || GCS.levelEventIcons.ContainsKey(EventType))
            {
                return;
            }

            Sprite icon = GetCustomIcon();
            if (icon != null)
            {
                registeredIcon = icon;
                GCS.levelEventIcons[EventType] = icon;
                return;
            }

            if (!GCS.levelEventIcons.TryGetValue(LevelEventType.EventSettings, out icon)
                && !GCS.levelEventIcons.TryGetValue(LevelEventType.SetSpeed, out icon))
            {
                foreach (KeyValuePair<LevelEventType, Sprite> pair in GCS.levelEventIcons)
                {
                    icon = pair.Value;
                    break;
                }
            }

            if (icon != null)
            {
                // 외부 아이콘이 없거나 손상된 경우에만 게임의 설정 이벤트 아이콘을 빌린다.
                registeredIcon = icon;
                GCS.levelEventIcons[EventType] = icon;
            }
        }

        private static Sprite GetCustomIcon()
        {
            if (customIcon != null)
            {
                return customIcon;
            }

            if (customIconLoadAttempted)
            {
                return null;
            }

            customIconLoadAttempted = true;
            string iconPath = string.IsNullOrEmpty(Main.ModDirectory)
                ? null
                : Path.Combine(Main.ModDirectory, IconRelativePath);
            if (string.IsNullOrEmpty(iconPath) || !File.Exists(iconPath))
            {
                if (Main.Logger != null)
                {
                    Main.Logger.Warning(
                        "PlanetGauge 이벤트 아이콘을 찾지 못해 기본 아이콘을 사용합니다: "
                        + (iconPath ?? IconRelativePath));
                }

                return null;
            }

            Texture2D texture = null;
            try
            {
                byte[] pngBytes = File.ReadAllBytes(iconPath);
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.name = "PlanetGauge.EventIcon.Texture";
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                if (!LoadPng(texture, pngBytes))
                {
                    throw new InvalidDataException("PNG 데이터를 Unity Texture2D로 변환하지 못했습니다.");
                }

                /*
                 * 원본이 현재 300x300이어도 에디터의 고정 아이콘 슬롯이 크기를 맞춘다.
                 * 128 PPU를 사용해 추후 128x128 파일로 교체해도 동일한 스케일 계약을 유지한다.
                 */
                customIcon = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    IconPixelsPerUnit);
                if (customIcon == null)
                {
                    throw new InvalidOperationException("Texture2D에서 Sprite를 생성하지 못했습니다.");
                }

                customIcon.name = "PlanetGauge.EventIcon";
                customIconTexture = texture;
                return customIcon;
            }
            catch (Exception exception)
            {
                if (customIcon != null)
                {
                    UnityEngine.Object.Destroy(customIcon);
                }

                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }

                customIcon = null;
                customIconTexture = null;
                Main.LogException(
                    "PlanetGauge 이벤트 아이콘 로드에 실패해 기본 아이콘을 사용합니다: " + iconPath,
                    exception);
                return null;
            }
        }

        private static bool LoadPng(Texture2D texture, byte[] pngBytes)
        {
            // 설치본의 ImageConversionModule은 netstandard 2.1을 참조하므로 net48에서 직접 참조하지 않는다.
            // 아이콘은 선택 기능이므로 확인된 정확한 오버로드만 런타임에 찾아 호출하고, 없으면 폴백한다.
            Type imageConversionType = Type.GetType(
                "UnityEngine.ImageConversion, UnityEngine.ImageConversionModule",
                false);
            MethodInfo loadImage = imageConversionType == null
                ? null
                : imageConversionType.GetMethod(
                    "LoadImage",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) },
                    null);
            if (loadImage == null)
            {
                throw new MissingMethodException(
                    "UnityEngine.ImageConversion.LoadImage(Texture2D, byte[], bool)을 찾을 수 없습니다.");
            }

            object result = loadImage.Invoke(null, new object[] { texture, pngBytes, true });
            return result is bool && (bool)result;
        }

        private static void DestroyCustomIcon()
        {
            if (customIcon != null)
            {
                UnityEngine.Object.Destroy(customIcon);
                customIcon = null;
            }

            if (customIconTexture != null)
            {
                UnityEngine.Object.Destroy(customIconTexture);
                customIconTexture = null;
            }
        }

        internal static void ValidateRequiredGameApi()
        {
            RequireMethod(typeof(ADOStartup), nameof(ADOStartup.SetupLevelEventsInfo), Type.EmptyTypes);
            RequireMethod(typeof(scnEditor), nameof(scnEditor.LoadEditorProperties), Type.EmptyTypes);
            RequireMethod(
                typeof(scnEditor),
                nameof(scnEditor.GetFloorEvents),
                new[] { typeof(int), typeof(LevelEventType) });
            RequireMethod(
                typeof(InspectorPanel),
                nameof(InspectorPanel.ShowTabsForFloor),
                new[] { typeof(int) });
            RequireMethod(
                typeof(ADOFAI.PropertyInfo),
                nameof(ADOFAI.PropertyInfo.Validate),
                new[] { typeof(float) });
            RequireMethod(
                typeof(PropertyControl_Toggle),
                nameof(PropertyControl_Toggle.SelectVar),
                new[] { typeof(string) });
            RequireMethod(
                typeof(scnGame),
                nameof(scnGame.ApplyEvent),
                new[]
                {
                    typeof(LevelEvent),
                    typeof(float),
                    typeof(float),
                    typeof(List<scrFloor>),
                    typeof(float),
                    typeof(int?)
                });

            if (FindParseEnumMethod() == null)
            {
                throw new MissingMethodException(
                    "호환성에 필요한 RDUtils.ParseEnum<LevelEventType> 메서드를 찾을 수 없습니다.");
            }

            if (FindLevelEventConstructor() == null)
            {
                throw new MissingMethodException(
                    "호환성에 필요한 LevelEvent 생성자를 찾을 수 없습니다.");
            }

            RequireMethod(
                typeof(LevelEvent),
                nameof(LevelEvent.Decode),
                new[] { typeof(Dictionary<string, object>), typeof(string), typeof(bool) });
            RequireMethod(
                typeof(LevelEvent),
                nameof(LevelEvent.Encode),
                new[] { typeof(bool) });
            RequireMethod(
                typeof(RDString),
                nameof(RDString.GetWithCheck),
                new[] { typeof(string), typeof(bool).MakeByRefType(), typeof(Dictionary<string, object>) });
        }

        internal static MethodInfo FindParseEnumMethod()
        {
            MethodInfo[] methods = typeof(RDUtils).GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            for (int index = 0; index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                if (method.Name == "ParseEnum"
                    && method.IsGenericMethodDefinition
                    && method.GetGenericArguments().Length == 1
                    && method.GetParameters().Length == 2)
                {
                    return method.MakeGenericMethod(typeof(LevelEventType));
                }
            }

            return null;
        }

        internal static ConstructorInfo FindLevelEventConstructor()
        {
            return AccessTools.Constructor(
                typeof(LevelEvent),
                new[]
                {
                    typeof(int),
                    typeof(LevelEventType),
                    typeof(LevelEventInfo),
                    typeof(Dictionary<string, object>),
                    typeof(Dictionary<string, bool>),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool)
                });
        }

        private static LevelEventInfo CreateEventInfo()
        {
            LevelEventInfo info = new LevelEventInfo
            {
                name = EventName,
                type = EventType,
                pro = false,
                taroDLC = false,
                executionTime = LevelEventExecutionTime.OnBar,
                allowFirstFloor = true,
                isDecoration = false,
                useGroups = false,
                stretchViewport = false,
                categories = new List<LevelEventCategory> { LevelEventCategory.Gameplay },
                propertiesInfo = new Dictionary<string, ADOFAI.PropertyInfo>()
            };

            ADOFAI.PropertyInfo attributeMode = CreateProperty(
                info,
                AttributeModeKey,
                "Enum:" + typeof(PlanetGaugeAttributeMode).AssemblyQualifiedName,
                PlanetGaugeAttributeMode.Normal.ToString(),
                "속성 설정");
            MakeOptional(attributeMode, true);
            AddProperty(info, attributeMode, 0);

            ADOFAI.PropertyInfo attributeEnabled = CreateProperty(
                info,
                AttributeEnabledKey,
                "Bool",
                true,
                "선택 속성 켜기");
            AddAttributeShowConditions(attributeEnabled);
            AddProperty(info, attributeEnabled, 1);

            ADOFAI.PropertyInfo disableOthers = CreateProperty(
                info,
                DisableOtherAttributesKey,
                "Bool",
                false,
                "다른 속성 설정 끄기");
            AddAttributeShowConditions(disableOthers);
            disableOthers.showIfVals.Add(Tuple.Create(
                AttributeModeKey,
                PlanetGaugeAttributeMode.Normal.ToString()));
            disableOthers.showIfVals.Add(Tuple.Create(
                AttributeModeKey,
                PlanetGaugeAttributeMode.ForceRecovery.ToString()));
            AddProperty(info, disableOthers, 2);

            ADOFAI.PropertyInfo multiplier = CreateProperty(
                info,
                MultiplierPercentKey,
                "Float",
                100f,
                "변경값 설정");
            multiplier.unit = "%";
            multiplier.float_min = 0f;
            multiplier.float_max = 1000f;
            multiplier.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyDecrease.ToString()));
            multiplier.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyIncrease.ToString()));
            multiplier.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyBoth.ToString()));
            MakeOptional(multiplier, false);
            AddProperty(info, multiplier, 3);

            ADOFAI.PropertyInfo multiplierNote = CreateNoteProperty(
                info,
                MultiplierReuseNoteKey,
                MultiplierReuseLocalizationKey);
            multiplierNote.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyDecrease.ToString()));
            multiplierNote.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyIncrease.ToString()));
            multiplierNote.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyBoth.ToString()));
            AddProperty(info, multiplierNote, 4);

            ADOFAI.PropertyInfo recoveryAmount = CreateProperty(
                info,
                RecoveryAmountPercentKey,
                "Float",
                0f,
                "회복량 설정");
            recoveryAmount.unit = "%";
            recoveryAmount.float_min = -1000f;
            recoveryAmount.float_max = 1000f;
            recoveryAmount.showIfVals.Add(Tuple.Create(
                AttributeModeKey,
                PlanetGaugeAttributeMode.ForceRecovery.ToString()));
            AddProperty(info, recoveryAmount, 5);

            ADOFAI.PropertyInfo warningOffset = CreateProperty(
                info,
                WarningOffsetAngleKey,
                "Float",
                0f,
                "사전 경고 각도 오프셋");
            warningOffset.unit = "°";
            warningOffset.float_max = 0f;
            warningOffset.showIfVals.Add(Tuple.Create(
                AttributeModeKey,
                PlanetGaugeAttributeMode.ForceRecovery.ToString()));
            AddProperty(info, warningOffset, 6);

            ADOFAI.PropertyInfo warningPulse = CreateProperty(
                info,
                WarningPulseBeatsKey,
                "Float",
                0.5f,
                "점멸 주기");
            warningPulse.unit = "beats";
            warningPulse.float_min = 0.125f;
            warningPulse.float_max = 16f;
            warningPulse.showIfVals.Add(Tuple.Create(
                AttributeModeKey,
                PlanetGaugeAttributeMode.ForceRecovery.ToString()));
            AddProperty(info, warningPulse, 7);

            ADOFAI.PropertyInfo recoveryAmountNote = CreateNoteProperty(
                info,
                RecoveryAmountNoteKey,
                RecoveryAmountLocalizationKey);
            recoveryAmountNote.showIfVals.Add(Tuple.Create(
                AttributeModeKey,
                PlanetGaugeAttributeMode.ForceRecovery.ToString()));
            AddProperty(info, recoveryAmountNote, 8);

            ADOFAI.PropertyInfo failureProtection = CreateProperty(
                info,
                FailureProtectionKey,
                "Bool",
                true,
                "실패 방지");
            MakeOptional(failureProtection, false);
            AddProperty(info, failureProtection, 9);

            ADOFAI.PropertyInfo recoveryCapEnabled = CreateProperty(
                info,
                RecoveryCapEnabledKey,
                "Bool",
                false,
                "회복 상한 설정");
            MakeOptional(recoveryCapEnabled, false);
            AddProperty(info, recoveryCapEnabled, 10);

            ADOFAI.PropertyInfo recoveryCap = CreateProperty(
                info,
                RecoveryCapPercentKey,
                "Float",
                100f,
                "회복 상한");
            recoveryCap.unit = "%";
            recoveryCap.float_min = 0.1f;
            recoveryCap.float_max = 1000f;
            // PropertyInfo.ValueMatch는 Bool 조건에서 소문자 "true"를 요구한다.
            recoveryCap.showIfVals.Add(Tuple.Create(RecoveryCapEnabledKey, "true"));
            AddProperty(info, recoveryCap, 11);

            ADOFAI.PropertyInfo forceCap = CreateProperty(
                info,
                ForceRecoveryCapKey,
                "Bool",
                true,
                "체력 상한 강제 제한");
            forceCap.showIfVals.Add(Tuple.Create(RecoveryCapEnabledKey, "true"));
            AddProperty(info, forceCap, 12);

            ADOFAI.PropertyInfo autoTileRecovery = CreateProperty(
                info,
                AutoTileRecoveryKey,
                "Bool",
                false,
                "자동 플레이 타일 체력 회복");
            MakeOptional(autoTileRecovery, false);
            AddProperty(info, autoTileRecovery, 13);

            return info;
        }

        private static void AddAttributeShowConditions(ADOFAI.PropertyInfo property)
        {
            property.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.BlockRecovery.ToString()));
            property.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyDecrease.ToString()));
            property.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyIncrease.ToString()));
            property.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyBoth.ToString()));
            property.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.Blindfold.ToString()));
        }

        private static ADOFAI.PropertyInfo CreateProperty(
            LevelEventInfo info,
            string name,
            string type,
            object defaultValue,
            string label)
        {
            Dictionary<string, object> schema = new Dictionary<string, object>
            {
                { "name", name },
                { "type", type },
                { "default", defaultValue },
                { "customLabel", label }
            };

            return new ADOFAI.PropertyInfo(schema, info);
        }

        private static ADOFAI.PropertyInfo CreateNoteProperty(
            LevelEventInfo info,
            string name,
            string noteKey)
        {
            Dictionary<string, object> schema = new Dictionary<string, object>
            {
                { "name", name },
                { "type", "Note" },
                { "noteKey", noteKey },
                { "encode", false }
            };

            return new ADOFAI.PropertyInfo(schema, info);
        }

        private static void MakeOptional(ADOFAI.PropertyInfo property, bool startEnabled)
        {
            property.canBeDisabled = true;
            property.startEnabled = startEnabled;
        }

        private static void AddProperty(LevelEventInfo info, ADOFAI.PropertyInfo property, int order)
        {
            property.order = order;
            info.propertiesInfo.Add(property.name, property);
        }

        private static void RequireMethod(Type type, string name, Type[] parameterTypes)
        {
            if (AccessTools.Method(type, name, parameterTypes) == null)
            {
                throw new MissingMethodException(
                    "호환성에 필요한 게임 메서드를 찾을 수 없습니다: " + type.FullName + "." + name);
            }
        }
    }

    internal sealed class PlanetGaugeLevelEventEffect : ffxPlusBase
    {
        private PlanetGaugeEventCommand command;

        public override void Decode(LevelEvent levelEvent)
        {
            PlanetGaugeAttributeMode mode = levelEvent.Get<PlanetGaugeAttributeMode>(
                PlanetGaugeLevelEventRegistry.AttributeModeKey,
                PlanetGaugeAttributeMode.Normal);
            if (!Enum.IsDefined(typeof(PlanetGaugeAttributeMode), mode))
            {
                mode = PlanetGaugeAttributeMode.Normal;
            }

            float multiplier = PlanetGaugeValueRules.SanitizeMultiplier(
                levelEvent.Get<float>(PlanetGaugeLevelEventRegistry.MultiplierPercentKey, 100f));
            float recoveryAmount = PlanetGaugeValueRules.SanitizeRecoveryAmount(
                levelEvent.Get<float>(PlanetGaugeLevelEventRegistry.RecoveryAmountPercentKey, 0f));
            float warningOffsetAngle = PlanetGaugeValueRules.SanitizeWarningOffsetAngle(
                levelEvent.Get<float>(PlanetGaugeLevelEventRegistry.WarningOffsetAngleKey, 0f));
            float warningPulseBeats = PlanetGaugeValueRules.SanitizeWarningPulseBeats(
                levelEvent.Get<float>(PlanetGaugeLevelEventRegistry.WarningPulseBeatsKey, 0.5f));
            bool failureProtection = levelEvent.Get<bool>(
                PlanetGaugeLevelEventRegistry.FailureProtectionKey,
                true);
            bool recoveryCapEnabled = levelEvent.Get<bool>(
                PlanetGaugeLevelEventRegistry.RecoveryCapEnabledKey,
                false);
            float recoveryCap = PlanetGaugeValueRules.SanitizeRecoveryCap(
                levelEvent.Get<float>(PlanetGaugeLevelEventRegistry.RecoveryCapPercentKey, 100f));
            bool forceRecoveryCap = levelEvent.Get<bool>(
                PlanetGaugeLevelEventRegistry.ForceRecoveryCapKey,
                true);
            bool attributeEnabled = levelEvent.Get<bool>(
                PlanetGaugeLevelEventRegistry.AttributeEnabledKey,
                true);
            bool disableOtherAttributes = levelEvent.Get<bool>(
                PlanetGaugeLevelEventRegistry.DisableOtherAttributesKey,
                false);
            bool autoTileRecovery = levelEvent.Get<bool>(
                PlanetGaugeLevelEventRegistry.AutoTileRecoveryKey,
                false);

            command = new PlanetGaugeEventCommand
            {
                ApplyAttributeMode = IsPropertyEnabled(
                    levelEvent,
                    PlanetGaugeLevelEventRegistry.AttributeModeKey),
                AttributeMode = mode,
                AttributeEnabled = attributeEnabled,
                DisableOtherAttributes = disableOtherAttributes,
                ApplyMultiplier = IsPropertyEnabled(
                    levelEvent,
                    PlanetGaugeLevelEventRegistry.MultiplierPercentKey),
                MultiplierPercent = multiplier,
                RecoveryAmountPercent = recoveryAmount,
                WarningOffsetAngle = warningOffsetAngle,
                WarningPulseBeats = warningPulseBeats,
                ApplyFailureProtection = IsPropertyEnabled(
                    levelEvent,
                    PlanetGaugeLevelEventRegistry.FailureProtectionKey),
                FailureProtection = failureProtection,
                ApplyRecoveryCap = IsPropertyEnabled(
                    levelEvent,
                    PlanetGaugeLevelEventRegistry.RecoveryCapEnabledKey),
                RecoveryCapEnabled = recoveryCapEnabled,
                RecoveryCapPercent = recoveryCap,
                ForceRecoveryCap = IsPropertyEnabled(
                    levelEvent,
                    PlanetGaugeLevelEventRegistry.RecoveryCapEnabledKey)
                    && recoveryCapEnabled
                    && forceRecoveryCap,
                ApplyAutoTileRecovery = IsPropertyEnabled(
                    levelEvent,
                    PlanetGaugeLevelEventRegistry.AutoTileRecoveryKey),
                AutoTileRecovery = autoTileRecovery
            };
        }

        public override void StartEffect(scrPlanet planet)
        {
            try
            {
                GaugeVisualTransitions.CancelWarning(VisualToken);
            }
            catch (Exception exception)
            {
                Main.LogException(
                    "SetPlanetGauge 사전 경고 정리에 실패했지만 이벤트 실행은 계속합니다.",
                    exception);
            }
            if (GaugeRuntime.ShouldHandle())
            {
                GaugeRuntime.ApplyEventSettings(command);
            }
        }

        internal int VisualToken { get; set; }

        private static bool IsPropertyEnabled(LevelEvent levelEvent, string propertyName)
        {
            bool disabled;
            return levelEvent.disabled == null
                || !levelEvent.disabled.TryGetValue(propertyName, out disabled)
                || !disabled;
        }
    }

    internal sealed class PlanetGaugeWarningLevelEventEffect : ffxPlusBase
    {
        private float recoveryAmount;
        private float warningPulseBeats;

        internal int VisualToken { get; set; }

        public override void Decode(LevelEvent levelEvent)
        {
            recoveryAmount = PlanetGaugeValueRules.SanitizeRecoveryAmount(
                levelEvent.Get<float>(PlanetGaugeLevelEventRegistry.RecoveryAmountPercentKey, 0f));
            warningPulseBeats = PlanetGaugeValueRules.SanitizeWarningPulseBeats(
                levelEvent.Get<float>(PlanetGaugeLevelEventRegistry.WarningPulseBeatsKey, 0.5f));
        }

        public override void StartEffect(scrPlanet planet)
        {
            if (GaugeRuntime.ShouldHandle())
            {
                try
                {
                    GaugeVisualTransitions.BeginWarning(
                        VisualToken,
                        recoveryAmount,
                        warningPulseBeats,
                        crotchet,
                        startTime);
                }
                catch (Exception exception)
                {
                    Main.LogException(
                        "SetPlanetGauge 사전 경고 등록에 실패해 경고 없이 계속합니다.",
                        exception);
                }
            }
        }
    }

    [HarmonyPatch]
    internal static class PlanetGaugeParseEnumPatch
    {
        private static MethodBase TargetMethod()
        {
            return PlanetGaugeLevelEventRegistry.FindParseEnumMethod();
        }

        private static bool Prefix(string str, ref LevelEventType __result)
        {
            if (!Main.IsEnabled
                || !string.Equals(str, PlanetGaugeLevelEventRegistry.EventName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            __result = PlanetGaugeLevelEventRegistry.EventType;
            return false;
        }
    }

    [HarmonyPatch]
    internal static class PlanetGaugeLevelEventConstructorPatch
    {
        private static MethodBase TargetMethod()
        {
            return PlanetGaugeLevelEventRegistry.FindLevelEventConstructor();
        }

        private static void Prefix(LevelEventType __1, ref LevelEventInfo __2)
        {
            if (Main.IsEnabled
                && __1 == PlanetGaugeLevelEventRegistry.EventType
                && __2 == null)
            {
                __2 = PlanetGaugeLevelEventRegistry.EventInfo;
            }
        }
    }

    [HarmonyPatch]
    internal static class PlanetGaugeLevelEventDecodePatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(LevelEvent),
                nameof(LevelEvent.Decode),
                new[] { typeof(Dictionary<string, object>), typeof(string), typeof(bool) });
        }

        private static void Prefix(Dictionary<string, object> dict, ref string __1)
        {
            if (!Main.IsEnabled || dict == null)
            {
                return;
            }

            object eventType;
            if (dict.TryGetValue("eventType", out eventType)
                && string.Equals(
                    Convert.ToString(eventType),
                    PlanetGaugeLevelEventRegistry.EventName,
                    StringComparison.OrdinalIgnoreCase))
            {
                // Decode는 같은 문자열로 enum 파싱과 levelEventsInfo 조회를 수행한다.
                // 숫자 문자열은 undefined enum으로 파싱되며 등록한 단일 숫자 키와도 일치한다.
                __1 = PlanetGaugeLevelEventRegistry.NumericEventId.ToString();
            }
        }
    }

    [HarmonyPatch]
    internal static class PlanetGaugeLevelEventEncodePatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(LevelEvent), nameof(LevelEvent.Encode), new[] { typeof(bool) });
        }

        private static void Prefix(LevelEvent __instance)
        {
            if (!Main.IsEnabled
                || __instance == null
                || __instance.eventType != PlanetGaugeLevelEventRegistry.EventType)
            {
                return;
            }

            SanitizeStoredFloat(
                __instance,
                PlanetGaugeLevelEventRegistry.MultiplierPercentKey,
                100f,
                PlanetGaugeValueRules.SanitizeMultiplier);
            SanitizeStoredFloat(
                __instance,
                PlanetGaugeLevelEventRegistry.RecoveryAmountPercentKey,
                0f,
                PlanetGaugeValueRules.SanitizeRecoveryAmount);
            SanitizeStoredFloat(
                __instance,
                PlanetGaugeLevelEventRegistry.WarningOffsetAngleKey,
                0f,
                PlanetGaugeValueRules.SanitizeWarningOffsetAngle);
            SanitizeStoredFloat(
                __instance,
                PlanetGaugeLevelEventRegistry.WarningPulseBeatsKey,
                0.5f,
                PlanetGaugeValueRules.SanitizeWarningPulseBeats);
            SanitizeStoredFloat(
                __instance,
                PlanetGaugeLevelEventRegistry.RecoveryCapPercentKey,
                100f,
                PlanetGaugeValueRules.SanitizeRecoveryCap);
        }

        private static void Postfix(LevelEvent __instance, Dictionary<string, object> __result)
        {
            if (Main.IsEnabled
                && __instance != null
                && __instance.eventType == PlanetGaugeLevelEventRegistry.EventType
                && __result != null)
            {
                // 정의되지 않은 enum의 ToString()은 숫자를 반환하므로 사람이 읽을 수 있는 계약명으로 저장한다.
                __result["eventType"] = PlanetGaugeLevelEventRegistry.EventName;
            }
        }

        private static void SanitizeStoredFloat(
            LevelEvent levelEvent,
            string key,
            float fallback,
            Func<float, float> sanitize)
        {
            float value = levelEvent.Get<float>(key, fallback);
            levelEvent[key] = sanitize(value);
        }
    }

    /// <summary>
    /// Float 입력이 확정되는 순간 PlanetGauge 값만 보정한다.
    /// NaN/Infinity는 각 속성 기본값으로 복구하고 회복 상한의 0 이하는 0.1로 올린다.
    /// </summary>
    [HarmonyPatch(typeof(ADOFAI.PropertyInfo), nameof(ADOFAI.PropertyInfo.Validate), typeof(float))]
    internal static class PlanetGaugeFloatValidationPatch
    {
        private static void Prefix(float value, out float __state)
        {
            __state = value;
        }

        private static void Postfix(
            ADOFAI.PropertyInfo __instance,
            float __state,
            ref float __result)
        {
            if (!Main.IsEnabled
                || __instance == null
                || !ReferenceEquals(__instance.levelEventInfo, PlanetGaugeLevelEventRegistry.EventInfo))
            {
                return;
            }

            if (string.Equals(
                __instance.name,
                PlanetGaugeLevelEventRegistry.MultiplierPercentKey,
                StringComparison.Ordinal))
            {
                __result = PlanetGaugeValueRules.SanitizeMultiplier(__state);
            }
            else if (string.Equals(
                __instance.name,
                PlanetGaugeLevelEventRegistry.RecoveryAmountPercentKey,
                StringComparison.Ordinal))
            {
                __result = PlanetGaugeValueRules.SanitizeRecoveryAmount(__state);
            }
            else if (string.Equals(
                __instance.name,
                PlanetGaugeLevelEventRegistry.WarningOffsetAngleKey,
                StringComparison.Ordinal))
            {
                __result = PlanetGaugeValueRules.SanitizeWarningOffsetAngle(__state);
            }
            else if (string.Equals(
                __instance.name,
                PlanetGaugeLevelEventRegistry.WarningPulseBeatsKey,
                StringComparison.Ordinal))
            {
                __result = PlanetGaugeValueRules.SanitizeWarningPulseBeats(__state);
            }
            else if (string.Equals(
                __instance.name,
                PlanetGaugeLevelEventRegistry.RecoveryCapPercentKey,
                StringComparison.Ordinal))
            {
                __result = PlanetGaugeValueRules.SanitizeRecoveryCap(__state);
            }
        }
    }

    /// <summary>
    /// 사용자가 증폭 모드를 직접 선택하면 같은 이벤트의 증폭값 O/X를 O로 전환한다.
    /// 패널 초기 표시 중 SelectVar가 호출되는 경우는 실제 값이 바뀌지 않으므로 제외된다.
    /// </summary>
    [HarmonyPatch(typeof(PropertyControl_Toggle), nameof(PropertyControl_Toggle.SelectVar), typeof(string))]
    internal static class PlanetGaugeAmplificationSelectionPatch
    {
        private struct SelectionState
        {
            internal bool Track;
            internal LevelEvent LevelEvent;
            internal PlanetGaugeAttributeMode PreviousMode;
        }

        private static void Prefix(PropertyControl_Toggle __instance, ref SelectionState __state)
        {
            __state = default(SelectionState);
            if (!Main.IsEnabled
                || __instance == null
                || __instance.propertyInfo == null
                || !string.Equals(
                    __instance.propertyInfo.name,
                    PlanetGaugeLevelEventRegistry.AttributeModeKey,
                    StringComparison.Ordinal)
                || __instance.propertiesPanel == null
                || __instance.propertiesPanel.inspectorPanel == null)
            {
                return;
            }

            LevelEvent levelEvent = __instance.propertiesPanel.inspectorPanel.selectedEvent;
            if (levelEvent == null
                || levelEvent.eventType != PlanetGaugeLevelEventRegistry.EventType)
            {
                return;
            }

            __state.Track = true;
            __state.LevelEvent = levelEvent;
            __state.PreviousMode = levelEvent.Get<PlanetGaugeAttributeMode>(
                PlanetGaugeLevelEventRegistry.AttributeModeKey,
                PlanetGaugeAttributeMode.Normal);
        }

        private static void Postfix(
            PropertyControl_Toggle __instance,
            string var,
            SelectionState __state)
        {
            PlanetGaugeAttributeMode selectedMode;
            if (!__state.Track
                || __state.LevelEvent == null
                || !Enum.TryParse(var, out selectedMode)
                || selectedMode == __state.PreviousMode
                || !PlanetGaugeValueRules.IsAmplificationMode(selectedMode)
                || __state.LevelEvent.disabled == null)
            {
                return;
            }

            bool disabled;
            if (!__state.LevelEvent.disabled.TryGetValue(
                    PlanetGaugeLevelEventRegistry.MultiplierPercentKey,
                    out disabled)
                || !disabled
                || __instance.propertiesPanel == null)
            {
                return;
            }

            ADOFAI.Property multiplierProperty;
            if (__instance.propertiesPanel.properties.TryGetValue(
                    PlanetGaugeLevelEventRegistry.MultiplierPercentKey,
                    out multiplierProperty)
                && multiplierProperty != null
                && multiplierProperty.enabledButton != null)
            {
                // 바닐라 버튼 경로를 사용해 O/X 표시, Undo, 타일 갱신을 함께 처리한다.
                multiplierProperty.enabledButton.onClick.Invoke();
            }
        }
    }

    [HarmonyPatch(typeof(ADOStartup), nameof(ADOStartup.SetupLevelEventsInfo))]
    internal static class PlanetGaugeLevelEventSetupPatch
    {
        private static void Postfix()
        {
            if (!Main.IsEnabled)
            {
                return;
            }

            try
            {
                bool wasRegistered = PlanetGaugeLevelEventRegistry.IsRegistered;
                bool registered = PlanetGaugeLevelEventRegistry.TryRegister();
                if (!registered && Main.Logger != null)
                {
                    Main.Logger.Error(
                        "ADOStartup.SetupLevelEventsInfo 실행 후에도 레벨 이벤트 사전이 준비되지 않아 "
                        + "PlanetGauge 설정 이벤트를 등록하지 못했습니다.");
                }
                else if (registered && !wasRegistered && Main.Logger != null)
                {
                    Main.Logger.Log("SetupLevelEventsInfo 완료 후 PlanetGauge 설정 이벤트를 등록했습니다.");
                }
            }
            catch (Exception exception)
            {
                // 게임 시작 Postfix에서 예외를 전파하면 전체 초기화를 깨뜨릴 수 있으므로
                // 커스텀 이벤트 통합만 비활성화하고 기존 PlanetGauge 기능은 유지한다.
                Main.LogException("SetupLevelEventsInfo 이후 PlanetGauge 설정 이벤트 등록에 실패했습니다.", exception);
            }
        }
    }

    [HarmonyPatch(typeof(scnEditor), nameof(scnEditor.LoadEditorProperties))]
    internal static class PlanetGaugeEditorIconPatch
    {
        private static void Prefix()
        {
            if (Main.IsEnabled)
            {
                PlanetGaugeLevelEventRegistry.EnsureIcon();
            }
        }
    }

    /// <summary>
    /// ShowTabsForFloor의 바닐라 대체 선택은 Enum.GetValues에 있는 이벤트만 찾는다.
    /// 정의되지 않은 숫자 enum인 SetPlanetGauge만 남은 타일에서는 None을 선택해 패널 본문이 비므로,
    /// 원본 선택이 실패한 경우에만 실제 PlanetGauge 이벤트를 다시 선택한다.
    /// </summary>
    [HarmonyPatch(typeof(InspectorPanel), nameof(InspectorPanel.ShowTabsForFloor), typeof(int))]
    internal static class PlanetGaugeInspectorSelectionPatch
    {
        private static void Postfix(InspectorPanel __instance, int floorID)
        {
            if (!Main.IsEnabled
                || __instance == null
                || __instance.selectedEventType != LevelEventType.None)
            {
                return;
            }

            scnEditor editor = scnEditor.instance;
            if (editor == null)
            {
                return;
            }

            List<LevelEvent> events = editor.GetFloorEvents(
                floorID,
                PlanetGaugeLevelEventRegistry.EventType);
            if (events == null || events.Count == 0)
            {
                return;
            }

            __instance.ShowPanel(PlanetGaugeLevelEventRegistry.EventType, 0);
        }
    }

    [HarmonyPatch]
    internal static class PlanetGaugeLocalizationPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(RDString),
                nameof(RDString.GetWithCheck),
                new[] { typeof(string), typeof(bool).MakeByRefType(), typeof(Dictionary<string, object>) });
        }

        private static bool Prefix(string key, ref bool exists, ref string __result)
        {
            if (!Main.IsEnabled)
            {
                return true;
            }

            string localized;
            if (!TryGetLocalization(key, out localized))
            {
                return true;
            }

            exists = true;
            __result = localized;
            return false;
        }

        private static bool TryGetLocalization(string key, out string localized)
        {
            if (string.Equals(
                key,
                PlanetGaugeLevelEventRegistry.MultiplierReuseLocalizationKey,
                StringComparison.Ordinal))
            {
                localized = "X면 이 속성에 마지막으로 설정한 변경값을 사용합니다.";
                return true;
            }

            if (string.Equals(
                key,
                PlanetGaugeLevelEventRegistry.RecoveryAmountLocalizationKey,
                StringComparison.Ordinal))
            {
                localized = "음수로 설정할 시 체력을 깎습니다.\n최대 체력을 넘는 회복은 상쇄됩니다.";
                return true;
            }

            if (string.Equals(key, "editor." + PlanetGaugeLevelEventRegistry.NumericEventId, StringComparison.Ordinal)
                || string.Equals(key, "editor." + PlanetGaugeLevelEventRegistry.EventName, StringComparison.Ordinal))
            {
                localized = "PlanetGauge 설정";
                return true;
            }

            if (key != null && key.IndexOf(nameof(PlanetGaugeAttributeMode), StringComparison.Ordinal) >= 0)
            {
                if (key.EndsWith(".Normal", StringComparison.Ordinal))
                {
                    localized = "일반";
                    return true;
                }

                if (key.EndsWith(".BlockRecovery", StringComparison.Ordinal))
                {
                    localized = "회복 차단";
                    return true;
                }

                if (key.EndsWith(".AmplifyDecrease", StringComparison.Ordinal))
                {
                    localized = "감소율 변경";
                    return true;
                }

                if (key.EndsWith(".AmplifyIncrease", StringComparison.Ordinal))
                {
                    localized = "증가율 변경";
                    return true;
                }

                if (key.EndsWith(".AmplifyBoth", StringComparison.Ordinal))
                {
                    localized = "증가·감소율 변경";
                    return true;
                }

                if (key.EndsWith(".Blindfold", StringComparison.Ordinal))
                {
                    localized = "체력 표시 차단";
                    return true;
                }

                if (key.EndsWith(".ForceRecovery", StringComparison.Ordinal))
                {
                    localized = "체력 강제 회복";
                    return true;
                }
            }

            localized = null;
            return false;
        }
    }

    [HarmonyPatch]
    internal static class PlanetGaugeApplyEventPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(scnGame),
                nameof(scnGame.ApplyEvent),
                new[]
                {
                    typeof(LevelEvent),
                    typeof(float),
                    typeof(float),
                    typeof(List<scrFloor>),
                    typeof(float),
                    typeof(int?)
                });
        }

        private static void Postfix(
            LevelEvent evnt,
            float bpm,
            float pitch,
            List<scrFloor> floors,
            float offset,
            int? customFloorID,
            ref ffxPlusBase __result)
        {
            if (!Main.IsEnabled
                || evnt == null
                || evnt.eventType != PlanetGaugeLevelEventRegistry.EventType
                || __result != null)
            {
                return;
            }

            int floorId = customFloorID ?? evnt.floor;
            if (floors == null || floorId < 0 || floorId >= floors.Count || floors[floorId] == null)
            {
                throw new InvalidOperationException(
                    "SetPlanetGauge 이벤트의 대상 타일을 찾을 수 없습니다: " + floorId);
            }

            scrFloor floor = floors[floorId];
            PlanetGaugeLevelEventEffect effect = null;
            PlanetGaugeWarningLevelEventEffect warningEffect = null;
            bool effectAddedToFloor = false;
            bool warningAddedToFloor = false;
            float angleOffset = 0f;
            try
            {
                effect = floor.gameObject.AddComponent<PlanetGaugeLevelEventEffect>();
                effect.floorID = floorId;
                effect.floors = floors;
                effect.crotchet = 60f / (bpm * pitch * floor.speed);
                effect.Decode(evnt);
                effect.VisualToken = effect.GetInstanceID();
                floor.plusEffects.Add(effect);
                effectAddedToFloor = true;

                evnt.TryGet("angleOffset", out angleOffset);
                effect.SetStartTime(bpm, angleOffset + offset);
                effect.sourceLevelEvent = evnt;
                __result = effect;
            }
            catch
            {
                if (effectAddedToFloor)
                {
                    floor.plusEffects.Remove(effect);
                }

                if (effect != null)
                {
                    UnityEngine.Object.Destroy(effect);
                }

                throw;
            }

            PlanetGaugeAttributeMode mode = evnt.Get<PlanetGaugeAttributeMode>(
                PlanetGaugeLevelEventRegistry.AttributeModeKey,
                PlanetGaugeAttributeMode.Normal);
            float recoveryAmount = PlanetGaugeValueRules.SanitizeRecoveryAmount(
                evnt.Get<float>(PlanetGaugeLevelEventRegistry.RecoveryAmountPercentKey, 0f));
            float warningOffsetAngle = PlanetGaugeValueRules.SanitizeWarningOffsetAngle(
                evnt.Get<float>(PlanetGaugeLevelEventRegistry.WarningOffsetAngleKey, 0f));
            if (mode != PlanetGaugeAttributeMode.ForceRecovery
                || !IsPropertyEnabled(
                    evnt,
                    PlanetGaugeLevelEventRegistry.AttributeModeKey)
                || Mathf.Approximately(recoveryAmount, 0f)
                || warningOffsetAngle >= 0f)
            {
                return;
            }

            try
            {
                warningEffect = floor.gameObject.AddComponent<PlanetGaugeWarningLevelEventEffect>();
                warningEffect.floorID = floorId;
                warningEffect.floors = floors;
                warningEffect.crotchet = effect.crotchet;
                warningEffect.VisualToken = effect.VisualToken;
                warningEffect.Decode(evnt);
                floor.plusEffects.Add(warningEffect);
                warningAddedToFloor = true;
                warningEffect.SetStartTime(
                    bpm,
                    angleOffset + offset + warningOffsetAngle);
                warningEffect.sourceLevelEvent = evnt;
            }
            catch (Exception exception)
            {
                if (warningAddedToFloor)
                {
                    floor.plusEffects.Remove(warningEffect);
                }

                if (warningEffect != null)
                {
                    UnityEngine.Object.Destroy(warningEffect);
                }

                Main.LogException(
                    "SetPlanetGauge 사전 경고 효과를 만들지 못해 경고 없이 계속합니다.",
                    exception);
            }
        }

        private static bool IsPropertyEnabled(LevelEvent levelEvent, string propertyName)
        {
            bool disabled;
            return levelEvent.disabled == null
                || !levelEvent.disabled.TryGetValue(propertyName, out disabled)
                || !disabled;
        }
    }
}
