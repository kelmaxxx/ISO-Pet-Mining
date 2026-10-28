using Godot;
using Godot.Collections;
using System.Collections.Generic;

// Headless check for the Step 1 visual fallbacks.
// Run: godot --headless --path . res://tests/VisualSmoke.tscn
public partial class VisualSmoke : Node
{
	private int _failures;

	public override async void _Ready()
	{
		// The test loads the real save; stop the idle tick so it can't earn/save.
		GameManager.Instance.SetProcess(false);

		var size = GetTree().Root.ContentScaleSize;
		Check(size == new Vector2I(640, 360), $"internal viewport is 640x360 (got {size.X}x{size.Y})");

		var world = GD.Load<PackedScene>("res://scenes/World.tscn").Instantiate<Node2D>();
		AddChild(world);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		var objectLayer = world.GetNode<Node2D>("ObjectLayer");

		int savedPets = GameManager.Instance.EquippedPets.Count;
		int spawnedPets = GetTree().GetNodesInGroup("pets").Count;
		Check(savedPets == 0 || spawnedPets >= savedPets,
			$"saved pets spawned ({spawnedPets}/{savedPets})");

		var character = objectLayer.GetNodeOrNull<Character>("Character");
		Check(character != null, "character node spawned");
		if (character != null)
		{
			var sprite = character.GetNode<AnimatedSprite2D>("Sprite");
			Check(sprite.SpriteFrames?.HasAnimation("idle") == true, "character has an idle animation");
			Check(sprite.SpriteFrames?.HasAnimation("attack") == true, "character has an attack animation");
			Check(sprite.SpriteFrames?.GetFrameTexture("idle", 0) != null, "character frame has a texture");
		}

		var pet = GD.Load<PackedScene>("res://scenes/Pet.tscn").Instantiate<Pet>();
		objectLayer.AddChild(pet);
		pet.Setup(new Dictionary
		{
			["name"] = "Test Pup",
			["icon"] = "dog",
			["power"] = 3,
			["rarity"] = "common",
		});
		var petSprite = pet.GetNode<AnimatedSprite2D>("Sprite");
		Check(petSprite.SpriteFrames?.HasAnimation("walk") == true, "pet has a walk animation");
		Check(petSprite.SpriteFrames?.HasAnimation("attack") == true, "pet has an attack animation");
		Check(petSprite.SpriteFrames?.GetFrameTexture("walk", 0) != null, "pet frame has a texture");
		pet.QueueFree();

		var distinctTiles = new HashSet<ulong>();
		var tileLayer = world.GetNode<Node2D>("TileLayer");
		foreach (var tile in tileLayer.GetChildren())
		{
			if (tile is not Sprite2D { Texture: not null } s) continue;
			// Lighting wraps each diffuse in a CanvasTexture; the wrapper RID is shared.
			var diffuse = s.Texture is CanvasTexture canvas ? canvas.DiffuseTexture : s.Texture;
			distinctTiles.Add(diffuse.GetRid().Id);
		}
		Check(distinctTiles.Count >= 2, $"map shows more than one tile look (got {distinctTiles.Count})");

		var grass = GD.Load<Texture2D>("res://assets/terrain/terrain_grass.png");
		var dirt = PlaceholderArt.TerrainVariant(grass, "dirt");
		Check(dirt.GetWidth() == grass.GetWidth() && dirt.GetHeight() == grass.GetHeight(),
			"terrain variant keeps the tile size");
		Check(!System.Linq.Enumerable.SequenceEqual(grass.GetImage().GetData(), dirt.GetImage().GetData()),
			"terrain variant is recolored");

		var meteor = GD.Load<PackedScene>("res://scenes/Ore.tscn").Instantiate<Ore>();
		objectLayer.AddChild(meteor);
		meteor.Setup("meteor", new Dictionary
		{
			["hp"] = 350,
			["reward"] = "gems",
			["amount"] = 15,
			["chance"] = 0f,
			["texture"] = "res://assets/ores/ore_meteor.png",
		});
		var meteorSprite = meteor.GetNode<Sprite2D>("OreSprite");
		Check(meteorSprite.Texture != null, "meteor has a texture");
		var gold = GD.Load<Texture2D>("res://assets/ores/ore_gold.png");
		var isGold = meteorSprite.Texture is CanvasTexture ct && ct.DiffuseTexture == gold;
		Check(!isGold, "meteor does not reuse the gold ore art");
		meteor.QueueFree();

		GD.Print(_failures == 0 ? "VISUAL SMOKE: PASS" : $"VISUAL SMOKE: FAIL ({_failures})");
		GetTree().Quit(_failures == 0 ? 0 : 1);
	}

	private void Check(bool ok, string label)
	{
		GD.Print(ok ? $"  PASS {label}" : $"  FAIL {label}");
		if (!ok) _failures++;
	}
}
