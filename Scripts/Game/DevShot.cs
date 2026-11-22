// 文件：DevShot.cs
// 描述：开发期截图开关 —— 带 --shot=秒数 启动，到点截一张图存到 user:// 然后退出。
//       用途：改完界面立刻自证"画面非空白、没有报错"，不靠猜。

using System;
using Godot;

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
