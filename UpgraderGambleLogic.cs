using System;
using Assets.Scripts.Inventory__Items__Pickups.Items;
using UnityEngine;

namespace MegaBonkUpgrader;

public static class UpgraderGambleLogic
{
    public static float GetItemValue(ItemData item)
    {
        if (item == null) return 0f;

        // Base nominal value derived from nominal rarity
        float baseVal = item.rarity switch
        {
            EItemRarity.Common => 15f,
            EItemRarity.Rare => 40f,
            EItemRarity.Epic => 90f,
            EItemRarity.Legendary => 190f,
            EItemRarity.Corrupted => 250f,
            _ => 15f
        };

        // Real dynamic scarcity valuation: items that can only drop once or a few times per run
        // are truly scarce and powerful, even if nominally marked Common or Rare by the game.
        if (UpgraderConfig.UseDynamicRarity.Value)
        {
            if (item.maxAmountPerRun == 1)
            {
                // Unique single-drop item in a run (e.g. Anvil) -> massive value bump!
                baseVal += 55f;
            }
            else if (item.maxAmountPerRun > 0 && item.maxAmountPerRun <= 3)
            {
                baseVal += 25f;
            }
            else if (item.maxAmountPerRun > 0 && item.maxAmountPerRun <= 5)
            {
                baseVal += 12f;
            }

            if (item.maxAmount == 1)
            {
                baseVal += 20f;
            }
        }

        float priorityBonus = 1f + Mathf.Clamp(item.itemTickPriority, 0, 10) * 0.02f;
        return baseVal * priorityBonus;
    }

    public static EItemRarity GetDynamicRarity(ItemData item)
    {
        if (item == null) return EItemRarity.Common;
        if (!UpgraderConfig.UseDynamicRarity.Value) return item.rarity;

        float val = GetItemValue(item);
        if (val >= 150f) return EItemRarity.Legendary;
        if (val >= 75f) return EItemRarity.Epic;
        if (val >= 35f) return EItemRarity.Rare;
        return EItemRarity.Common;
    }

    public static float CalculateSuccessChance(ItemData source, ItemData target, int goldBet = 0)
    {
        if (source == null || target == null) return 0f;

        float min = Mathf.Clamp01(UpgraderConfig.MinimumChance.Value);
        float max = Mathf.Clamp01(UpgraderConfig.MaximumChance.Value);
        if (min > max)
        {
            (min, max) = (max, min);
        }

        float sourceVal = GetItemValue(source);
        float targetVal = GetItemValue(target);

        if (sourceVal <= 0f || targetVal <= 0f) return min;

        float ratio = (sourceVal / targetVal) * UpgraderConfig.SourceValueMultiplier.Value;

        // Gold Bet Bonus
        if (goldBet > 0 && UpgraderConfig.EnableGoldBetting.Value)
        {
            int maxBet = Mathf.Max(50, UpgraderConfig.MaxGoldBet.Value);
            float betFraction = Mathf.Clamp01((float)goldBet / maxBet);
            float maxBonus = Mathf.Clamp01(UpgraderConfig.MaxGoldChanceBonus.Value);
            ratio += betFraction * maxBonus;
        }

        return Mathf.Clamp(ratio, min, max);
    }

    public static bool IsChestPassiveItem(ItemData item)
    {
        if (item == null) return false;
        if (item.eItem == UpgraderItemManager.UpgraderItem) return false;
        if (!item.inItemPool) return false;
        if (item.rarity == EItemRarity.Quest) return false;

        return true;
    }

    public static Color GetRarityColor(EItemRarity rarity)
    {
        return rarity switch
        {
            EItemRarity.Common => new Color(0.72f, 0.75f, 0.80f),
            EItemRarity.Rare => new Color(0.25f, 0.65f, 1.0f),
            EItemRarity.Epic => new Color(0.72f, 0.35f, 0.95f),
            EItemRarity.Legendary => new Color(1.0f, 0.78f, 0.20f),
            EItemRarity.Corrupted => new Color(0.95f, 0.25f, 0.25f),
            _ => Color.white
        };
    }

    public static string GetRarityDisplayName(EItemRarity rarity)
    {
        return rarity switch
        {
            EItemRarity.Common => "Обычный",
            EItemRarity.Rare => "Редкий",
            EItemRarity.Epic => "Эпический",
            EItemRarity.Legendary => "Легендарный",
            EItemRarity.Corrupted => "Искажённый",
            _ => rarity.ToString()
        };
    }
}
