using System;
using Assets.Scripts.Inventory__Items__Pickups;
using Assets.Scripts.Inventory__Items__Pickups.Items;
using Assets.Scripts.Inventory__Items__Pickups.Items.ItemImplementations;
using HarmonyLib;
using UnityEngine;

namespace MegaBonkUpgrader;

public static class UpgraderItemManager
{
    public const EItem UpgraderItem = (EItem)1000;
    public static ItemData UpgraderData { get; private set; }
    private static Texture2D upgraderIconTexture;
    private static bool isRegistered;

    public static Texture2D GetOrCreateIcon()
    {
        if (upgraderIconTexture != null)
            return upgraderIconTexture;

        int size = 64;
        upgraderIconTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        upgraderIconTexture.filterMode = FilterMode.Bilinear;
        upgraderIconTexture.wrapMode = TextureWrapMode.Clamp;

        Color bgDark = new Color(0.08f, 0.10f, 0.15f, 0.95f);
        Color goldBorder = new Color(1.0f, 0.80f, 0.20f, 1.0f);
        Color chevronColor = new Color(1.0f, 0.88f, 0.35f, 1.0f);
        Color coreGlow = new Color(0.20f, 0.80f, 1.0f, 0.9f);
        Color clear = new Color(0, 0, 0, 0);

        float center = size * 0.5f;
        float radius = size * 0.46f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > radius)
                {
                    upgraderIconTexture.SetPixel(x, y, clear);
                }
                else if (dist > radius - 2.5f)
                {
                    upgraderIconTexture.SetPixel(x, y, goldBorder);
                }
                else if (dist > radius - 4.0f)
                {
                    upgraderIconTexture.SetPixel(x, y, Color.Lerp(goldBorder, bgDark, 0.5f));
                }
                else
                {
                    bool isUpperChevron = (y >= 34 && y <= 46) && (Mathf.Abs(dx) <= (y - 32) * 1.5f) && (Mathf.Abs(dx) >= (y - 38) * 1.5f);
                    bool isLowerChevron = (y >= 18 && y <= 30) && (Mathf.Abs(dx) <= (y - 16) * 1.5f) && (Mathf.Abs(dx) >= (y - 22) * 1.5f);
                    bool isCenterCrystal = (y >= 12 && y <= 22) && (Mathf.Abs(dx) <= 4 - Mathf.Abs(y - 17) * 0.6f);

                    if (isUpperChevron)
                    {
                        upgraderIconTexture.SetPixel(x, y, chevronColor);
                    }
                    else if (isLowerChevron)
                    {
                        upgraderIconTexture.SetPixel(x, y, Color.Lerp(chevronColor, goldBorder, 0.4f));
                    }
                    else if (isCenterCrystal)
                    {
                        upgraderIconTexture.SetPixel(x, y, coreGlow);
                    }
                    else
                    {
                        float subtleGrad = 1f - (dist / radius) * 0.35f;
                        upgraderIconTexture.SetPixel(x, y, bgDark * subtleGrad);
                    }
                }
            }
        }

        upgraderIconTexture.Apply();
        return upgraderIconTexture;
    }

    public static void EnsureRegistered()
    {
        if (isRegistered && UpgraderData != null)
            return;

        try
        {
            DataManager data = DataManager.Instance;
            if (data == null || data.unsortedItems == null || data.itemData == null)
                return;

            if (UpgraderData == null)
            {
                ItemData template = null;
                for (int i = 0; i < data.unsortedItems.Count; i++)
                {
                    var it = data.unsortedItems[i];
                    if (it != null && (it.eItem == EItem.Anvil || it.rarity == EItemRarity.Legendary))
                    {
                        template = it;
                        break;
                    }
                }

                if (template == null && data.unsortedItems.Count > 0)
                {
                    template = data.unsortedItems[0];
                }

                if (template == null)
                {
                    return;
                }

                UpgraderData = UnityEngine.Object.Instantiate(template);
                UpgraderData.name = "Upgrader";
                UpgraderData.eItem = UpgraderItem;
                UpgraderData.itemTickPriority = 0;
                UpgraderData.dummyItem = null;
                UpgraderData.unlockRequirement = null;
                UpgraderData.icon = GetOrCreateIcon();
            }

            EItemRarity configuredRarity = UpgraderConfig.ItemRarity.Value;
            if (configuredRarity == EItemRarity.Quest)
                configuredRarity = EItemRarity.Legendary;

            int maxRun = Mathf.Max(1, UpgraderConfig.MaxPerRun.Value);

            UpgraderData.rarity = configuredRarity;
            UpgraderData.inItemPool = true;
            UpgraderData.maxAmount = maxRun;
            UpgraderData.maxAmountPerRun = maxRun;

            if (!data.unsortedItems.Contains(UpgraderData))
                data.unsortedItems.Add(UpgraderData);

            if (!data.itemData.ContainsKey(UpgraderItem))
                data.itemData.Add(UpgraderItem, UpgraderData);
            else
                data.itemData[UpgraderItem] = UpgraderData;

            isRegistered = true;
            Plugin.ModLog?.LogInfo($"Upgrader item registered in DataManager (Rarity: {configuredRarity}, MaxPerRun: {maxRun}).");
        }
        catch (Exception ex)
        {
            Plugin.ModLog?.LogWarning($"Failed to register Upgrader item: {ex.Message}");
        }
    }

    public static void EnsureInRunPool()
    {
        EnsureRegistered();
        if (UpgraderData == null) return;

        try
        {
            var dict = RunUnlockables.availableItems;
            if (dict == null) return;

            EItemRarity rarity = UpgraderConfig.ItemRarity.Value;
            if (rarity == EItemRarity.Quest)
                rarity = EItemRarity.Legendary;

            if (!dict.ContainsKey(rarity))
            {
                dict.Add(rarity, new Il2CppSystem.Collections.Generic.List<ItemData>());
            }

            var list = dict[rarity];
            if (list == null) return;

            int pickedThisRun = 0;
            if (RunUnlockables.numItemsPickedupThisRun != null && RunUnlockables.numItemsPickedupThisRun.ContainsKey(UpgraderItem))
            {
                pickedThisRun = RunUnlockables.numItemsPickedupThisRun[UpgraderItem];
            }

            int maxAllowed = Mathf.Max(1, UpgraderConfig.MaxPerRun.Value);
            if (pickedThisRun < maxAllowed)
            {
                if (!list.Contains(UpgraderData))
                {
                    list.Add(UpgraderData);
                    Plugin.ModLog?.LogInfo($"Upgrader added to chest pool: availableItems[{rarity}] ({pickedThisRun}/{maxAllowed} picked).");
                }
            }
            else
            {
                if (list.Contains(UpgraderData))
                {
                    list.Remove(UpgraderData);
                    Plugin.ModLog?.LogInfo($"Upgrader reached run limit ({pickedThisRun}/{maxAllowed}); removed from chest pool.");
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.ModLog?.LogWarning($"Error updating Upgrader chest pool: {ex.Message}");
        }
    }

    // --- Harmony Patches ---

    [HarmonyPatch(typeof(ItemFactory), nameof(ItemFactory.CreateItem))]
    private static class ItemFactoryCreatePatch
    {
        private static bool Prefix(EItem eItem, ItemInventory inventory, ref ItemBase __result)
        {
            if (eItem == UpgraderItem)
            {
                __result = new ItemNoImplementation(inventory);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(DataManager), nameof(DataManager.GetItem))]
    private static class DataManagerGetItemPatch
    {
        private static bool Prefix(EItem item, ref ItemData __result)
        {
            if (item == UpgraderItem && UpgraderData != null)
            {
                __result = UpgraderData;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(DataManager), nameof(DataManager.Load))]
    private static class DataManagerLoadPatch
    {
        private static void Postfix()
        {
            EnsureRegistered();
        }
    }

    [HarmonyPatch(typeof(RunUnlockables), nameof(RunUnlockables.Init))]
    private static class RunUnlockablesInitPatch
    {
        private static void Postfix()
        {
            EnsureInRunPool();
        }
    }

    [HarmonyPatch(typeof(RunUnlockables), nameof(RunUnlockables.OnNewRunStarted))]
    private static class RunUnlockablesNewRunPatch
    {
        private static void Postfix()
        {
            EnsureInRunPool();
        }
    }

    [HarmonyPatch(typeof(ItemUtility), nameof(ItemUtility.GetRandomItemFromRarity))]
    private static class ItemUtilityRarityPatch
    {
        private static void Prefix(EItemRarity rarity)
        {
            EnsureInRunPool();
        }
    }

    [HarmonyPatch(typeof(ItemUtility), nameof(ItemUtility.GetRandomChestItem))]
    private static class ItemUtilityChestPatch
    {
        private static void Prefix()
        {
            EnsureInRunPool();
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.GetName))]
    private static class ItemDataGetNamePatch
    {
        private static bool Prefix(ItemData __instance, ref string __result)
        {
            if (__instance != null && __instance.eItem == UpgraderItem)
            {
                __result = UpgraderConfig.ItemName.Value;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.GetDescription))]
    private static class ItemDataGetDescPatch
    {
        private static bool Prefix(ItemData __instance, ref string __result)
        {
            if (__instance != null && __instance.eItem == UpgraderItem)
            {
                __result = UpgraderConfig.ItemDescription.Value;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.GetInternalName))]
    private static class ItemDataGetInternalPatch
    {
        private static bool Prefix(ItemData __instance, ref string __result)
        {
            if (__instance != null && __instance.eItem == UpgraderItem)
            {
                __result = "Upgrader";
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.GetIcon))]
    private static class ItemDataGetIconPatch
    {
        private static bool Prefix(ItemData __instance, ref Texture __result)
        {
            if (__instance != null && __instance.eItem == UpgraderItem)
            {
                __result = GetOrCreateIcon();
                return false;
            }
            return true;
        }
    }
}
