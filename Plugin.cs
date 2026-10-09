using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace MegaBonkUpgrader;

[BepInPlugin(Guid, Name, Version)]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "codex.megabonk.upgrader";
    public const string Name = "MegaBonk Upgrader";
    public const string Version = "1.0.0";

    internal static ManualLogSource ModLog { get; private set; }

    public override void Load()
    {
        ModLog = Log;

        UpgraderConfig.Bind(Config);

        ClassInjector.RegisterTypeInIl2Cpp<UpgraderBehaviour>();

        Harmony harmony = new Harmony(Guid);
        harmony.PatchAll();

        var host = new GameObject("MegaBonkUpgraderHost");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<UpgraderBehaviour>();

        ModLog.LogInfo($"{Name} v{Version} loaded. Hotkey: {UpgraderConfig.OpenKey.Value}. Upgrader registered in chest loot pool.");
    }
}
