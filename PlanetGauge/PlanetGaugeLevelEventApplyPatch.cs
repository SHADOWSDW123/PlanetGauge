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
