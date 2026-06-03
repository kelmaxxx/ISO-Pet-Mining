using Godot;
using System.Collections.Generic;

// Fake-3D lighting for the 2D isometric world.
//
// How it works (no new art needed):
//   * Each sprite's texture is wrapped in a CanvasTexture that carries a
//     procedurally generated NORMAL MAP (derived from the art's own
//     brightness). A DirectionalLight2D "sun" then shades each pixel by the
//     direction it faces -> the flat sprite looks rounded / embossed.
//   * A CanvasModulate dims the scene slightly so the sun's light reads.
//   * A soft elliptical DROP SHADOW is dropped under each object to ground it.
//
// Everything is tunable from the constants below. If the relief looks
// "engraved" instead of "raised", flip NormalYSign.
public static class Lighting
{
	// ---- Tuning -------------------------------------------------------------

	// Ambient color the whole world is multiplied by. Keep it bright for the
	// "subtle daytime" look; lower it toward 0.4 for a moodier scene.
	private static readonly Color Ambient = new(0.78f, 0.78f, 0.84f);

	// The "sun". Energy is how much light it ADDS on top of the ambient.
	private static readonly Color SunColor = new(1.0f, 0.97f, 0.90f);
	private const float SunEnergy = 0.38f;     // higher = stronger relief & brighter
	private const float SunRotationDeg = 115f; // where the light comes from
	private const float SunHeight = 0.55f;     // 0..1, how grazing the light is on normals

	// Normal-map generation.
	private const float NormalStrength = 2.2f; // bump intensity (raise for more relief)
	private const float NormalYSign = 1.0f;    // flip to -1 if relief looks inverted

	// Drop shadow.
	private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.32f);

	// ---- Caches -------------------------------------------------------------

	private static readonly Dictionary<ulong, CanvasTexture> _litCache = new();
	private static Texture2D _shadowTex;

	// -------------------------------------------------------------------------
	// World setup: ambient + sun. Call once from World._Ready().
	// -------------------------------------------------------------------------
	public static void BuildWorldLighting(Node2D worldRoot)
	{
		var modulate = new CanvasModulate { Color = Ambient };
		worldRoot.AddChild(modulate);

		var sun = new DirectionalLight2D
		{
			Color = SunColor,
			Energy = SunEnergy,
			Height = SunHeight,
			RotationDegrees = SunRotationDeg,
			BlendMode = Light2D.BlendModeEnum.Add,
		};
		worldRoot.AddChild(sun);
	}

	// -------------------------------------------------------------------------
	// Returns a CanvasTexture (diffuse + generated normal map) for a texture.
	// Cached & shared, so 64 grass tiles only generate one normal map.
	// Falls back to the plain texture if anything goes wrong.
	// -------------------------------------------------------------------------
	public static Texture2D GetLitTexture(Texture2D diffuse)
	{
		if (diffuse == null) return null;

		ulong key = diffuse.GetRid().Id;
		if (_litCache.TryGetValue(key, out var cached))
			return cached;

		var normal = GenerateNormalMap(diffuse);
		if (normal == null)
			return diffuse;

		var ct = new CanvasTexture
		{
			DiffuseTexture = diffuse,
			NormalTexture = normal,
		};
		_litCache[key] = ct;
		return ct;
	}

	// Sets a Sprite2D's texture and swaps in the normal-mapped version.
	public static void MakeLit(Sprite2D sprite)
	{
		if (sprite?.Texture == null) return;
		sprite.Texture = GetLitTexture(sprite.Texture);
	}

	// -------------------------------------------------------------------------
	// Height-from-luminance -> Sobel -> tangent-space normal map.
	// -------------------------------------------------------------------------
	private static ImageTexture GenerateNormalMap(Texture2D source)
	{
		Image img = source.GetImage();
		if (img == null) return null;
		if (img.IsCompressed())
			img.Decompress();

		int w = img.GetWidth();
		int h = img.GetHeight();
		if (w < 2 || h < 2) return null;

		// Precompute a height field (luminance weighted by alpha so transparent
		// pixels read as "flat ground", which gives a nice beveled silhouette).
		var height = new float[w, h];
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				Color c = img.GetPixel(x, y);
				float lum = 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
				height[x, y] = lum * c.A;
			}
		}

		var normalImg = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				float hl = height[Mathf.Max(x - 1, 0), y];
				float hr = height[Mathf.Min(x + 1, w - 1), y];
				float hu = height[x, Mathf.Max(y - 1, 0)];
				float hd = height[x, Mathf.Min(y + 1, h - 1)];

				float dx = (hl - hr) * NormalStrength;
				float dy = (hu - hd) * NormalStrength * NormalYSign;

				var n = new Vector3(dx, dy, 1.0f).Normalized();
				var encoded = new Color(
					n.X * 0.5f + 0.5f,
					n.Y * 0.5f + 0.5f,
					n.Z * 0.5f + 0.5f,
					1.0f);
				normalImg.SetPixel(x, y, encoded);
			}
		}

		return ImageTexture.CreateFromImage(normalImg);
	}

	// -------------------------------------------------------------------------
	// Drop shadow: a soft black ellipse placed under an object. Not affected by
	// the sun (LightMask = 0) so it stays dark, and drawn behind its siblings.
	// localY is the ground-contact point in the owner's local space.
	// -------------------------------------------------------------------------
	public static void AttachDropShadow(Node2D owner, float localY, float width)
	{
		var shadow = new Sprite2D
		{
			Texture = GetShadowTexture(),
			Position = new Vector2(0, localY),
			Modulate = ShadowColor,
			LightMask = 0,         // ignore the sun so it stays dark
			ZIndex = -1,           // behind the object's own visuals
			ZAsRelative = true,
		};
		float baseW = shadow.Texture.GetWidth();
		float scale = width / baseW;
		shadow.Scale = new Vector2(scale, scale);

		owner.AddChild(shadow);
		owner.MoveChild(shadow, 0); // draw first => under everything else
	}

	private static Texture2D GetShadowTexture()
	{
		if (_shadowTex != null) return _shadowTex;

		const int w = 64, h = 32;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		float cx = (w - 1) / 2f;
		float cy = (h - 1) / 2f;
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				float nx = (x - cx) / cx; // -1..1
				float ny = (y - cy) / cy; // -1..1
				float d = Mathf.Sqrt(nx * nx + ny * ny);
				// Soft falloff: solid in the middle, fading to nothing at the rim.
				float a = Mathf.Clamp(1.0f - d, 0f, 1f);
				a = a * a; // ease for a softer edge
				img.SetPixel(x, y, new Color(1f, 1f, 1f, a));
			}
		}
		_shadowTex = ImageTexture.CreateFromImage(img);
		return _shadowTex;
	}
}
