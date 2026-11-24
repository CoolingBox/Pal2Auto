// 文件：SettingsPanel.cs
// 描述：设置界面 —— 画面精细度、战斗倍速、音乐/音效音量。
//       每项改动立即生效并写回 SettingData，不设"确定/取消"，避免玩家以为要按确认。

using Godot;
using TinyFramework;

public class SettingsPanel : BasePanel
{
    private const float W = 960f;
    private const float H = 540f;

    private const float PanelX = 280f;
    private const float PanelY = 80f;
    private const float PanelW = 400f;
    private const float PanelH = 414f;

    private Control _root;
    private bool _built;

    private OptionButton _scaleOption;
    private Label _scaleState;
    private OptionButton _aaOption;
    private Label _aaState;
    private OptionButton _speedOption;
    private HSlider _bgmSlider;
    private HSlider _sfxSlider;
    private Label _bgmValue;
    private Label _sfxValue;

    public override void OnBind(Control control)
    {
        base.OnBind(control);
        _root = control;
        _root.Position = Vector2.Zero;
        _root.Size = new Vector2(W, H);
        Build();
    }

    public override void OnShow()
    {
        base.OnShow();
        RefreshFromSettings();
    }

    private void Build()
    {
        if (_built)
        {
            return;
        }
        _built = true;

        ColorRect dim = new ColorRect();
        dim.Color = new Color(0.02f, 0.02f, 0.02f, 0.72f);
        dim.Size = new Vector2(W, H);
        dim.MouseFilter = Control.MouseFilterEnum.Stop;
        _root.AddChild(dim);

        Panel panel = new Panel();
        panel.Position = new Vector2(PanelX, PanelY);
        panel.Size = new Vector2(PanelW, PanelH);
        panel.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.108f, 0.094f, 0.078f), new Color(0.500f, 0.400f, 0.240f), 2, 8));
        _root.AddChild(panel);

        Label title = UiKit.MakeLabel("设 置", 26, UiKit.Gold);
        UiKit.Place(title, PanelX, PanelY + 16f, PanelW, 40f, HorizontalAlignment.Center);
        _root.AddChild(title);

        // —— 画面精细度：清晰度只由它决定（放大滤镜那一套已移除）——
        Label scaleLabel = UiKit.MakeLabel("画面精细度", 15, UiKit.Paper);
        UiKit.Place(scaleLabel, PanelX + 32f, PanelY + 76f, 190f, 30f);
        _root.AddChild(scaleLabel);

        _scaleOption = new OptionButton();
        _scaleOption.Position = new Vector2(PanelX + 176f, PanelY + 76f);
        _scaleOption.Size = new Vector2(192f, 32f);
        _scaleOption.FocusMode = Control.FocusModeEnum.None;
        _scaleOption.AddThemeFontOverride("font", UiKit.GameFont);
        _scaleOption.AddThemeFontSizeOverride("font_size", 14);
        _scaleOption.AddItem("像素 960×540", 0);
        _scaleOption.AddItem("高清 1920×1080", 1);
        _scaleOption.ItemSelected += OnScaleSelected;
        _root.AddChild(_scaleOption);

        _scaleState = UiKit.MakeLabel("", 11, UiKit.PaperDim);
        UiKit.Place(_scaleState, PanelX + 32f, PanelY + 106f, PanelW - 64f, 20f);
        _root.AddChild(_scaleState);

        // —— 抗锯齿：一格下拉，档位就是 Unity 那套的组合 ——
        Label aaLabel = UiKit.MakeLabel("抗锯齿", 15, UiKit.Paper);
        UiKit.Place(aaLabel, PanelX + 32f, PanelY + 136f, 190f, 30f);
        _root.AddChild(aaLabel);

        _aaOption = new OptionButton();
        _aaOption.Position = new Vector2(PanelX + 176f, PanelY + 136f);
        _aaOption.Size = new Vector2(192f, 32f);
        _aaOption.FocusMode = Control.FocusModeEnum.None;
        _aaOption.AddThemeFontOverride("font", UiKit.GameFont);
        _aaOption.AddThemeFontSizeOverride("font_size", 14);
        // 0 关 / 1 解析 / 2 解析+FXAA / 3 解析+SMAA —— 索引与 SettingData.AaMode 一一对应
        _aaOption.AddItem("关（硬边）", 0);
        _aaOption.AddItem("解析（推荐）", 1);
        _aaOption.AddItem("解析 + FXAA", 2);
        _aaOption.AddItem("解析 + SMAA", 3);
        _aaOption.ItemSelected += OnAaSelected;
        _root.AddChild(_aaOption);

        _aaState = UiKit.MakeLabel("", 11, UiKit.PaperDim);
        UiKit.Place(_aaState, PanelX + 32f, PanelY + 166f, PanelW - 64f, 20f);
        _root.AddChild(_aaState);

        // —— 战斗倍速 ——
        Label speedLabel = UiKit.MakeLabel("战斗倍速", 15, UiKit.Paper);
        UiKit.Place(speedLabel, PanelX + 32f, PanelY + 198f, 160f, 30f);
        _root.AddChild(speedLabel);

        _speedOption = new OptionButton();
        _speedOption.Position = new Vector2(PanelX + 200f, PanelY + 198f);
        _speedOption.Size = new Vector2(120f, 32f);
        _speedOption.FocusMode = Control.FocusModeEnum.None;
        _speedOption.AddThemeFontOverride("font", UiKit.GameFont);
        _speedOption.AddThemeFontSizeOverride("font_size", 14);
        for (int i = 0; i < SettingData.SpeedSteps.Length; i++)
        {
            _speedOption.AddItem((int)SettingData.SpeedSteps[i] + " 倍", i);
        }
        _speedOption.ItemSelected += OnSpeedSelected;
        _root.AddChild(_speedOption);

        // —— 音乐 ——
        Label bgmLabel = UiKit.MakeLabel("音乐", 15, UiKit.Paper);
        UiKit.Place(bgmLabel, PanelX + 32f, PanelY + 242f, 60f, 30f);
        _root.AddChild(bgmLabel);

        _bgmSlider = new HSlider();
        _bgmSlider.Position = new Vector2(PanelX + 92f, PanelY + 248f);
        _bgmSlider.Size = new Vector2(230f, 18f);
        _bgmSlider.MinValue = 0;
        _bgmSlider.MaxValue = 100;
        _bgmSlider.FocusMode = Control.FocusModeEnum.None;
        _bgmSlider.ValueChanged += OnBgmChanged;
        _root.AddChild(_bgmSlider);

        StyleSlider(_bgmSlider);

        _bgmValue = UiKit.MakeLabel("", 12, UiKit.PaperDim);
        UiKit.Place(_bgmValue, PanelX + 330f, PanelY + 242f, 40f, 30f, HorizontalAlignment.Right);
        _root.AddChild(_bgmValue);

        // —— 音效 ——
        Label sfxLabel = UiKit.MakeLabel("音效", 15, UiKit.Paper);
        UiKit.Place(sfxLabel, PanelX + 32f, PanelY + 282f, 60f, 30f);
        _root.AddChild(sfxLabel);

        _sfxSlider = new HSlider();
        _sfxSlider.Position = new Vector2(PanelX + 92f, PanelY + 288f);
        _sfxSlider.Size = new Vector2(230f, 18f);
        _sfxSlider.MinValue = 0;
        _sfxSlider.MaxValue = 100;
        _sfxSlider.FocusMode = Control.FocusModeEnum.None;
        _sfxSlider.ValueChanged += OnSfxChanged;
        _root.AddChild(_sfxSlider);

        StyleSlider(_sfxSlider);

        _sfxValue = UiKit.MakeLabel("", 12, UiKit.PaperDim);
        UiKit.Place(_sfxValue, PanelX + 330f, PanelY + 282f, 40f, 30f, HorizontalAlignment.Right);
        _root.AddChild(_sfxValue);

        Label note = UiKit.MakeLabel("改动即时生效，自动保存。", 11, UiKit.Dim);
        UiKit.Place(note, PanelX + 32f, PanelY + 324f, PanelW - 64f, 20f);
        _root.AddChild(note);

        Button reset = UiKit.MakeButton("恢复默认", false);
        reset.Position = new Vector2(PanelX + 50f, PanelY + 350f);
        reset.Size = new Vector2(140f, 42f);
        reset.Pressed += OnResetPressed;
        _root.AddChild(reset);

        Button close = UiKit.MakeButton("关 闭", true);
        close.Position = new Vector2(PanelX + 210f, PanelY + 350f);
        close.Size = new Vector2(140f, 42f);
        close.Pressed += OnClosePressed;
        _root.AddChild(close);
    }

    /// <summary>滑条换成本项目的配色：深色槽 + 旧金已选段，默认主题的浅色圆条太跳。</summary>
    private static void StyleSlider(HSlider slider)
    {
        StyleBoxFlat track = UiKit.Box(new Color(0.150f, 0.132f, 0.110f), new Color(0.320f, 0.280f, 0.210f), 1, 3);
        track.ContentMarginTop = 5;
        track.ContentMarginBottom = 5;

        StyleBoxFlat filled = UiKit.Box(new Color(0.560f, 0.430f, 0.220f), new Color(0.700f, 0.560f, 0.320f), 0, 3);
        filled.ContentMarginTop = 5;
        filled.ContentMarginBottom = 5;

        slider.AddThemeStyleboxOverride("slider", track);
        slider.AddThemeStyleboxOverride("grabber_area", filled);
        slider.AddThemeStyleboxOverride("grabber_area_highlight", filled);
    }

    private void RefreshFromSettings()
    {
        SettingData data = SettingManager.Data;

        if (_scaleOption != null)
        {
            _scaleOption.Selected = data.RenderScale >= 2 ? 1 : 0;
        }

        RefreshScaleState();

        if (_aaOption != null)
        {
            _aaOption.Selected = data.AaMode;
        }
        RefreshAntiAliasState();

        if (_speedOption != null)
        {
            int index = 0;
            for (int i = 0; i < SettingData.SpeedSteps.Length; i++)
            {
                if (Mathf.IsEqualApprox(SettingData.SpeedSteps[i], data.BattleSpeed))
                {
                    index = i;
                }
            }
            _speedOption.Selected = index;
        }

        if (_bgmSlider != null)
        {
            _bgmSlider.Value = data.BGMVolume;
        }

        if (_sfxSlider != null)
        {
            _sfxSlider.Value = data.SFXVolume;
        }

        RefreshVolumeText();
    }

    private void OnScaleSelected(long index)
    {
        int scale = index == 0 ? 1 : 2;
        SettingManager.Data.RenderScale = scale;
        SaveManager.Save(SettingManager.Data);

        GameRoot root = GameRoot.Instance;
        if (root != null)
        {
            root.ApplyRenderScale(scale);
        }
        RefreshScaleState();
    }

    private void RefreshScaleState()
    {
        if (_scaleState == null)
        {
            return;
        }

        if (SettingManager.Data.RenderScale >= 2)
        {
            _scaleState.Text = "当前：内部按 1920×1080 画，立绘与文字最清楚";
        }
        else
        {
            _scaleState.Text = "当前：内部按 960×540 画，颗粒感最重、最像老游戏";
        }
    }

    private void OnAaSelected(long index)
    {
        SettingManager.Data.AntiAlias = index >= 1;
        SettingManager.Data.PostAa = index == 2 ? 1 : (index == 3 ? 2 : 0);
        SaveManager.Save(SettingManager.Data);

        // 解析 AA 由各面板在自己显示时读设置（BattlePanel.OnShow），这里只推整屏后处理
        if (GameRoot.Instance != null)
        {
            GameRoot.Instance.ApplyPostAa(SettingManager.Data.PostAa);
        }

        RefreshAntiAliasState();
        AudioManager.PlayUISFX("ui_click");
    }

    private void RefreshAntiAliasState()
    {
        if (_aaState == null)
        {
            return;
        }

        int mode = SettingManager.Data.AaMode;
        if (mode == 0)
        {
            _aaState.Text = "当前：全关（切角、宝石、铆片都是硬台阶）";
        }
        else if (mode == 1)
        {
            _aaState.Text = "当前：解析（只修着色器画的边，文字与位图不动）";
        }
        else if (mode == 2)
        {
            _aaState.Text = "当前：解析 + FXAA（整屏后处理，字会软一档）";
        }
        else
        {
            _aaState.Text = "当前：解析 + SMAA（比 FXAA 锐，本项目是简化版）";
        }
    }

    private void RefreshVolumeText()
    {
        if (_bgmValue != null)
        {
            _bgmValue.Text = (int)SettingManager.Data.BGMVolume + "";
        }
        if (_sfxValue != null)
        {
            _sfxValue.Text = (int)SettingManager.Data.SFXVolume + "";
        }
    }

    private void OnSpeedSelected(long index)
    {
        if (index < 0 || index >= SettingData.SpeedSteps.Length)
        {
            return;
        }

        SettingManager.Data.BattleSpeed = SettingData.SpeedSteps[index];
        SaveManager.Save(SettingManager.Data);
        AudioManager.PlayUISFX("ui_click");
    }

    private void OnBgmChanged(double value)
    {
        SettingManager.Data.BGMVolume = (float)value;
        AudioManager.SetBGMVolume((float)value);
        SaveManager.Save(SettingManager.Data);
        RefreshVolumeText();
    }

    private void OnSfxChanged(double value)
    {
        SettingManager.Data.SFXVolume = (float)value;
        AudioManager.SetSFXVolume((float)value);
        SaveManager.Save(SettingManager.Data);
        RefreshVolumeText();
    }

    private void OnResetPressed()
    {
        SettingManager.Data.BGMVolume = 100f;
        SettingManager.Data.SFXVolume = 100f;
        SettingManager.Data.Volume = 100f;
        SettingManager.Data.BattleSpeed = SettingData.SpeedSteps[0];
        SettingManager.Data.RenderScale = 2;
        SettingManager.Data.AntiAlias = true;
        SettingManager.Data.PostAa = 0;
        SaveManager.Save(SettingManager.Data);

        AudioManager.SetVolume(100f);
        AudioManager.SetBGMVolume(100f);
        AudioManager.SetSFXVolume(100f);
        if (GameRoot.Instance != null)
        {
            GameRoot.Instance.ApplyRenderScale(2);
            GameRoot.Instance.ApplyPostAa(0);
        }

        RefreshFromSettings();
        AudioManager.PlayUISFX("ui_click");
    }

    private void OnClosePressed()
    {
        AudioManager.PlayUISFX("ui_close");
        HidePanel<SettingsPanel>();
    }
}
