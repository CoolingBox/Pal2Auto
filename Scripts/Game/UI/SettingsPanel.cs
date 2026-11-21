// 文件：SettingsPanel.cs
// 描述：设置界面 —— 画质（xBRZ 开关）、战斗倍速、音乐/音效音量。
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
    private const float PanelH = 380f;

    private Control _root;
    private bool _built;

    private CheckButton _xbrzToggle;
    private Label _xbrzState;
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

        // —— 画质 ——
        Label xbrzLabel = UiKit.MakeLabel("画质：xBRZ 放大", 15, UiKit.Paper);
        UiKit.Place(xbrzLabel, PanelX + 32f, PanelY + 76f, 190f, 30f);
        _root.AddChild(xbrzLabel);

        _xbrzToggle = new CheckButton();
        _xbrzToggle.Position = new Vector2(PanelX + 250f, PanelY + 76f);
        _xbrzToggle.Size = new Vector2(60f, 30f);
        _xbrzToggle.FocusMode = Control.FocusModeEnum.None;
        _xbrzToggle.Toggled += OnXbrzToggled;
        _root.AddChild(_xbrzToggle);

        _xbrzState = UiKit.MakeLabel("", 11, UiKit.PaperDim);
        UiKit.Place(_xbrzState, PanelX + 32f, PanelY + 104f, PanelW - 64f, 20f);
        _root.AddChild(_xbrzState);

        // —— 战斗倍速 ——
        Label speedLabel = UiKit.MakeLabel("战斗倍速", 15, UiKit.Paper);
        UiKit.Place(speedLabel, PanelX + 32f, PanelY + 136f, 160f, 30f);
        _root.AddChild(speedLabel);

        _speedOption = new OptionButton();
        _speedOption.Position = new Vector2(PanelX + 200f, PanelY + 136f);
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
        UiKit.Place(bgmLabel, PanelX + 32f, PanelY + 186f, 60f, 30f);
        _root.AddChild(bgmLabel);

        _bgmSlider = new HSlider();
        _bgmSlider.Position = new Vector2(PanelX + 92f, PanelY + 192f);
        _bgmSlider.Size = new Vector2(230f, 18f);
        _bgmSlider.MinValue = 0;
        _bgmSlider.MaxValue = 100;
        _bgmSlider.FocusMode = Control.FocusModeEnum.None;
        _bgmSlider.ValueChanged += OnBgmChanged;
        _root.AddChild(_bgmSlider);

        StyleSlider(_bgmSlider);

        _bgmValue = UiKit.MakeLabel("", 12, UiKit.PaperDim);
        UiKit.Place(_bgmValue, PanelX + 330f, PanelY + 186f, 40f, 30f, HorizontalAlignment.Right);
        _root.AddChild(_bgmValue);

        // —— 音效 ——
        Label sfxLabel = UiKit.MakeLabel("音效", 15, UiKit.Paper);
        UiKit.Place(sfxLabel, PanelX + 32f, PanelY + 226f, 60f, 30f);
        _root.AddChild(sfxLabel);

        _sfxSlider = new HSlider();
        _sfxSlider.Position = new Vector2(PanelX + 92f, PanelY + 232f);
        _sfxSlider.Size = new Vector2(230f, 18f);
        _sfxSlider.MinValue = 0;
        _sfxSlider.MaxValue = 100;
        _sfxSlider.FocusMode = Control.FocusModeEnum.None;
        _sfxSlider.ValueChanged += OnSfxChanged;
        _root.AddChild(_sfxSlider);

        StyleSlider(_sfxSlider);

        _sfxValue = UiKit.MakeLabel("", 12, UiKit.PaperDim);
        UiKit.Place(_sfxValue, PanelX + 330f, PanelY + 226f, 40f, 30f, HorizontalAlignment.Right);
        _root.AddChild(_sfxValue);

        Label note = UiKit.MakeLabel("改动即时生效，自动保存。", 11, UiKit.Dim);
        UiKit.Place(note, PanelX + 32f, PanelY + 272f, PanelW - 64f, 20f);
        _root.AddChild(note);

        Button reset = UiKit.MakeButton("恢复默认", false);
        reset.Position = new Vector2(PanelX + 50f, PanelY + 316f);
        reset.Size = new Vector2(140f, 42f);
        reset.Pressed += OnResetPressed;
        _root.AddChild(reset);

        Button close = UiKit.MakeButton("关 闭", true);
        close.Position = new Vector2(PanelX + 210f, PanelY + 316f);
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

        if (_xbrzToggle != null)
        {
            _xbrzToggle.ButtonPressed = data.XbrzEnabled;
        }

        RefreshXbrzState();

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

    private void RefreshXbrzState()
    {
        if (_xbrzState == null)
        {
            return;
        }

        if (SettingManager.Data.XbrzEnabled)
        {
            _xbrzState.Text = "当前：960x540 离屏出图，边缘感知放大（斜角更圆）";
        }
        else
        {
            _xbrzState.Text = "当前：原像素放大（硬边，字口更利）";
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

    private void OnXbrzToggled(bool pressed)
    {
        SettingManager.Data.XbrzEnabled = pressed;
        SaveManager.Save(SettingManager.Data);

        if (GameRoot.Instance != null)
        {
            GameRoot.Instance.ApplyXbrz(pressed);
        }

        RefreshXbrzState();
        AudioManager.PlayUISFX("ui_click");
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
        SettingManager.Data.XbrzEnabled = true;
        SaveManager.Save(SettingManager.Data);

        AudioManager.SetVolume(100f);
        AudioManager.SetBGMVolume(100f);
        AudioManager.SetSFXVolume(100f);
        if (GameRoot.Instance != null)
        {
            GameRoot.Instance.ApplyXbrz(true);
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
