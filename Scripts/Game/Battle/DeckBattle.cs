// 文件：DeckBattle.cs
// 描述：卡组战斗内核（纯逻辑，不碰节点）。规则见 Docs/卡组战斗设计.md。
//
// 关键约定：
//   · 手牌固定 5 个槽位，各自独立倒计时；归零即自动打出、进弃牌堆、立刻补抽一张。
//   · 抽牌堆空则把弃牌堆洗回去；两边都空则该槽位留空，直到有牌洗回。
//   · 敌方不做牌组，用固定三段循环的行动条（见 EnemyMove）。

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
    Play,       // 一张牌打出（带牌面信息）
    Draw,       // 补抽进某个槽位
    Damage,
    Shield,
    Heal,
    Poison,
    EnemyMove,  // 敌方行动的宣告（无效果，纯提示）
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

/// <summary>敌方一段行动：间隔、伤害或护盾。</summary>
public sealed class EnemyMove
{
    public string Name = "";
    public float Interval = 2.4f;
    public int Damage;
    public int Shield;
}

public sealed class DeckBattle
{
    public const int HandSize = 5;
    public const float PoisonPerStackPerSecond = 1f;
    public const float TimeLimit = 60f;
    /// <summary>护体上限。不设上限的话守牌会把伤害全吃掉，双方都打不死，战斗变成看血条不动。</summary>
    public const int ShieldCap = 20;

    public FighterState Player = new FighterState();
    public FighterState Enemy = new FighterState();

    public bool Finished;
    public bool TimedOut;
    public bool PlayerWon;

    public float Elapsed;
    public int PlayedCount;
    public int EnemyMoveCount;
    public int DamageToPlayer;
    public int ShieldGained;

    /// <summary>敌方当前招式与倒计时，UI 要显示"下一招"。</summary>
    public EnemyMove CurrentMove;
    public float MoveTimer;

    private EnemyMove[] _moves = new EnemyMove[0];
    private int _moveIndex;
    private readonly Random _rng = new Random();

    public static DeckBattle Create(List<int> deckCardIds, int playerMaxHp, string playerName,
        string playerPortrait, string enemyName, int enemyHp)
    {
        DeckBattle battle = new DeckBattle();

        battle.Player = BuildFighter(playerName, playerPortrait, playerMaxHp, deckCardIds);
        battle.Enemy = new FighterState();
        battle.Enemy.Name = enemyName;
        battle.Enemy.MaxHp = enemyHp;
        battle.Enemy.Hp = enemyHp;
        battle.Enemy.Hand = new HandSlot[0];

        battle._moves = new EnemyMove[]
        {
            MakeMove("挥拳", 1.8f, 12, 0),
            MakeMove("重击", 3.0f, 20, 0),
            MakeMove("架势", 2.4f, 0, 8),
        };
        battle._moveIndex = 0;
        battle.CurrentMove = battle._moves[0];
        battle.MoveTimer = battle.CurrentMove.Interval;

        battle.DrawOpeningHand();
        return battle;
    }

    private static EnemyMove MakeMove(string name, float interval, int damage, int shield)
    {
        EnemyMove move = new EnemyMove();
        move.Name = name;
        move.Interval = interval;
        move.Damage = damage;
        move.Shield = shield;
        return move;
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

    /// <summary>开局把 5 个槽位填满。</summary>
    private void DrawOpeningHand()
    {
        List<BattleEvent> ignored = new List<BattleEvent>();
        for (int slot = 0; slot < HandSize; slot++)
        {
            DrawInto(slot, ignored);
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

        TickHand(dt, events);
        TickEnemy(dt, events);

        CheckEnd(events);
        return events;
    }

    private void TickHand(float dt, List<BattleEvent> events)
    {
        FighterState unit = Player;
        if (unit.Dead)
        {
            return;
        }

        // 中毒：每层每秒 1 点，按累计值取整结算
        if (unit.PoisonStacks > 0)
        {
            unit.PoisonAccum += unit.PoisonStacks * PoisonPerStackPerSecond * dt;
            if (unit.PoisonAccum >= 1f)
            {
                int tick = (int)unit.PoisonAccum;
                unit.PoisonAccum -= tick;
                int lost = ApplyDamage(unit, tick, false);
                if (lost > 0)
                {
                    BattleEvent poisonEvent = new BattleEvent();
                    poisonEvent.Kind = BattleEventKind.Poison;
                    poisonEvent.Side = BattleSide.Player;
                    poisonEvent.Slot = -1;
                    poisonEvent.Value = lost;
                    poisonEvent.Text = "中毒 -" + lost;
                    events.Add(poisonEvent);
                }
            }
        }

        for (int slot = 0; slot < HandSize; slot++)
        {
            HandSlot handSlot = unit.Hand[slot];
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
            ResolveCard(card, slot, events);

            unit.DiscardPile.Add(card);
            handSlot.Card = null;
            handSlot.Countdown = 0f;
            PlayedCount++;

            DrawInto(slot, events);
        }
    }

    private void ResolveCard(CardConfig card, int slot, List<BattleEvent> events)
    {
        BattleEvent playEvent = new BattleEvent();
        playEvent.Kind = BattleEventKind.Play;
        playEvent.Side = BattleSide.Player;
        playEvent.Slot = slot;
        playEvent.Card = card;
        playEvent.Text = card.Name;
        events.Add(playEvent);

        if (card.Damage > 0)
        {
            int dealt = ApplyDamage(Enemy, card.Damage, card.IgnoreShield);
            BattleEvent dmg = new BattleEvent();
            dmg.Kind = BattleEventKind.Damage;
            dmg.Side = BattleSide.Enemy;
            dmg.Slot = slot;
            dmg.Value = dealt > 0 ? dealt : card.Damage;
            dmg.IgnoredShield = card.IgnoreShield;
            dmg.Text = card.Name + " " + card.Damage;
            events.Add(dmg);
        }

        if (card.Shield > 0)
        {
            ShieldGained += card.Shield;
            Player.Shield = Math.Min(ShieldCap, Player.Shield + card.Shield);
            BattleEvent sh = new BattleEvent();
            sh.Kind = BattleEventKind.Shield;
            sh.Side = BattleSide.Player;
            sh.Slot = slot;
            sh.Value = card.Shield;
            sh.Text = "护体 +" + card.Shield;
            events.Add(sh);
        }

        if (card.Heal > 0)
        {
            int before = Player.Hp;
            Player.Hp = Math.Min(Player.MaxHp, Player.Hp + card.Heal);
            BattleEvent heal = new BattleEvent();
            heal.Kind = BattleEventKind.Heal;
            heal.Side = BattleSide.Player;
            heal.Slot = slot;
            heal.Value = Player.Hp - before;
            heal.Text = "回气 +" + (Player.Hp - before);
            events.Add(heal);
        }

        if (card.Poison > 0)
        {
            Enemy.PoisonStacks = Math.Min(20, Enemy.PoisonStacks + card.Poison);
            BattleEvent poison = new BattleEvent();
            poison.Kind = BattleEventKind.Poison;
            poison.Side = BattleSide.Enemy;
            poison.Slot = slot;
            poison.Value = card.Poison;
            poison.Text = "中毒 +" + card.Poison;
            events.Add(poison);
        }
    }

    /// <summary>把一个槽位补满：先抽牌堆，空了就把弃牌堆洗回去再抽。</summary>
    private void DrawInto(int slot, List<BattleEvent> events)
    {
        FighterState unit = Player;
        if (unit.Hand[slot].Occupied)
        {
            return;
        }

        if (unit.DrawPile.Count == 0)
        {
            if (unit.DiscardPile.Count == 0)
            {
                return;   // 两边都空，槽位空着
            }

            for (int i = 0; i < unit.DiscardPile.Count; i++)
            {
                unit.DrawPile.Add(unit.DiscardPile[i]);
            }
            unit.DiscardPile.Clear();
            Shuffle(unit.DrawPile, _rng);
        }

        CardConfig card = unit.DrawPile[0];
        unit.DrawPile.RemoveAt(0);

        HandSlot handSlot = unit.Hand[slot];
        handSlot.Card = card;
        handSlot.CountdownMax = card.Countdown;
        handSlot.Countdown = card.Countdown;

        BattleEvent draw = new BattleEvent();
        draw.Kind = BattleEventKind.Draw;
        draw.Side = BattleSide.Player;
        draw.Slot = slot;
        draw.Card = card;
        draw.Text = card.Name;
        events.Add(draw);
    }

    private void TickEnemy(float dt, List<BattleEvent> events)
    {
        if (Enemy.Dead || Player.Dead)
        {
            return;
        }

        // 敌方中毒也按秒结算
        if (Enemy.PoisonStacks > 0)
        {
            Enemy.PoisonAccum += Enemy.PoisonStacks * PoisonPerStackPerSecond * dt;
            if (Enemy.PoisonAccum >= 1f)
            {
                int tick = (int)Enemy.PoisonAccum;
                Enemy.PoisonAccum -= tick;
                int lost = ApplyDamage(Enemy, tick, false);
                if (lost > 0)
                {
                    BattleEvent poisonEvent = new BattleEvent();
                    poisonEvent.Kind = BattleEventKind.Poison;
                    poisonEvent.Side = BattleSide.Enemy;
                    poisonEvent.Slot = -1;
                    poisonEvent.Value = lost;
                    poisonEvent.Text = "中毒 -" + lost;
                    events.Add(poisonEvent);
                }
            }
        }

        MoveTimer -= dt;
        if (MoveTimer > 0f)
        {
            return;
        }

        EnemyMove move = CurrentMove;

        BattleEvent announce = new BattleEvent();
        announce.Kind = BattleEventKind.EnemyMove;
        announce.Side = BattleSide.Enemy;
        announce.Slot = -1;
        announce.Text = move.Name;
        events.Add(announce);

        EnemyMoveCount++;
        if (move.Damage > 0)
        {
            int dealt = ApplyDamage(Player, move.Damage, false);
            DamageToPlayer += dealt;
            BattleEvent dmg = new BattleEvent();
            dmg.Kind = BattleEventKind.Damage;
            dmg.Side = BattleSide.Player;
            dmg.Slot = -1;
            dmg.Value = dealt > 0 ? dealt : move.Damage;
            dmg.Text = move.Name;
            events.Add(dmg);
        }

        if (move.Shield > 0)
        {
            Enemy.Shield = Math.Min(ShieldCap, Enemy.Shield + move.Shield);
            BattleEvent sh = new BattleEvent();
            sh.Kind = BattleEventKind.Shield;
            sh.Side = BattleSide.Enemy;
            sh.Slot = -1;
            sh.Value = move.Shield;
            sh.Text = "护体 +" + move.Shield;
            events.Add(sh);
        }

        _moveIndex = (_moveIndex + 1) % _moves.Length;
        CurrentMove = _moves[_moveIndex];
        MoveTimer += CurrentMove.Interval;
    }

    private static int ApplyDamage(FighterState target, int amount, bool ignoreShield)
    {
        if (amount <= 0 || target.Hp <= 0)
        {
            return 0;
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
