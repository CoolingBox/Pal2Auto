// 文件：DeckBattle.cs
// 描述：卡组战斗内核（纯逻辑，不碰节点）。规则见 Docs/卡组战斗设计.md。
//
// 敌我同构：双方都是 5 张手牌 + 独立倒计时 + 打出补抽 + 弃牌堆洗回。
// 唯一的差别是牌组内容——敌方用 EnemyConfig.Deck，玩家用 GameFlow 里选定的牌组。

using System;
using System.Collections.Generic;
using Godot;
using TinyFramework;

public enum BattleSide
{
    Player,
    Enemy,
}

public enum BattleEventKind
{
    Play,       // 一张牌打出（Side = 打出方，带 Slot 与 Card）
    Draw,       // 补抽进某个槽位（Side = 抽牌方）
    Damage,     // 掉血（Side = 挨打方）
    Shield,     // 获得护体（Side = 获得方）
    Heal,       // 回复（Side = 回复方）
    Poison,     // 中毒结算/叠加（Side = 受影响方）
    Expose,     // 破绽叠层（Side = 受影响方）
    End,
}

public struct BattleEvent
{
    public BattleEventKind Kind;
    public BattleSide Side;
    public int Slot;
    public int Value;
    public string Text;
    public CardConfig Card;
    public bool IgnoredShield;
    public SpellElement Element;   // 枚举默认值就是 None（物理伤害）
}

public sealed class HandSlot
{
    public CardConfig Card;
    public float Countdown;
    public float CountdownMax;

    public bool Occupied
    {
        get { return Card != null; }
    }

    public float Progress
    {
        get
        {
            if (CountdownMax <= 0.001f)
            {
                return 0f;
            }
            return Mathf.Clamp(1f - Countdown / CountdownMax, 0f, 1f);
        }
    }
}

public sealed class FighterState
{
    public string Name = "";
    public string Portrait = "";
    public int MaxHp;
    public int Hp;
    public int Shield;
    public int PoisonStacks;
    public float PoisonAccum;

    /// <summary>破绽层数：每层使其受到的所有伤害 +15%，最多 3 层，4 秒后清零。</summary>
    public int ExposeStacks;
    public float ExposeTime;

    public HandSlot[] Hand;
    public List<CardConfig> DrawPile = new List<CardConfig>();
    public List<CardConfig> DiscardPile = new List<CardConfig>();

    public bool Dead
    {
        get { return Hp <= 0; }
    }

    public float HpRatio
    {
        get
        {
            if (MaxHp <= 0)
            {
                return 0f;
            }
            return Mathf.Clamp((float)Hp / MaxHp, 0f, 1f);
        }
    }
}

public sealed class DeckBattle
{
    public const int HandSize = 5;
    public const float PoisonPerStackPerSecond = 1f;
    public const float TimeLimit = 60f;

    /// <summary>护体上限。不封顶的话守牌会把伤害全吃掉，双方都打不死。</summary>
    public const int ShieldCap = 20;

    public const int ExposeMaxStacks = 3;
    public const float ExposeDuration = 4f;
    public const float ExposePerStack = 0.15f;

    public FighterState Player = new FighterState();
    public FighterState Enemy = new FighterState();

    public bool Finished;
    public bool TimedOut;
    public bool PlayerWon;

    public float Elapsed;
    public int PlayerPlayed;
    public int EnemyPlayed;
    public int DamageToPlayer;
    public int DamageToEnemy;
    public int ShieldGained;

    private readonly Random _rng = new Random();

    public static DeckBattle Create(string playerName, string playerPortrait, int playerHp,
        List<int> playerDeck, string enemyName, string enemyPortrait, int enemyHp, int[] enemyDeck)
    {
        DeckBattle battle = new DeckBattle();
        battle.Player = BuildFighter(playerName, playerPortrait, playerHp, playerDeck);
        battle.Enemy = BuildFighter(enemyName, enemyPortrait, enemyHp, new List<int>(enemyDeck));
        battle.DrawOpeningHand();
        return battle;
    }

    private static FighterState BuildFighter(string name, string portrait, int maxHp, List<int> cardIds)
    {
        FighterState fighter = new FighterState();
        fighter.Name = name;
        fighter.Portrait = portrait;
        fighter.MaxHp = maxHp;
        fighter.Hp = maxHp;
        fighter.Hand = new HandSlot[HandSize];

        for (int i = 0; i < HandSize; i++)
        {
            fighter.Hand[i] = new HandSlot();
        }

        for (int i = 0; i < cardIds.Count; i++)
        {
            CardConfig cfg = ConfigManager.GetOne<CardConfig>(cardIds[i]);
            if (cfg == null)
            {
                continue;
            }
            fighter.DrawPile.Add(cfg);
        }

        Shuffle(fighter.DrawPile, new Random());
        return fighter;
    }

    private static void Shuffle(List<CardConfig> pile, Random rng)
    {
        for (int i = pile.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            CardConfig tmp = pile[i];
            pile[i] = pile[j];
            pile[j] = tmp;
        }
    }

    private void DrawOpeningHand()
    {
        List<BattleEvent> ignored = new List<BattleEvent>();
        for (int slot = 0; slot < HandSize; slot++)
        {
            DrawInto(Player, slot, ignored);
            DrawInto(Enemy, slot, ignored);
        }
    }

    public List<BattleEvent> Step(float dt)
    {
        List<BattleEvent> events = new List<BattleEvent>();
        if (Finished)
        {
            return events;
        }

        Elapsed += dt;

        TickFighter(Player, Enemy, dt, events);
        TickFighter(Enemy, Player, dt, events);

        CheckEnd(events);
        return events;
    }

    private void TickFighter(FighterState owner, FighterState foe, float dt, List<BattleEvent> events)
    {
        if (owner.Dead)
        {
            return;
        }

        TickExpose(owner, dt);
        TickPoison(owner, dt, events);
        TickHand(owner, foe, dt, events);
    }

    private void TickExpose(FighterState owner, float dt)
    {
        if (owner.ExposeTime <= 0f)
        {
            return;
        }

        owner.ExposeTime -= dt;
        if (owner.ExposeTime <= 0f)
        {
            owner.ExposeTime = 0f;
            owner.ExposeStacks = 0;
        }
    }

    private void TickPoison(FighterState owner, float dt, List<BattleEvent> events)
    {
        if (owner.PoisonStacks <= 0)
        {
            return;
        }

        owner.PoisonAccum += owner.PoisonStacks * PoisonPerStackPerSecond * dt;
        if (owner.PoisonAccum < 1f)
        {
            return;
        }

        int tick = (int)owner.PoisonAccum;
        owner.PoisonAccum -= tick;

        int lost = ApplyDamage(owner, tick, false);
        if (lost > 0)
        {
            BattleEvent poisonEvent = new BattleEvent();
            poisonEvent.Kind = BattleEventKind.Poison;
            poisonEvent.Side = SideOf(owner);
            poisonEvent.Slot = -1;
            poisonEvent.Value = lost;
            poisonEvent.Text = "中毒 -" + lost;
            events.Add(poisonEvent);
        }
    }

    private void TickHand(FighterState owner, FighterState foe, float dt, List<BattleEvent> events)
    {
        for (int slot = 0; slot < HandSize; slot++)
        {
            HandSlot handSlot = owner.Hand[slot];
            if (!handSlot.Occupied)
            {
                continue;
            }

            handSlot.Countdown -= dt;
            if (handSlot.Countdown > 0f)
            {
                continue;
            }

            CardConfig card = handSlot.Card;
            ResolveCard(owner, foe, card, slot, events);

            owner.DiscardPile.Add(card);
            handSlot.Card = null;
            handSlot.Countdown = 0f;

            if (ReferenceEquals(owner, Player))
            {
                PlayerPlayed++;
            }
            else
            {
                EnemyPlayed++;
            }

            DrawInto(owner, slot, events);
        }
    }

    private void ResolveCard(FighterState owner, FighterState foe, CardConfig card, int slot, List<BattleEvent> events)
    {
        BattleEvent playEvent = new BattleEvent();
        playEvent.Kind = BattleEventKind.Play;
        playEvent.Side = SideOf(owner);
        playEvent.Slot = slot;
        playEvent.Card = card;
        playEvent.Text = card.Name;
        events.Add(playEvent);

        if (card.Damage > 0)
        {
            int dealt = ApplyDamage(foe, card.Damage, card.IgnoreShield);
            AddDamageEvent(owner, foe, slot, card, dealt, SpellElement.None, events);
        }

        if (card.Spell > 0)
        {
            int spellDamage = ApplyDamage(foe, card.Spell, false);
            AddDamageEvent(owner, foe, slot, card, spellDamage, card.Element, events);
        }

        if (card.Shield > 0)
        {
            owner.Shield = Math.Min(ShieldCap, owner.Shield + card.Shield);
            if (ReferenceEquals(owner, Player))
            {
                ShieldGained += card.Shield;
            }

            BattleEvent sh = new BattleEvent();
            sh.Kind = BattleEventKind.Shield;
            sh.Side = SideOf(owner);
            sh.Slot = slot;
            sh.Value = card.Shield;
            sh.Text = "护体 +" + card.Shield;
            events.Add(sh);
        }

        if (card.Heal > 0)
        {
            int before = owner.Hp;
            owner.Hp = Math.Min(owner.MaxHp, owner.Hp + card.Heal);

            BattleEvent heal = new BattleEvent();
            heal.Kind = BattleEventKind.Heal;
            heal.Side = SideOf(owner);
            heal.Slot = slot;
            heal.Value = owner.Hp - before;
            heal.Text = "回气 +" + (owner.Hp - before);
            events.Add(heal);
        }

        if (card.Expose > 0)
        {
            foe.ExposeStacks = Math.Min(ExposeMaxStacks, foe.ExposeStacks + card.Expose);
            foe.ExposeTime = ExposeDuration;

            BattleEvent expose = new BattleEvent();
            expose.Kind = BattleEventKind.Expose;
            expose.Side = SideOf(foe);
            expose.Slot = slot;
            expose.Value = foe.ExposeStacks;
            expose.Text = "破绽 " + foe.ExposeStacks;
            events.Add(expose);
        }

        if (card.Poison > 0)
        {
            foe.PoisonStacks = Math.Min(20, foe.PoisonStacks + card.Poison);

            BattleEvent poison = new BattleEvent();
            poison.Kind = BattleEventKind.Poison;
            poison.Side = SideOf(foe);
            poison.Slot = slot;
            poison.Value = card.Poison;
            poison.Text = "中毒 +" + card.Poison;
            events.Add(poison);
        }
    }

    private void AddDamageEvent(FighterState owner, FighterState foe, int slot, CardConfig card,
        int hpDamage, SpellElement element, List<BattleEvent> events)
    {
        BattleEvent dmg = new BattleEvent();
        dmg.Kind = BattleEventKind.Damage;
        dmg.Side = SideOf(foe);
        dmg.Slot = slot;
        dmg.Value = hpDamage > 0 ? hpDamage : (element != SpellElement.None ? card.Spell : card.Damage);
        dmg.IgnoredShield = card.IgnoreShield;
        dmg.Element = element;
        dmg.Text = card.Name;
        events.Add(dmg);

        if (ReferenceEquals(foe, Player))
        {
            DamageToPlayer += hpDamage;
        }
        else
        {
            DamageToEnemy += hpDamage;
        }
    }

    /// <summary>把一个槽位补满：先抽牌堆，空了就把弃牌堆洗回去再抽；两边都空就留空。</summary>
    private void DrawInto(FighterState owner, int slot, List<BattleEvent> events)
    {
        if (owner.Hand[slot].Occupied)
        {
            return;
        }

        if (owner.DrawPile.Count == 0)
        {
            if (owner.DiscardPile.Count == 0)
            {
                return;
            }

            for (int i = 0; i < owner.DiscardPile.Count; i++)
            {
                owner.DrawPile.Add(owner.DiscardPile[i]);
            }
            owner.DiscardPile.Clear();
            Shuffle(owner.DrawPile, _rng);
        }

        CardConfig card = owner.DrawPile[0];
        owner.DrawPile.RemoveAt(0);

        HandSlot handSlot = owner.Hand[slot];
        handSlot.Card = card;
        handSlot.CountdownMax = card.Countdown;
        handSlot.Countdown = card.Countdown;

        BattleEvent draw = new BattleEvent();
        draw.Kind = BattleEventKind.Draw;
        draw.Side = SideOf(owner);
        draw.Slot = slot;
        draw.Card = card;
        draw.Text = card.Name;
        events.Add(draw);
    }

    private BattleSide SideOf(FighterState unit)
    {
        return ReferenceEquals(unit, Player) ? BattleSide.Player : BattleSide.Enemy;
    }

    /// <summary>敌方最先要打出的那张牌，用于"下一手"提示。</summary>
    public CardConfig EnemyFastestCard()
    {
        CardConfig best = null;
        float bestTime = float.MaxValue;

        for (int i = 0; i < Enemy.Hand.Length; i++)
        {
            HandSlot slot = Enemy.Hand[i];
            if (!slot.Occupied)
            {
                continue;
            }
            if (slot.Countdown < bestTime)
            {
                bestTime = slot.Countdown;
                best = slot.Card;
            }
        }

        return best;
    }

    private static int ApplyDamage(FighterState target, int amount, bool ignoreShield)
    {
        if (amount <= 0 || target.Hp <= 0)
        {
            return 0;
        }

        // 破绽：目标身上每层 +15% 受伤，先放大再加护体吸收
        if (target.ExposeStacks > 0)
        {
            float mult = 1f + ExposePerStack * target.ExposeStacks;
            amount = Mathf.RoundToInt(amount * mult);
        }

        int remaining = amount;
        if (!ignoreShield && target.Shield > 0)
        {
            int absorbed = Math.Min(target.Shield, remaining);
            target.Shield -= absorbed;
            remaining -= absorbed;
        }

        if (remaining > 0)
        {
            target.Hp -= remaining;
            if (target.Hp < 0)
            {
                target.Hp = 0;
            }
        }

        return remaining;
    }

    private void CheckEnd(List<BattleEvent> events)
    {
        if (Finished)
        {
            return;
        }

        bool playerDead = Player.Dead;
        bool enemyDead = Enemy.Dead;

        if (!playerDead && !enemyDead && Elapsed < TimeLimit)
        {
            return;
        }

        Finished = true;
        TimedOut = !playerDead && !enemyDead;

        if (TimedOut)
        {
            PlayerWon = Player.HpRatio > Enemy.HpRatio;
        }
        else
        {
            PlayerWon = enemyDead && !playerDead;
        }

        BattleEvent end = new BattleEvent();
        end.Kind = BattleEventKind.End;
        end.Side = PlayerWon ? BattleSide.Player : BattleSide.Enemy;
        end.Text = TimedOut ? "时辰已尽" : (PlayerWon ? "胜" : "败");
        events.Add(end);
    }
}
