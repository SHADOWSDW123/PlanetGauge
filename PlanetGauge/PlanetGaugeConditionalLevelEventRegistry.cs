using System;
using System.Collections.Generic;
using ADOFAI;
using UnityEngine;

namespace PlanetGauge
{
    internal enum PlanetGaugeConditionalDirection
    {
        BelowOrEqual = 0,
        AboveOrEqual = 1
    }

    /// <summary>
    /// PlanetGauge 체력이 기준값을 지나갈 때 지정된 이벤트 태그를 발동시키는 조건부 이벤트 계약이다.
    /// 레벨 에디터의 이벤트 조절(FxModifiers) 탭에 등록된다.
    /// </summary>
    internal static class PlanetGaugeConditionalLevelEventRegistry
    {
        internal const int NumericEventId = 0x5049;
        internal const string EventName = "SetPlanetGaugeConditionalEvents";

        internal const string ConditionKey = "condition";
        internal const string TriggerHealthKey = "triggerHealth";
        internal const string TriggerTagKey = "triggerTag";
        internal const float DefaultTriggerHealth = 50f;

        internal static readonly LevelEventType EventType = (LevelEventType)NumericEventId;

        private static LevelEventInfo eventInfo;
        private static Sprite registeredIcon;

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

        internal static float SanitizeTriggerHealth(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return DefaultTriggerHealth;
            }

            return value < 0f ? 0f : value;
        }

        internal static void RefreshLocalization()
        {
            if (eventInfo == null || eventInfo.propertiesInfo == null)
            {
                return;
            }

            SetPropertyLabel(ConditionKey, LocalizedStrings.ConditionalDirectionLabel);
            SetPropertyLabel(TriggerHealthKey, LocalizedStrings.TriggerHealthLabel);
            SetPropertyLabel(TriggerTagKey, LocalizedStrings.TriggerTagLabel);
        }

        private static void SetPropertyLabel(string propertyName, string label)
        {
            ADOFAI.PropertyInfo property;
            if (eventInfo.propertiesInfo.TryGetValue(propertyName, out property) && property != null)
            {
                property.customLabel = label;
                if (property.dict != null)
                {
                    property.dict["customLabel"] = label;
                }
            }
        }

        internal static bool TryRegister()
        {
            if (GCS.levelEventsInfo == null || GCS.levelEventTypeString == null)
            {
                return false;
            }

            LevelEventInfo nativeInfo;
            string nativeName;
            if (!GCS.levelEventsInfo.TryGetValue(nameof(LevelEventType.SetSpeed), out nativeInfo)
                || nativeInfo == null
                || !GCS.levelEventTypeString.TryGetValue(LevelEventType.SetSpeed, out nativeName)
                || !string.Equals(nativeName, nameof(LevelEventType.SetSpeed), StringComparison.Ordinal))
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

            GCS.levelEventsInfo[NumericEventId.ToString()] = EventInfo;
            GCS.levelEventTypeString[EventType] = EventName;
            EnsureIcon();
            return true;
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
        }

        internal static void EnsureIcon()
        {
            if (GCS.levelEventIcons == null || GCS.levelEventIcons.ContainsKey(EventType))
            {
                return;
            }

            PlanetGaugeLevelEventRegistry.EnsureIcon();
            Sprite icon;
            if (!GCS.levelEventIcons.TryGetValue(PlanetGaugeLevelEventRegistry.EventType, out icon)
                && !GCS.levelEventIcons.TryGetValue(LevelEventType.SetConditionalEvents, out icon)
                && !GCS.levelEventIcons.TryGetValue(LevelEventType.RepeatEvents, out icon))
            {
                return;
            }

            registeredIcon = icon;
            GCS.levelEventIcons[EventType] = icon;
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
                categories = new List<LevelEventCategory> { LevelEventCategory.FxModifiers },
                propertiesInfo = new Dictionary<string, ADOFAI.PropertyInfo>()
            };

            ADOFAI.PropertyInfo condition = CreateProperty(
                info,
                ConditionKey,
                "Enum:" + typeof(PlanetGaugeConditionalDirection).AssemblyQualifiedName,
                PlanetGaugeConditionalDirection.BelowOrEqual.ToString(),
                LocalizedStrings.ConditionalDirectionLabel);
            AddProperty(info, condition, 0);

            ADOFAI.PropertyInfo triggerHealth = CreateProperty(
                info,
                TriggerHealthKey,
                "Float",
                DefaultTriggerHealth,
                LocalizedStrings.TriggerHealthLabel);
            triggerHealth.unit = "%";
            triggerHealth.float_min = 0f;
            triggerHealth.float_max = float.MaxValue;
            AddProperty(info, triggerHealth, 1);

            ADOFAI.PropertyInfo triggerTag = CreateProperty(
                info,
                TriggerTagKey,
                "String",
                string.Empty,
                LocalizedStrings.TriggerTagLabel);
            AddProperty(info, triggerTag, 2);

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

        private static void AddProperty(LevelEventInfo info, ADOFAI.PropertyInfo property, int order)
        {
            property.order = order;
            info.propertiesInfo.Add(property.name, property);
        }
    }
}
