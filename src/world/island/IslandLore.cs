namespace SeaAnomaly;

using System;
using System.Collections.Generic;

/// <summary>
///   Worldbuilding lore scattered across the product archipelago — one
///   readable <see cref="StoryInteractable"/> per non-tutorial island
///   (E 交互阅读，一次性字幕，StoryPointId 留空、纯叙述不进 quest 链),
///   except <see cref="RuinQuestLogs"/> which is the ordered three-log
///   chapter-2 quest chain on the Ruin island (final log raises "ruin").
///   Texts live here as data instead of being hardcoded in the
///   <see cref="IslandBuilder"/> switch, so the builder stays mechanical.
///   Each tier keeps 1–2 candidate texts; when two exist the island's own
///   deterministic RNG picks one, so the same world seed still reproduces
///   the same archipelago. Style: 残缺、短句、省略号、不解释清楚.
/// </summary>
public static class IslandLore
{
    /// <summary>
    ///   Ordered Ruin-island quest logs (chapter 2). Logs 0–1 are narration
    ///   only; log 2 carries <c>StoryPointId = "ruin"</c> and completes
    ///   <c>quest_ruin</c> on first read. Never shuffled — same seed, same order.
    /// </summary>
    public static readonly string[] RuinQuestLogs =
    {
        "石阶裂开，旧灶台还在。『……我们在这里住过三个雨季。渔网、晒架、孩子的名字……潮水涨过门槛那晚，人先走了。』灰烬里没有骨头。",
        "墙根堆着裂开的陶罐，内侧结着盐霜。『……夜里林子里有脚步，不像兽，也不像人……井水变苦。别喝东边那口。』刻痕到一半断了。",
        "最高的石碑几乎被藤蔓吞没。『……它们从密林里来。会学我们的影子走路……别应声。带刃的去更远处那片黑林——斩断一只，才知道它们怕什么。』碑脚画着一张歪斜的岛图。"
    };

    /// <summary>Candidate log texts per island tier. Tutorial / Spawn are intentionally absent.</summary>
    public static readonly IReadOnlyDictionary<IslandTier, string[]> LoreTexts =
        new Dictionary<IslandTier, string[]>
        {
            // 主线岛 —— 引导者的信号。
            [IslandTier.Main] = new[]
            {
                "电文断断续续。『……能听到吗？风暴中心有呼吸……旧石头记得答案……先活下来。』信号塔的灯还亮着。"
            },

            // 主线岛 —— 种不下去的地，走了的生存者。
            [IslandTier.Harvest] = new[]
            {
                "稻草人怀里揣着一本泡烂的日记。『……第三十次播种。秧苗一夜枯死——海水是咸的，雨也是……后来的人，田里什么都没剩下，只剩风在数浪。』",
                "田埂上钉着一块木牌，字被晒得发白。『……今年的收成喂了海。别问海为什么收租……它不是第一次来。』"
            },

            // Ruin quest logs live in RuinQuestLogs (ordered, three tablets).
            // Keep a Pick() fallback so non-quest callers stay valid.
            [IslandTier.Ruin] = RuinQuestLogs,

            // 主线岛 —— 变异的林子，撤离者的话。
            [IslandTier.Mutant] = new[]
            {
                "树干上钉着一面锈铁皮，漆剥落了大半。『……别吃这里的果子。别在林子里过夜。树影比树多……我们撤向东边，能走的都走了。』",
                "一本猎户手册散在落叶里，页脚重复着同一句话，一遍比一遍潦草。『……它们在学我们说话。别应声。别应声。』"
            },

            // 主线岛 —— 鲨王，风暴中心。
            [IslandTier.Storm] = new[]
            {
                "半截桅杆上绑着油布包，里面是船长的最后几页。『……它在风暴里呼吸。浪是它的肋骨，雷是它的喉音……老人说，斩断呼吸。我们把船留在了这里。』"
            },

            // 探索岛 —— 猎人的去向。
            [IslandTier.Wild] = new[]
            {
                "兽皮卷压在石头下，炭条画的地图已经晕开。『……鹿群不怕人，怕海。月圆那晚它们全往山里跑……陷阱埋在北坡，回不来的话，替我收。』",
                "一棵老树刻满了正字，最深的一道旁边刻着：『……第三百天。对岸的灯还亮着，不知道是不是人点的……明天再数一遍。』"
            },

            // 探索岛 —— 沉在水下的旧城。
            [IslandTier.Atoll] = new[]
            {
                "漂流木上刻着几行小字，被盐浸得发胀。『……环礁是旧城的屋顶。退潮时能看见街道……下海的人，别数窗子里的灯。』",
                "玻璃瓶里的纸条只剩半截。『……水涨上来之前，这里有人晒鱼、晾网、骂孩子……如果你读到这个，替我们记住：海原来是有边的。』"
            },

            // 探索岛 —— 沉船的航海日志。
            [IslandTier.Wreck] = new[]
            {
                "船长室的铁盒里躺着航海日志，墨迹被海水洇成了云。『……罗盘从第三天开始打转。不是我们迷了路，是海换了方向……货物不重要了。把名字刻在桅杆上。』",
                "舱壁内侧刻着一排名字，最下面一行还新。『……船不是沉了，是被留下了。潮水退进龙骨那晚，我们都听见了呼吸……』"
            },

            // 彩蛋岛 —— 火山的祭文，放在高海拔。
            [IslandTier.Volcano] = new[]
            {
                "玄武岩壁上烧灼出的字，石头至今温热。『……山先醒的。火熄的那天，海开始涨……祭品不是献给山的——是献给山底下压着的东西。』"
            },

            // 彩蛋岛 —— 冰下的信号，放在高海拔。
            [IslandTier.Polar] = new[]
            {
                "冻在冰里的笔记本，字迹完好得像昨天写的。『……信号不是从塔上来的，是从冰下面。它数着我们的心跳发报……别回应。别回应。别回应。』"
            }
        };

    /// <summary>
    ///   Picks the log text for one island. Single-candidate tiers consume
    ///   no RNG draws, keeping the placement stream bit-identical for the
    ///   islands whose layout was already landed; two-candidate tiers draw
    ///   from the island's own seeded stream (deterministic per world seed).
    ///   Ruin uses <see cref="RuinQuestLogs"/> via the builder, not Pick.
    /// </summary>
    public static string Pick(IslandTier tier, Random rng)
    {
        var texts = LoreTexts[tier];
        return texts.Length == 1 ? texts[0] : texts[rng.Next(texts.Length)];
    }
}
