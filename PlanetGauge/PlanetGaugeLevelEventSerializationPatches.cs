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
    [HarmonyPatch]
    internal static class PlanetGaugeParseEnumPatch
    {
        private static MethodBase TargetMethod()
        {
            return PlanetGaugeLevelEventRegistry.FindParseEnumMethod();
        }

        private static bool Prefix(string str, ref LevelEventType __result)
        {
            if (!Main.IsEnabled)
            {
                return true;
            }

            if (string.Equals(
                str,
                PlanetGaugeLevelEventRegistry.EventName,
                StringComparison.OrdinalIgnoreCase))
            {
                __result = PlanetGaugeLevelEventRegistry.EventType;
                return false;
            }

            if (string.Equals(
                str,
                PlanetGaugeSkinLevelEventRegistry.EventName,
                StringComparison.OrdinalIgnoreCase))
            {
                __result = PlanetGaugeSkinLevelEventRegistry.EventType;
                return false;
            }

            return true;
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
            if (!Main.IsEnabled || __2 != null)
            {
                return;
            }

            if (__1 == PlanetGaugeLevelEventRegistry.EventType)
            {
                __2 = PlanetGaugeLevelEventRegistry.EventInfo;
            }
            else if (__1 == PlanetGaugeSkinLevelEventRegistry.EventType)
            {
                __2 = PlanetGaugeSkinLevelEventRegistry.EventInfo;
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
            if (!dict.TryGetValue("eventType", out eventType))
            {
                return;
            }

            string eventName = Convert.ToString(eventType);
            if (string.Equals(
                eventName,
                PlanetGaugeLevelEventRegistry.EventName,
                StringComparison.OrdinalIgnoreCase))
            {
                // Decode는 같은 문자열로 enum 파싱과 levelEventsInfo 조회를 수행한다.
                // 숫자 문자열은 undefined enum으로 파싱되며 등록한 단일 숫자 키와도 일치한다.
                __1 = PlanetGaugeLevelEventRegistry.NumericEventId.ToString();
            }
            else if (string.Equals(
                eventName,
                PlanetGaugeSkinLevelEventRegistry.EventName,
                StringComparison.OrdinalIgnoreCase))
            {
                __1 = PlanetGaugeSkinLevelEventRegistry.NumericEventId.ToString();
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
            if (!Main.IsEnabled || __instance == null || __result == null)
            {
                return;
            }

            if (__instance.eventType == PlanetGaugeLevelEventRegistry.EventType)
            {
                // 정의되지 않은 enum의 ToString()은 숫자를 반환하므로 사람이 읽을 수 있는 계약명으로 저장한다.
                __result["eventType"] = PlanetGaugeLevelEventRegistry.EventName;
            }
            else if (__instance.eventType == PlanetGaugeSkinLevelEventRegistry.EventType)
            {
                __result["eventType"] = PlanetGaugeSkinLevelEventRegistry.EventName;
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
}
