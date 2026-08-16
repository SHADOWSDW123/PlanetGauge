using System;
using System.Collections.Generic;
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
        AmplifyBoth
    }

    internal struct PlanetGaugeEventSettings
    {
        internal static readonly PlanetGaugeEventSettings Default = new PlanetGaugeEventSettings(
            PlanetGaugeAttributeMode.Normal,
            100f,
            true,
            false,
            100f);

        internal PlanetGaugeEventSettings(
            PlanetGaugeAttributeMode attributeMode,
            float multiplierPercent,
            bool failureProtection,
            bool recoveryCapEnabled,
            float recoveryCapPercent)
        {
            AttributeMode = attributeMode;
            MultiplierPercent = multiplierPercent;
            FailureProtection = failureProtection;
            RecoveryCapEnabled = recoveryCapEnabled;
            RecoveryCapPercent = recoveryCapPercent;
        }

        internal PlanetGaugeAttributeMode AttributeMode { get; }

        internal float MultiplierPercent { get; }

        internal bool FailureProtection { get; }

        internal bool RecoveryCapEnabled { get; }

        internal float RecoveryCapPercent { get; }

    }

    /// <summary>
    /// 이벤트 하나가 현재 런타임 설정 중 어떤 값만 바꿀지 나타낸다.
    /// PropertyInfo의 O/X 상태는 LevelEvent.disabled에 저장되며 여기서 한 번만 해석한다.
    /// </summary>
    internal struct PlanetGaugeEventCommand
    {
        internal bool ApplyAttributeMode;
        internal PlanetGaugeAttributeMode AttributeMode;
        internal bool ApplyMultiplier;
        internal float MultiplierPercent;
        internal bool ApplyFailureProtection;
        internal bool FailureProtection;
        internal bool ApplyRecoveryCap;
        internal bool RecoveryCapEnabled;
        internal float RecoveryCapPercent;
        internal bool ForceRecoveryCap;
    }

    internal static class PlanetGaugeValueRules
    {
        internal static float SanitizeMultiplier(float value)
        {
            return SanitizePercent(value, 100f, 0f, 1000f);
        }

        internal static float SanitizeRecoveryCap(float value)
        {
            return SanitizePercent(value, 100f, 0.1f, 100f);
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
        internal const string MultiplierPercentKey = "multiplierPercent";
        internal const string FailureProtectionKey = "failureProtection";
        internal const string RecoveryCapEnabledKey = "recoveryCapEnabled";
        internal const string RecoveryCapPercentKey = "recoveryCapPercent";
        internal const string ForceRecoveryCapKey = "forceRecoveryCap";
        internal const string MultiplierReuseNoteKey = "multiplierReuseNote";
        internal const string MultiplierReuseLocalizationKey = "planetGauge.multiplierReuseNote";

        internal static readonly LevelEventType EventType = (LevelEventType)NumericEventId;

        private static LevelEventInfo eventInfo;
        private static Sprite borrowedIcon;

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

            if (GCS.levelEventIcons != null && borrowedIcon != null)
            {
                Sprite registeredIcon;
                if (GCS.levelEventIcons.TryGetValue(EventType, out registeredIcon)
                    && ReferenceEquals(registeredIcon, borrowedIcon))
                {
                    GCS.levelEventIcons.Remove(EventType);
                }

                borrowedIcon = null;
            }
        }

        internal static void EnsureIcon()
        {
            if (GCS.levelEventIcons == null || GCS.levelEventIcons.ContainsKey(EventType))
            {
                return;
            }

            Sprite icon;
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
                // 전용 에셋을 추가하기 전까지 게임의 설정 이벤트 아이콘을 빌린다.
                borrowedIcon = icon;
                GCS.levelEventIcons[EventType] = icon;
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

            ADOFAI.PropertyInfo multiplier = CreateProperty(
                info,
                MultiplierPercentKey,
                "Float",
                100f,
                "증폭값 설정");
            multiplier.unit = "%";
            multiplier.float_min = 0f;
            multiplier.float_max = 1000f;
            multiplier.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyDecrease.ToString()));
            multiplier.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyIncrease.ToString()));
            multiplier.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyBoth.ToString()));
            MakeOptional(multiplier, false);
            AddProperty(info, multiplier, 1);

            ADOFAI.PropertyInfo multiplierNote = CreateNoteProperty(
                info,
                MultiplierReuseNoteKey,
                MultiplierReuseLocalizationKey);
            multiplierNote.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyDecrease.ToString()));
            multiplierNote.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyIncrease.ToString()));
            multiplierNote.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyBoth.ToString()));
            AddProperty(info, multiplierNote, 2);

            ADOFAI.PropertyInfo failureProtection = CreateProperty(
                info,
                FailureProtectionKey,
                "Bool",
                true,
                "실패 방지");
            MakeOptional(failureProtection, false);
            AddProperty(info, failureProtection, 3);

            ADOFAI.PropertyInfo recoveryCapEnabled = CreateProperty(
                info,
                RecoveryCapEnabledKey,
                "Bool",
                false,
                "회복 상한 제한");
            MakeOptional(recoveryCapEnabled, false);
            AddProperty(info, recoveryCapEnabled, 4);

            ADOFAI.PropertyInfo recoveryCap = CreateProperty(
                info,
                RecoveryCapPercentKey,
                "Float",
                100f,
                "회복 상한");
            recoveryCap.unit = "%";
            recoveryCap.float_min = 0.1f;
            recoveryCap.float_max = 100f;
            recoveryCap.showIfVals.Add(Tuple.Create(RecoveryCapEnabledKey, bool.TrueString));
            AddProperty(info, recoveryCap, 5);

            ADOFAI.PropertyInfo forceCap = CreateProperty(
                info,
                ForceRecoveryCapKey,
                "Bool",
                true,
                "체력 상한 강제 제한");
            MakeOptional(forceCap, false);
            AddProperty(info, forceCap, 6);

            return info;
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

            command = new PlanetGaugeEventCommand
            {
                ApplyAttributeMode = IsPropertyEnabled(
                    levelEvent,
                    PlanetGaugeLevelEventRegistry.AttributeModeKey),
                AttributeMode = mode,
                ApplyMultiplier = IsPropertyEnabled(
                    levelEvent,
                    PlanetGaugeLevelEventRegistry.MultiplierPercentKey),
                MultiplierPercent = multiplier,
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
                    PlanetGaugeLevelEventRegistry.ForceRecoveryCapKey)
                    && forceRecoveryCap
            };
        }

        public override void StartEffect(scrPlanet planet)
        {
            if (GaugeRuntime.ShouldHandle())
            {
                GaugeRuntime.ApplyEventSettings(command);
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
                PlanetGaugeValueRules.SanitizeMultiplier);
            SanitizeStoredFloat(
                __instance,
                PlanetGaugeLevelEventRegistry.RecoveryCapPercentKey,
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
            Func<float, float> sanitize)
        {
            float value = levelEvent.Get<float>(key, 100f);
            levelEvent[key] = sanitize(value);
        }
    }

    /// <summary>
    /// Float 입력이 확정되는 순간 PlanetGauge 값만 보정한다.
    /// NaN/Infinity는 100으로 복구하고 회복 상한의 0 이하는 0.1로 올린다.
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
                localized = "증폭값 설정이 X이면 이전에 설정된 증폭값을 사용합니다.";
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
                    localized = "감소율 증폭";
                    return true;
                }

                if (key.EndsWith(".AmplifyIncrease", StringComparison.Ordinal))
                {
                    localized = "증가율 증폭";
                    return true;
                }

                if (key.EndsWith(".AmplifyBoth", StringComparison.Ordinal))
                {
                    localized = "증가·감소율 증폭";
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
            bool addedToFloor = false;
            try
            {
                effect = floor.gameObject.AddComponent<PlanetGaugeLevelEventEffect>();
                effect.floorID = floorId;
                effect.floors = floors;
                effect.crotchet = 60f / (bpm * pitch * floor.speed);
                effect.Decode(evnt);
                floor.plusEffects.Add(effect);
                addedToFloor = true;

                float angleOffset;
                evnt.TryGet("angleOffset", out angleOffset);
                effect.SetStartTime(bpm, angleOffset + offset);
                effect.sourceLevelEvent = evnt;
                __result = effect;
            }
            catch
            {
                if (addedToFloor)
                {
                    floor.plusEffects.Remove(effect);
                }

                if (effect != null)
                {
                    UnityEngine.Object.Destroy(effect);
                }

                throw;
            }
        }
    }
}
