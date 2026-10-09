using Assets.Scripts.Inventory__Items__Pickups.Items;
using BepInEx.Configuration;
using UnityEngine;

namespace MegaBonkUpgrader;

public static class UpgraderConfig
{
    public static ConfigEntry<KeyCode> OpenKey { get; private set; }
    public static ConfigEntry<string> ItemName { get; private set; }
    public static ConfigEntry<string> ItemDescription { get; private set; }
    public static ConfigEntry<EItemRarity> ItemRarity { get; private set; }
    public static ConfigEntry<int> MaxPerRun { get; private set; }
    public static ConfigEntry<float> MinimumChance { get; private set; }
    public static ConfigEntry<float> MaximumChance { get; private set; }
    public static ConfigEntry<float> SourceValueMultiplier { get; private set; }
    public static ConfigEntry<float> RollDuration { get; private set; }
    public static ConfigEntry<float> TickVolumeMultiplier { get; private set; }
    public static ConfigEntry<bool> UseDynamicRarity { get; private set; }
    public static ConfigEntry<bool> EnableGoldBetting { get; private set; }
    public static ConfigEntry<float> MaxGoldChanceBonus { get; private set; }
    public static ConfigEntry<int> MaxGoldBet { get; private set; }
    public static ConfigEntry<bool> EnableDebugKeys { get; private set; }
    public static ConfigEntry<KeyCode> DebugGrantKey { get; private set; }
    public static ConfigEntry<KeyCode> DebugGrantPassiveKey { get; private set; }

    public static void Bind(ConfigFile config)
    {
        OpenKey = config.Bind(
            "General",
            "OpenKey",
            KeyCode.F8,
            "Keyboard key that opens the Upgrader modal interface."
        );

        ItemName = config.Bind(
            "Item",
            "Name",
            "Upgrader",
            "Display name of the Upgrader passive item."
        );

        ItemDescription = config.Bind(
            "Item",
            "Description",
            "A legendary device that gambles one passive item into another. Failure destroys the source item.",
            "Description shown in item details and tooltips."
        );

        ItemRarity = config.Bind(
            "Item",
            "Rarity",
            EItemRarity.Legendary,
            "Rarity of the item in chest loot pools. Default is Legendary."
        );

        MaxPerRun = config.Bind(
            "Item",
            "MaxPerRun",
            1,
            new ConfigDescription("Maximum number of Upgrader items that can drop in a single run.", new AcceptableValueRange<int>(1, 99))
        );

        MinimumChance = config.Bind(
            "Roll",
            "MinimumChance",
            0.05f,
            new ConfigDescription("Lowest possible success chance as a fraction (0.05 = 5%).", new AcceptableValueRange<float>(0.01f, 1.0f))
        );

        MaximumChance = config.Bind(
            "Roll",
            "MaximumChance",
            0.90f,
            new ConfigDescription("Highest possible success chance as a fraction (0.90 = 90%).", new AcceptableValueRange<float>(0.01f, 1.0f))
        );

        SourceValueMultiplier = config.Bind(
            "Roll",
            "SourceValueMultiplier",
            0.90f,
            new ConfigDescription("Multiplier applied to (sourceValue / targetValue) when calculating success chance.", new AcceptableValueRange<float>(0.1f, 5.0f))
        );

        RollDuration = config.Bind(
            "Roll",
            "RollDuration",
            4.2f,
            new ConfigDescription("Duration of the roulette spin animation in seconds.", new AcceptableValueRange<float>(0.5f, 15.0f))
        );

        TickVolumeMultiplier = config.Bind(
            "Roll",
            "TickVolumeMultiplier",
            0.10f,
            new ConfigDescription("Volume multiplier for the roulette tick sound (0.10 = 10% volume).", new AcceptableValueRange<float>(0.01f, 1.0f))
        );

        UseDynamicRarity = config.Bind(
            "Roll",
            "UseDynamicRarity",
            true,
            "Calculate real dynamic item value and rarity based on scarcity (maxAmountPerRun) and caps."
        );

        EnableGoldBetting = config.Bind(
            "Betting",
            "EnableGoldBetting",
            true,
            "Allow betting in-game gold on the roulette to boost chance and earn gold profit on victory."
        );

        MaxGoldChanceBonus = config.Bind(
            "Betting",
            "MaxGoldChanceBonus",
            0.15f,
            new ConfigDescription("Maximum success chance bonus obtainable through gold betting (0.15 = +15%).", new AcceptableValueRange<float>(0.01f, 0.40f))
        );

        MaxGoldBet = config.Bind(
            "Betting",
            "MaxGoldBet",
            500,
            new ConfigDescription("Maximum amount of gold that can be wagered in a single gamble.", new AcceptableValueRange<int>(50, 5000))
        );

        EnableDebugKeys = config.Bind(
            "Debug",
            "EnableDebugKeys",
            true,
            "Enable debug hotkeys to grant items in a run for rapid in-game testing."
        );

        DebugGrantKey = config.Bind(
            "Debug",
            "DebugGrantKey",
            KeyCode.F9,
            "Hotkey to grant 1 Upgrader item to the player during an active run."
        );

        DebugGrantPassiveKey = config.Bind(
            "Debug",
            "DebugGrantPassiveKey",
            KeyCode.F10,
            "Hotkey to grant a sample passive item to the player for quick gambling testing."
        );
    }
}
