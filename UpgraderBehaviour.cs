using System;
using System.Collections.Generic;
using Assets.Scripts.Actors.Player;
using Assets.Scripts.Inventory__Items__Pickups;
using Assets.Scripts.Inventory__Items__Pickups.Items;
using Assets.Scripts.Utility;
using UnityEngine;

namespace MegaBonkUpgrader;

public sealed class UpgraderBehaviour : MonoBehaviour
{
    private static UpgraderBehaviour instance;
    public static UpgraderBehaviour Instance => instance;

    // Window State
    private bool isWindowOpen;
    private bool isPausedByMod;
    private float prevTimeScale = 1f;
    private float prevMyTimeScale = 1f;

    // Item Selections & Filtering
    private ItemData selectedSource;
    private ItemData selectedTarget;
    private int sourceRarityFilter = -1; // -1 = All, 0 = Common, 1 = Rare, 2 = Epic, 3 = Legendary
    private int targetRarityFilter = -1; // -1 = All, 0 = Common, 1 = Rare, 2 = Epic, 3 = Legendary

    private readonly List<ItemData> ownedList = new();
    private readonly List<ItemData> targetList = new();
    private readonly List<ItemData> allChestPassives = new();

    private Vector2 sourceScroll;
    private Vector2 targetScroll;

    // Roulette State
    private bool isRolling;
    private bool hasFinishedRoll;
    private bool lastRollWon;
    private float rollElapsed;
    private float rollDuration;
    private float targetScrollX;
    private float currentScrollX;
    private int lastTickCardIndex = -1;
    private string outcomeMessage = string.Empty;

    private readonly List<RouletteCard> rouletteReel = new();
    private const int WIN_CARD_INDEX = 25;
    private const float CARD_WIDTH = 115f;
    private const float CARD_SPACING = 12f;
    private const float CARD_TOTAL_WIDTH = CARD_WIDTH + CARD_SPACING; // 127f
    private const float VIEWPORT_WIDTH = 740f;

    // Audio Clips cache
    private AudioClip positiveClip;
    private AudioClip negativeClip;
    private AudioSource sfxSource;

    // GUI Styles
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle headerStyle;
    private GUIStyle columnTitleStyle;
    private GUIStyle itemCardStyle;
    private GUIStyle itemCardSelectedStyle;
    private GUIStyle itemDescStyle;
    private GUIStyle buttonPrimaryStyle;
    private GUIStyle buttonSecondaryStyle;
    private GUIStyle tabButtonStyle;
    private GUIStyle tabButtonActiveStyle;
    private GUIStyle chanceLabelStyle;
    private GUIStyle statusLabelStyle;
    private GUIStyle badgeStyle;
    private GUIStyle tagStyle;

    private void Awake()
    {
        instance = this;
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
    }

    private void Start()
    {
        // Run initial component validation 2 seconds after game load
        Invoke(nameof(RunInitialValidation), 2f);
    }

    private void RunInitialValidation()
    {
        TestRunner.TriggerSelfTest(this);
    }

    private void OnDestroy()
    {
        if (isPausedByMod)
        {
            RestoreGameTime();
        }
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        // Debug keys for rapid in-game testing
        if (UpgraderConfig.EnableDebugKeys.Value)
        {
            if (Input.GetKeyDown(KeyCode.F11))
            {
                TestRunner.TriggerSelfTest(this);
            }

            if (Input.GetKeyDown(UpgraderConfig.DebugGrantKey.Value))
            {
                PlayerInventory inv = GetCurrentPlayerInventory();
                if (inv != null && inv.itemInventory != null)
                {
                    inv.itemInventory.AddItem(UpgraderItemManager.UpgraderItem);
                    Plugin.ModLog?.LogInfo("DEBUG: Granted 1 Upgrader to player for testing.");
                }
            }

            if (Input.GetKeyDown(UpgraderConfig.DebugGrantPassiveKey.Value))
            {
                PlayerInventory inv = GetCurrentPlayerInventory();
                if (inv != null && inv.itemInventory != null)
                {
                    RefreshLists();
                    if (allChestPassives.Count > 0)
                    {
                        ItemData sample = allChestPassives[UnityEngine.Random.Range(0, allChestPassives.Count)];
                        inv.itemInventory.AddItem(sample.eItem);
                        Plugin.ModLog?.LogInfo($"DEBUG: Granted sample passive [{sample.GetName()}] to player for testing.");
                    }
                }
            }
        }

        // Hotkey: Open / Close
        if (Input.GetKeyDown(UpgraderConfig.OpenKey.Value))
        {
            if (isWindowOpen)
            {
                CloseWindow(true);
            }
            else
            {
                TryOpenWindow();
            }
        }

        if (!isWindowOpen)
            return;

        // Ensure cursor is freed during modal window
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // If native pause opened externally, close modal without unpausing
        if (IsNativeGamePaused())
        {
            CloseWindow(false);
            return;
        }

        // Escape closes window
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseWindow(true);
            return;
        }

        // Update Roulette Reel Spin
        if (isRolling)
        {
            rollElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(rollElapsed / rollDuration);

            // Smooth cubic ease-out deceleration
            float eased = 1f - Mathf.Pow(1f - t, 3.5f);
            currentScrollX = targetScrollX * eased;

            // Audio/Visual Tick check
            int currentCardUnderNeedle = Mathf.FloorToInt((currentScrollX + VIEWPORT_WIDTH * 0.5f) / CARD_TOTAL_WIDTH);
            if (currentCardUnderNeedle != lastTickCardIndex)
            {
                lastTickCardIndex = currentCardUnderNeedle;
                PlayTickSound();
            }

            if (rollElapsed >= rollDuration)
            {
                isRolling = false;
                FinalizeRoll();
            }
        }
    }

    private void TryOpenWindow()
    {
        // Safety checks: game must not be already paused
        if (IsNativeGamePaused())
        {
            Plugin.ModLog?.LogInfo("Upgrader window refused to open: game is already paused.");
            return;
        }

        try
        {
            if (MyTime.paused)
            {
                Plugin.ModLog?.LogInfo("Upgrader window refused to open: MyTime is paused.");
                return;
            }
        }
        catch { }

        try
        {
            if (WindowManager.HasOpenWindow())
            {
                Plugin.ModLog?.LogInfo("Upgrader window refused to open: a game window is already open.");
                return;
            }
        }
        catch { }

        PlayerInventory playerInventory = GetCurrentPlayerInventory();
        if (playerInventory == null || playerInventory.itemInventory == null)
        {
            Plugin.ModLog?.LogInfo("Upgrader window refused to open: player inventory not found.");
            return;
        }

        // Player must own the Upgrader item to open the interface
        int upgraderCount = playerInventory.itemInventory.GetAmount(UpgraderItemManager.UpgraderItem);
        if (upgraderCount <= 0)
        {
            Plugin.ModLog?.LogInfo("Upgrader window refused to open: Upgrader item not owned.");
            return;
        }

        OpenWindow();
    }

    public void OpenWindow()
    {
        try
        {
            prevTimeScale = Time.timeScale;
            prevMyTimeScale = MyTime.timeScale;

            MyTime.Pause();
            Time.timeScale = 0f;
            isPausedByMod = true;
        }
        catch (Exception ex)
        {
            Plugin.ModLog?.LogWarning("Error pausing game time: " + ex.Message);
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        isWindowOpen = true;
        isRolling = false;
        hasFinishedRoll = false;
        selectedSource = null;
        selectedTarget = null;
        outcomeMessage = string.Empty;

        FetchGameAudioClips();
        RefreshLists();
        Plugin.ModLog?.LogInfo("Upgrader modal window opened.");
    }

    public void CloseWindow(bool resumeGame)
    {
        isWindowOpen = false;
        isRolling = false;
        hasFinishedRoll = false;
        selectedSource = null;
        selectedTarget = null;

        if (resumeGame && isPausedByMod)
        {
            RestoreGameTime();
        }
        Plugin.ModLog?.LogInfo("Upgrader modal window closed.");
    }

    private void RestoreGameTime()
    {
        try
        {
            MyTime.Unpause();
            Time.timeScale = prevTimeScale > 0.01f ? prevTimeScale : 1f;
            MyTime.timeScale = prevMyTimeScale > 0.01f ? prevMyTimeScale : 1f;
        }
        catch (Exception ex)
        {
            Plugin.ModLog?.LogWarning("Error resuming game time: " + ex.Message);
        }
        finally
        {
            isPausedByMod = false;
        }
    }

    private static bool IsNativeGamePaused()
    {
        try
        {
            if (MyTime.paused && instance != null && !instance.isPausedByMod)
                return true;
        }
        catch { }

        try
        {
            return WindowManager.HasOpenWindow();
        }
        catch
        {
            return false;
        }
    }

    private static PlayerInventory GetCurrentPlayerInventory()
    {
        try
        {
            if (MyPlayer.Instance != null && MyPlayer.Instance.inventory != null)
                return MyPlayer.Instance.inventory;
        }
        catch { }

        try
        {
            if (GameManager.Instance != null && GameManager.Instance.player != null && GameManager.Instance.player.inventory != null)
                return GameManager.Instance.player.inventory;
        }
        catch { }

        return null;
    }

    private void RefreshLists()
    {
        ownedList.Clear();
        targetList.Clear();
        allChestPassives.Clear();

        PlayerInventory playerInv = GetCurrentPlayerInventory();
        DataManager dm = DataManager.Instance;
        if (dm == null || dm.unsortedItems == null)
            return;

        for (int i = 0; i < dm.unsortedItems.Count; i++)
        {
            ItemData it = dm.unsortedItems[i];
            if (it == null) continue;

            if (UpgraderGambleLogic.IsChestPassiveItem(it))
            {
                allChestPassives.Add(it);
                targetList.Add(it);

                if (playerInv != null && playerInv.itemInventory != null)
                {
                    int amount = playerInv.itemInventory.GetAmount(it.eItem);
                    if (amount > 0)
                    {
                        ownedList.Add(it);
                    }
                }
            }
        }

        // Sort items by rarity descending, then by name
        Comparison<ItemData> comp = (a, b) =>
        {
            int r = b.rarity.CompareTo(a.rarity);
            return r != 0 ? r : string.Compare(a.GetName(), b.GetName(), StringComparison.Ordinal);
        };

        ownedList.Sort(comp);
        targetList.Sort(comp);
        allChestPassives.Sort(comp);
    }

    private void StartRoll()
    {
        if (isRolling || selectedSource == null || selectedTarget == null)
            return;

        PlayerInventory inv = GetCurrentPlayerInventory();
        if (inv == null || inv.itemInventory == null)
            return;

        int sourceAmount = inv.itemInventory.GetAmount(selectedSource.eItem);
        if (sourceAmount <= 0)
        {
            outcomeMessage = "Ошибка: исходный предмет больше не найден в вашем инвентаре!";
            RefreshLists();
            return;
        }

        // Calculate chance and determine outcome
        float chance = UpgraderGambleLogic.CalculateSuccessChance(selectedSource, selectedTarget);
        lastRollWon = UnityEngine.Random.value <= chance;

        // Build Roulette Reel
        rouletteReel.Clear();
        int totalCards = 36;
        for (int i = 0; i < totalCards; i++)
        {
            if (i == WIN_CARD_INDEX)
            {
                if (lastRollWon)
                {
                    rouletteReel.Add(new RouletteCard(selectedTarget, false));
                }
                else
                {
                    rouletteReel.Add(new RouletteCard(selectedSource, true)); // destroyed card
                }
            }
            else
            {
                ItemData randItem = allChestPassives.Count > 0
                    ? allChestPassives[UnityEngine.Random.Range(0, allChestPassives.Count)]
                    : selectedTarget;
                rouletteReel.Add(new RouletteCard(randItem, false));
            }
        }

        // Position alignment: WIN_CARD_INDEX centered in viewport
        float cardCenter = WIN_CARD_INDEX * CARD_TOTAL_WIDTH + CARD_WIDTH * 0.5f;
        float viewportCenter = VIEWPORT_WIDTH * 0.5f;
        float jitter = (UnityEngine.Random.value - 0.5f) * 16f; // natural organic jitter
        targetScrollX = cardCenter - viewportCenter + jitter;

        currentScrollX = 0f;
        rollElapsed = 0f;
        rollDuration = Mathf.Max(0.5f, UpgraderConfig.RollDuration.Value);
        isRolling = true;
        hasFinishedRoll = false;
        lastTickCardIndex = -1;
        outcomeMessage = "Рулетка вращается...";
        Plugin.ModLog?.LogInfo($"Roulette roll started: [{selectedSource.GetName()}] -> [{selectedTarget.GetName()}], Chance: {chance * 100f:F1}%. Won: {lastRollWon}.");
    }

    private void FinalizeRoll()
    {
        hasFinishedRoll = true;
        PlayerInventory inv = GetCurrentPlayerInventory();
        if (inv == null || inv.itemInventory == null || selectedSource == null || selectedTarget == null)
            return;

        if (lastRollWon)
        {
            inv.itemInventory.RemoveItem(selectedSource.eItem, false);
            inv.itemInventory.AddItem(selectedTarget.eItem);
            PlayOutcomeSound(true);
            outcomeMessage = $"УСПЕХ! [{selectedSource.GetName()}] улучшен до [{selectedTarget.GetName()}]!";
            Plugin.ModLog?.LogInfo($"Roulette Result: WIN. Replaced [{selectedSource.GetName()}] with [{selectedTarget.GetName()}].");
        }
        else
        {
            inv.itemInventory.RemoveItem(selectedSource.eItem, true);
            PlayOutcomeSound(false);
            outcomeMessage = $"ПРОВАЛ! [{selectedSource.GetName()}] сгорел и был уничтожен!";
            Plugin.ModLog?.LogInfo($"Roulette Result: LOSS. Destroyed [{selectedSource.GetName()}].");
        }

        RefreshLists();
    }

    private void PlayTickSound()
    {
        if (positiveClip != null && sfxSource != null)
        {
            sfxSource.pitch = UnityEngine.Random.Range(1.3f, 1.6f);
            sfxSource.PlayOneShot(positiveClip, 0.22f);
        }
    }

    private void PlayOutcomeSound(bool win)
    {
        if (sfxSource == null) return;
        sfxSource.pitch = 1.0f;

        if (win && positiveClip != null)
        {
            sfxSource.PlayOneShot(positiveClip, 0.9f);
        }
        else if (!win && negativeClip != null)
        {
            sfxSource.PlayOneShot(negativeClip, 0.9f);
        }
    }

    private void FetchGameAudioClips()
    {
        if (positiveClip != null && negativeClip != null)
            return;

        try
        {
            ChestItem[] chestItems = Resources.FindObjectsOfTypeAll<ChestItem>();
            if (chestItems != null && chestItems.Length > 0)
            {
                foreach (var ci in chestItems)
                {
                    if (ci == null) continue;
                    if (positiveClip == null && ci.clipPositive != null) positiveClip = ci.clipPositive;
                    if (negativeClip == null && ci.clipNegative != null) negativeClip = ci.clipNegative;
                    if (positiveClip != null && negativeClip != null) break;
                }
            }
        }
        catch { }
    }

    // --- GUI Helpers ---

    private static void DrawRect(Rect rect, Color color)
    {
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prev;
    }

    private static void DrawBorder(Rect rect, Color color, int thickness = 1)
    {
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.color = prev;
    }

    private static void DrawPanel(Rect rect, Color bg, Color border, int borderThickness = 1)
    {
        DrawRect(rect, bg);
        if (borderThickness > 0 && border.a > 0f)
        {
            DrawBorder(rect, border, borderThickness);
        }
    }

    // --- GUI Rendering ---

    private void OnGUI()
    {
        if (!isWindowOpen)
            return;

        InitStyles();

        GUI.depth = -2000;

        // Dark dim backdrop
        DrawRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.03f, 0.05f, 0.90f));

        float winW = Mathf.Min(1160f, Screen.width - 30f);
        float winH = Mathf.Min(780f, Screen.height - 30f);
        Rect winRect = new Rect((Screen.width - winW) * 0.5f, (Screen.height - winH) * 0.5f, winW, winH);

        // Outer window with double border
        DrawPanel(winRect, new Color(0.08f, 0.09f, 0.13f, 0.98f), new Color(0.24f, 0.28f, 0.36f, 1f), 2);
        DrawBorder(new Rect(winRect.x + 3f, winRect.y + 3f, winRect.width - 6f, winRect.height - 6f), new Color(0.95f, 0.78f, 0.25f, 0.30f), 1);

        // Header
        DrawHeader(winRect);

        if (isRolling || hasFinishedRoll)
        {
            DrawRouletteView(winRect);
        }
        else
        {
            DrawSelectionView(winRect);
        }
    }

    private void DrawHeader(Rect win)
    {
        float pad = 16f;
        Rect headerRect = new Rect(win.x + pad, win.y + 12f, win.width - pad * 2f, 58f);

        // Background bar
        DrawPanel(headerRect, new Color(0.05f, 0.06f, 0.09f, 0.95f), new Color(0.18f, 0.22f, 0.28f, 1f), 1);

        // Title & Pill Tag
        GUI.Label(new Rect(headerRect.x + 16f, headerRect.y + 8f, 260f, 26f), "МОДИФИКАТОР ПРЕДМЕТОВ", titleStyle);

        Rect tagRect = new Rect(headerRect.x + 282f, headerRect.y + 11f, 115f, 20f);
        DrawPanel(tagRect, new Color(0.25f, 0.20f, 0.05f, 0.9f), new Color(1.0f, 0.80f, 0.20f, 0.9f), 1);
        GUI.Label(tagRect, "ЛЕГЕНДАРНЫЙ", tagStyle);

        GUI.Label(new Rect(headerRect.x + 16f, headerRect.y + 32f, 540f, 20f), "Рискните имеющимся пассивным предметом ради нового. При провале исходный сгорает.", subtitleStyle);

        // Upgrader Inventory Badge
        PlayerInventory inv = GetCurrentPlayerInventory();
        int upgraderCount = inv != null && inv.itemInventory != null ? inv.itemInventory.GetAmount(UpgraderItemManager.UpgraderItem) : 0;
        Texture icon = UpgraderItemManager.GetOrCreateIcon();

        Rect badgeRect = new Rect(headerRect.x + headerRect.width - 290f, headerRect.y + 10f, 175f, 38f);
        DrawPanel(badgeRect, new Color(0.12f, 0.14f, 0.20f, 0.9f), new Color(1.0f, 0.80f, 0.20f, 0.6f), 1);
        if (icon != null)
        {
            GUI.DrawTexture(new Rect(badgeRect.x + 6f, badgeRect.y + 5f, 28f, 28f), icon);
        }
        GUI.Label(new Rect(badgeRect.x + 38f, badgeRect.y + 8f, 130f, 22f), $"В инвентаре: {upgraderCount} шт.", badgeStyle);

        // Close Button [ESC]
        Rect closeBtnRect = new Rect(headerRect.x + headerRect.width - 100f, headerRect.y + 10f, 92f, 38f);
        if (GUI.Button(closeBtnRect, $"✕ [{UpgraderConfig.OpenKey.Value}]", buttonSecondaryStyle))
        {
            CloseWindow(true);
        }
    }

    private void DrawSelectionView(Rect win)
    {
        float pad = 16f;
        float top = win.y + 76f;
        float colGap = 14f;
        float colWidth = (win.width - pad * 2f - colGap) * 0.5f;
        float actionPanelHeight = 215f;
        float listHeight = win.height - (top - win.y) - actionPanelHeight - 16f;

        // Left Column: Source Items
        DrawItemColumn(new Rect(win.x + pad, top, colWidth, listHeight), true);

        // Right Column: Target Items
        DrawItemColumn(new Rect(win.x + pad + colWidth + colGap, top, colWidth, listHeight), false);

        // Bottom Action Area
        float bottomY = top + listHeight + 10f;
        DrawActionPanel(new Rect(win.x + pad, bottomY, win.width - pad * 2f, actionPanelHeight));
    }

    private void DrawItemColumn(Rect rect, bool isSource)
    {
        // Column panel
        DrawPanel(rect, new Color(0.06f, 0.07f, 0.10f, 0.90f), new Color(0.18f, 0.21f, 0.28f, 1f), 1);

        // Column Header Banner
        float headerHeight = 32f;
        Rect colHeaderRect = new Rect(rect.x, rect.y, rect.width, headerHeight);
        DrawPanel(colHeaderRect, new Color(0.10f, 0.12f, 0.17f, 0.95f), new Color(0.20f, 0.24f, 0.32f, 1f), 1);

        List<ItemData> list = isSource ? ownedList : targetList;
        string colTitle = isSource ? "1. ИСХОДНАЯ ПАССИВКА (ВАШ ИНВЕНТАРЬ)" : "2. ЦЕЛЕВАЯ ПАССИВКА (ПРЕДМЕТЫ ИЗ СУНДУКОВ)";
        string countBadge = isSource ? $"({ownedList.Count} шт.)" : $"({targetList.Count} в пуле)";

        GUI.color = isSource ? new Color(1f, 0.85f, 0.4f) : new Color(0.4f, 0.85f, 1f);
        GUI.Label(new Rect(colHeaderRect.x + 10f, colHeaderRect.y + 6f, colHeaderRect.width - 120f, 22f), colTitle, columnTitleStyle);
        GUI.color = new Color(0.7f, 0.75f, 0.85f);
        GUI.Label(new Rect(colHeaderRect.x + colHeaderRect.width - 110f, colHeaderRect.y + 7f, 100f, 20f), countBadge, subtitleStyle);
        GUI.color = Color.white;

        // Rarity Filter Tabs
        float tabsY = rect.y + 38f;
        DrawRarityTabs(new Rect(rect.x + 8f, tabsY, rect.width - 16f, 28f), isSource);

        // Items Scroll View
        float scrollY = tabsY + 34f;
        Rect viewRect = new Rect(rect.x + 8f, scrollY, rect.width - 16f, rect.height - (scrollY - rect.y) - 8f);

        int activeFilter = isSource ? sourceRarityFilter : targetRarityFilter;
        PlayerInventory inv = GetCurrentPlayerInventory();
        float itemRowHeight = 50f;

        // Filter items
        List<ItemData> filtered = new();
        for (int i = 0; i < list.Count; i++)
        {
            ItemData it = list[i];
            if (it == null) continue;
            if (!isSource && selectedSource != null && it.eItem == selectedSource.eItem)
                continue; // Cannot gamble item into itself

            if (activeFilter >= 0 && (int)it.rarity != activeFilter)
                continue;

            filtered.Add(it);
        }

        float contentHeight = Mathf.Max(viewRect.height, filtered.Count * itemRowHeight + 6f);
        Vector2 scroll = isSource ? sourceScroll : targetScroll;

        scroll = GUI.BeginScrollView(viewRect, scroll, new Rect(0, 0, viewRect.width - 18f, contentHeight));
        float curY = 4f;

        for (int i = 0; i < filtered.Count; i++)
        {
            ItemData it = filtered[i];
            bool isSelected = isSource
                ? selectedSource != null && selectedSource.eItem == it.eItem
                : selectedTarget != null && selectedTarget.eItem == it.eItem;

            Rect rowRect = new Rect(2f, curY, viewRect.width - 22f, itemRowHeight - 4f);

            // Row Card Panel
            Color cardBg = isSelected ? new Color(0.18f, 0.22f, 0.32f, 0.95f) : new Color(0.10f, 0.12f, 0.16f, 0.85f);
            Color cardBorder = isSelected ? new Color(1.0f, 0.82f, 0.20f, 1f) : new Color(0.16f, 0.19f, 0.26f, 0.7f);
            DrawPanel(rowRect, cardBg, cardBorder, isSelected ? 2 : 1);

            // Invisible button for entire row click
            if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
            {
                if (isSource)
                {
                    selectedSource = it;
                    if (selectedTarget != null && selectedTarget.eItem == it.eItem)
                        selectedTarget = null;
                }
                else
                {
                    selectedTarget = it;
                }
            }

            // Draw Icon with rarity frame
            Rect iconFrame = new Rect(rowRect.x + 6f, rowRect.y + 4f, 38f, 38f);
            Color rarityColor = UpgraderGambleLogic.GetRarityColor(it.rarity);
            DrawPanel(iconFrame, new Color(0.05f, 0.06f, 0.08f, 0.9f), rarityColor, 1);

            Texture icon = it.GetIcon();
            if (icon != null)
            {
                GUI.DrawTexture(new Rect(iconFrame.x + 3f, iconFrame.y + 3f, 32f, 32f), icon);
            }

            // Draw Name and Rarity
            GUI.color = isSelected ? new Color(1f, 0.90f, 0.35f) : rarityColor;
            GUI.Label(new Rect(rowRect.x + 50f, rowRect.y + 4f, rowRect.width - 190f, 20f), it.GetName(), headerStyle);
            GUI.color = Color.white;

            // Details Line
            string rarityName = UpgraderGambleLogic.GetRarityDisplayName(it.rarity);
            string valStr = $"Базовая ценность: {UpgraderGambleLogic.GetItemValue(it):F0}";
            GUI.Label(new Rect(rowRect.x + 50f, rowRect.y + 24f, 220f, 18f), $"{rarityName}  •  {valStr}", subtitleStyle);

            // Right side count badge
            int ownedCount = inv != null && inv.itemInventory != null ? inv.itemInventory.GetAmount(it.eItem) : 0;
            if (isSource)
            {
                Rect countPill = new Rect(rowRect.x + rowRect.width - 64f, rowRect.y + 11f, 56f, 24f);
                DrawPanel(countPill, new Color(0.14f, 0.22f, 0.30f, 0.9f), new Color(0.3f, 0.65f, 0.9f, 0.7f), 1);
                GUI.Label(countPill, $"x{ownedCount}", badgeStyle);
            }
            else
            {
                string targetStatus = ownedCount > 0 ? $"есть {ownedCount}" : "новый";
                Color statusBg = ownedCount > 0 ? new Color(0.12f, 0.16f, 0.22f, 0.9f) : new Color(0.10f, 0.20f, 0.14f, 0.9f);
                Color statusBorder = ownedCount > 0 ? new Color(0.3f, 0.5f, 0.7f, 0.6f) : new Color(0.3f, 0.8f, 0.4f, 0.6f);
                Rect statusPill = new Rect(rowRect.x + rowRect.width - 78f, rowRect.y + 11f, 70f, 24f);
                DrawPanel(statusPill, statusBg, statusBorder, 1);
                GUI.Label(statusPill, targetStatus, badgeStyle);
            }

            curY += itemRowHeight;
        }

        GUI.EndScrollView();

        if (filtered.Count == 0)
        {
            string emptyMsg = isSource
                ? "В инвентаре пока нет предметов этой категории."
                : "В пуле сундуков нет подходящих целей.";
            GUI.Label(new Rect(rect.x + 20f, rect.y + rect.height * 0.5f - 15f, rect.width - 40f, 30f), emptyMsg, subtitleStyle);
        }

        if (isSource)
            sourceScroll = scroll;
        else
            targetScroll = scroll;
    }

    private void DrawRarityTabs(Rect rect, bool isSource)
    {
        string[] labels = { "Все", "Обычные", "Редкие", "Эпические", "Легендарные" };
        int[] filterVals = { -1, (int)EItemRarity.Common, (int)EItemRarity.Rare, (int)EItemRarity.Epic, (int)EItemRarity.Legendary };
        Color[] tabColors = {
            Color.white,
            UpgraderGambleLogic.GetRarityColor(EItemRarity.Common),
            UpgraderGambleLogic.GetRarityColor(EItemRarity.Rare),
            UpgraderGambleLogic.GetRarityColor(EItemRarity.Epic),
            UpgraderGambleLogic.GetRarityColor(EItemRarity.Legendary)
        };

        int current = isSource ? sourceRarityFilter : targetRarityFilter;
        float tabW = rect.width / labels.Length;

        for (int i = 0; i < labels.Length; i++)
        {
            Rect tRect = new Rect(rect.x + i * tabW, rect.y, tabW - 3f, rect.height);
            bool isActive = current == filterVals[i];

            Color tabBg = isActive ? new Color(0.18f, 0.22f, 0.30f, 0.95f) : new Color(0.10f, 0.12f, 0.15f, 0.80f);
            Color tabBorder = isActive ? tabColors[i] : new Color(0.18f, 0.21f, 0.26f, 0.7f);
            DrawPanel(tRect, tabBg, tabBorder, 1);

            if (isActive)
            {
                // Active indicator bar at bottom
                DrawRect(new Rect(tRect.x + 4f, tRect.y + tRect.height - 3f, tRect.width - 8f, 2f), tabColors[i]);
            }

            GUI.color = isActive ? tabColors[i] : new Color(0.75f, 0.80f, 0.85f);
            if (GUI.Button(tRect, labels[i], isActive ? tabButtonActiveStyle : tabButtonStyle))
            {
                if (isSource)
                    sourceRarityFilter = filterVals[i];
                else
                    targetRarityFilter = filterVals[i];
            }
            GUI.color = Color.white;
        }
    }

    private void DrawActionPanel(Rect rect)
    {
        // Panel Background
        DrawPanel(rect, new Color(0.05f, 0.06f, 0.08f, 0.95f), new Color(0.20f, 0.24f, 0.32f, 1f), 1);

        float pad = 12f;
        float cardW = 330f;
        float cardH = rect.height - pad * 2f;

        // Source Preview Box
        Rect sourceBox = new Rect(rect.x + pad, rect.y + pad, cardW, cardH);
        DrawPreviewCard(sourceBox, selectedSource, true);

        // Target Preview Box
        Rect targetBox = new Rect(rect.x + rect.width - pad - cardW, rect.y + pad, cardW, cardH);
        DrawPreviewCard(targetBox, selectedTarget, false);

        // Center Action Area
        float centerX = sourceBox.x + cardW + 14f;
        float centerW = targetBox.x - centerX - 14f;
        Rect centerRect = new Rect(centerX, rect.y + pad, centerW, cardH);
        DrawPanel(centerRect, new Color(0.08f, 0.09f, 0.13f, 0.85f), new Color(0.18f, 0.22f, 0.28f, 0.8f), 1);

        bool canRoll = selectedSource != null && selectedTarget != null && selectedSource.eItem != selectedTarget.eItem;
        float chance = canRoll ? UpgraderGambleLogic.CalculateSuccessChance(selectedSource, selectedTarget) : 0f;

        // Title
        GUI.Label(new Rect(centerRect.x, centerRect.y + 10f, centerRect.width, 22f), "ВЕРОЯТНОСТЬ УЛУЧШЕНИЯ", columnTitleStyle);

        if (canRoll)
        {
            Color chanceColor = chance >= 0.60f ? new Color(0.35f, 1f, 0.45f) : (chance >= 0.35f ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 0.35f, 0.35f));

            // Chance Percentage Text
            GUI.color = chanceColor;
            GUI.Label(new Rect(centerRect.x, centerRect.y + 32f, centerRect.width, 36f), $"{chance * 100f:F1}%", chanceLabelStyle);
            GUI.color = Color.white;

            // Chance Progress Bar
            Rect barBg = new Rect(centerRect.x + 24f, centerRect.y + 72f, centerRect.width - 48f, 14f);
            DrawPanel(barBg, new Color(0.06f, 0.07f, 0.09f, 1f), new Color(0.20f, 0.24f, 0.32f, 1f), 1);
            Rect barFill = new Rect(barBg.x + 2f, barBg.y + 2f, (barBg.width - 4f) * Mathf.Clamp01(chance), barBg.height - 4f);
            DrawRect(barFill, chanceColor);

            // Ratio calculation details
            float sVal = UpgraderGambleLogic.GetItemValue(selectedSource);
            float tVal = UpgraderGambleLogic.GetItemValue(selectedTarget);
            GUI.Label(new Rect(centerRect.x, centerRect.y + 92f, centerRect.width, 20f), $"Ценность: {sVal:F0} -> {tVal:F0} (множитель x{UpgraderConfig.SourceValueMultiplier.Value:F2})", subtitleStyle);
        }
        else
        {
            string selectHint = selectedSource == null
                ? "← Выберите исходный предмет слева"
                : "Выберите целевой предмет справа →";
            GUI.Label(new Rect(centerRect.x, centerRect.y + 44f, centerRect.width, 24f), selectHint, subtitleStyle);
            GUI.Label(new Rect(centerRect.x, centerRect.y + 70f, centerRect.width, 20f), "Шанс рассчитается автоматически по ценности", subtitleStyle);
        }

        // Spin Button
        float btnY = centerRect.y + centerRect.height - 56f;
        Rect spinBtnRect = new Rect(centerRect.x + 24f, btnY, centerRect.width - 48f, 44f);

        GUI.enabled = canRoll;
        if (GUI.Button(spinBtnRect, canRoll ? "ЗАПУСТИТЬ РУЛЕТКУ" : "ВЫБЕРИТЕ ПРЕДМЕТЫ", buttonPrimaryStyle))
        {
            StartRoll();
        }
        GUI.enabled = true;
    }

    private void DrawPreviewCard(Rect rect, ItemData item, bool isSource)
    {
        Color panelBg = new Color(0.08f, 0.09f, 0.13f, 0.90f);
        Color panelBorder = isSource ? new Color(0.8f, 0.4f, 0.2f, 0.7f) : new Color(0.2f, 0.7f, 0.9f, 0.7f);
        DrawPanel(rect, panelBg, panelBorder, 1);

        string headerLabel = isSource ? "ИСХОДНЫЙ (СГОРИТ ПРИ НЕУДАЧЕ)" : "ЦЕЛЕВОЙ (НАГРАДА ПРИ ПОБЕДЕ)";
        Color headerColor = isSource ? new Color(1f, 0.75f, 0.4f) : new Color(0.4f, 0.85f, 1f);

        // Header Title
        GUI.color = headerColor;
        GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, 20f), headerLabel, columnTitleStyle);
        GUI.color = Color.white;

        if (item != null)
        {
            // Icon Frame
            Rect iconRect = new Rect(rect.x + 12f, rect.y + 34f, 44f, 44f);
            Color rarityColor = UpgraderGambleLogic.GetRarityColor(item.rarity);
            DrawPanel(iconRect, new Color(0.05f, 0.06f, 0.08f, 0.9f), rarityColor, 1);

            Texture icon = item.GetIcon();
            if (icon != null)
            {
                GUI.DrawTexture(new Rect(iconRect.x + 3f, iconRect.y + 3f, 38f, 38f), icon);
            }

            // Name & Rarity
            GUI.color = rarityColor;
            GUI.Label(new Rect(rect.x + 64f, rect.y + 32f, rect.width - 74f, 22f), item.GetName(), headerStyle);
            GUI.color = Color.white;

            string rarityName = UpgraderGambleLogic.GetRarityDisplayName(item.rarity);
            float itemVal = UpgraderGambleLogic.GetItemValue(item);
            GUI.Label(new Rect(rect.x + 64f, rect.y + 54f, rect.width - 74f, 18f), $"{rarityName}  •  Ценность: {itemVal:F0}", subtitleStyle);

            // Description Box
            Rect descRect = new Rect(rect.x + 10f, rect.y + 86f, rect.width - 20f, rect.height - 96f);
            DrawPanel(descRect, new Color(0.05f, 0.06f, 0.08f, 0.75f), new Color(0.16f, 0.19f, 0.25f, 0.6f), 1);

            string desc = item.GetDescription();
            if (string.IsNullOrEmpty(desc))
            {
                desc = "(Пассивный предмет)";
            }
            GUI.Label(new Rect(descRect.x + 8f, descRect.y + 6f, descRect.width - 16f, descRect.height - 12f), desc, itemDescStyle);
        }
        else
        {
            string emptyMsg = isSource
                ? "← Выберите исходную пассивку из левой колонки"
                : "Выберите целевую пассивку из правой колонки →";
            GUI.Label(new Rect(rect.x + 16f, rect.y + rect.height * 0.5f - 14f, rect.width - 32f, 30f), emptyMsg, subtitleStyle);
        }
    }

    private void DrawRouletteView(Rect win)
    {
        float pad = 20f;
        float centerW = win.width - pad * 2f;
        float top = win.y + 80f;

        DrawPanel(new Rect(win.x + pad, top, centerW, win.height - 100f), new Color(0.06f, 0.07f, 0.10f, 0.95f), new Color(0.22f, 0.26f, 0.35f, 1f), 1);

        // Roulette Status Title
        string spinStatus = isRolling ? "ВРАЩЕНИЕ РУЛЕТКИ..." : (lastRollWon ? "ПОБЕДА!" : "НЕУДАЧА!");
        Color statusColor = isRolling ? new Color(1f, 0.85f, 0.25f) : (lastRollWon ? new Color(0.35f, 1f, 0.45f) : new Color(1f, 0.3f, 0.3f));
        GUI.color = statusColor;
        GUI.Label(new Rect(win.x + pad, top + 18f, centerW, 36f), spinStatus, titleStyle);
        GUI.color = Color.white;

        string sub = isRolling
            ? "Определяется исход улучшения предмета..."
            : (lastRollWon ? "Поздравляем! Предмет успешно улучшен!" : "Исходный предмет был уничтожен.");
        GUI.Label(new Rect(win.x + pad, top + 54f, centerW, 20f), sub, subtitleStyle);

        // Reel Viewport
        float viewportW = Mathf.Min(VIEWPORT_WIDTH, centerW - 40f);
        float viewportH = 160f;
        float viewportX = win.x + pad + (centerW - viewportW) * 0.5f;
        float viewportY = top + 84f;

        Rect viewportRect = new Rect(viewportX, viewportY, viewportW, viewportH);
        DrawPanel(viewportRect, new Color(0.04f, 0.05f, 0.07f, 0.95f), new Color(0.24f, 0.28f, 0.38f, 1f), 2);

        // Clip and Draw Reel Cards
        GUI.BeginGroup(viewportRect);
        float startDrawX = -currentScrollX;

        for (int i = 0; i < rouletteReel.Count; i++)
        {
            float cardX = startDrawX + i * CARD_TOTAL_WIDTH;
            if (cardX + CARD_WIDTH < 0 || cardX > viewportW)
                continue; // outside viewport

            RouletteCard card = rouletteReel[i];
            Rect cardRect = new Rect(cardX, 8f, CARD_WIDTH, viewportH - 16f);

            Color cardBorder = card.IsFailure ? new Color(1f, 0.25f, 0.25f) : UpgraderGambleLogic.GetRarityColor(card.Item.rarity);
            Color cardBg = card.IsFailure ? new Color(0.20f, 0.06f, 0.06f, 0.95f) : new Color(0.09f, 0.11f, 0.16f, 0.95f);
            DrawPanel(cardRect, cardBg, cardBorder, 2);

            if (card.IsFailure)
            {
                GUI.color = new Color(1f, 0.3f, 0.3f);
                GUI.Label(new Rect(cardRect.x, cardRect.y + 24f, cardRect.width, 36f), "✕", titleStyle);
                GUI.Label(new Rect(cardRect.x + 4f, cardRect.y + 70f, cardRect.width - 8f, 32f), "СГОРЕЛ", headerStyle);
                GUI.color = Color.white;
            }
            else if (card.Item != null)
            {
                Texture icon = card.Item.GetIcon();
                if (icon != null)
                {
                    GUI.DrawTexture(new Rect(cardRect.x + (cardRect.width - 48f) * 0.5f, cardRect.y + 14f, 48f, 48f), icon);
                }
                GUI.color = cardBorder;
                GUI.Label(new Rect(cardRect.x + 4f, cardRect.y + 74f, cardRect.width - 8f, 22f), card.Item.GetName(), headerStyle);
                GUI.color = Color.white;
                GUI.Label(new Rect(cardRect.x + 4f, cardRect.y + 98f, cardRect.width - 8f, 18f), UpgraderGambleLogic.GetRarityDisplayName(card.Item.rarity), subtitleStyle);
            }
        }
        GUI.EndGroup();

        // Center Needle Marker
        float needleX = viewportX + viewportW * 0.5f;
        GUI.color = new Color(1f, 0.88f, 0.25f);
        GUI.Label(new Rect(needleX - 12f, viewportY - 16f, 24f, 20f), "▼", headerStyle);
        GUI.Label(new Rect(needleX - 12f, viewportY + viewportH - 4f, 24f, 20f), "▲", headerStyle);
        DrawRect(new Rect(needleX - 1.5f, viewportY, 3f, viewportH), new Color(1f, 0.88f, 0.25f, 0.9f));
        GUI.color = Color.white;

        // Outcome Banner & Controls
        float resultY = viewportY + viewportH + 26f;
        Rect outcomeBox = new Rect(win.x + pad + (centerW - 640f) * 0.5f, resultY, 640f, 44f);
        DrawPanel(outcomeBox, new Color(0.08f, 0.10f, 0.14f, 0.95f), statusColor, 1);
        GUI.color = statusColor;
        GUI.Label(outcomeBox, outcomeMessage, statusLabelStyle);
        GUI.color = Color.white;

        if (hasFinishedRoll)
        {
            float btnW = 190f;
            float btnH = 44f;
            float btnX = win.x + pad + (centerW - (btnW * 2f + 24f)) * 0.5f;
            float btnTop = resultY + 62f;

            if (GUI.Button(new Rect(btnX, btnTop, btnW, btnH), "К ВЫБОРУ", buttonPrimaryStyle))
            {
                isRolling = false;
                hasFinishedRoll = false;
                selectedSource = null;
                selectedTarget = null;
            }

            if (GUI.Button(new Rect(btnX + btnW + 24f, btnTop, btnW, btnH), "ЗАКРЫТЬ [ESC]", buttonSecondaryStyle))
            {
                CloseWindow(true);
            }
        }
    }

    private void InitStyles()
    {
        if (titleStyle != null)
            return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(1.0f, 0.84f, 0.25f) }
        };

        tagStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1.0f, 0.84f, 0.25f) }
        };

        columnTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };

        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.72f, 0.76f, 0.82f) }
        };

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };

        itemDescStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = new Color(0.80f, 0.84f, 0.90f) }
        };

        chanceLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        statusLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            normal = { textColor = Color.white }
        };

        badgeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.9f, 0.95f, 1f) }
        };

        itemCardStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };

        itemCardSelectedStyle = new GUIStyle(itemCardStyle)
        {
            normal = { textColor = new Color(1f, 0.88f, 0.35f) }
        };

        buttonPrimaryStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.88f, 0.35f) }
        };

        buttonSecondaryStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.85f, 0.88f, 0.94f) }
        };

        tabButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.70f, 0.75f, 0.82f) }
        };

        tabButtonActiveStyle = new GUIStyle(tabButtonStyle)
        {
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1.0f, 0.85f, 0.35f) }
        };
    }

    private readonly struct RouletteCard
    {
        public readonly ItemData Item;
        public readonly bool IsFailure;

        public RouletteCard(ItemData item, bool isFailure)
        {
            Item = item;
            IsFailure = isFailure;
        }
    }
}
