// 文件：CharacterSelectPanel.cs
// 描述：选人界面 —— 六位侠客，左网格右详情。
//       确认后只写入 GameFlow.SelectedCharacterId 并给出提示：摆摊界面还没做，不假装能进。

using System.Collections.Generic;
using Godot;
using TinyFramework;

public class CharacterSelectPanel : BasePanel
{
    private const float W = 960f;
    private const float H = 540f;

    private const float CardWidth = 150f;
    private const float CardHeight = 184f;
    private const float CardGapX = 16f;
    private const float CardGapY = 12f;
    private const float GridX = 24f;
    private const float GridY = 86f;

    private const float DetailX = 522f;
    private const float DetailY = 80f;
    private const float DetailWidth = 414f;
    private const float DetailHeight = 400f;

    private Control _root;
    private bool _built;

    private readonly List<CharacterCardView> _cards = new List<CharacterCardView>();
    private List<CharacterConfig> _characters = new List<CharacterConfig>();
    private int _selectedId = 1;

    private TextureRect _detailPortrait;
    private Label _detailName;
    private Label _detailTitle;
    private Label _detailStyle;
    private Label _detailSpecialty;
    private Label _detailDesc;
    private TextureRect _starterIcon;
    private Label _starterName;
    private Label _starterDesc;

    private Control _confirmOverlay;
    private Label _confirmText;

    public override void OnBind(Control control)
    {
        base.OnBind(control);
        _root = control;
        // 版面按 960x540 写；工程画布是 1920x1080，整块按 2 倍放大铺满
        _root.Position = Vector2.Zero;
        _root.Size = new Vector2(960f, 540f);
        _root.Scale = new Vector2(2f, 2f);
        Build();
    }

    public override void OnShow()
    {
        base.OnShow();
        ReloadCharacters();
        if (_confirmOverlay != null)
        {
            _confirmOverlay.Visible = false;
        }
    }

    private void Build()
    {
        if (_built)
        {
            return;
        }
        _built = true;

        ColorRect ink = new ColorRect();
        ink.Color = new Color(0.050f, 0.045f, 0.040f);
        ink.Size = new Vector2(W, H);
        ink.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(ink);

        Gradient gradient = new Gradient();
        gradient.SetColor(0, new Color(0.135f, 0.115f, 0.088f));
        gradient.SetColor(1, new Color(0.042f, 0.038f, 0.034f));
        GradientTexture2D gradientTexture = new GradientTexture2D();
        gradientTexture.Gradient = gradient;
        gradientTexture.Width = 8;
        gradientTexture.Height = 540;
        gradientTexture.FillFrom = new Vector2(0f, 0f);
        gradientTexture.FillTo = new Vector2(0f, 1f);

        TextureRect backdrop = new TextureRect();
        backdrop.Texture = gradientTexture;
        backdrop.Size = new Vector2(W, H);
        backdrop.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        backdrop.StretchMode = TextureRect.StretchModeEnum.Scale;
        backdrop.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(backdrop);

        Label title = UiKit.MakeLabel("挑 一 个 能 打 的", 28, UiKit.Gold);
        UiKit.Place(title, 24f, 16f, 420f, 38f);
        _root.AddChild(title);

        Label hint = UiKit.MakeLabel("选谁只影响开局那件家伙；各人特长规则还在做。", 12, UiKit.Dim);
        UiKit.Place(hint, 24f, 54f, 460f, 22f);
        _root.AddChild(hint);

        Button back = UiKit.MakeButton("返 回", false);
        back.Position = new Vector2(846f, 16f);
        back.Size = new Vector2(90f, 34f);
        back.Pressed += OnBackPressed;
        _root.AddChild(back);

        Panel detail = new Panel();
        detail.Position = new Vector2(DetailX, DetailY);
        detail.Size = new Vector2(DetailWidth, DetailHeight);
        detail.MouseFilter = Control.MouseFilterEnum.Ignore;
        detail.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.100f, 0.088f, 0.074f), new Color(0.400f, 0.330f, 0.220f), 1, 6));
        _root.AddChild(detail);

        _detailPortrait = new TextureRect();
        _detailPortrait.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _detailPortrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _detailPortrait.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(_detailPortrait);
        _detailPortrait.Position = new Vector2(DetailX + 16f, DetailY + 16f);
        _detailPortrait.Size = new Vector2(180f, 140f);

        _detailName = UiKit.MakeLabel("", 26, UiKit.Paper);
        UiKit.Place(_detailName, DetailX + 208f, DetailY + 18f, 190f, 34f);
        _root.AddChild(_detailName);

        _detailTitle = UiKit.MakeLabel("", 14, UiKit.Gold);
        UiKit.Place(_detailTitle, DetailX + 208f, DetailY + 54f, 190f, 24f);
        _root.AddChild(_detailTitle);

        _detailStyle = UiKit.MakeLabel("", 12, UiKit.PaperDim);
        UiKit.Place(_detailStyle, DetailX + 208f, DetailY + 82f, 190f, 22f);
        _root.AddChild(_detailStyle);

        _detailSpecialty = UiKit.MakeLabel("", 13, new Color(0.760f, 0.680f, 0.520f));
        UiKit.Place(_detailSpecialty, DetailX + 16f, DetailY + 168f, DetailWidth - 32f, 44f);
        _detailSpecialty.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        _detailSpecialty.VerticalAlignment = VerticalAlignment.Top;
        _root.AddChild(_detailSpecialty);

        Label starterTag = UiKit.MakeLabel("开局携带", 12, UiKit.Dim);
        UiKit.Place(starterTag, DetailX + 16f, DetailY + 216f, 120f, 20f);
        _root.AddChild(starterTag);

        _starterIcon = new TextureRect();
        _starterIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _starterIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _starterIcon.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(_starterIcon);
        _starterIcon.Position = new Vector2(DetailX + 16f, DetailY + 238f);
        _starterIcon.Size = new Vector2(62f, 62f);

        _starterName = UiKit.MakeLabel("", 16, UiKit.Paper);
        UiKit.Place(_starterName, DetailX + 90f, DetailY + 242f, 300f, 24f);
        _root.AddChild(_starterName);

        _starterDesc = UiKit.MakeLabel("", 11, UiKit.PaperDim);
        UiKit.Place(_starterDesc, DetailX + 90f, DetailY + 262f, 306f, 46f);
        _starterDesc.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        _starterDesc.VerticalAlignment = VerticalAlignment.Top;
        _root.AddChild(_starterDesc);

        _detailDesc = UiKit.MakeLabel("", 12, UiKit.PaperDim);
        UiKit.Place(_detailDesc, DetailX + 16f, DetailY + 306f, 380f, 44f);
        _detailDesc.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        _detailDesc.VerticalAlignment = VerticalAlignment.Top;
        _root.AddChild(_detailDesc);

        Button confirm = UiKit.MakeButton("就 他 了", true);
        confirm.Position = new Vector2(DetailX + 212f, DetailY + 350f);
        confirm.Size = new Vector2(186f, 44f);
        confirm.AddThemeFontSizeOverride("font_size", 20);
        confirm.Pressed += OnConfirmPressed;
        _root.AddChild(confirm);

        Label note = UiKit.MakeLabel("※ 特长标注为预告，本版只生效初始物件", 11, UiKit.Dim);
        UiKit.Place(note, 24f, 478f, 460f, 20f);
        _root.AddChild(note);

        BuildConfirmOverlay();
    }

    /// <summary>确认后的提示层：摆摊界面还没做，这里给一个明确的"到此为止"，不糊弄。</summary>
    private void BuildConfirmOverlay()
    {
        _confirmOverlay = new Control();
        _confirmOverlay.Position = Vector2.Zero;
        _confirmOverlay.Size = new Vector2(W, H);
        _confirmOverlay.Visible = false;
        _confirmOverlay.MouseFilter = Control.MouseFilterEnum.Stop;
        _root.AddChild(_confirmOverlay);

        ColorRect dim = new ColorRect();
        dim.Color = new Color(0.02f, 0.02f, 0.02f, 0.72f);
        dim.Size = new Vector2(W, H);
        dim.MouseFilter = Control.MouseFilterEnum.Ignore;
        _confirmOverlay.AddChild(dim);

        Panel panel = new Panel();
        panel.Position = new Vector2(280f, 170f);
        panel.Size = new Vector2(400f, 210f);
        panel.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.105f, 0.090f, 0.074f), new Color(0.500f, 0.400f, 0.240f), 2, 8));
        _confirmOverlay.AddChild(panel);

        _confirmText = UiKit.MakeLabel("", 16, UiKit.Paper);
        UiKit.Place(_confirmText, 300f, 196f, 360f, 70f, HorizontalAlignment.Center);
        _confirmText.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        _confirmText.VerticalAlignment = VerticalAlignment.Top;
        _confirmOverlay.AddChild(_confirmText);

        Label sub = UiKit.MakeLabel("摆摊 / 打擂界面还没开始做，先把人选定下来。", 12, UiKit.Dim);
        UiKit.Place(sub, 300f, 268f, 360f, 22f, HorizontalAlignment.Center);
        _confirmOverlay.AddChild(sub);

        Button backToMenu = UiKit.MakeButton("回 主 菜 单", true);
        backToMenu.Position = new Vector2(380f, 306f);
        backToMenu.Size = new Vector2(200f, 44f);
        backToMenu.Pressed += OnBackToMenuPressed;
        _confirmOverlay.AddChild(backToMenu);
    }

    private void ReloadCharacters()
    {
        _characters = ConfigManager.GetAll<CharacterConfig>();
        if (_characters == null)
        {
            _characters = new List<CharacterConfig>();
        }

        if (_cards.Count == 0)
        {
            for (int i = 0; i < _characters.Count; i++)
            {
                int col = i % 3;
                int row = i / 3;

                CharacterCardView card = new CharacterCardView();
                card.Position = new Vector2(GridX + col * (CardWidth + CardGapX),
                                            GridY + row * (CardHeight + CardGapY));
                card.Size = new Vector2(CardWidth, CardHeight);
                card.Clicked += OnCardClicked;
                _root.AddChild(card);
                _cards.Add(card);
            }
        }

        for (int i = 0; i < _cards.Count && i < _characters.Count; i++)
        {
            _cards[i].SetCharacter(_characters[i]);
        }

        int wanted = GameFlow.SelectedCharacterId > 0 ? GameFlow.SelectedCharacterId : 1;
        Select(wanted);
    }

    private void Select(int characterId)
    {
        _selectedId = characterId;

        for (int i = 0; i < _cards.Count; i++)
        {
            _cards[i].SetSelected(_cards[i].CharacterId == characterId);
        }

        CharacterConfig cfg = FindCharacter(characterId);
        if (cfg == null)
        {
            return;
        }

        _detailPortrait.Texture = UiKit.LoadTexture("res://Resources/Image/Portrait/" + cfg.Portrait + ".png");
        _detailName.Text = cfg.Name;
        _detailTitle.Text = cfg.Title;
        _detailStyle.Text = cfg.Style;
        _detailSpecialty.Text = "特长（预告）：" + cfg.Specialty;
        _detailDesc.Text = cfg.Desc;

        _starterIcon.Texture = UiKit.LoadTexture("res://Resources/Image/Items/" + cfg.StarterIcon + ".png");
        _starterName.Text = cfg.StarterName;
        _starterDesc.Text = cfg.StarterDesc;
    }

    private CharacterConfig FindCharacter(int id)
    {
        for (int i = 0; i < _characters.Count; i++)
        {
            if (_characters[i].Id == id)
            {
                return _characters[i];
            }
        }
        return null;
    }

    private void OnCardClicked(int characterId)
    {
        AudioManager.PlayUISFX("ui_click");
        Select(characterId);
    }

    private void OnConfirmPressed()
    {
        AudioManager.PlayUISFX("ui_click");
        GameFlow.SelectedCharacterId = _selectedId;

        CharacterConfig cfg = FindCharacter(_selectedId);
        if (cfg != null && _confirmText != null)
        {
            _confirmText.Text = "已选：" + cfg.Name + "（" + cfg.Title + "）";
        }

        if (_confirmOverlay != null)
        {
            // 卡片是在 Build 之后动态加的，浮层要先提到最前，否则会被卡面盖住
            _confirmOverlay.MoveToFront();
            _confirmOverlay.Visible = true;
        }
    }

    private void OnBackToMenuPressed()
    {
        AudioManager.PlayUISFX("ui_close");
        if (_confirmOverlay != null)
        {
            _confirmOverlay.Visible = false;
        }
        UIManager.ShowPanel<MainMenuPanel>();
    }

    private void OnBackPressed()
    {
        AudioManager.PlayUISFX("ui_close");
        UIManager.ShowPanel<MainMenuPanel>();
    }
}
