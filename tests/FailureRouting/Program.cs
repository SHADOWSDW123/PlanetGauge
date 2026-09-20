using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using PlanetGauge;

internal static class Program
{
    private static int checks;
    private static void Main(string[] args)
    {
        string managed = args.Length > 0 ? args[0] : @"E:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data\Managed";
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            string path = Path.Combine(managed, "UnityModManager", new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        Run();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        var harmony = new Harmony("PlanetGauge.FailureRouting.Tests");
        try
        {
            harmony.CreateClassProcessor(typeof(SwitchChosenPatch)).Patch();
            harmony.CreateClassProcessor(typeof(VanillaMarginRecordedPatch)).Patch();
            harmony.CreateClassProcessor(typeof(PlayerDiePatch)).Patch();

            var player = new scrPlayer();
            var planet = new scrPlanet { player = player };
            // No fail-bar API exists in the double: judgement must come exclusively from AddHit.
            planet.Body = () => player.marginTracker.AddHit(HitMargin.XPerfect);
            planet.SwitchChosen(null);
            Expect(GaugeRuntime.Charges.Count == 1 && GaugeRuntime.Charges[0] == HitMargin.XPerfect, "native perfect");
            Expect(!scrController.instance.noFail && !SwitchChosenPatch.IsBorrowingNoFail, "normal restore");

            GaugeRuntime.Charges.Clear();
            planet.Body = () => player.marginTracker.AddHit(HitMargin.FailOverload);
            for (int i = 0; i < 4; i++) planet.SwitchChosen(null);
            Expect(GaugeRuntime.Charges.Count == 0, "repeated failure display is not damage");
            player.Die(true);
            Expect(GaugeRuntime.Charges.Count == 1 && GaugeRuntime.Charges[0] == HitMargin.FailOverload, "actual overload charged once");
            Expect(player.OriginalSawNoFail && !scrController.instance.noFail, "overload uses native protection");
            player.Die(false);
            Expect(GaugeRuntime.Charges.Count == 2 && GaugeRuntime.Charges[1] == HitMargin.FailMiss, "later miss not consumed by stale token");

            GaugeRuntime.Charges.Clear();
            planet.Body = () => { player.Die(true, true); player.marginTracker.AddHit(HitMargin.VeryEarly); };
            planet.SwitchChosen(null);
            Expect(GaugeRuntime.Charges.Count == 1, "nested Die owns input failure");
            Expect(!scrController.instance.noFail && GaugeRuntime.RecoveryDepth == 0, "nested restore");

            GaugeRuntime.Charges.Clear();
            player.DieBody = () => player.marginTracker.AddHit(HitMargin.FailMiss);
            planet.Body = () => player.Die();
            planet.SwitchChosen(null);
            Expect(GaugeRuntime.Charges.Count == 1, "recovery record not charged twice");
            player.DieBody = null;

            GaugeRuntime.Charges.Clear();
            planet.Body = () => player.marginTracker.AddHit(HitMargin.TooLate);
            planet.SwitchChosen(null);
            Expect(GaugeRuntime.Charges.Count == 0, "TooLate waits for Die");

            planet.Body = () => { player.marginTracker.AddHit(HitMargin.VeryLate); throw new InvalidOperationException("switch"); };
            try { planet.SwitchChosen(null); } catch (InvalidOperationException e) { Expect(e.Message == "switch", "switch exception preserved"); }
            Expect(!scrController.instance.noFail && !SwitchChosenPatch.IsBorrowingNoFail && GaugeRuntime.Charges.Count == 0, "switch exception cleanup");

            player.DieBody = () => { throw new InvalidOperationException("die"); };
            planet.Body = () => player.Die();
            try { planet.SwitchChosen(null); } catch (InvalidOperationException e) { Expect(e.Message == "die", "die exception preserved"); }
            Expect(!scrController.instance.noFail && GaugeRuntime.RecoveryDepth == 0 && !SwitchChosenPatch.IsBorrowingNoFail, "die exception cleanup");
            player.DieBody = null;

            GaugeRuntime.DeathRequested = true;
            planet.Body = () => player.Die();
            planet.SwitchChosen(null);
            Expect(!player.OriginalSawNoFail && !scrController.instance.noFail, "exhaustion releases borrowed protection");
            GaugeRuntime.DeathRequested = false;

            GaugeRuntime.Charges.Clear();
            player.DieBody = () =>
            {
                player.DieBody = null;
                GaugeRuntime.ForceDie(player);
                Expect(!player.OriginalSawNoFail, "forced death inside recovery releases protection");
            };
            player.Die();
            Expect(GaugeRuntime.Charges.Count == 1 && GaugeRuntime.RecoveryDepth == 0
                && !PlayerDiePatch.IsBorrowingNoFail && !scrController.instance.noFail, "forced death recovery cleanup");

            scrController.instance.noFail = true;
            planet.SwitchChosen(null);
            Expect(player.OriginalSawNoFail && scrController.instance.noFail, "real no-fail preserved");
            scrController.instance.noFail = false;

            GaugeRuntime.Charges.Clear();
            GaugeRuntime.Auto = true;
            planet.Result = new scrPlanet();
            planet.Body = () => player.marginTracker.AddHit(HitMargin.Auto);
            planet.SwitchChosen(null);
            Expect(GaugeRuntime.Charges.Count == 1 && GaugeRuntime.Charges[0] == HitMargin.Auto,
                "actual autoplay always recovers");
            GaugeRuntime.Auto = false;

            GaugeRuntime.Charges.Clear();
            planet.currfloor.nextfloor.auto = true;
            planet.SwitchChosen(null);
            Expect(GaugeRuntime.Charges.Count == 1 && GaugeRuntime.Charges[0] == HitMargin.Auto,
                "auto tile always recovers");
            planet.currfloor.nextfloor.auto = false;

            var outer = VanillaJudgementObservation.Begin(player.marginTracker);
            VanillaJudgementObservation.Record(player.marginTracker, HitMargin.VeryEarly);
            var inner = VanillaJudgementObservation.Begin(player.marginTracker);
            VanillaJudgementObservation.Record(player.marginTracker, HitMargin.XPerfect);
            Expect(VanillaJudgementObservation.End(ref inner) == HitMargin.XPerfect, "inner observation");
            Expect(VanillaJudgementObservation.End(ref outer) == HitMargin.VeryEarly, "outer observation retained");
            var old = VanillaJudgementObservation.Begin(player.marginTracker);
            VanillaJudgementObservation.Reset();
            var fresh = VanillaJudgementObservation.Begin(player.marginTracker);
            VanillaJudgementObservation.Record(player.marginTracker, HitMargin.XPerfect);
            VanillaJudgementObservation.End(ref old);
            Expect(VanillaJudgementObservation.End(ref fresh) == HitMargin.XPerfect, "reset invalidates old finalizer");
            Expect(!VanillaJudgementObservation.End(ref fresh).HasValue, "idempotent observation cleanup");
            Console.WriteLine("PASS: " + checks + " routing checks (real Harmony, game/runtime doubles; no Unity gameplay).");
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static void Expect(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        checks++;
    }
}
