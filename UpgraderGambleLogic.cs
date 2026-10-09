using System;
using Assets.Scripts.Inventory__Items__Pickups.Items;
using UnityEngine;

namespace MegaBonkUpgrader;

public static class UpgraderGambleLogic
{
    public static float GetItemValue(ItemData item)
    {
        if (item == null) return 0f;

        float baseVal = item.rarity switch
        {
            EItemRarity.Common => 10f,
            EItemRarity.Rare => 25f,
            EItemRarity.Epic => 60f,
            EItemRarity.Legendary => 140f,
            EItemRarity.Corrupted => 200f,
            _ => 10f
        };

        float priorityBonus = 1f + Mathf.Clamp(item.itemTickPriority, 0, 10) * 0.02f;
        return baseVal * priorityBonus;
    }

    public static float CalculateSuccessChance(ItemData source, ItemData target)
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
