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
                bool settingsWasRegistered = PlanetGaugeLevelEventRegistry.IsRegistered;
                bool skinWasRegistered = PlanetGaugeSkinLevelEventRegistry.IsRegistered;
                bool settingsRegistered = PlanetGaugeLevelEventRegistry.TryRegister();
                bool skinRegistered = PlanetGaugeSkinLevelEventRegistry.TryRegister();
                if ((!settingsRegistered || !skinRegistered) && Main.Logger != null)
                {
                    Main.Logger.Error(
                        "ADOStartup.SetupLevelEventsInfo 실행 후에도 레벨 이벤트 사전이 준비되지 않아 "
                        + "PlanetGauge 커스텀 이벤트를 등록하지 못했습니다.");
                }
                else if (Main.Logger != null
                    && (!settingsWasRegistered || !skinWasRegistered))
                {
                    Main.Logger.Log("SetupLevelEventsInfo 완료 후 PlanetGauge 커스텀 이벤트를 등록했습니다.");
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
        private static bool warningLogged;

        private static void Prefix()
        {
            if (Main.IsEnabled)
            {
                try
                {
                    PlanetGaugeLevelEventRegistry.EnsureIcon();
                    PlanetGaugeSkinLevelEventRegistry.EnsureIcon();
                }
                catch (Exception exception)
                {
                    // 아이콘은 선택 기능이다. 원본 에디터 초기화를 깨지 않고 네이티브 아이콘으로 저하한다.
                    if (!warningLogged)
                    {
                        warningLogged = true;
                        Main.LogException(
                            "PlanetGauge 이벤트 아이콘 등록에 실패해 기본 아이콘을 사용합니다.",
                            exception);
                    }
                }
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

            LevelEventType targetType = PlanetGaugeLevelEventRegistry.EventType;
            List<LevelEvent> events = editor.GetFloorEvents(floorID, targetType);
            if (events == null || events.Count == 0)
            {
                targetType = PlanetGaugeSkinLevelEventRegistry.EventType;
                events = editor.GetFloorEvents(floorID, targetType);
                if (events == null || events.Count == 0)
                {
                    return;
                }
            }

            __instance.ShowPanel(targetType, 0);
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

            if (string.Equals(key, "editor." + PlanetGaugeSkinLevelEventRegistry.NumericEventId, StringComparison.Ordinal)
                || string.Equals(key, "editor." + PlanetGaugeSkinLevelEventRegistry.EventName, StringComparison.Ordinal))
            {
                localized = "PlanetGauge 스킨";
                return true;
            }

            if (key != null && key.IndexOf(nameof(PlanetGaugeSkinGaugeType), StringComparison.Ordinal) >= 0)
            {
                if (key.EndsWith(".Horizontal", StringComparison.Ordinal))
                {
                    localized = "가로";
                    return true;
                }

                if (key.EndsWith(".Vertical", StringComparison.Ordinal))
                {
                    localized = "세로";
                    return true;
                }
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

                if (key.EndsWith(".HideGaugeHud", StringComparison.Ordinal))
                {
                    localized = "게이지 HUD 숨기기";
                    return true;
                }
            }

            localized = null;
            return false;
        }
    }

}
