using System;
using System.Collections.Generic;
using ADOFAI;
using UnityEngine;

namespace PlanetGauge
{
    internal enum PlanetGaugeSkinGaugeType
    {
        Horizontal = 0,
        Vertical = 1
    }

    /// <summary>
    /// 태그 장식을 PlanetGauge 체력 비율로 변형하는 장식 이벤트 계약이다.
    /// 실제 게이지 판정이나 기본 HUD에는 관여하지 않는다.
    /// </summary>
    internal static class PlanetGaugeSkinLevelEventRegistry
    {
        internal const int NumericEventId = 0x5048;
        internal const string EventName = "PlanetgaugeSkin";

        internal const string TargetTagKey = "targetTag";
        internal const string EnabledKey = "enabled";
        internal const string GaugeTypeKey = "gaugeType";

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
                && !GCS.levelEventIcons.TryGetValue(LevelEventType.MoveDecorations, out icon)
                && !GCS.levelEventIcons.TryGetValue(LevelEventType.AddDecoration, out icon))
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
                // 장식을 대상으로 삼는 타일 이벤트이지 AddDecoration 자체는 아니다.
                isDecoration = false,
                useGroups = false,
                stretchViewport = false,
                categories = new List<LevelEventCategory> { LevelEventCategory.DecorationFx },
                propertiesInfo = new Dictionary<string, ADOFAI.PropertyInfo>()
            };

            AddProperty(info, CreateProperty(
                info,
                TargetTagKey,
                "String",
                string.Empty,
                "목표 태그"), 0);

            AddProperty(info, CreateProperty(
                info,
                EnabledKey,
                "Bool",
                true,
                "기능 가동"), 1);

            AddProperty(info, CreateProperty(
                info,
                GaugeTypeKey,
                "Enum:" + typeof(PlanetGaugeSkinGaugeType).AssemblyQualifiedName,
                PlanetGaugeSkinGaugeType.Horizontal.ToString(),
                "게이지 타입"), 2);

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
