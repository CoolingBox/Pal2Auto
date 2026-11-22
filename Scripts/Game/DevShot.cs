// 文件：DevShot.cs
// 描述：开发期截图开关 —— 带 --shot=秒数 启动，到点截一张图存到 user:// 然后退出。
//       用途：改完界面立刻自证"画面非空白、没有报错"，不靠猜。

using System;
using System.Collections.Generic;
using Godot;
using TinyFramework;

public static class DevShot
{
    /// <summary>命令行里有没有这个参数。调试开关散在各面板时，统一走这里查。</summary>
    public static bool HasArg(string key)
    {
        string[] args = OS.GetCmdlineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == key)
            {
                return true;
            }
        }
        return false;
    }

    public static void RunIfRequested(Node host)
    {
        string[] all = OS.GetCmdlineArgs();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == "--ui=select")
            {
                OpenSelectLater(host);
            }
            if (all[i] == "--ui=settings")
            {
                OpenSettingsLater(host);
            }
            if (all[i] == "--ui=battle")
            {
                OpenBattleLater(host);
            }
            if (all[i] == "--clicktest")
            {
                RunClickTest(host);
            }
            if (all[i] == "--clicklog")
            {
                RunLogClickTest(host);
            }
            if (all[i] == "--resetdeck")
            {
                // 「移除」关键字会永久扣牌库，调试时用它复原
                DeckStore.ResetAll();
                GD.Print("RESETDECK 已清空永久移除记录");
            }
            if (all[i].StartsWith("--sim="))
            {
                RunSim(all[i].Substring(6), host);
            }
        }

        float seconds = 2f;
        string[] args = OS.GetCmdlineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--shot="))
            {
                float parsed;
                if (float.TryParse(args[i].Substring(7), out parsed))
                {
                    seconds = parsed;
                }
            }
        }

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--shot" || args[i].StartsWith("--shot="))
            {
                Capture(host, seconds);
                return;
            }
        }
    }

    /// <summary>用真实输入事件走一遍：开局 → 选王小虎 → 就他了。位置按 2 倍放大换算。</summary>
    private static async void RunClickTest(Node host)
    {
        await host.ToSignal(host.GetTree().CreateTimer(0.6f), SceneTreeTimer.SignalName.Timeout);
        await ClickAt(host, new Vector2(328f, 472f));
        await host.ToSignal(host.GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
        await ClickAt(host, new Vector2(198f, 356f));
        await host.ToSignal(host.GetTree().CreateTimer(0.4f), SceneTreeTimer.SignalName.Timeout);
        await ClickAt(host, new Vector2(1654f, 904f));
        GD.Print("CLICKTEST 已点击：开局 / 第一张卡 / 就他了");
    }

    private static async System.Threading.Tasks.Task ClickAt(Node host, Vector2 position)
    {
        InputEventMouseMotion motion = new InputEventMouseMotion();
        motion.Position = position;
        Input.ParseInputEvent(motion);
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);

        InputEventMouseButton down = new InputEventMouseButton();
        down.ButtonIndex = MouseButton.Left;
        down.Pressed = true;
        down.Position = position;
        Input.ParseInputEvent(down);
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);

        InputEventMouseButton up = new InputEventMouseButton();
        up.ButtonIndex = MouseButton.Left;
        up.Pressed = false;
        up.Position = position;
        Input.ParseInputEvent(up);
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>真实鼠标点一遍：进战斗 → 开交战记录 → 切统计页。</summary>
    private static async void RunLogClickTest(Node host)
    {
        await host.ToSignal(host.GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);

        GameFlow.SelectedCharacterId = ReadCharId();
        TinyFramework.UIManager.ShowPanel<BattlePanel>();
        TinyFramework.UIManager.HidePanel<MainMenuPanel>();

        await host.ToSignal(host.GetTree().CreateTimer(1.2f), SceneTreeTimer.SignalName.Timeout);

        Vector2 scale = host.GetViewport().GetVisibleRect().Size / new Vector2(960f, 540f);

        await ClickAt(host, new Vector2(872f, 21f) * scale);
        await host.ToSignal(host.GetTree().CreateTimer(0.4f), SceneTreeTimer.SignalName.Timeout);

        await ClickAt(host, new Vector2(164f, 103f) * scale);
        GD.Print("CLICKLOG 已点击：交战记录开关 / 统计页");
    }

    /// <summary>
    /// 无头批量对局：--sim=20 打 20 场，只打印胜负与用时。
    /// 用来在没人陪玩时量一下改动对胜率的影响，别靠感觉调数值。
    /// </summary>
    private static void RunSim(string raw, Node host)
    {
        int rounds;
        if (!int.TryParse(raw, out rounds) || rounds <= 0)
        {
            rounds = 10;
        }

        int charId = ReadCharId();
        CharacterConfig character = ConfigManager.GetOne<CharacterConfig>(charId);
        EnemyConfig enemy = ConfigManager.GetOne<EnemyConfig>(1);
        if (character == null || enemy == null)
        {
            GD.Print("SIM 配置缺失");
            host.GetTree().Quit(1);
            return;
        }

        int wins = 0;
        float totalTime = 0f;
        int maxHand = DeckBattle.HandBase;
        int minHand = DeckBattle.HandBase;

        for (int round = 0; round < rounds; round++)
        {
            DeckBattle battle = DeckBattle.Create(character.Name, character.Portrait, 120,
                new List<int>(character.Deck), enemy.Name, enemy.Portrait, enemy.Hp, enemy.Deck);

            while (!battle.Finished)
            {
                battle.Step(0.05f);
            }

            if (battle.PlayerWon)
            {
                wins++;
            }
            totalTime += battle.Elapsed;

            int peak = Mathf.Max(battle.Player.HandCapacity, battle.Enemy.HandCapacity);
            if (peak > maxHand)
            {
                maxHand = peak;
            }
            if (peak < minHand)
            {
                minHand = peak;
            }
        }

        GD.Print("SIM " + character.Name + " vs " + enemy.Name + "：" + rounds + " 场 " + wins + " 胜（" +
                 Mathf.RoundToInt(100f * wins / rounds) + "%），均时 " +
                 (totalTime / rounds).ToString("0.0") + " 秒，上场数区间 " + minHand + "~" + maxHand);
        host.GetTree().Quit(0);
    }

    private static async void OpenBattleLater(Node host)
    {
        GameFlow.SelectedCharacterId = ReadCharId();
        await host.ToSignal(host.GetTree().CreateTimer(0.3f), SceneTreeTimer.SignalName.Timeout);
        TinyFramework.UIManager.ShowPanel<BattlePanel>();
        TinyFramework.UIManager.HidePanel<MainMenuPanel>();
    }

    /// <summary>--char=N 指定进战斗时用哪位侠客（截不同牌组用）。</summary>
    private static int ReadCharId()
    {
        string[] args = OS.GetCmdlineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--char="))
            {
                int parsed;
                if (int.TryParse(args[i].Substring(7), out parsed))
                {
                    return parsed;
                }
            }
        }
        return 1;
    }

    private static async void OpenSettingsLater(Node host)
    {
        await host.ToSignal(host.GetTree().CreateTimer(0.3f), SceneTreeTimer.SignalName.Timeout);
        TinyFramework.UIManager.ShowPanel<SettingsPanel>();
    }

    private static async void OpenSelectLater(Node host)
    {
        await host.ToSignal(host.GetTree().CreateTimer(0.3f), SceneTreeTimer.SignalName.Timeout);
        TinyFramework.UIManager.ShowPanel<CharacterSelectPanel>();
        TinyFramework.UIManager.HidePanel<MainMenuPanel>();
    }

    private static async void Capture(Node host, float seconds)
    {
        await host.ToSignal(host.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);

        Image image = host.GetViewport().GetTexture().GetImage();
        string path = "user://shot_" + DateTime.Now.ToString("HHmmss") + ".png";
        Error error = image.SavePng(path);
        GD.Print("SHOT " + (error == Error.Ok ? "已保存 " : "保存失败 ") + ProjectSettings.GlobalizePath(path));

        host.GetTree().Quit(0);
    }
}
