﻿using System;
using HarmonyLib;
using MultiplayerMod.Core.Patch;
using MultiplayerMod.Multiplayer.Objects;
using MultiplayerMod.Multiplayer.Objects.Reference;

namespace MultiplayerMod.Game.UI.SideScreens;

[HarmonyPatch(typeof(TimerSideScreen))]
public static class TimerSideScreenEvents {

    public static event Action<TimerSideScreenEventArgs>? UpdateLogicTimeSensor;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(TimerSideScreen.ChangeSetting))]
    // ReSharper disable once InconsistentNaming, UnusedMember.Local
    private static void ChangeSetting(TimerSideScreen __instance) => TriggerEvent(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(TimerSideScreen.ToggleMode))]
    // ReSharper disable once InconsistentNaming, UnusedMember.Local
    private static void UpdateMaxCapacity(TimerSideScreen __instance) => TriggerEvent(__instance);

    private static void TriggerEvent(TimerSideScreen instance) => PatchControl.RunIfEnabled(
        () => UpdateLogicTimeSensor?.Invoke(
            new TimerSideScreenEventArgs(
                instance.targetTimedSwitch.GetReference(),
                instance.targetTimedSwitch.displayCyclesMode,
                instance.targetTimedSwitch.onDuration,
                instance.targetTimedSwitch.offDuration,
                instance.targetTimedSwitch.timeElapsedInCurrentState
            )
        )
    );

    [Serializable]
    public record TimerSideScreenEventArgs(
        ComponentReference<LogicTimerSensor> Target,
        bool DisplayCyclesMode,
        float OnDuration,
        float OffDuration,
        float TimeElapsedInCurrentState
    );

}
