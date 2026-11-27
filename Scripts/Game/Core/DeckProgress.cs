// 文件：DeckProgress.cs
// 描述：牌组的永久变化。目前只有「移除」关键字的牌会落到这里——
//       打出去就从牌组永远消失，下一场也不会再出现。
//
// 为什么单独存一份：牌组来自 CharacterConfig（配置表，只读），
// 玩家的损耗必须写到存档里，不能改配置。

using System.Collections.Generic;
using TinyFramework;

public class DeckProgress : SaveData
{
    /// <summary>每位侠客被永久移除的牌（CardConfig.Id）。允许重复：同名牌移除两张就记两次。</summary>
    public Dictionary<int, List<int>> Removed = new Dictionary<int, List<int>>();
}

public static class DeckStore
{
    private static DeckProgress _data;

    public static DeckProgress Data
    {
        get
        {
            if (_data == null)
            {
                _data = SaveManager.Load<DeckProgress>();
                if (_data == null)
                {
                    _data = new DeckProgress();
                }
            }
            return _data;
        }
    }

    public static List<int> RemovedOf(int characterId)
    {
        List<int> list;
        if (Data.Removed.TryGetValue(characterId, out list))
        {
            return list;
        }

        list = new List<int>();
        Data.Removed[characterId] = list;
        return list;
    }

    /// <summary>把本场移除的牌记进存档。</summary>
    public static void AddRemoved(int characterId, List<int> cardIds)
    {
        if (cardIds == null || cardIds.Count == 0)
        {
            return;
        }

        List<int> list = RemovedOf(characterId);
        for (int i = 0; i < cardIds.Count; i++)
        {
            list.Add(cardIds[i]);
        }

        SaveManager.Save(Data);
    }

    /// <summary>复原：把所有人的牌组损失清空（调试与"重开一局"用）。</summary>
    public static void ResetAll()
    {
        Data.Removed.Clear();
        SaveManager.Save(Data);
    }

    /// <summary>按移除记录从牌组里实际扣牌（同名牌只扣一张）。</summary>
    public static List<int> BuildDeck(int characterId, int[] sourceDeck)
    {
        List<int> deck = new List<int>();
        if (sourceDeck != null)
        {
            for (int i = 0; i < sourceDeck.Length; i++)
            {
                deck.Add(sourceDeck[i]);
            }
        }

        List<int> removed = RemovedOf(characterId);
        for (int i = 0; i < removed.Count; i++)
        {
            int index = deck.IndexOf(removed[i]);
            if (index >= 0)
            {
                deck.RemoveAt(index);
            }
        }

        return deck;
    }
}
