using Godot;

// Owns the world's ambient tint and one directional sun/moon.
// Phase 0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset.
// ShadowCaster2D reads the static values, so every shadow follows the same sun.
public partial class DayNightCycle : Node2D
{
	[Export(PropertyHint.Range, "0,1,0.01")] public float StartTime = 0.5f;
	[Export(PropertyHint.Range, "10,3600,10")] public float CycleSeconds = 300f;

	private const float SunHeight = 0.30f;
	private const float MinSunEnergy = 0.12f; // faint moon fill, keeps art readable
	private const float DaySunEnergy = 0.70f;

	private static readonly Color DayAmbient = new(0.45f, 0.46f, 0.52f);
	private static readonly Color NightAmbient = new(0.30f, 0.35f, 0.50f);
	private static readonly Color DayColor = new(1.0f, 0.97f, 0.90f);
	private static readonly Color DuskColor = new(1.0f, 0.62f, 0.38f);
	private static readonly Color MoonColor = new(0.55f, 0.70f, 1.0f);

	private CanvasModulate _ambient;
	private DirectionalLight2D _sun;
	private float _time;

	public bool Paused { get; set; }

	// Screen-space direction the sunlight travels (i.e. where shadows fall).
	public static Vector2 SunDirection { get; private set; } = Vector2.Down;
	public static float ShadowStrength { get; private set; } = 1f;
	public static float Daylight { get; private set; } = 1f;

	public DirectionalLight2D Sun => _sun;

	public override void _Ready()
	{
		_ambient = new CanvasModulate { Name = "Ambient" };
		AddChild(_ambient);

		// The HUD lives in its own CanvasLayer, so this light cannot reach it.
		_sun = new DirectionalLight2D
		{
			Name = "Sun",
			Height = SunHeight,
			BlendMode = Light2D.BlendModeEnum.Add,
		};
		AddChild(_sun);

		SetTimeOfDay(StartTime);
	}

	public override void _Process(double delta)
	{
		if (Paused) return;
		_time = Mathf.PosMod(_time + (float)delta / Mathf.Max(CycleSeconds, 1f), 1f);
		Apply();
	}

	// Also used by screenshots/tests to jump straight to a time of day.
	public void SetTimeOfDay(float phase)
	{
		_time = Mathf.PosMod(phase, 1f);
		Apply();
	}

	private void Apply()
	{
		float elevation = Mathf.Sin((_time - 0.25f) * Mathf.Tau);
		float day = Mathf.Clamp(elevation, 0f, 1f);
		Daylight = day;

		// Sweep the sun across the sky between sunrise and sunset. Rotation 0
		// points the light straight down; +Y is the direction the light travels.
		float dayT = Mathf.Clamp((_time - 0.25f) * 2f, 0f, 1f);
		float rotation = Mathf.Lerp(-45f, 45f, dayT);
		_sun.RotationDegrees = rotation;
		SunDirection = Vector2.Down.Rotated(Mathf.DegToRad(rotation));

		float warm = Mathf.SmoothStep(0f, 0.55f, elevation);
		_sun.Color = elevation > 0f ? DuskColor.Lerp(DayColor, warm) : MoonColor;
		_sun.Energy = MinSunEnergy + day * DaySunEnergy;
		_ambient.Color = NightAmbient.Lerp(DayAmbient, Mathf.SmoothStep(0f, 0.6f, elevation));
		ShadowStrength = Mathf.Lerp(0.18f, 1f, day);
	}
}
