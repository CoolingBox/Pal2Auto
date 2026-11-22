// 文件：PortraitFrame.cs
// 描述：战斗里的角色头像框。切角八边形 + 金边 + 四角菱形钉（形状在 portrait_frame.gdshader 里）。
//       立绘走"铺满裁切"，因为仙剑2 的立绘比例有 1.28 / 1.0 / 0.8 三种，直接塞进固定框会拉变形。

using Godot;

public partial class PortraitFrame : Control
{
    private TextureRect _art;
    private ShaderMaterial _material;
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

        AddChild(_art);
        _art.Position = Vector2.Zero;
        _art.Size = Size;
    }

    /// <summary>换人。key 是 Resources/Image/Portrait 下的文件名（如 p01）。</summary>
    public void SetPortrait(string key)
    {
        EnsureBuilt();

        Texture2D texture = UiKit.LoadTexture("res://Resources/Image/Portrait/" + key + ".png");
        _art.Texture = texture;
        _art.Size = Size;
        ApplyCoverScale(texture);
    }

    /// <summary>边框色。敌我用不同的金，扫一眼就知道谁是谁。</summary>
    public void SetAccent(Color color)
    {
        EnsureBuilt();
        _material.SetShaderParameter("frame_color", color);
    }

    public void SetShape(float cut, float band)
    {
        EnsureBuilt();
        _material.SetShaderParameter("cut", cut);
        _material.SetShaderParameter("band", band);
    }

    /// <summary>
    /// 按"铺满裁切"算 UV 缩放：贴图比框更长的那一边多出来，从中间裁掉。
    /// 传 1.0 的一边不动，另一边缩到可见比例。
    /// </summary>
    private void ApplyCoverScale(Texture2D texture)
    {
        if (texture == null || Size.X <= 0f || Size.Y <= 0f)
        {
            return;
        }

        float textureAspect = (float)texture.GetWidth() / texture.GetHeight();
        float rectAspect = Size.X / Size.Y;

        Vector2 scale;
        if (textureAspect > rectAspect)
        {
            scale = new Vector2(rectAspect / textureAspect, 1f);
        }
        else
        {
            scale = new Vector2(1f, textureAspect / rectAspect);
        }

        _material.SetShaderParameter("uv_scale", scale);
    }
}
