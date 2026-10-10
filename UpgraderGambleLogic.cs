using System;
using System.Collections.Generic;
using Assets.Scripts.Inventory__Items__Pickups.Items;
using UnityEngine;

namespace MegaBonkUpgrader;

public static class UpgraderGambleLogic
{
    // Real combat and gameplay effect significance scores (10 to 120)
    private static readonly Dictionary<EItem, float> ItemEffectScores = new()
    {
        // === S+ TIER: GOD / GAME-WARPING EFFECTS (95 - 120) ===
        { EItem.ZaWarudo, 120f },             // Полная остановка времени — абсолютный контроль над полем боя
        { EItem.OverpoweredLamp, 115f },       // Исполнение желаний / мощнейшие глобальные баффы
        { EItem.Anvil, 110f },                 // Бесплатные перманентные улучшения экипировки и оружия
        { EItem.EchoShard, 105f },             // Дублирование всех ударов / эхо-атаки
        { EItem.DemonBlade, 100f },            // Колоссальный скейлинг урона
        { EItem.SoulHarvester, 98f },          // Жатва душ с бесконечным скейлом урона
        { EItem.TimeBracelet, 95f },           // Защита от смерти / заморозка времени при смертельном ударе

        // === S TIER: META-DEFINING / BUILD-CARRYING (70 - 92) ===
        { EItem.BloodyCleaver, 92f },          // Казнь врагов и тяжелое кровотечение
        { EItem.LeechingCrystal, 88f },        // Вампиризм при попадании — ключевой элемент выживания
        { EItem.DemonicSoul, 85f },            // Сбор душ демонов
        { EItem.GoldenGlove, 85f },            // Золотая перчатка — взрывной рост экономики
        { EItem.GoldenShield, 82f },           // Золотой щит — мощнейшая защита
        { EItem.BossBuster, 80f },             // Огромный мультипликатор урона по боссам и элите
        { EItem.Dragonfire, 78f },             // Огненное дыхание дракона / массовый периодический урон
        { EItem.LightningOrb, 76f },           // Цепные молнии, зачищающие весь экран
        { EItem.GiantFork, 75f },              // Мульти-удары / колоссальный сплэш
        { EItem.SluttyCannon, 75f },           // Тяжелая артиллерия
        { EItem.Kevin, 74f },                  // Автономный компаньон Кевин
        { EItem.BobDead, 72f },                // Призыв мертвого Боба
        { EItem.Ghost, 72f },                  // Призрачный спутник
        { EItem.BobsLantern, 70f },            // Фонарь Боба
        { EItem.Clover, 70f },                 // Клевер — множитель удачи на все проки в игре
        { EItem.UnstableTransfusion, 70f },    // Взрывное восстановление

        // === A TIER: HIGH-IMPACT COMBAT MULTIPLIERS (45 - 68) ===
        { EItem.JoesDagger, 68f },             // Высокий критический урон
        { EItem.TacticalGlasses, 65f },        // Тактические очки — шанс и урон крита
        { EItem.GamerGoggles, 62f },           // Геймерские очки — скейлинг критов
        { EItem.EnergyCore, 60f },             // Энергетическое ядро — огромная скорость атаки
        { EItem.GlovePower, 58f },             // Перчатка силы
        { EItem.TurboSocks, 56f },             // Турбо-носки — максимальная маневренность
        { EItem.GloveLightning, 55f },         // Перчатка молнии
        { EItem.Beacon, 54f },                 // Маяк урона
        { EItem.DemonicBlood, 52f },           // Демоническая кровь — урон на грани гибели
        { EItem.HolyBook, 50f },               // Священная книга — орбитальные снаряды
        { EItem.WizardsHat, 50f },             // Шляпа мага — усиление способностей
        { EItem.ToxicBarrel, 48f },            // Токсичная бочка — масштабное отравление
        { EItem.GlovePoison, 48f },            // Ядовитая перчатка
        { EItem.GloveBlood, 48f },             // Кровавая перчатка
        { EItem.GloveCurse, 48f },             // Проклятая перчатка
        { EItem.IceCrystal, 46f },             // Ледяной кристалл — глубокая заморозка
        { EItem.IceCube, 45f },                // Кубик льда — замедление
        { EItem.Medkit, 45f },                 // Аптечка первой помощи
        { EItem.GrandmasSecretTonic, 45f },    // Секретный тоник бабушки
        { EItem.CreditCardRed, 45f },          // Красная карта — скидки и манипуляции в магазине
        { EItem.CreditCardGreen, 45f },        // Зеленая карта — процентный кэшбэк
        { EItem.SpicyMeatball, 44f },          // Острая фрикаделька — взрывы
        { EItem.ElectricPlug, 42f },           // Электро-вилка — шоковый урон

        // === B TIER: SOLID DEFENSE & SUSTAIN (25 - 40) ===
        { EItem.Chonkplate, 40f },             // Тяжелая броня — прямое поглощение урона
        { EItem.SpikyShield, 38f },            // Шипастый щит — броня и возврат урона
        { EItem.BeefyRing, 36f },              // Мясное кольцо — большой запас здоровья
        { EItem.Borgar, 36f },                 // Бургер — максимальное здоровье
        { EItem.GoldenSneakers, 35f },         // Золотые кеды — генерация золота на бегу
        { EItem.GoldenRing, 34f },             // Золотое кольцо
        { EItem.PhantomShroud, 32f },          // Саван фантома — шанс уклонения
        { EItem.CowardsCloak, 30f },           // Плащ труса — спасительное ускорение
        { EItem.Campfire, 30f },               // Костер — регенерация здоровья в ауре
        { EItem.Scarf, 28f },                  // Теплый шарф — восстановление
        { EItem.Rollerblades, 28f },           // Ролики — скорость бега
        { EItem.SpeedBoi, 26f },               // Спидбой — ускорение
        { EItem.EagleClaw, 26f },              // Коготь орла — отбрасывание врагов
        { EItem.BrassKnuckles, 25f },          // Кастет — оглушение
        { EItem.Cactus, 25f },                 // Кактус — шипы
        { EItem.Backpack, 25f },               // Рюкзак — слоты и вместимость
        { EItem.SuckyMagnet, 24f },            // Магнит опыта
        { EItem.Wrench, 24f },                 // Гаечный ключ
        { EItem.ShatteredWisdom, 22f },        // Расколотая мудрость
        { EItem.CursedDoll, 22f },             // Проклятая кукла
        { EItem.Mirror, 20f },                 // Зеркало
        { EItem.Gasmask, 20f },                // Противогаз
        { EItem.WeebHeadset, 20f },            // Аниме-гарнитура

        // === C TIER: MINOR / SITUATIONAL UTILITIES (10 - 18) ===
        { EItem.Beer, 18f },
        { EItem.Oats, 16f },
        { EItem.MoldyCheese, 14f },
        { EItem.Battery, 15f },
        { EItem.SlipperyRing, 14f },
        { EItem.Bonker, 15f },
        { EItem.GymSauce, 16f },
        { EItem.ForbiddenJuice, 18f },
        { EItem.Feathers, 15f },
        { EItem.Skuleg, 16f },
        { EItem.QuinsMask, 18f },
        { EItem.OldMask, 18f },
        { EItem.Snek, 15f },
        { EItem.Pot, 16f },
        { EItem.Pumpkin, 17f },
        { EItem.Key, 12f },
        { EItem.CageKey, 12f },
        { EItem.CryptKey, 12f },
        { EItem.IdleJuice, 14f }
    };

    public static float GetItemEffectScore(ItemData item)
    {
        if (item == null) return 15f;

        if (ItemEffectScores.TryGetValue(item.eItem, out float score))
            return score;

        // Fallback dynamic heuristic from description text for unknown/custom items
        string desc = item.GetDescription()?.ToLowerInvariant() ?? string.Empty;
        float dynamicScore = 20f;
        if (desc.Contains("останов") || desc.Contains("freeze") || desc.Contains("time") || desc.Contains("бессмерт"))
            dynamicScore += 75f;
        if (desc.Contains("казн") || desc.Contains("уничтож") || desc.Contains("execute") || desc.Contains("урон"))
            dynamicScore += 40f;
        if (desc.Contains("вампир") || desc.Contains("lifesteal") || desc.Contains("краж") || desc.Contains("лечен"))
            dynamicScore += 35f;
        if (desc.Contains("крит") || desc.Contains("crit"))
            dynamicScore += 30f;
        if (desc.Contains("снаряд") || desc.Contains("projectile") || desc.Contains("молни"))
            dynamicScore += 25f;

        return Mathf.Clamp(dynamicScore, 10f, 120f);
    }

    public static string GetEffectRankDisplayName(float effectScore)
    {
        if (effectScore >= 95f) return "S+ [БОГОПОДОБНЫЙ]";
        if (effectScore >= 70f) return "S [ВЫСШИЙ ТИР]";
        if (effectScore >= 45f) return "A [БОЕВОЙ ТИР]";
        if (effectScore >= 25f) return "B [ПОДДЕРЖКА]";
        return "C [БАЗОВЫЙ]";
    }

    public static Color GetEffectRankColor(float effectScore)
    {
        if (effectScore >= 95f) return new Color(1.0f, 0.84f, 0.25f); // Radiant Gold
        if (effectScore >= 70f) return new Color(0.85f, 0.40f, 1.0f);  // Royal Purple
        if (effectScore >= 45f) return new Color(0.35f, 0.75f, 1.0f);  // Sky Blue
        if (effectScore >= 25f) return new Color(0.40f, 0.85f, 0.50f); // Emerald Green
        return new Color(0.70f, 0.74f, 0.80f);                         // Slate
    }

    public static float GetItemValue(ItemData item)
    {
        if (item == null) return 0f;

        float effectScore = GetItemEffectScore(item);

        // Base nominal value derived from nominal rarity
        float baseRarityVal = item.rarity switch
        {
            EItemRarity.Common => 15f,
            EItemRarity.Rare => 35f,
            EItemRarity.Epic => 75f,
            EItemRarity.Legendary => 150f,
            EItemRarity.Corrupted => 200f,
            _ => 15f
        };

        // Scarcity bonus: items that only drop once or a few times per run are scarce
        float scarcityBonus = 0f;
        if (UpgraderConfig.UseDynamicRarity.Value)
        {
            if (item.maxAmountPerRun == 1)
            {
                scarcityBonus += 40f;
            }
            else if (item.maxAmountPerRun > 0 && item.maxAmountPerRun <= 3)
            {
                scarcityBonus += 20f;
            }
        }

        float priorityBonus = 1f + Mathf.Clamp(item.itemTickPriority, 0, 10) * 0.015f;

        // Dynamic True Value = (Rarity * 0.35 + EffectScore * 1.15 + Scarcity) * Priority
        float finalValue = (baseRarityVal * 0.35f + effectScore * 1.15f + scarcityBonus) * priorityBonus;
        return Mathf.Max(10f, finalValue);
    }

    public static EItemRarity GetDynamicRarity(ItemData item)
    {
        if (item == null) return EItemRarity.Common;
        if (!UpgraderConfig.UseDynamicRarity.Value) return item.rarity;

        float val = GetItemValue(item);
        if (val >= 140f) return EItemRarity.Legendary;
        if (val >= 75f) return EItemRarity.Epic;
        if (val >= 38f) return EItemRarity.Rare;
        return EItemRarity.Common;
    }

    public static int GetMaxAllowedQuantity(ItemData item)
    {
        if (item == null) return 1;

        if (item.maxAmountPerRun == 1 || item.maxAmount == 1)
            return 1;

        int configMax = UpgraderConfig.MaxTargetQuantity != null ? UpgraderConfig.MaxTargetQuantity.Value : 50;
        int limit = configMax;

        if (item.maxAmount > 1 && item.maxAmount < limit)
            limit = item.maxAmount;
        if (item.maxAmountPerRun > 1 && item.maxAmountPerRun < limit)
            limit = item.maxAmountPerRun;

        return Mathf.Max(1, limit);
    }

    public static float CalculateSuccessChance(ItemData source, ItemData target, int sourceCount = 1, int targetCount = 1, int goldBet = 0)
    {
        if (source == null || target == null) return 0f;
        if (sourceCount < 1) sourceCount = 1;
        if (targetCount < 1) targetCount = 1;

        float min = Mathf.Clamp01(UpgraderConfig.MinimumChance.Value);
        float max = Mathf.Clamp01(UpgraderConfig.MaximumChance.Value);
        if (min > max)
        {
            (min, max) = (max, min);
        }

        float sourceVal = GetItemValue(source) * sourceCount;
        float targetVal = GetItemValue(target) * targetCount;

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
