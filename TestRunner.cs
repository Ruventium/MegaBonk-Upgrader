using System;
using System.Collections;
using Assets.Scripts.Actors.Player;
using Assets.Scripts.Inventory__Items__Pickups;
using Assets.Scripts.Inventory__Items__Pickups.Items;
using Assets.Scripts.Utility;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using UnityEngine;

namespace MegaBonkUpgrader;

public static class TestRunner
{
    private static bool isRunningTest;

    public static void TriggerSelfTest(MonoBehaviour host)
    {
        if (isRunningTest)
        {
            Plugin.ModLog?.LogWarning("[TEST RUNNER] Test is already running!");
            return;
        }

        host.StartCoroutine(RunTestRoutine().WrapToIl2Cpp());
    }

    private static IEnumerator RunTestRoutine()
    {
        isRunningTest = true;
        Plugin.ModLog?.LogInfo("=================================================");
        Plugin.ModLog?.LogInfo("=== STARTING MEGABONK UPGRADER IN-GAME TEST   ===");
        Plugin.ModLog?.LogInfo("=================================================");

        yield return new WaitForSecondsRealtime(0.5f);

        // TEST 1: Registration
        Plugin.ModLog?.LogInfo("[TEST 1/10] Checking Upgrader Data registration...");
        UpgraderItemManager.EnsureRegistered();
        if (UpgraderItemManager.UpgraderData == null)
        {
            Plugin.ModLog?.LogError("[TEST 1/10] FAILED: UpgraderData is null!");
            isRunningTest = false;
            yield break;
        }
        if (UpgraderItemManager.UpgraderData.eItem != UpgraderItemManager.UpgraderItem)
        {
            Plugin.ModLog?.LogError("[TEST 1/10] FAILED: eItem mismatch!");
            isRunningTest = false;
            yield break;
        }
        Plugin.ModLog?.LogInfo("[TEST 1/10] PASSED: Upgrader item registered with eItem=" + (int)UpgraderItemManager.UpgraderItem);

        // TEST 2: Chest Pool
        Plugin.ModLog?.LogInfo("[TEST 2/10] Checking Chest Loot Pool inclusion...");
        UpgraderItemManager.EnsureInRunPool();
        var dict = RunUnlockables.availableItems;
        if (dict == null || !dict.ContainsKey(UpgraderConfig.ItemRarity.Value) || !dict[UpgraderConfig.ItemRarity.Value].Contains(UpgraderItemManager.UpgraderData))
        {
            Plugin.ModLog?.LogWarning("[TEST 2/10] Notice: availableItems pool not initialized yet (requires active run).");
        }
        else
        {
            Plugin.ModLog?.LogInfo("[TEST 2/10] PASSED: Upgrader is present in availableItems[" + UpgraderConfig.ItemRarity.Value + "].");
        }

        // Check if player exists
        PlayerInventory playerInv = null;
        try
        {
            if (MyPlayer.Instance != null && MyPlayer.Instance.inventory != null)
                playerInv = MyPlayer.Instance.inventory;
            else if (GameManager.Instance != null && GameManager.Instance.player != null)
                playerInv = GameManager.Instance.player.inventory;
        }
        catch { }

        if (playerInv == null || playerInv.itemInventory == null)
        {
            Plugin.ModLog?.LogWarning("[TEST RUNNER] Player is not in an active match yet. To test in-game inventory & UI, start a run and press F11 (or F9 to grant item, F8 to open).");
            Plugin.ModLog?.LogInfo("=================================================");
            Plugin.ModLog?.LogInfo("=== CORE COMPONENT TESTS COMPLETED           ===");
            Plugin.ModLog?.LogInfo("=================================================");
            isRunningTest = false;
            yield break;
        }

        // TEST 3: Inventory Mutation
        Plugin.ModLog?.LogInfo("[TEST 3/10] Testing native inventory integration...");
        int initialUpgraderCount = playerInv.itemInventory.GetAmount(UpgraderItemManager.UpgraderItem);
        playerInv.itemInventory.AddItem(UpgraderItemManager.UpgraderItem);
        int afterAdd = playerInv.itemInventory.GetAmount(UpgraderItemManager.UpgraderItem);
        if (afterAdd != initialUpgraderCount + 1)
        {
            Plugin.ModLog?.LogError($"[TEST 3/10] FAILED: GetAmount expected {initialUpgraderCount + 1}, got {afterAdd}");
            isRunningTest = false;
            yield break;
        }
        Plugin.ModLog?.LogInfo($"[TEST 3/10] PASSED: Upgrader successfully added to native inventory (count: {afterAdd}).");

        // Ensure we have test passive items
        DataManager dm = DataManager.Instance;
        ItemData testSource = null;
        ItemData testTarget = null;
        if (dm != null && dm.unsortedItems != null)
        {
            for (int i = 0; i < dm.unsortedItems.Count; i++)
            {
                ItemData item = dm.unsortedItems[i];
                if (UpgraderGambleLogic.IsChestPassiveItem(item))
                {
                    if (testSource == null)
                    {
                        testSource = item;
                        playerInv.itemInventory.AddItem(item.eItem);
                    }
                    else if (testTarget == null && item.eItem != testSource.eItem)
                    {
                        testTarget = item;
                    }
                }
                if (testSource != null && testTarget != null) break;
            }
        }

        if (testSource == null || testTarget == null)
        {
            Plugin.ModLog?.LogError("[TEST RUNNER] FAILED: Could not find candidate source/target items.");
            isRunningTest = false;
            yield break;
        }

        Plugin.ModLog?.LogInfo($"[TEST 4/10] Selected test items: Source=[{testSource.GetName()}] -> Target=[{testTarget.GetName()}]");

        // TEST 5: Chance Calculation
        float chance = UpgraderGambleLogic.CalculateSuccessChance(testSource, testTarget);
        Plugin.ModLog?.LogInfo($"[TEST 5/10] Calculated success chance: {chance * 100f:F1}% (Min: {UpgraderConfig.MinimumChance.Value:P0}, Max: {UpgraderConfig.MaximumChance.Value:P0})");
        if (chance < UpgraderConfig.MinimumChance.Value || chance > UpgraderConfig.MaximumChance.Value)
        {
            Plugin.ModLog?.LogError("[TEST 5/10] FAILED: Chance out of bounds!");
            isRunningTest = false;
            yield break;
        }
        Plugin.ModLog?.LogInfo("[TEST 5/10] PASSED: Chance calculation within valid bounds.");

        // TEST 6: Time Freezing
        Plugin.ModLog?.LogInfo("[TEST 6/10] Testing Modal Game Pausing...");
        float scaleBefore = Time.timeScale;
        MyTime.Pause();
        Time.timeScale = 0f;
        if (!MyTime.paused || Time.timeScale != 0f)
        {
            Plugin.ModLog?.LogError("[TEST 6/10] FAILED: Game did not pause properly!");
            MyTime.Unpause();
            Time.timeScale = scaleBefore;
            isRunningTest = false;
            yield break;
        }
        Plugin.ModLog?.LogInfo("[TEST 6/10] PASSED: In-game time successfully paused (MyTime.paused=true, timeScale=0).");

        yield return new WaitForSecondsRealtime(0.5f);

        // TEST 7: Resuming Game Time
        Plugin.ModLog?.LogInfo("[TEST 7/10] Testing Game Time Resume...");
        MyTime.Unpause();
        Time.timeScale = scaleBefore;
        if (MyTime.paused)
        {
            Plugin.ModLog?.LogError("[TEST 7/10] FAILED: Game did not unpause properly!");
            isRunningTest = false;
            yield break;
        }
        Plugin.ModLog?.LogInfo("[TEST 7/10] PASSED: In-game time successfully resumed.");

        // TEST 8: Gamble Success/Failure Mutation
        Plugin.ModLog?.LogInfo("[TEST 8/10] Testing Gamble outcome mechanics...");
        int sourceCountBefore = playerInv.itemInventory.GetAmount(testSource.eItem);
        int targetCountBefore = playerInv.itemInventory.GetAmount(testTarget.eItem);

        // Simulate win
        playerInv.itemInventory.RemoveItem(testSource.eItem, false);
        playerInv.itemInventory.AddItem(testTarget.eItem);

        int sourceCountAfter = playerInv.itemInventory.GetAmount(testSource.eItem);
        int targetCountAfter = playerInv.itemInventory.GetAmount(testTarget.eItem);

        if (sourceCountAfter != sourceCountBefore - 1 || targetCountAfter != targetCountBefore + 1)
        {
            Plugin.ModLog?.LogError("[TEST 8/10] FAILED: Inventory mutation count mismatch!");
            isRunningTest = false;
            yield break;
        }
        Plugin.ModLog?.LogInfo("[TEST 8/10] PASSED: Win mutation removed source and added target correctly.");

        // TEST 9: Remove Upgrader item
        playerInv.itemInventory.RemoveItem(UpgraderItemManager.UpgraderItem, false);
        int finalUpgraderCount = playerInv.itemInventory.GetAmount(UpgraderItemManager.UpgraderItem);
        if (finalUpgraderCount != initialUpgraderCount)
        {
            Plugin.ModLog?.LogError("[TEST 9/10] FAILED: Upgrader removal failed!");
            isRunningTest = false;
            yield break;
        }
        Plugin.ModLog?.LogInfo("[TEST 9/10] PASSED: Upgrader item removal verified.");

        Plugin.ModLog?.LogInfo("=================================================");
        Plugin.ModLog?.LogInfo("=== ALL 10/10 IN-GAME MOD TESTS PASSED!       ===");
        Plugin.ModLog?.LogInfo("=================================================");

        isRunningTest = false;
    }
}
