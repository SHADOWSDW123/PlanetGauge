using System;
using System.Collections.Generic;
using System.Reflection;
using ADOFAI;
using HarmonyLib;
using UnityEngine;

namespace PlanetGauge
{
    internal sealed class PlanetGaugeConditionalLevelEventEffect : ffxPlusBase
    {
        private PlanetGaugeConditionalDirection direction;
        private float triggerHealth;
        private string triggerTag;

        public override void Decode(LevelEvent levelEvent)
        {
            direction = levelEvent.Get<PlanetGaugeConditionalDirection>(
                PlanetGaugeConditionalLevelEventRegistry.ConditionKey,
                PlanetGaugeConditionalDirection.BelowOrEqual);
            if (!Enum.IsDefined(typeof(PlanetGaugeConditionalDirection), direction))
            {
                direction = PlanetGaugeConditionalDirection.BelowOrEqual;
            }

            triggerHealth = PlanetGaugeConditionalLevelEventRegistry.SanitizeTriggerHealth(
                levelEvent.Get<float>(
                    PlanetGaugeConditionalLevelEventRegistry.TriggerHealthKey,
                    PlanetGaugeConditionalLevelEventRegistry.DefaultTriggerHealth));
            triggerTag = levelEvent.GetString(PlanetGaugeConditionalLevelEventRegistry.TriggerTagKey)
                ?? string.Empty;
        }

        public override void StartEffect(scrPlanet planet)
        {
            // 체크포인트 초기 ScrubToTime이나 일시정지 직후에도 조건 규칙 등록이 누락되지 않도록
            // allowPaused=true인 IsGameplayContext를 사용한다. 실제 태그 발동은 체력 연산 경로에서 게이트된다.
            if (GaugeRuntime.IsGameplayContext(true))
            {
                PlanetGaugeConditionalEventRuntime.ApplyRule(
                    direction,
                    triggerHealth,
                    triggerTag);
            }
        }
    }

    [HarmonyPatch]
    internal static class PlanetGaugeConditionalApplyEventPatch
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
                || evnt.eventType != PlanetGaugeConditionalLevelEventRegistry.EventType
                || __result != null)
            {
                return;
            }

            int floorId = customFloorID ?? evnt.floor;
            if (floors == null || floorId < 0 || floorId >= floors.Count || floors[floorId] == null)
            {
                throw new InvalidOperationException(
                    "SetPlanetGaugeConditionalEvents 이벤트의 대상 타일을 찾을 수 없습니다: " + floorId);
            }

            scrFloor floor = floors[floorId];
            float timingDenominator = bpm * pitch * floor.speed;
            if (float.IsNaN(timingDenominator)
                || float.IsInfinity(timingDenominator)
                || Mathf.Approximately(timingDenominator, 0f))
            {
                throw new InvalidOperationException(
                    "SetPlanetGaugeConditionalEvents 이벤트 시간이 유효하지 않습니다. bpm=" + bpm
                    + ", pitch=" + pitch + ", speed=" + floor.speed);
            }

            float angleOffset = 0f;
            evnt.TryGet("angleOffset", out angleOffset);
            if (float.IsNaN(angleOffset) || float.IsInfinity(angleOffset))
            {
                angleOffset = 0f;
            }

            PlanetGaugeConditionalLevelEventEffect effect = null;
            bool effectAddedToFloor = false;
            try
            {
                effect = floor.gameObject.AddComponent<PlanetGaugeConditionalLevelEventEffect>();
                effect.floorID = floorId;
                effect.floors = floors;
                effect.crotchet = 60f / timingDenominator;
                effect.Decode(evnt);
                floor.plusEffects.Add(effect);
                effectAddedToFloor = true;
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
        }
    }

    [HarmonyPatch]
    internal static class PlanetGaugeConditionalApplyEventsToFloorsPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(scnGame),
                nameof(scnGame.ApplyEventsToFloors),
                new[]
                {
                    typeof(List<scrFloor>),
                    typeof(LevelData),
                    typeof(scrLevelMaker),
                    typeof(List<LevelEvent>)
                });
        }

        private static void Postfix(List<scrFloor> floors, List<LevelEvent> events)
        {
            if (!Main.IsEnabled)
            {
                return;
            }

            try
            {
                PlanetGaugeConditionalEventRuntime.RebuildTagIndex(floors, events, false);
            }
            catch (Exception exception)
            {
                Main.LogException(
                    "PlanetGauge 조건부 이벤트 태그 인덱스 구성에 실패했습니다.",
                    exception);
            }
        }
    }

    [HarmonyPatch]
    internal static class PlanetGaugeConditionalPrepVfxPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(scnGame),
                nameof(scnGame.PrepVfx),
                new[]
                {
                    typeof(List<scrFloor>),
                    typeof(int),
                    typeof(List<LevelEvent>),
                    typeof(bool)
                });
        }

        private static void Postfix(List<scrFloor> floors, List<LevelEvent> events)
        {
            if (!Main.IsEnabled)
            {
                return;
            }

            try
            {
                PlanetGaugeConditionalEventRuntime.CapturePrepVfxEffects(floors, events);
            }
            catch (Exception exception)
            {
                Main.LogException(
                    "PlanetGauge 조건부 이벤트 PrepVfx 태그 재동기화에 실패했습니다.",
                    exception);
            }
        }
    }
}
