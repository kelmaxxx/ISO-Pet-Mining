using Godot;
using System.Collections.Generic;

// Procedural stand-in art so the game renders before the real PNGs exist.
// Callers only use these when a file or animation is missing, so adding real
// art later automatically wins without touching this class.
public static class PlaceholderArt
{
	private static readonly Color Outline = new(0.08f, 0.07f, 0.10f);
	private static readonly Color Skin = new(0.96f, 0.79f, 0.62f);
	private static readonly Color Shirt = new(0.18f, 0.55f, 0.52f);
	private static readonly Color ShirtDark = new(0.12f, 0.40f, 0.40f);
	private static readonly Color Pants = new(0.20f, 0.25f, 0.40f);
	private static readonly Color Boots = new(0.38f, 0.24f, 0.15f);
	private static readonly Color Helmet = new(1.00f, 0.78f, 0.20f);
	private static readonly Color HelmetDark = new(0.80f, 0.55f, 0.12f);
	private static readonly Color Wood = new(0.48f, 0.30f, 0.16f);
	private static readonly Color Steel = new(0.72f, 0.75f, 0.80f);

	private static SpriteFrames _character;
	private static Texture2D _meteor;
	private static Texture2D _tower;
	private static readonly Dictionary<string, SpriteFrames> _pets = new();

	// ------------------------------------------------------------- character --
	public static SpriteFrames CharacterFrames()
	{
		if (_character != null) return _character;

		_character = new SpriteFrames();
		AddAnim(_character, "idle", 2f,
			DrawCharacter(0, 0, false),
			DrawCharacter(1, 0, false));
		AddAnim(_character, "walk", 8f,
			DrawCharacter(0, -2, false),
			DrawCharacter(1, 0, false),
			DrawCharacter(0, 2, false),
			DrawCharacter(1, 0, false));
		AddAnim(_character, "attack", 8f,
			DrawCharacter(0, 0, true, true),
			DrawCharacter(0, 0, true, false));
		return _character;
	}

	private static Image DrawCharacter(int bob, int legSwing, bool attacking, bool pickUp = false)
	{
		var img = Canvas();

		DrawLeg(img, 25 + legSwing);
		DrawLeg(img, 34 - legSwing);

		// torso
		Ellipse(img, 32, 37 + bob, 12, 11, Outline);
		Ellipse(img, 32, 37 + bob, 11, 10, Shirt);
		Ellipse(img, 32, 41 + bob, 6, 6, ShirtDark);

		// arms + hands
		Rect(img, 19, 31 + bob, 5, 12, Outline);
		Rect(img, 20, 32 + bob, 4, 10, Shirt);
		Rect(img, 20, 42 + bob, 4, 4, Skin);
		Rect(img, 40, 31 + bob, 5, 12, Outline);
		Rect(img, 40, 32 + bob, 4, 10, Shirt);
		Rect(img, 40, 42 + bob, 4, 4, Skin);

		// head
		Ellipse(img, 32, 21 + bob, 10, 9, Outline);
		Ellipse(img, 32, 21 + bob, 9, 8, Skin);

		// helmet
		Ellipse(img, 32, 15 + bob, 10, 6, Outline);
		Ellipse(img, 32, 15 + bob, 9, 5, Helmet);
		Rect(img, 21, 18 + bob, 23, 3, Outline);
		Rect(img, 22, 18 + bob, 21, 2, HelmetDark);

		// eyes
		Rect(img, 27, 21 + bob, 2, 3, Outline);
		Rect(img, 35, 21 + bob, 2, 3, Outline);

		if (attacking) DrawPickaxe(img, pickUp);
		return img;
	}

	private static void DrawLeg(Image img, int x)
	{
		Rect(img, x, 45, 6, 10, Outline);
		Rect(img, x + 1, 45, 4, 9, Pants);
		Rect(img, x - 1, 53, 8, 5, Outline);
		Rect(img, x, 53, 6, 4, Boots);
	}

	private static void DrawPickaxe(Image img, bool raised)
	{
		if (raised)
		{
			Rect(img, 44, 8, 3, 18, Wood);
			Rect(img, 38, 6, 14, 3, Outline);
			Rect(img, 39, 7, 12, 2, Steel);
		}
		else
		{
			Rect(img, 46, 28, 3, 18, Wood);
			Rect(img, 41, 44, 16, 3, Outline);
			Rect(img, 42, 45, 14, 2, Steel);
		}
	}

	// ------------------------------------------------------------------ pets --
	public static SpriteFrames PetFrames(string icon, string rarity)
	{
		icon = string.IsNullOrEmpty(icon) ? "dog" : icon.ToLowerInvariant();
		rarity = string.IsNullOrEmpty(rarity) ? "common" : rarity.ToLowerInvariant();
		string key = icon + "|" + rarity;
		if (_pets.TryGetValue(key, out var cached)) return cached;

		var frames = new SpriteFrames();
		AddAnim(frames, "idle", 3f,
			DrawPet(icon, rarity, 0),
			DrawPet(icon, rarity, -1));
		AddAnim(frames, "walk", 6f,
			DrawPet(icon, rarity, 0),
			DrawPet(icon, rarity, -1),
			DrawPet(icon, rarity, 0),
			DrawPet(icon, rarity, 1));
		AddAnim(frames, "attack", 8f,
			DrawPet(icon, rarity, -1, -2),
			DrawPet(icon, rarity, 1, 4));
		_pets[key] = frames;
		return frames;
	}

	private static Image DrawPet(string icon, string rarity, int bob, int lean = 0)
	{
		var img = Canvas();
		Color body = icon switch
		{
			"dog" => new Color(0.72f, 0.50f, 0.30f),
			"rabbit" => new Color(0.86f, 0.85f, 0.90f),
			"cat" => new Color(0.90f, 0.60f, 0.25f),
			"fox" => new Color(0.88f, 0.42f, 0.18f),
			"bear" => new Color(0.44f, 0.30f, 0.20f),
			"wolf" => new Color(0.50f, 0.56f, 0.66f),
			"eagle" => new Color(0.90f, 0.86f, 0.72f),
			"dragon" => new Color(0.55f, 0.35f, 0.80f),
			_ => new Color(0.65f, 0.65f, 0.68f),
		};
		Color dark = body.Darkened(0.28f);
		Color light = body.Lightened(0.22f);
		Color accent = rarity switch
		{
			"rare" => new Color(0.30f, 0.65f, 1.00f),
			"epic" => new Color(0.72f, 0.38f, 1.00f),
			"legendary" => new Color(1.00f, 0.80f, 0.20f),
			_ => new Color(0.65f, 0.66f, 0.72f),
		};
		int x = 32 + lean;
		int y = bob;

		switch (icon)
		{
			case "eagle":
				Ellipse(img, x - 14, 36 + y, 9, 6, Outline);
				Ellipse(img, x - 14, 36 + y, 8, 5, light);
				Ellipse(img, x + 14, 36 + y, 9, 6, Outline);
				Ellipse(img, x + 14, 36 + y, 8, 5, light);
				break;
			case "dragon":
				Ellipse(img, x - 14, 35 + y, 9, 7, Outline);
				Ellipse(img, x - 14, 35 + y, 8, 6, dark);
				Ellipse(img, x + 14, 35 + y, 9, 7, Outline);
				Ellipse(img, x + 14, 35 + y, 8, 6, dark);
				break;
			default:
				Ellipse(img, x + 17, 42 + y, 6, 3, Outline);
				Ellipse(img, x + 17, 42 + y, 5, 2, dark);
				break;
		}

		DrawEars(img, icon, x, y, body, dark);

		// body
		Ellipse(img, x, 40 + y, 16, 11, Outline);
		Ellipse(img, x, 40 + y, 15, 10, body);
		Ellipse(img, x, 44 + y, 10, 6, light);

		// head
		Ellipse(img, x, 24 + y, 12, 11, Outline);
		Ellipse(img, x, 24 + y, 11, 10, body);

		// rarity shows as a collar under the chin
		Rect(img, x - 9, 34 + y, 18, 2, accent);

		if (icon == "eagle")
		{
			Rect(img, x - 2, 26 + y, 5, 3, Outline);
			Rect(img, x - 1, 26 + y, 3, 2, new Color(1f, 0.62f, 0.15f));
		}
		else
		{
			Ellipse(img, x, 28 + y, 7, 4, light);
			Rect(img, x - 1, 28 + y, 3, 2, Outline);
		}

		// eyes
		Rect(img, x - 6, 21 + y, 2, 3, Outline);
		Rect(img, x + 4, 21 + y, 2, 3, Outline);
		Rect(img, x - 6, 21 + y, 1, 1, new Color(1f, 1f, 1f, 0.8f));
		Rect(img, x + 4, 21 + y, 1, 1, new Color(1f, 1f, 1f, 0.8f));

		return img;
	}

	private static void DrawEars(Image img, string icon, int x, int y, Color body, Color dark)
	{
		switch (icon)
		{
			case "rabbit":
				Rect(img, x - 7, 6 + y, 4, 14, Outline);
				Rect(img, x - 6, 7 + y, 2, 12, body);
				Rect(img, x + 3, 6 + y, 4, 14, Outline);
				Rect(img, x + 4, 7 + y, 2, 12, body);
				break;
			case "cat":
			case "fox":
			case "wolf":
				Rect(img, x - 11, 14 + y, 6, 7, Outline);
				Rect(img, x - 10, 12 + y, 4, 4, Outline);
				Rect(img, x - 10, 15 + y, 4, 5, dark);
				Rect(img, x - 9, 13 + y, 2, 3, dark);
				Rect(img, x + 5, 14 + y, 6, 7, Outline);
				Rect(img, x + 6, 12 + y, 4, 4, Outline);
				Rect(img, x + 6, 15 + y, 4, 5, dark);
				Rect(img, x + 7, 13 + y, 2, 3, dark);
				break;
			case "bear":
				Ellipse(img, x - 9, 17 + y, 6, 6, Outline);
				Ellipse(img, x - 9, 17 + y, 5, 5, body);
				Ellipse(img, x + 9, 17 + y, 6, 6, Outline);
				Ellipse(img, x + 9, 17 + y, 5, 5, body);
				break;
			case "dog":
				Ellipse(img, x - 11, 25 + y, 5, 8, Outline);
				Ellipse(img, x - 11, 25 + y, 4, 7, dark);
				Ellipse(img, x + 11, 25 + y, 5, 8, Outline);
				Ellipse(img, x + 11, 25 + y, 4, 7, dark);
				break;
			case "dragon":
				Rect(img, x - 8, 10 + y, 3, 7, new Color(1f, 0.85f, 0.35f));
				Rect(img, x + 5, 10 + y, 3, 7, new Color(1f, 0.85f, 0.35f));
				break;
		}
	}

	// ---------------------------------------------------------------- tower --
	// 48x80 stone spawn tower, drawn bottom-centre so World can anchor it on
	// the front corner of its 2x2 footprint.
	public static Texture2D TowerTexture()
	{
		if (_tower != null) return _tower;

		var img = Image.CreateEmpty(48, 80, false, Image.Format.Rgba8);
		var stone = new Color(0.58f, 0.60f, 0.68f);
		var stoneDark = new Color(0.40f, 0.42f, 0.50f);
		var stoneLight = new Color(0.72f, 0.74f, 0.82f);
		var gold = new Color(1f, 0.80f, 0.30f);

		Rect(img, 2, 70, 44, 10, Outline);
		Rect(img, 4, 71, 40, 7, stoneDark);

		Rect(img, 9, 26, 30, 45, Outline);
		Rect(img, 11, 27, 26, 43, stone);
		Rect(img, 30, 27, 7, 43, stoneDark);
		for (int y = 36; y < 68; y += 10)
			Rect(img, 11, y, 26, 1, stoneDark);

		Rect(img, 5, 18, 38, 9, Outline);
		Rect(img, 7, 19, 34, 6, stoneLight);
		for (int x = 6; x <= 34; x += 14)
		{
			Rect(img, x, 10, 9, 9, Outline);
			Rect(img, x + 1, 11, 7, 7, stoneLight);
		}

		Rect(img, 19, 58, 10, 14, Outline);
		Ellipse(img, 24, 58, 5, 5, Outline);
		Rect(img, 21, 60, 6, 12, new Color(0.12f, 0.11f, 0.16f));
		Ellipse(img, 24, 60, 3, 3, new Color(0.12f, 0.11f, 0.16f));

		Rect(img, 11, 31, 26, 2, gold);
		Rect(img, 22, 44, 4, 7, Outline);
		Rect(img, 23, 45, 2, 5, gold);

		_tower = ImageTexture.CreateFromImage(img);
		return _tower;
	}

	// --------------------------------------------------------------- meteor --
	public static Texture2D MeteorTexture()
	{
		if (_meteor != null) return _meteor;

		var img = Canvas();
		const float cx = 31.5f;
		const float cy = 33f;

		for (int y = 0; y < 64; y++)
		{
			for (int x = 0; x < 64; x++)
			{
				float dx = x - cx;
				float dy = y - cy;
				float dist = Mathf.Sqrt(dx * dx + dy * dy);
				float angle = Mathf.Atan2(dy, dx);
				float radius = 24f
					+ 1.8f * Mathf.Sin(angle * 5f)
					+ 1.2f * Mathf.Sin(angle * 9f + 1.3f);

				if (dist > radius + 2f) continue;
				if (dist > radius)
				{
					img.SetPixel(x, y, new Color(1f, 0.75f, 0.20f, (radius + 2f - dist) / 2f * 0.35f));
					continue;
				}
				if (dist > radius - 1.2f)
				{
					img.SetPixel(x, y, Outline);
					continue;
				}

				float shade = 0.55f + 0.6f * Mathf.Clamp(0.5f - (dx + dy) / (2f * radius), 0f, 1f);
				var col = new Color(0.20f * shade, 0.17f * shade, 0.19f * shade, 1f);

				// glowing gold veins
				float vein = Mathf.Abs(Mathf.Sin(angle * 3f + dist * 0.35f));
				float vein2 = Mathf.Abs(Mathf.Sin(angle * 5f - dist * 0.22f + 0.7f));
				if (dist < radius - 3f && (vein < 0.16f || vein2 < 0.09f))
					col = vein < 0.10f
						? new Color(1f, 0.88f, 0.35f)
						: new Color(0.95f, 0.58f, 0.12f);

				// hot rim
				if (dist > radius - 3.5f)
					col = col.Lerp(new Color(1f, 0.55f, 0.10f), 0.55f);

				img.SetPixel(x, y, col);
			}
		}

		// floating sparks
		Rect(img, 12, 14, 2, 2, new Color(1f, 0.85f, 0.25f, 0.9f));
		Rect(img, 51, 18, 2, 2, new Color(1f, 0.85f, 0.25f, 0.8f));
		Rect(img, 48, 47, 2, 2, new Color(1f, 0.65f, 0.15f, 0.8f));

		_meteor = ImageTexture.CreateFromImage(img);
		return _meteor;
	}

	// -------------------------------------------------------------- helpers --
	private static Image Canvas()
	{
		return Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
	}

	private static void Rect(Image img, int x, int y, int w, int h, Color color)
	{
		for (int yy = y; yy < y + h; yy++)
			for (int xx = x; xx < x + w; xx++)
				if (xx >= 0 && yy >= 0 && xx < img.GetWidth() && yy < img.GetHeight())
					img.SetPixel(xx, yy, color);
	}

	private static void Ellipse(Image img, int cx, int cy, int rx, int ry, Color color)
	{
		for (int y = cy - ry; y <= cy + ry; y++)
			for (int x = cx - rx; x <= cx + rx; x++)
			{
				float dx = (x - cx) / (float)rx;
				float dy = (y - cy) / (float)ry;
				if (dx * dx + dy * dy <= 1f)
					Rect(img, x, y, 1, 1, color);
			}
	}

	private static void AddAnim(SpriteFrames frames, string name, float fps, params Image[] images)
	{
		frames.AddAnimation(name);
		frames.SetAnimationSpeed(name, fps);
		frames.SetAnimationLoopMode(name, SpriteFrames.LoopMode.Linear);
		foreach (var image in images)
			frames.AddFrame(name, ImageTexture.CreateFromImage(image));
	}
}
