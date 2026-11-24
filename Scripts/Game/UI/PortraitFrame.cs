// 文件：PortraitFrame.cs
// 描述：战斗里的角色头像框。切角矩形 + 带打光的金属边 + 内圈亮线 + 四角铆钉 + 顶边宝石扣
//       （形状与材质都在 portrait_frame.gdshader 里）。
//       立绘排版按"内容包围盒"算，不按整张贴图：
//         1. 先取不透明像素的包围盒，把四周透明留白去掉，不然小头像会被留白顶到边角
//         2. 整张立绘塞进框内可见区（外框 + 内圈亮线之内），上沿对齐并留一条缝 —— 头不能被内框切掉
//         3. 立绘比框高的时候只从下摆裁（胸以下压到框边下面），宽度尽量撑满

using Godot;

public partial class PortraitFrame : Control
{
    // 立绘最多裁掉多少：0.63 = 至少露出内容高度的 63%（头 + 一点脖子），剩下的用来把宽度撑满。
    // 仙剑2 的立绘都是半身，头大约占上面一半，低于 0.55 就会切到下巴。
    private const float MinVisibleHeight = 0.63f;

    // 内容上沿与内框之间的缝（占可见区高度）。太小头会贴着内框，像被裁掉。
    private const float TopGap = 0.075f;

    private TextureRect _art;
    private ShaderMaterial _material;
    private Texture2D _texture;
    private float _rim = 0.082f;
    private float _lip = 0.016f;
    private bool _built;

    public override void _Ready()
    {
        EnsureBuilt();
    }

    private void EnsureBuilt()
    {
        if (_built)
        {
            return;
        }
        _built = true;

        MouseFilter = MouseFilterEnum.Ignore;

        _art = new TextureRect();
        _art.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _art.StretchMode = TextureRect.StretchModeEnum.Scale;
        _art.MouseFilter = MouseFilterEnum.Ignore;

        _material = new ShaderMaterial();
        _material.Shader = ResourceLoader.Load<Shader>("res://Resources/Shaders/portrait_frame.gdshader");
        _art.Material = _material;
        ApplyAspect();

        AddChild(_art);
        _art.Position = Vector2.Zero;
        _art.Size = Size;
    }

    /// <summary>换人。key 是 Resources/Image/Portrait 下的文件名（如 p01）。</summary>
    public void SetPortrait(string key)
    {
        EnsureBuilt();

        _texture = UiKit.LoadTexture("res://Resources/Image/Portrait/" + key + ".png");
        _art.Texture = _texture;
        _art.Size = Size;
        ApplyAspect();
        ApplyFit();
    }

    /// <summary>边框色。敌我用不同的金，扫一眼就知道谁是谁。</summary>
    public void SetAccent(Color color)
    {
        EnsureBuilt();
        _material.SetShaderParameter("frame_color", color);
    }

    /// <summary>框形：cut = 切角大小，rim = 外框厚度（都按高度比例）。内圈亮线按外框的 1/5 自动给。</summary>
    public void SetShape(float cut, float rim)
    {
        EnsureBuilt();
        _rim = rim;
        _lip = rim * 0.2f;
        _material.SetShaderParameter("cut", cut);
        _material.SetShaderParameter("rim", rim);
        _material.SetShaderParameter("lip", _lip);
        ApplyFit();
    }

    /// <summary>解析抗锯齿开关。关掉就是硬台阶 —— 切角、宝石、铆片的边缘不再做 1 像素过渡。</summary>
    public void SetAntiAlias(bool enabled)
    {
        EnsureBuilt();
        _material.SetShaderParameter("aa_scale", enabled ? 1.0f : 0.0f);
    }

    public void SetGem(Color color, bool visible)
    {
        EnsureBuilt();
        _material.SetShaderParameter("gem_color", color);
        _material.SetShaderParameter("gem", visible ? 1f : 0f);
    }

    /// <summary>着色器按"以高度为 1"的坐标算边框，得知道宽高比，否则宽框的左右边会变粗。</summary>
    private void ApplyAspect()
    {
        if (_material == null || Size.Y <= 0f)
        {
            return;
        }
        _material.SetShaderParameter("aspect", Size.X / Size.Y);
    }

    /// <summary>
    /// 算立绘在框内的摆放：art_size 是整张贴图的显示尺寸，art_pos 是立绘中心落在框里的位置
    /// （两者都以框的宽高为 1）。着色器拿这两个值把贴图映射进来。
    /// </summary>
    private void ApplyFit()
    {
        if (_material == null || _texture == null)
        {
            return;
        }
        if (Size.X <= 0f || Size.Y <= 0f)
        {
            return;
        }

        float texW = _texture.GetWidth();
        float texH = _texture.GetHeight();
        if (texW <= 0f || texH <= 0f)
        {
            return;
        }

        // 不透明内容在贴图里的范围（uv）。取不到就当成满幅。
        float usedX = 0f;
        float usedY = 0f;
        float usedW = 1f;
        float usedH = 1f;
        Rect2I used = GetUsedRect(_texture);
        if (used.Size.X > 0 && used.Size.Y > 0)
        {
            usedX = used.Position.X / texW;
            usedY = used.Position.Y / texH;
            usedW = used.Size.X / texW;
            usedH = used.Size.Y / texH;
        }

        // 框里真正能看见画的区域：外框和内圈亮线以内。
        float inset = (_rim + _lip) * Size.Y;
        float innerW = Size.X - inset * 2.0f;
        float innerH = Size.Y - inset * 2.0f;
        if (innerW < 8.0f || innerH < 8.0f)
        {
            return;
        }

        float contentW = usedW * texW;
        float contentH = usedH * texH;

        // 两个约束取小的那个：撑满宽度，或者最多裁掉下摆（露出 MinVisibleHeight）。
        float scaleByWidth = innerW / contentW;
        float scaleByHeight = innerH / (contentH * MinVisibleHeight);
        float scale = Mathf.Min(scaleByWidth, scaleByHeight);

        float drawW = contentW * scale;
        float drawH = contentH * scale;

        // 比可见区矮就居中，比可见区高就上沿对齐 —— 头永远在内框下面露出来。
        float topGap = innerH * TopGap;
        float centerY;
        if (drawH <= innerH)
        {
            centerY = inset + innerH * 0.5f;
        }
        else
        {
            centerY = inset + topGap + drawH * 0.5f;
        }

        Vector2 artSize = new Vector2(drawW / (Size.X * usedW), drawH / (Size.Y * usedH));
        Vector2 contentCenter = new Vector2(usedX + usedW * 0.5f, usedY + usedH * 0.5f);
        Vector2 artPos = Vector2.Zero;
        artPos.X = 0.5f - (contentCenter.X - 0.5f) * artSize.X;
        artPos.Y = (centerY / Size.Y) - (contentCenter.Y - 0.5f) * artSize.Y;

        _material.SetShaderParameter("art_size", artSize);
        _material.SetShaderParameter("art_pos", artPos);
    }

    /// <summary>贴图里不透明像素的包围盒（像素坐标）。整张都透明就返回空。</summary>
    private static Rect2I GetUsedRect(Texture2D texture)
    {
        Image image = texture.GetImage();
        if (image == null)
        {
            return new Rect2I();
        }
        return image.GetUsedRect();
    }
}
