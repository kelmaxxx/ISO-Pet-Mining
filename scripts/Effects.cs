using Godot;

public static class Effects
{
    private static Texture2D _particleTex;

    public static void SpawnBurst(Node parent, Vector2 globalPosition, Color color, int amount = 16, float scale = 1f, float lifetime = 0.6f)
    {
        if (parent == null) return;

        var fade = new Gradient();
        fade.SetColor(0, new Color(1f, 1f, 1f, 1f));
        fade.SetColor(1, new Color(1f, 1f, 1f, 0f));

        var mat = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, -1, 0),
            Spread = 180f,
            InitialVelocityMin = 60f * scale,
            InitialVelocityMax = 160f * scale,
            Gravity = new Vector3(0, 300f, 0),
            DampingMin = 30f,
            DampingMax = 80f,
            ScaleMin = 1.5f * scale,
            ScaleMax = 3f * scale,
            Color = color,
            ColorRamp = new GradientTexture1D { Gradient = fade },
        };

        var particles = new GPUParticles2D
        {
            Texture = GetParticleTexture(),
            Amount = amount,
            OneShot = true,
            Explosiveness = 1f,
            Lifetime = lifetime,
            ProcessMaterial = mat,
        };

        parent.AddChild(particles);
        particles.GlobalPosition = globalPosition; // must be set after AddChild to resolve against the parent's transform
        particles.Emitting = true;

        parent.GetTree().CreateTimer(lifetime + 0.2).Timeout += particles.QueueFree;
    }

    // A small soft white circle, generated once and reused for every burst.
    private static Texture2D GetParticleTexture()
    {
        if (_particleTex != null) return _particleTex;

        const int size = 16;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float c = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x - c, y - c).Length() / c;
                float a = Mathf.Clamp(1f - d, 0f, 1f);
                img.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }
        _particleTex = ImageTexture.CreateFromImage(img);
        return _particleTex;
    }
}
