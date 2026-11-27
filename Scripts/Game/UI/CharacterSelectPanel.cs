// 文件：CharacterSelectPanel.cs
// 描述：选人界面 —— 四位侠客（王小虎 / 沈欺霜 / 苏媚 / 李忆如），左网格右详情。
//       确认后写入 GameFlow.SelectedCharacterId 并进入局内（RunPanel）。

using System.Collections.Generic;
using Godot;
using TinyFramework;

public class CharacterSelectPanel : BasePanel
{
    private const float W = 960f;
    private const float H = 540f;

    private const float CardWidth = 232f;
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
    private readonly TextureRect[] _deckIcons = new TextureRect[16];
    private Label _deckSummary;


    public override void OnBind(Control control)
    {
        base.OnBind(control);
        _root = control;
        // 版面按 960x540 写，和离屏画布同尺寸，不需要缩放
        _root.Position = Vector2.Zero;
        _root.Size = new Vector2(960f, 540f);
        Build();
    }

    public override void OnShow()
    {
        base.OnShow();
        ReloadCharacters();
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

        Label hint = UiKit.MakeLabel("选人即选牌组：十二张牌决定你打得多快、扛得多住。", 12, UiKit.Dim);
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

        Label deckTag = UiKit.MakeLabel("随身牌组", 12, UiKit.Dim);
        UiKit.Place(deckTag, DetailX + 16f, DetailY + 216f, 120f, 20f);
        _root.AddChild(deckTag);

        for (int i = 0; i < _deckIcons.Length; i++)
        {
            TextureRect icon = new TextureRect();
            icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            icon.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChildIcon(icon, i);
        }

        _deckSummary = UiKit.MakeLabel("", 11, UiKit.PaperDim);
        UiKit.Place(_deckSummary, DetailX + 200f, DetailY + 216f, 190f, 20f);
        _root.AddChild(_deckSummary);

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

        Label note = UiKit.MakeLabel("※ 随身牌组已生效；每人特长规则仍是预告", 11, UiKit.Dim);
        UiKit.Place(note, 24f, 478f, 460f, 20f);
        _root.AddChild(note);

    }

    /// <summary>12 个牌位按 6 列 2 行摆开：一眼看出这套牌是快是慢、攻多还是守多。</summary>
    private void AddChildIcon(TextureRect icon, int index)
    {
        int col = index % 8;
        int row = index / 8;
        _root.AddChild(icon);
        icon.Position = new Vector2(DetailX + 16f + col * 30f, DetailY + 240f + row * 30f);
        icon.Size = new Vector2(28f, 28f);
        _deckIcons[index] = icon;
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
                int col = i % 2;
                int row = i / 2;

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

        RefreshDeck(cfg);
    }

    /// <summary>把这位侠客的 12 张牌铺成图标，并给一句配比摘要。</summary>
    private void RefreshDeck(CharacterConfig cfg)
    {
        int weapons = 0;
        int spells = 0;
        int guards = 0;
        int heals = 0;
        int poisons = 0;
        int supports = 0;

        int deckCount = 0;
        if (cfg.Deck != null)
        {
            deckCount = cfg.Deck.Length;
        }

        for (int i = 0; i < deckCount; i++)
        {
            CardConfig card = ConfigManager.GetOne<CardConfig>(cfg.Deck[i]);
            if (card == null)
            {
                continue;
            }

            if (card.Type == CardType.Spell)
            {
                spells++;
            }
            else if (card.Type == CardType.Guard)
            {
                guards++;
            }
            else if (card.Type == CardType.Heal)
            {
                heals++;
            }
            else if (card.Type == CardType.Poison)
            {
                poisons++;
            }
            else if (card.Type == CardType.Support)
            {
                supports++;
            }
            else
            {
                weapons++;
            }
        }

        // 图标位可能比牌少（16 格 vs 最多 14 张），够用就把多的清空
        for (int i = 0; i < _deckIcons.Length; i++)
        {
            if (cfg.Deck == null || i >= cfg.Deck.Length)
            {
                _deckIcons[i].Texture = null;
                continue;
            }

            CardConfig card = ConfigManager.GetOne<CardConfig>(cfg.Deck[i]);
            if (card != null)
            {
                _deckIcons[i].Texture = UiKit.LoadTexture("res://Resources/Image/Items/" + card.Icon + ".png");
            }
        }

        // 一行放得下才算配比：分隔点省掉，靠空格分组
        _deckSummary.Text = "兵" + weapons + " 术" + spells + " 守" + guards +
                            " 疗" + heals + " 毒" + poisons + " 器" + supports;
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
        GameFlow.InRun = true;

        // 选人即开局：这一局的天数、银两、声望、牌组都在 RunState 里，战斗只是其中一个时辰
        RunManager.Instance.StartRun(_selectedId, 0);

        UIManager.ShowPanel<RunPanel>();
        UIManager.HidePanel<CharacterSelectPanel>();
    }

    private void OnBackPressed()
    {
        AudioManager.PlayUISFX("ui_close");
        UIManager.ShowPanel<MainMenuPanel>();
    }
}
