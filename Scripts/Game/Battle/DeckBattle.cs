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
    HandMod,    // 上场数变化（Side = 受影响方，Value = 变化后的值）
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

    /// <summary>本次实际掉掉的气血。Value 是"有伤害就显示伤害"的展示值，
    /// 护体全挡住时两者不同：记录要的是真实掉血。</summary>
    public int HpLoss;

    /// <summary>被护体吸收掉的量。负伤日志里的"挡 N"。</summary>
    public int Absorbed;

    /// <summary>上场数事件是不是"限时到期收回"（日志措辞不同）。</summary>
    public bool Revert;
}

/// <summary>一方的战斗统计。界面"统计"页与结算面板都读这里，不各自再算一份。</summary>
public sealed class FightStats
{
    public int Played;
    public int DamageDealt;
    public int DamageTaken;
    public int ShieldGained;
    public int Healed;
    public int PoisonDamage;
    public int ExposeApplied;
    public float FirstPlayAt = -1f;

    /// <summary>平均每手打掉多少气血。没出手时为 0，避免除零。</summary>
    public float AverageDamage
    {
        get
        {
            if (Played <= 0)
            {
                return 0f;
            }
            return (float)DamageDealt / Played;
        }
    }
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

    public FightStats Stats = new FightStats();

    public HandSlot[] Hand;

    /// <summary>当前上场数（手牌槽位数）。基础 5，可被卡牌临时或本场改写。</summary>
    public int HandCapacity = DeckBattle.HandBase;

    /// <summary>实际参与推进的槽位数。</summary>
    public int ActiveSlots
    {
        get
        {
            if (HandCapacity < 1)
            {
                return 1;
            }
            if (HandCapacity > DeckBattle.HandMax)
            {
                return DeckBattle.HandMax;
            }
            return HandCapacity;
        }
    }
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
    /// <summary>基础上场数。卡牌可以临时或本场改写它（见 CardConfig.HandSelf / HandFoe）。</summary>
    public const int HandBase = 5;

    /// <summary>上场数上限。界面按这个数建槽位，改这里要同步改 BattlePanel 的布局。</summary>
    public const int HandMax = 8;
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

    private readonly Random _rng = new Random();

    /// <summary>限时上场数修正。Remain 归零时把当时加上去的量原样收回。</summary>
    private sealed class HandModifier
    {
        public FighterState Target;
        public int Applied;
        public float Remain;
    }

    private readonly List<HandModifier> _handMods = new List<HandModifier>();

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
        fighter.Hand = new HandSlot[HandMax];

        for (int i = 0; i < HandMax; i++)
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
        int playerSlots = Player.ActiveSlots;
        for (int slot = 0; slot < playerSlots; slot++)
        {
            DrawInto(Player, slot, ignored);
        }

        int enemySlots = Enemy.ActiveSlots;
        for (int slot = 0; slot < enemySlots; slot++)
        {
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

        TickHandMods(dt, events);
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

    private void TickHandMods(float dt, List<BattleEvent> events)
    {
        for (int i = _handMods.Count - 1; i >= 0; i--)
        {
            HandModifier mod = _handMods[i];
            mod.Remain -= dt;
            if (mod.Remain > 0f)
            {
                continue;
            }

            // 到期收回：只收回当初真正加上去的量（可能被上限截断过）
            ApplyCapacity(mod.Target, -mod.Applied, events, true);
            _handMods.RemoveAt(i);
        }
    }

    /// <summary>
    /// 改上场数。加：新槽位立刻补抽；减：被砍掉的槽位里的牌进弃牌堆（不是凭空消失）。
    /// 返回真正生效的变化量——顶到上下限时可能是 0。
    /// </summary>
    private int ApplyCapacity(FighterState target, int delta, List<BattleEvent> events, bool revert)
    {
        int before = target.HandCapacity;
        int after = Mathf.Clamp(before + delta, 1, HandMax);
        int applied = after - before;
        if (applied == 0)
        {
            return 0;
        }

        target.HandCapacity = after;

        // 先报事件、后补抽：补抽会发 Draw，界面收到 Draw 就把当前这一手收尾了，
        // 事件发晚了就会丢掉"上场 +1"这条明细。
        BattleEvent evt = new BattleEvent();
        evt.Kind = BattleEventKind.HandMod;
        evt.Side = SideOf(target);
        evt.Slot = -1;
        evt.Value = after;
        evt.Revert = revert;
        evt.Text = "上场 " + after;
        events.Add(evt);

        if (applied > 0)
        {
            for (int slot = before; slot < after; slot++)
            {
                DrawInto(target, slot, events);
            }
        }
        else
        {
            for (int slot = after; slot < before; slot++)
            {
                HandSlot handSlot = target.Hand[slot];
                if (!handSlot.Occupied)
                {
                    continue;
                }

                target.DiscardPile.Add(handSlot.Card);
                handSlot.Card = null;
                handSlot.Countdown = 0f;
                handSlot.CountdownMax = 0f;
            }
        }

        return applied;
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

        int absorbed;
        int lost = ApplyDamage(owner, tick, false, out absorbed);
        owner.Stats.PoisonDamage += lost;
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
        int activeSlots = owner.ActiveSlots;
        for (int slot = 0; slot < activeSlots; slot++)
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

            owner.Stats.Played++;
            if (owner.Stats.FirstPlayAt < 0f)
            {
                owner.Stats.FirstPlayAt = Elapsed;
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
            int absorbed;
            int dealt = ApplyDamage(foe, card.Damage, card.IgnoreShield, out absorbed);
            AddDamageEvent(owner, foe, slot, card, dealt, absorbed, SpellElement.None, events);
        }

        if (card.Spell > 0)
        {
            int absorbed;
            int spellDamage = ApplyDamage(foe, card.Spell, false, out absorbed);
            AddDamageEvent(owner, foe, slot, card, spellDamage, absorbed, card.Element, events);
        }

        if (card.Shield > 0)
        {
            // 记"实际拿到的护体"：已经顶到上限时，写 +0 比写 +8 诚实
            int shieldBefore = owner.Shield;
            owner.Shield = Math.Min(ShieldCap, owner.Shield + card.Shield);
            int gained = owner.Shield - shieldBefore;
            owner.Stats.ShieldGained += gained;

            BattleEvent sh = new BattleEvent();
            sh.Kind = BattleEventKind.Shield;
            sh.Side = SideOf(owner);
            sh.Slot = slot;
            sh.Value = gained;
            sh.Text = "护体 +" + gained;
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
            owner.Stats.Healed += owner.Hp - before;
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
            owner.Stats.ExposeApplied += card.Expose;
            events.Add(expose);
        }

        if (card.HandSelf != 0)
        {
            int applied = ApplyCapacity(owner, card.HandSelf, events, false);
            if (card.HandTime > 0f && applied != 0)
            {
                HandModifier mod = new HandModifier();
                mod.Target = owner;
                mod.Applied = applied;
                mod.Remain = card.HandTime;
                _handMods.Add(mod);
            }
        }

        if (card.HandFoe != 0)
        {
            int applied = ApplyCapacity(foe, card.HandFoe, events, false);
            if (card.HandTime > 0f && applied != 0)
            {
                HandModifier mod = new HandModifier();
                mod.Target = foe;
                mod.Applied = applied;
                mod.Remain = card.HandTime;
                _handMods.Add(mod);
            }
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
        int hpDamage, int absorbed, SpellElement element, List<BattleEvent> events)
    {
        BattleEvent dmg = new BattleEvent();
        dmg.Kind = BattleEventKind.Damage;
        dmg.Side = SideOf(foe);
        dmg.Slot = slot;
        dmg.Value = hpDamage > 0 ? hpDamage : (element != SpellElement.None ? card.Spell : card.Damage);
        dmg.HpLoss = hpDamage;
        dmg.Absorbed = absorbed;
        dmg.IgnoredShield = card.IgnoreShield;
        dmg.Element = element;
        dmg.Text = card.Name;
        events.Add(dmg);

        owner.Stats.DamageDealt += hpDamage;
        foe.Stats.DamageTaken += hpDamage;
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

    private static int ApplyDamage(FighterState target, int amount, bool ignoreShield, out int absorbed)
    {
        absorbed = 0;

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
            absorbed = Math.Min(target.Shield, remaining);
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
