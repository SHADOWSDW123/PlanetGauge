// Game doubles model only the inspected routing contract; this is not an in-game test.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public enum HitMargin { TooEarly, VeryEarly, EarlyPerfect, PerfectMinus, XPerfect, PerfectPlus,
    LatePerfect, VeryLate, TooLate, Multipress, FailMiss, FailOverload, Auto, OverPress, Midspin }
public class scrMarginTracker
{
    [MethodImpl(MethodImplOptions.NoInlining)] public void AddHit(HitMargin hit) { }
}
public class scrFloor { public scrFloor nextfloor; public bool auto; }
public class PlanetarySystem { }
public class scrConductor { public static scrConductor instance = new scrConductor(); }
public class scrPlanet
{
    public scrPlayer player;
    public PlanetarySystem planetarySystem = new PlanetarySystem();
    public scrFloor currfloor = new scrFloor { nextfloor = new scrFloor() };
    public Action Body;
    public scrPlanet Result;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public scrPlanet SwitchChosen(long? tick) { Body?.Invoke(); return Result ?? this; }
}
public class scrPlayer
{
    public bool midspinInfiniteMargin;
    public scrMarginTracker marginTracker = new scrMarginTracker();
    public Action DieBody;
    public bool OriginalSawNoFail;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Die(bool overload = false, bool multipress = false, string failMessage = "", bool hitbox = false)
    {
        OriginalSawNoFail = scrController.instance.noFail;
        DieBody?.Invoke();
    }
}
public class Portal { }
public class scrController
{
    public static scrController instance = new scrController();
    public bool noFail, noFailInfiniteMargin;
    public void Restart() { }
    public void ResetCustomLevel() { }
    public void OnLandOnPortal(scrPlanet p, Portal portal, string s) { }
}
public class scnEditor { public void Play() { } public void SwitchToEditMode() { } }
public class scnGame { public void Play(int floor, bool b) { } }
public class scrVfxPlus { public void ScrubToTime(float t) { } }
public static class RDC { public static bool useOldAuto; }

namespace PlanetGauge
{
    public static class Main
    {
        public static bool IsEnabled = true, EditorGaugeEnabled = true;
        public static bool IsBorrowingNoFail => SwitchChosenPatch.IsBorrowingNoFail || PlayerDiePatch.IsBorrowingNoFail;
        public static void ResetSessionState() => SwitchChosenPatch.ResetSessionState();
    }
    public static class RuntimeHost { public static void ResetDebugVisibility() { } }
    public static class GaugeRuntime
    {
        public class Settings { public bool FailureProtection = true; }
        public static Settings EventSettings = new Settings();
        public static float Current = 100;
        public static int RecoveryDepth;
        public static bool Enabled = true, Auto, IsForcingDeath, DeathRequested;
        public static bool IsRecoveringFailure => RecoveryDepth > 0;
        public static readonly List<HitMargin> Charges = new List<HitMargin>();
        public static bool ShouldHandle(scrPlayer p = null) => Enabled;
        public static bool IsGameplayContext(bool paused) => Enabled;
        public static bool IsAutoPlay(scrPlayer p = null) => Auto;
        public static bool ApplyJudgement(HitMargin hit)
        {
            Charges.Add(hit);
            return DeathRequested;
        }
        public static void ForceDie(scrPlayer p)
        {
            IsForcingDeath = true;
            try { p.Die(); } finally { IsForcingDeath = false; }
        }
        public static void ApplyAutomaticRecovery() => Charges.Add(HitMargin.Auto);
        public static void BeginFailureRecovery() => RecoveryDepth++;
        public static void EndFailureRecovery() => RecoveryDepth--;
        public static void RevealBlindfold() { }
        public static void PrepareSessionStart(int f) { }
        public static void CancelSessionStartRestore() { }
        public static bool TryBeginInitialVfxScrub() => false;
        public static void EndInitialVfxScrub(bool b) { }
        public static void MarkLevelCompleted() { }
        public static void DisableBlindfoldForLevelCompletion() { }
    }
}
