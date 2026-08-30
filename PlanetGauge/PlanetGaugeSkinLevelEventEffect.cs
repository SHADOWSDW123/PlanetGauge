using System;
using System.Collections.Generic;
using System.Reflection;
using ADOFAI;
using HarmonyLib;
using UnityEngine;

namespace PlanetGauge
{
    internal sealed class PlanetGaugeSkinLevelEventEffect : ffxPlusBase
    {
        private string targetTag;
        private bool featureEnabled;
        private PlanetGaugeSkinGaugeType gaugeType;

        public override void Decode(LevelEvent levelEvent)
        {
            targetTag = levelEvent.GetString(PlanetGaugeSkinLevelEventRegistry.TargetTagKey)
                ?? string.Empty;
            featureEnabled = levelEvent.Get<bool>(
                PlanetGaugeSkinLevelEventRegistry.EnabledKey,
                true);
            gaugeType = levelEvent.Get<PlanetGaugeSkinGaugeType>(
                PlanetGaugeSkinLevelEventRegistry.GaugeTypeKey,
                PlanetGaugeSkinGaugeType.Horizontal);
            if (!Enum.IsDefined(typeof(PlanetGaugeSkinGaugeType), gaugeType))
            {
                gaugeType = PlanetGaugeSkinGaugeType.Horizontal;
            }
        }

        public override void StartEffect(scrPlanet planet)
        {
            if (GaugeRuntime.ShouldHandle())
            {
                PlanetGaugeDecorationSkinRuntime.ApplyCommand(targetTag, featureEnabled, gaugeType);
            }
        }
    }

    [HarmonyPatch]
    internal static class PlanetGaugeSkinApplyEventPatch
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
                || evnt.eventType != PlanetGaugeSkinLevelEventRegistry.EventType
                || __result != null)
            {
                return;
            }

            int floorId = customFloorID ?? evnt.floor;
            if (floors == null || floorId < 0 || floorId >= floors.Count || floors[floorId] == null)
            {
                throw new InvalidOperationException(
                    "PlanetgaugeSkin 이벤트의 대상 타일을 찾을 수 없습니다: " + floorId);
            }

            scrFloor floor = floors[floorId];
            float timingDenominator = bpm * pitch * floor.speed;
            if (float.IsNaN(timingDenominator)
                || float.IsInfinity(timingDenominator)
                || Mathf.Approximately(timingDenominator, 0f))
            {
                throw new InvalidOperationException(
                    "PlanetgaugeSkin 이벤트 시간이 유효하지 않습니다. bpm=" + bpm
                    + ", pitch=" + pitch + ", speed=" + floor.speed);
            }

            float angleOffset = 0f;
            evnt.TryGet("angleOffset", out angleOffset);
            if (float.IsNaN(angleOffset) || float.IsInfinity(angleOffset))
            {
                angleOffset = 0f;
            }

            PlanetGaugeSkinLevelEventEffect effect = null;
            bool effectAddedToFloor = false;
            try
            {
                effect = floor.gameObject.AddComponent<PlanetGaugeSkinLevelEventEffect>();
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
}
