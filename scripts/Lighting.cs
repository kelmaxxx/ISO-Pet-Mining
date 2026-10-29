using Godot;
using System.Collections.Generic;

// Fake-3D lighting for the 2D isometric world.
//
// How it works (no new art needed):
//   * Each sprite's texture is wrapped in a CanvasTexture that carries a
//     NORMAL MAP: a sibling "_n.png" file if one exists, otherwise one is
//     generated from the art's own brightness. DayNightCycle's sun then shades
//     each pixel by the direction it faces -> flat sprites look rounded.
//   * AnimatedSprite2D frames get the same treatment (AnimatedSprite2D has no
//     normal_map property; wrapping each frame texture is the equivalent).
//   * DayNightCycle also drives a CanvasModulate ambient tint.
//   * ShadowCaster2D drops a flattened replica of the sprite on the ground;
//     AttachDropShadow remains the cheap option for blocky buildings.
//
// Normal generation is tunable below. If the relief looks "engraved" instead
// of "raised", flip NormalYSign.
public static class Lighting
{
	// Normal-map generation.
	private const float NormalStrength = 4.0f; // bump intensity (raise for more relief)
	private const float NormalYSign = 1.0f;    // flip to -1 if relief looks inverted

	// Drop shadow.
	private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.32f);

	// ---- Caches -------------------------------------------------------------

	private static readonly Dictionary<ulong, CanvasTexture> _litCache = new();
	private static Texture2D _shadowTex;
	private static Texture2D _glowTex;

	// -------------------------------------------------------------------------
	// World setup: ambient + sun. Call once from World._Ready().
	// -------------------------------------------------------------------------
	public static DayNightCycle BuildWorldLighting(Node2D worldRoot)
	{
		var cycle = new DayNightCycle { Name = "DayNightCycle" };
		worldRoot.AddChild(cycle);
		return cycle;
	}

	// -------------------------------------------------------------------------
	// Returns a CanvasTexture (diffuse + normal map) for a texture.
	// An authored sibling "<name>_n.png" wins over the generated normal.
	// Cached & shared, so 64 grass tiles only generate one normal map.
	// Falls back to the plain texture if anything goes wrong.
	// -------------------------------------------------------------------------
	public static Texture2D GetLitTexture(Texture2D diffuse)
	{
		if (diffuse == null) return null;
		if (diffuse is CanvasTexture existing) return existing;

		ulong key = diffuse.GetRid().Id;
		if (_litCache.TryGetValue(key, out var cached))
			return cached;

		var normal = LoadAuthoredNormal(diffuse) ?? GenerateNormalMap(diffuse);
		if (normal == null)
			return diffuse;

		// Generated normals use the whole texture image; atlas frames need a
		// per-region normal when real atlas art lands.
		var ct = new CanvasTexture
		{
			DiffuseTexture = diffuse,
			NormalTexture = normal,
			SpecularTexture = GenerateSpecularMap(diffuse),
			SpecularColor = Colors.White,
			SpecularShininess = 12f,
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

	// Wraps every frame of an AnimatedSprite2D in a normal-mapped CanvasTexture.
	// Safe to call more than once / on shared SpriteFrames: lit frames are skipped.
	public static void MakeLit(AnimatedSprite2D animated)
	{
		var frames = animated?.SpriteFrames;
		if (frames == null) return;

		foreach (var anim in frames.GetAnimationNames())
		{
			int count = frames.GetFrameCount(anim);
			for (int i = 0; i < count; i++)
			{
				var frame = frames.GetFrameTexture(anim, i);
				if (frame == null || frame is CanvasTexture) continue;
				var lit = GetLitTexture(frame);
				if (!ReferenceEquals(lit, frame))
					frames.SetFrame(anim, i, lit, frames.GetFrameDuration(anim, i));
			}
		}
	}

	// "<folder>/<name>.png" -> "<folder>/<name>_n.png" when that file exists.
	private static Texture2D LoadAuthoredNormal(Texture2D diffuse)
	{
		string path = diffuse.ResourcePath;
		if (string.IsNullOrEmpty(path)) return null;

		int dot = path.LastIndexOf('.');
		if (dot <= 0) return null;

		string normalPath = path[..dot] + "_n" + path[dot..];
		return ResourceLoader.Exists(normalPath) ? GD.Load<Texture2D>(normalPath) : null;
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

		// Blend a blurred copy in: painted pixel-art shading is a broad gradient,
		// and per-pixel gradients alone only catch outlines (flat interiors).
		float[,] smooth = Blur(height, w, h);
		for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
				height[x, y] = height[x, y] * 0.35f + smooth[x, y] * 0.65f;

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

	// Bright pixels reflect more: metal, gems and highlights glint under the sun.
	private static ImageTexture GenerateSpecularMap(Texture2D source)
	{
		Image img = source.GetImage();
		if (img == null) return null;
		if (img.IsCompressed())
			img.Decompress();

		int w = img.GetWidth();
		int h = img.GetHeight();
		var specImg = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
			{
				Color c = img.GetPixel(x, y);
				float lum = 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
				float spec = Mathf.Pow(lum, 3f);
				specImg.SetPixel(x, y, new Color(spec, spec, spec, c.A));
			}
		return ImageTexture.CreateFromImage(specImg);
	}

	// Separable 5-tap blur, edges clamped. Feeds broad shading into the normals.
	public static float[,] Blur(float[,] src, int w, int h)
	{
		int[] taps = { 1, 4, 6, 4, 1 };
		const float sum = 16f;
		var tmp = new float[w, h];
		var dst = new float[w, h];

		for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
			{
				float acc = 0f;
				for (int k = -2; k <= 2; k++)
					acc += src[Mathf.Clamp(x + k, 0, w - 1), y] * taps[k + 2];
				tmp[x, y] = acc / sum;
			}

		for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
			{
				float acc = 0f;
				for (int k = -2; k <= 2; k++)
					acc += tmp[x, Mathf.Clamp(y + k, 0, h - 1)] * taps[k + 2];
				dst[x, y] = acc / sum;
			}
		return dst;
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

		const int w = 32, h = 16;
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

	// Soft white radial falloff, used for the tower orb and its PointLight2D.
	public static Texture2D GetGlowTexture()
	{
		if (_glowTex != null) return _glowTex;

		const int s = 64;
		var img = Image.CreateEmpty(s, s, false, Image.Format.Rgba8);
		float c = (s - 1) / 2f;
		for (int y = 0; y < s; y++)
			for (int x = 0; x < s; x++)
			{
				float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
				float a = Mathf.Clamp(1f - d, 0f, 1f);
				img.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
			}
		_glowTex = ImageTexture.CreateFromImage(img);
		return _glowTex;
	}
}
