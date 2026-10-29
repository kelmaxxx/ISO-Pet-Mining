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

		int writes = GameManager.Instance.SaveWrites;
		GameManager.Instance.AddCoins(1);
		Check(GameManager.Instance.SaveWrites == writes,
			"coin changes don't write the save immediately");

		var world = GD.Load<PackedScene>("res://scenes/World.tscn").Instantiate<Node2D>();
		AddChild(world);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		var objectLayer = world.GetNode<Node2D>("ObjectLayer");

		var camera = world.GetNodeOrNull<Camera2D>("Camera");
		Check(camera != null && camera.Enabled && camera.IsCurrent(), "world camera is active");
		Check(AudioManager.Instance != null, "audio manager autoload exists");
		AudioManager.Instance?.PlayHit();
		AudioManager.Instance?.PlayCoin();
		AudioManager.Instance?.PlayHatch();

		// The full-screen backdrop used to swallow every click on the map.
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		var oreNodes = GetTree().GetNodesInGroup("ores");
		if (oreNodes.Count > 0)
		{
			var ore = (Ore)oreNodes[0];
			float hpBefore = ore.CurrentHp;
			Vector2 clickPos = GetViewport().GetCanvasTransform() * (ore.GlobalPosition + new Vector2(0, -16));
			GetViewport().PushInput(new InputEventMouseButton
			{
				ButtonIndex = MouseButton.Left, Pressed = true, Position = clickPos,
				GlobalPosition = clickPos, ButtonMask = MouseButtonMask.Left,
			});
			GetViewport().PushInput(new InputEventMouseButton
			{
				ButtonIndex = MouseButton.Left, Pressed = false, Position = clickPos,
				GlobalPosition = clickPos,
			});
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			Check(ore.CurrentHp < hpBefore, "clicking an ore damages it");
		}

		int savedPets = GameManager.Instance.EquippedPets.Count;
		int spawnedPets = GetTree().GetNodesInGroup("pets").Count;
		Check(savedPets == 0 || spawnedPets >= savedPets,
			$"saved pets spawned ({spawnedPets}/{savedPets})");

		Check(GameManager.CalculateOfflineCoins(10f, 3600) == 36000,
			"offline income pays pet dps for the time away");
		Check(GameManager.CalculateOfflineCoins(10f, GameManager.MaxOfflineSeconds + 5000)
			== GameManager.CalculateOfflineCoins(10f, GameManager.MaxOfflineSeconds),
			"offline income is capped");
		Check(GameManager.CalculateOfflineCoins(10f, -60) == 0,
			"clock going backwards earns nothing");

		var character = objectLayer.GetNodeOrNull<Character>("Character");
		Check(character != null, "character node spawned");
		if (character != null)
		{
			var sprite = character.GetNode<AnimatedSprite2D>("Sprite");
			Check(sprite.SpriteFrames?.HasAnimation("idle") == true, "character has an idle animation");
			Check(sprite.SpriteFrames?.HasAnimation("attack") == true, "character has an attack animation");
			Check(sprite.SpriteFrames?.GetFrameTexture("idle", 0) != null, "character frame has a texture");
			Check(sprite.SpriteFrames?.GetFrameTexture("idle", 0) is CanvasTexture { NormalTexture: not null },
				"character frames carry normal maps");
			var shadowCaster = character.GetNodeOrNull<ShadowCaster2D>("ShadowCaster");
			Check(shadowCaster?.Shadow != null && shadowCaster.Shadow.LightMask == 0,
				"character has an unlit replica shadow");
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
		Check(petSprite.SpriteFrames?.GetFrameTexture("walk", 0) is CanvasTexture { NormalTexture: not null },
			"pet frames carry normal maps");
		Check(pet.GetNodeOrNull<ShadowCaster2D>("ShadowCaster")?.Shadow != null, "pet has a replica shadow");
		pet.QueueFree();

		var cycle = world.GetNodeOrNull<DayNightCycle>("DayNightCycle");
		Check(cycle?.Sun != null, "day/night cycle drives the sun");
		if (cycle?.GetNodeOrNull<CanvasModulate>("Ambient") is CanvasModulate ambient)
		{
			float noon = ambient.Color.R;
			cycle.SetTimeOfDay(0f);
			Check(ambient.Color.R < noon, "night lowers the ambient light");
			cycle.SetTimeOfDay(0.5f);
		}

		var distinctTiles = new HashSet<ulong>();
		var tileLayer = world.GetNode<Node2D>("TileLayer");
		Check(tileLayer.GetChildCount() == 64,
			$"map is the 64-tile plus island ({tileLayer.GetChildCount()} tiles)");
		Check(world.GetNodeOrNull<Sprite2D>("IslandShadow")?.Texture != null,
			"island casts a soft shadow");
		var tower = objectLayer.GetNodeOrNull<Node2D>("SpawnTower");
		Check(tower?.GetNodeOrNull<PointLight2D>("OrbLight") != null,
			"spawn tower has a lit orb");
		bool tilesFillCells = true;
		foreach (var tile in tileLayer.GetChildren())
		{
			if (tile is not Sprite2D { Texture: not null } s) continue;
			tilesFillCells &= s.Scale == Vector2.One && !s.Centered;
			// Lighting wraps each diffuse in a CanvasTexture; the wrapper RID is shared.
			var diffuse = s.Texture is CanvasTexture canvas ? canvas.DiffuseTexture : s.Texture;
			distinctTiles.Add(diffuse.GetRid().Id);
		}
		Check(tilesFillCells, "terrain tiles fill their cells (no gaps)");
		var grass = GD.Load<Texture2D>("res://assets/terrain/terrain_grass.png");
		Check(distinctTiles.Count == 1 && distinctTiles.Contains(grass.GetRid().Id),
			$"map is grass only (got {distinctTiles.Count} tile looks)");

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
		Check(meteorSprite.Texture is CanvasTexture { NormalTexture: not null }, "ore texture has a normal map");
		var gold = GD.Load<Texture2D>("res://assets/ores/ore_gold.png");
		var isGold = meteorSprite.Texture is CanvasTexture ct && ct.DiffuseTexture == gold;
		Check(!isGold, "meteor does not reuse the gold ore art");
		meteor.QueueFree();

		var hud = world.GetNode<HUD>("UI/HUD");
		var inventory = hud.GetNode<PetInventory>("PetInventory");
		inventory.Open();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		int listedPets = inventory
			.GetNode<VBoxContainer>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/ScrollContainer/PetList")
			.GetChildCount();
		Check(listedPets == GameManager.Instance.EquippedPets.Count,
			$"pet inventory lists every pet ({listedPets}/{GameManager.Instance.EquippedPets.Count})");
		inventory.Close();

		hud.ShowHatchReveal(new Dictionary
		{
			["name"] = "Test Pup",
			["icon"] = "dog",
			["power"] = 3,
			["rarity"] = "common",
		});
		Check(hud.GetNode<Control>("HatchReveal").Visible, "hatch reveal overlay opens");
		Check(hud.GetNode<Label>("HatchReveal/CenterContainer/PanelContainer/MarginContainer/VBoxContainer/NameLabel").Text == "Test Pup",
			"hatch reveal shows the pet name");
		hud.OnCloseHatchReveal();

		if (GameManager.Instance.EquippedPets.Count >= GameManager.Instance.MaxPets)
		{
			int coinsBefore = GameManager.Instance.Coins;
			var denied = GameManager.Instance.RollEgg("basic");
			Check(denied.Count == 0 && GameManager.Instance.Coins == coinsBefore,
				"full roster refuses an egg without charging");
		}

		GD.Print(_failures == 0 ? "VISUAL SMOKE: PASS" : $"VISUAL SMOKE: FAIL ({_failures})");
		GetTree().Quit(_failures == 0 ? 0 : 1);
	}

	private void Check(bool ok, string label)
	{
		GD.Print(ok ? $"  PASS {label}" : $"  FAIL {label}");
		if (!ok) _failures++;
	}
}
