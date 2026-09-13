using System;

namespace PlanetGauge
{
    // AddHit는 결과 통계에도 쓰이므로 수동 SwitchChosen 호출 안에서만 관찰한다.
    // 실패 피해의 유일한 소유자는 Die다. 복구/통계의 실패 기록은 다시 차감하지 않는다.
    internal static class VanillaJudgementObservation
    {
        internal struct Token
        {
            internal int Depth;
            internal int Generation;
        }

        private struct Frame
        {
            internal scrMarginTracker Tracker;
            internal HitMargin? Judgement;
            internal bool FailureHandled;
        }

        private static Frame[] frames = new Frame[4];
        private static int depth;
        private static int generation;

        internal static Token Begin(scrMarginTracker tracker)
        {
            if (depth == frames.Length) Array.Resize(ref frames, frames.Length * 2);
            frames[depth++] = new Frame { Tracker = tracker };
            return new Token { Depth = depth, Generation = generation };
        }

        internal static void Record(scrMarginTracker tracker, HitMargin judgement)
        {
            if (depth == 0 || !ReferenceEquals(frames[depth - 1].Tracker, tracker)) return;
            frames[depth - 1].Judgement = judgement;
        }

        internal static void MarkFailureHandled()
        {
            if (depth > 0) frames[depth - 1].FailureHandled = true;
        }

        internal static HitMargin? End(ref Token token)
        {
            HitMargin? result = null;
            if (token.Depth > 0 && token.Generation == generation && token.Depth == depth)
            {
                Frame frame = frames[--depth];
                frames[depth] = default(Frame);
                if (!frame.FailureHandled && IsRegularJudgement(frame.Judgement))
                    result = frame.Judgement;
            }
            token = default(Token);
            return result;
        }

        private static bool IsRegularJudgement(HitMargin? judgement)
        {
            switch (judgement)
            {
                case HitMargin.TooEarly:
                case HitMargin.VeryEarly:
                case HitMargin.EarlyPerfect:
                case HitMargin.PerfectMinus:
                case HitMargin.XPerfect:
                case HitMargin.PerfectPlus:
                case HitMargin.LatePerfect:
                case HitMargin.VeryLate:
                    return true;
                default:
                    return false;
            }
        }

        internal static void Reset()
        {
            Array.Clear(frames, 0, frames.Length);
            depth = 0;
            unchecked { generation++; }
        }
    }
}
