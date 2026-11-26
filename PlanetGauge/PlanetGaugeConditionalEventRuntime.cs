using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ADOFAI;
using UnityEngine;

namespace PlanetGauge
{
    internal readonly struct PlanetGaugeConditionalRule
    {
        internal PlanetGaugeConditionalRule(
            PlanetGaugeConditionalDirection direction,
            float threshold,
            string[] tags)
        {
            Direction = direction;
            Threshold = threshold;
            Tags = tags ?? Array.Empty<string>();
        }

        internal PlanetGaugeConditionalDirection Direction { get; }
        internal float Threshold { get; }
        internal string[] Tags { get; }
    }

    /// <summary>
    /// PlanetGauge 조건부 이벤트의 태그 인덱스와 다중 활성 조건 규칙 테이블을 관리하고,
    /// 체력 연산 중 기준 체력을 지나갈 때 해당 태그 이벤트를 결정적 순서로 발동시킨다.
    /// </summary>
    internal static class PlanetGaugeConditionalEventRuntime
    {
        private const int MaxTriggerDepth = 4;
        private static readonly char[] TagSeparators = { ' ' };

        private static readonly HashSet<string> referencedTags =
            new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<ffxPlusBase>> tagEffects =
            new Dictionary<string, List<ffxPlusBase>>(StringComparer.Ordinal);
        private static readonly List<PlanetGaugeConditionalRule> activeRules =
            new List<PlanetGaugeConditionalRule>();

        private static int triggerDepth;
        private static string lastFiredTag;
        private static int totalFireCount;

        internal static Action<string> DiagnosticTagFired { get; set; }

        internal static bool HasActiveRule { get { return activeRules.Count > 0; } }
        internal static int ActiveRuleCount { get { return activeRules.Count; } }
        internal static int IndexedTagCount { get { return tagEffects.Count; } }
        internal static int IndexedEffectCount
        {
            get
            {
                int total = 0;
                foreach (KeyValuePair<string, List<ffxPlusBase>> pair in tagEffects)
                {
                    if (pair.Value != null)
                    {
                        total += pair.Value.Count;
                    }
                }

                return total;
            }
        }
        internal static int TotalFireCount { get { return totalFireCount; } }
        internal static string LastFiredTag { get { return string.IsNullOrEmpty(lastFiredTag) ? "None" : lastFiredTag; } }

        internal static string DescribeActiveRules()
        {
            if (activeRules.Count == 0)
            {
                return "None";
            }

            const int maxDisplayed = 6;
            int count = activeRules.Count;
            int limit = count < maxDisplayed ? count : maxDisplayed;
            StringBuilder builder = new StringBuilder(limit * 24);
            for (int index = 0; index < limit; index++)
            {
                if (index > 0)
                {
                    builder.Append(", ");
                }

                PlanetGaugeConditionalRule rule = activeRules[index];
                builder.Append(rule.Direction == PlanetGaugeConditionalDirection.BelowOrEqual ? "<=" : ">=")
                    .Append(rule.Threshold.ToString("0.###", CultureInfo.InvariantCulture))
                    .Append("%->")
                    .Append(string.Join(" ", rule.Tags));
            }

            if (count > maxDisplayed)
            {
                builder.Append(" (+").Append(count - maxDisplayed).Append(')');
            }

            return builder.ToString();
        }

        internal static PlanetGaugeConditionalRule[] GetActiveRulesSnapshot()
        {
            return activeRules.Count == 0
                ? Array.Empty<PlanetGaugeConditionalRule>()
                : activeRules.ToArray();
        }

        internal static bool IsNoneTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return true;
            }

            if (string.Equals(tag, "NONE", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                return RDUtils.IsNoneConditionalTag(tag);
            }
            catch
            {
                return false;
            }
        }

        internal static string[] ParseTags(string rawTag)
        {
            if (string.IsNullOrWhiteSpace(rawTag))
            {
                return Array.Empty<string>();
            }

            string[] tokens = rawTag.Split(TagSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                return Array.Empty<string>();
            }

            List<string> result = new List<string>(tokens.Length);
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < tokens.Length; index++)
            {
                string token = tokens[index];
                if (!IsNoneTag(token) && seen.Add(token))
                {
                    result.Add(token);
                }
            }

            return result.Count == 0 ? Array.Empty<string>() : result.ToArray();
        }

        internal static bool DidCrossThreshold(
            float previous,
            float current,
            PlanetGaugeConditionalDirection direction,
            float threshold)
        {
            if (float.IsNaN(previous)
                || float.IsInfinity(previous)
                || float.IsNaN(current)
                || float.IsInfinity(current))
            {
                return false;
            }

            float target = PlanetGaugeConditionalLevelEventRegistry.SanitizeTriggerHealth(threshold);
            switch (direction)
            {
                case PlanetGaugeConditionalDirection.BelowOrEqual:
                    return previous > target && current <= target;
                case PlanetGaugeConditionalDirection.AboveOrEqual:
                    return previous < target && current >= target;
                default:
                    return false;
            }
        }

        internal static void ResetSession()
        {
            activeRules.Clear();
            triggerDepth = 0;
            lastFiredTag = null;
            totalFireCount = 0;
        }

        internal static void ClearAll()
        {
            ResetSession();
            referencedTags.Clear();
            tagEffects.Clear();
        }

        internal static void ApplyRule(
            PlanetGaugeConditionalDirection direction,
            float triggerHealth,
            string rawTag)
        {
            PlanetGaugeConditionalDirection sanitizedDirection =
                Enum.IsDefined(typeof(PlanetGaugeConditionalDirection), direction)
                    ? direction
                    : PlanetGaugeConditionalDirection.BelowOrEqual;
            float sanitizedThreshold =
                PlanetGaugeConditionalLevelEventRegistry.SanitizeTriggerHealth(triggerHealth);
            string[] parsed = ParseTags(rawTag);

            int existingIndex = FindRuleIndex(sanitizedDirection, sanitizedThreshold);
            if (parsed.Length == 0)
            {
                if (existingIndex >= 0)
                {
                    activeRules.RemoveAt(existingIndex);
                }

                return;
            }

            PlanetGaugeConditionalRule nextRule = new PlanetGaugeConditionalRule(
                sanitizedDirection,
                sanitizedThreshold,
                parsed);
            if (existingIndex >= 0)
            {
                activeRules[existingIndex] = nextRule;
            }
            else
            {
                activeRules.Add(nextRule);
            }
        }

        internal static void RebuildTagIndex(
            List<scrFloor> floors,
            List<LevelEvent> events,
            bool removeFromScheduledVfx = false)
        {
            referencedTags.Clear();
            tagEffects.Clear();

            if (events == null || events.Count == 0)
            {
                return;
            }

            for (int index = 0; index < events.Count; index++)
            {
                LevelEvent levelEvent = events[index];
                if (levelEvent == null
                    || !levelEvent.active
                    || levelEvent.eventType != PlanetGaugeConditionalLevelEventRegistry.EventType)
                {
                    continue;
                }

                string rawTag = levelEvent.GetString(PlanetGaugeConditionalLevelEventRegistry.TriggerTagKey);
                string[] tags = ParseTags(rawTag);
                for (int tagIndex = 0; tagIndex < tags.Length; tagIndex++)
                {
                    referencedTags.Add(tags[tagIndex]);
                }
            }

            if (referencedTags.Count == 0 || floors == null)
            {
                return;
            }

            RegisterFloorEffects(floors, removeFromScheduledVfx);
        }

        internal static void CapturePrepVfxEffects(List<scrFloor> floors, List<LevelEvent> events)
        {
            // PrepVfx는 재생/재시작/체크포인트마다 새 ffxPlusBase(예: ffxCustomBackgroundPlus)를 만들거나
            // 타일을 갱신할 수 있으므로, 기존 인덱스를 누적하지 않고 현재 살아있는 floors/events 기준으로 재동기화한다.
            RebuildTagIndex(floors, events, true);
        }

        internal static void OnGaugeChanged(float previous, float current, bool isInitialVfxScrub)
        {
            if (activeRules.Count == 0
                || isInitialVfxScrub
                || triggerDepth >= MaxTriggerDepth
                || float.IsNaN(previous)
                || float.IsInfinity(previous)
                || float.IsNaN(current)
                || float.IsInfinity(current)
                || Mathf.Approximately(previous, current))
            {
                return;
            }

            List<PlanetGaugeConditionalRule> crossed = null;
            int ruleCount = activeRules.Count;
            for (int index = 0; index < ruleCount; index++)
            {
                PlanetGaugeConditionalRule rule = activeRules[index];
                if (!DidCrossThreshold(previous, current, rule.Direction, rule.Threshold))
                {
                    continue;
                }

                if (crossed == null)
                {
                    crossed = new List<PlanetGaugeConditionalRule>(2);
                }

                crossed.Add(rule);
            }

            if (crossed == null || crossed.Count == 0)
            {
                return;
            }

            if (crossed.Count > 1)
            {
                bool descending = current < previous;
                crossed.Sort((left, right) => descending
                    ? right.Threshold.CompareTo(left.Threshold)
                    : left.Threshold.CompareTo(right.Threshold));
            }

            triggerDepth++;
            try
            {
                for (int index = 0; index < crossed.Count; index++)
                {
                    FireTags(crossed[index].Tags);
                }
            }
            finally
            {
                triggerDepth--;
            }
        }

        private static int FindRuleIndex(PlanetGaugeConditionalDirection direction, float threshold)
        {
            for (int index = 0; index < activeRules.Count; index++)
            {
                PlanetGaugeConditionalRule rule = activeRules[index];
                if (rule.Direction == direction && Mathf.Approximately(rule.Threshold, threshold))
                {
                    return index;
                }
            }

            return -1;
        }

        private static void RegisterFloorEffects(List<scrFloor> floors, bool removeFromScheduledVfx)
        {
            List<ffxPlusBase> scheduledEffects = null;
            if (removeFromScheduledVfx
                && scrVfxPlus.instance != null
                && scrVfxPlus.instance.effects != null)
            {
                scheduledEffects = scrVfxPlus.instance.effects;
            }

            for (int floorIndex = 0; floorIndex < floors.Count; floorIndex++)
            {
                scrFloor floor = floors[floorIndex];
                if (floor == null || floor.plusEffects == null)
                {
                    continue;
                }

                for (int effectIndex = 0; effectIndex < floor.plusEffects.Count; effectIndex++)
                {
                    ffxPlusBase effect = floor.plusEffects[effectIndex];
                    if (effect == null
                        || effect.sourceLevelEvent == null
                        || !effect.sourceLevelEvent.active)
                    {
                        continue;
                    }

                    string rawEventTag;
                    if (!effect.sourceLevelEvent.TryGet("eventTag", out rawEventTag)
                        || string.IsNullOrWhiteSpace(rawEventTag))
                    {
                        continue;
                    }

                    string[] eventTags = rawEventTag.Split(
                        TagSeparators,
                        StringSplitOptions.RemoveEmptyEntries);
                    bool matched = false;
                    for (int tagIndex = 0; tagIndex < eventTags.Length; tagIndex++)
                    {
                        string token = eventTags[tagIndex];
                        if (!referencedTags.Contains(token))
                        {
                            continue;
                        }

                        matched = true;
                        List<ffxPlusBase> list;
                        if (!tagEffects.TryGetValue(token, out list))
                        {
                            list = new List<ffxPlusBase>();
                            tagEffects[token] = list;
                        }

                        if (!list.Contains(effect))
                        {
                            list.Add(effect);
                        }
                    }

                    if (matched)
                    {
                        effect.runManually = true;
                        if (scheduledEffects != null)
                        {
                            scheduledEffects.Remove(effect);
                        }
                    }
                }
            }
        }

        private static void FireTags(string[] tags)
        {
            if (tags == null || tags.Length == 0)
            {
                return;
            }

            scrPlanet planet = null;
            bool planetResolved = false;

            for (int tagIndex = 0; tagIndex < tags.Length; tagIndex++)
            {
                string tag = tags[tagIndex];
                lastFiredTag = tag;
                totalFireCount++;
                if (DiagnosticTagFired != null)
                {
                    DiagnosticTagFired(tag);
                }

                List<ffxPlusBase> effects;
                if (!tagEffects.TryGetValue(tag, out effects) || effects == null || effects.Count == 0)
                {
                    continue;
                }

                if (!planetResolved)
                {
                    scrController controller = scrController.instance;
                    planet = controller == null ? null : controller.chosenPlanet;
                    planetResolved = true;
                }

                int count = effects.Count;
                for (int index = 0; index < count; index++)
                {
                    ffxPlusBase effect = effects[index];
                    if (effect == null)
                    {
                        continue;
                    }

                    try
                    {
                        if (!float.IsNaN(effect.degreeOffset)
                            && !float.IsInfinity(effect.degreeOffset)
                            && effect.degreeOffset > 0f)
                        {
                            effect.StartEffectWithOffset(planet);
                        }
                        else
                        {
                            effect.StartEffect(planet);
                        }
                    }
                    catch (Exception exception)
                    {
                        Main.LogException(
                            "PlanetGauge 조건부 이벤트 태그 실행에 실패했습니다: " + tag,
                            exception);
                    }
                }
            }
        }
    }
}
