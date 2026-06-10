using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class World : Node2D
{
	private const int Cols = 8;
	private const int Rows = 8;
	private const int TileW = 64;
	private const int TileH = 32;
	private const int GridOriginY = 180;

	private static readonly PackedScene OreScene = GD.Load<PackedScene>("res://scenes/Ore.tscn");
	private static readonly PackedScene CharacterScene = GD.Load<PackedScene>("res://scenes/Character.tscn");
	private static readonly PackedScene PetScene = GD.Load<PackedScene>("res://scenes/Pet.tscn");
	private static readonly PackedScene BuildingScene = GD.Load<PackedScene>("res://scenes/Building.tscn");

	private static readonly System.Collections.Generic.Dictionary<string, string> TerrainPaths = new()
	{
		["grass"] = "res://assets/terrain/terrain_grass.png",
		["dirt"] = "res://assets/terrain/terrain_dirt.png",
		["stone"] = "res://assets/terrain/terrain_stone.png",
	};

	private class OreInfo
	{
		public int Hp;
		public string Reward;
		public int Amount;
		public float Chance;
		public string Texture;
	}

	private static readonly System.Collections.Generic.Dictionary<string, OreInfo> OreDataTable = new()
	{
		["coal"] = new OreInfo { Hp = 40, Reward = "coins", Amount = 8, Chance = 0.22f, Texture = "res://assets/ores/ore_coal.png" },
		["iron"] = new OreInfo { Hp = 80, Reward = "coins", Amount = 25, Chance = 0.16f, Texture = "res://assets/ores/ore_iron.png" },
		["gold"] = new OreInfo { Hp = 130, Reward = "coins", Amount = 80, Chance = 0.10f, Texture = "res://assets/ores/ore_gold.png" },
		["crystal"] = new OreInfo { Hp = 100, Reward = "gems", Amount = 3, Chance = 0.07f, Texture = "res://assets/ores/ore_crystal.png" },
		["ruby"] = new OreInfo { Hp = 200, Reward = "gems", Amount = 10, Chance = 0.04f, Texture = "res://assets/ores/ore_ruby.png" },
		["meteor"] = new OreInfo { Hp = 350, Reward = "gems", Amount = 15, Chance = 0.0f, Texture = "res://assets/ores/ore_gold.png" },
	};

	private readonly System.Collections.Generic.Dictionary<string, Texture2D> _terrainCache = new();
	private Node _characterNode;
	private readonly List<Ore> _oreNodes = new();

	// Ore types whose texture file actually exists on disk. Built once at startup
	// so we never roll an ore we can't draw (which would just spawn nothing).
	private readonly List<string> _availableOreTypes = new();

	// Tracks which grid cells are taken (buildings, character, live ores) so
	// ores never respawn on top of something.
	private readonly HashSet<Vector2I> _occupiedCells = new();
	private static readonly Vector2I CharacterCell = new(Cols / 2, Rows / 2);

	private double _meteorTimer = 0.0;
	private double _nextMeteorTime = 0.0;

	private struct BuildingDef
	{
		public int Col;
		public int Row;
		public string Type;
		public string Label;
		public string Texture;
		public Color Color;
	}

	// Add or edit buildings here. Texture is optional — if the file is missing
	// a colored placeholder with the label is shown instead.
	private static readonly BuildingDef[] BuildingDefs =
	{
		new BuildingDef { Col = 1, Row = 1, Type = "shop",     Label = "Shop",     Texture = "res://assets/buildings/shop.png",     Color = new Color(0.40f, 0.35f, 0.62f) },
		new BuildingDef { Col = 6, Row = 1, Type = "hatchery", Label = "Hatchery", Texture = "res://assets/buildings/hatchery.png", Color = new Color(0.62f, 0.45f, 0.30f) },
	};

	public override void _Ready()
	{
		GD.Randomize();
		Lighting.BuildWorldLighting(this);
		BuildAvailableOreTypes();
		BuildTerrain();
		SpawnBuildings();
		SpawnOres();
		SpawnCharacter();
		ResetMeteorTimer();
		GD.Print("Game Started");
	}

	// Figure out which ore types we can actually display. Anything missing its
	// PNG is left out so it never gets rolled. Add the texture file later and it
	// joins the pool automatically — no code change needed.
	private void BuildAvailableOreTypes()
	{
		_availableOreTypes.Clear();
		foreach (var kv in OreDataTable)
		{
			if (ResourceLoader.Exists(kv.Value.Texture))
				_availableOreTypes.Add(kv.Key);
		}
		if (_availableOreTypes.Count == 0)
			GD.PushWarning("No ore textures found in assets/ores/ — no ores will spawn.");
	}

	private Vector2 GridToScreen(int col, int row)
	{
		float screenCenterX = GetViewport().GetVisibleRect().Size.X / 2.0f;
		float x = screenCenterX + (col - row) * (TileW / 2.0f);
		float y = GridOriginY + (col + row) * (TileH / 2.0f);
		return new Vector2(x, y);
	}

	private Texture2D LoadTerrain(string typeName)
	{
		if (_terrainCache.TryGetValue(typeName, out var cached))
			return cached;

		string path = TerrainPaths.GetValueOrDefault(typeName, "");
		Texture2D tex = null;
		if (path != "" && ResourceLoader.Exists(path))
			tex = GD.Load<Texture2D>(path);

		_terrainCache[typeName] = tex;
		return tex;
	}

	private Texture2D ResolveTerrain(string preferred)
	{
		var tex = LoadTerrain(preferred);
		if (tex != null) return tex;
		return LoadTerrain("grass");
	}

	private void BuildTerrain()
	{
		var tileLayer = GetNode<Node2D>("TileLayer");
		string[] tileTypes = { "grass", "dirt", "stone" };
		float[] weights = { 0.7f, 0.2f, 0.1f };

		for (int row = 0; row < Rows; row++)
		{
			for (int col = 0; col < Cols; col++)
			{
				string typeName = WeightedRandom(tileTypes, weights);
				var tex = ResolveTerrain(typeName);
				if (tex == null) continue;

				var sprite = new Sprite2D
				{
					Texture = Lighting.GetLitTexture(tex),
					Position = GridToScreen(col, row),
					ZIndex = col + row,
				};
				tileLayer.AddChild(sprite);
			}
		}
	}

	private void SpawnOres()
	{
		var objectLayer = GetNode<Node2D>("ObjectLayer");
		for (int row = 0; row < Rows; row++)
		{
			for (int col = 0; col < Cols; col++)
			{
				var cell = new Vector2I(col, row);
				if (_occupiedCells.Contains(cell)) continue;
				if (GD.Randf() >= 0.35f) continue;

				string oreType = RollOreType();
				if (oreType == "") continue;
				var info = OreDataTable[oreType];

				if (!ResourceLoader.Exists(info.Texture)) continue;

				var ore = OreScene.Instantiate<Ore>();
				var pos = GridToScreen(col, row);
				ore.Position = new Vector2(pos.X, pos.Y - 16);
				ore.ZIndex = col + row + 1;
				objectLayer.AddChild(ore);

				var data = new Dictionary
				{
					["hp"] = info.Hp,
					["reward"] = info.Reward,
					["amount"] = info.Amount,
					["chance"] = info.Chance,
					["texture"] = info.Texture,
				};
				ore.Setup(oreType, data);
				ore.Cell = cell;
				ore.World = this;
				ore.AddToGroup("ores");
				ore.OreClicked += OnOreClicked;
				_oreNodes.Add(ore);
				_occupiedCells.Add(cell);
			}
		}
	}

	private void SpawnBuildings()
	{
		// Reserve the character's home cell so nothing else lands on it.
		_occupiedCells.Add(CharacterCell);

		var objectLayer = GetNode<Node2D>("ObjectLayer");
		foreach (var def in BuildingDefs)
		{
			var cell = new Vector2I(def.Col, def.Row);
			_occupiedCells.Add(cell);

			var building = BuildingScene.Instantiate<Building>();
			var pos = GridToScreen(def.Col, def.Row);
			building.Position = new Vector2(pos.X, pos.Y - 8);
			objectLayer.AddChild(building);
			building.Setup(def.Type, def.Label, def.Texture, def.Color);
			building.BuildingClicked += OnBuildingClicked;
		}
	}

	private void OnBuildingClicked(string buildingType)
	{
		var hud = GetNodeOrNull<HUD>("UI/HUD");
		if (hud == null) return;

		if (buildingType == "shop")
			hud.OpenShop();
		else if (buildingType == "hatchery")
			hud.OpenHatchery();
	}

	// Called by an Ore when it is mined out. Frees its old cell, picks a random
	// free cell elsewhere on the grid, and returns the screen position to move to.
	public Vector2 RequestRespawnPosition(Ore ore)
	{
		_occupiedCells.Remove(ore.Cell);

		var freeCells = new List<Vector2I>();
		for (int row = 0; row < Rows; row++)
			for (int col = 0; col < Cols; col++)
			{
				var cell = new Vector2I(col, row);
				if (!_occupiedCells.Contains(cell))
					freeCells.Add(cell);
			}

		Vector2I chosen = freeCells.Count > 0
			? freeCells[(int)(GD.Randi() % (uint)freeCells.Count)]
			: ore.Cell;

		_occupiedCells.Add(chosen);
		ore.Cell = chosen;

		var pos = GridToScreen(chosen.X, chosen.Y);
		return new Vector2(pos.X, pos.Y - 16);
	}

	private void SpawnCharacter()
	{
		if (!ResourceLoader.Exists("res://assets/character/character_idle.png"))
			return;
		var objectLayer = GetNode<Node2D>("ObjectLayer");
		_characterNode = CharacterScene.Instantiate<Node>();
		var node2d = _characterNode as Node2D;
		if (node2d != null)
		{
			node2d.Position = GridToScreen(Cols / 2, Rows / 2);
			node2d.ZIndex = 999;
			// Character art is a tall, centered sprite, so its feet sit well below
			// the node origin. Drop the shadow down near the feet (tune if needed).
			Lighting.AttachDropShadow(node2d, 96f, 60f);
		}
		objectLayer.AddChild(_characterNode);
	}

	private string RollOreType()
	{
		if (_availableOreTypes.Count == 0) return "";

		// Roll only among ore types we can actually draw, weighted by their Chance
		// and renormalized so the odds always sum to 1 (an ore is always chosen).
		float total = 0f;
		foreach (var type in _availableOreTypes)
			total += OreDataTable[type].Chance;

		float r = GD.Randf() * total;
		float cum = 0f;
		foreach (var type in _availableOreTypes)
		{
			cum += OreDataTable[type].Chance;
			if (r < cum) return type;
		}
		return _availableOreTypes[_availableOreTypes.Count - 1];
	}

	private string WeightedRandom(string[] items, float[] weights)
	{
		float r = GD.Randf();
		float cum = 0f;
		for (int i = 0; i < items.Length; i++)
		{
			cum += weights[i];
			if (r < cum) return items[i];
		}
		return items[0];
	}

	private void OnOreClicked(Ore oreNode)
	{
		oreNode.TakeDamage(GameManager.Instance.GetClickDamage());

		if (_characterNode is Character ch)
			ch.SetTarget(oreNode);

		foreach (var node in GetTree().GetNodesInGroup("pets"))
		{
			if (node is Pet pet)
				pet.TargetOre = oreNode;
		}
	}

	public override void _Process(double delta)
	{
		var objectLayer = GetNode<Node2D>("ObjectLayer");
		foreach (var child in objectLayer.GetChildren())
		{
			if (child is Node2D n2d)
				n2d.ZIndex = (int)n2d.Position.Y;
		}

		_meteorTimer += delta;
		if (_meteorTimer >= _nextMeteorTime)
		{
			ResetMeteorTimer();
			TriggerMeteorCrash();
		}
	}

	public void SpawnPet(Dictionary petData)
	{
		if (!ResourceLoader.Exists("res://scenes/Pet.tscn"))
			return;
		var pet = PetScene.Instantiate<Pet>();
		int col = Cols / 2 + (int)GD.RandRange(-2, 2);
		int row = Rows / 2 + (int)GD.RandRange(-2, 2);
		pet.Position = GridToScreen(col, row);
		GetNode<Node2D>("ObjectLayer").AddChild(pet);
		pet.AddToGroup("pets");
		pet.Setup(petData);
	}

	private void ResetMeteorTimer()
	{
		_meteorTimer = 0.0;
		_nextMeteorTime = GD.RandRange(45.0, 90.0); // Crash every 45-90 seconds
	}

	private void TriggerMeteorCrash()
	{
		var freeCells = new List<Vector2I>();
		for (int row = 0; row < Rows; row++)
		{
			for (int col = 0; col < Cols; col++)
			{
				var cell = new Vector2I(col, row);
				if (!_occupiedCells.Contains(cell))
					freeCells.Add(cell);
			}
		}

		if (freeCells.Count == 0) return;
		Vector2I chosen = freeCells[(int)(GD.Randi() % (uint)freeCells.Count)];
		_occupiedCells.Add(chosen);

		var objectLayer = GetNode<Node2D>("ObjectLayer");
		var ore = OreScene.Instantiate<Ore>();
		var pos = GridToScreen(chosen.X, chosen.Y);
		ore.Position = new Vector2(pos.X, pos.Y - 16);
		ore.ZIndex = chosen.X + chosen.Y + 1;
		objectLayer.AddChild(ore);

		var info = OreDataTable["meteor"];
		var data = new Dictionary
		{
			["hp"] = info.Hp,
			["reward"] = info.Reward,
			["amount"] = info.Amount,
			["chance"] = info.Chance,
			["texture"] = info.Texture,
		};
		ore.Setup("meteor", data);
		ore.Cell = chosen;
		ore.World = this;
		ore.AddToGroup("ores");
		ore.OreClicked += OnOreClicked;
		_oreNodes.Add(ore);

		// Visually modify: bigger scale, glowing gold tint
		var sprite = ore.GetNode<Sprite2D>("OreSprite");
		if (sprite != null)
			sprite.SelfModulate = new Color(1.5f, 1.3f, 0.4f, 1f);
		ore.Scale = new Vector2(1.5f, 1.5f);

		ScreenShake();
		ShowAnnouncement("A Golden Meteor has crashed!");
	}

	private void ScreenShake()
	{
		Vector2 originalPos = Position;
		var tween = CreateTween();
		for (int i = 0; i < 4; i++)
		{
			Vector2 offset = new Vector2((float)GD.RandRange(-6, 6), (float)GD.RandRange(-6, 6));
			tween.TweenProperty(this, "position", originalPos + offset, 0.05);
		}
		tween.TweenProperty(this, "position", originalPos, 0.05);
	}

	private void ShowAnnouncement(string message)
	{
		var hud = GetNodeOrNull<HUD>("UI/HUD");
		hud?.ShowAnnouncement(message);
	}

	public void RemoveMeteor(Ore ore)
	{
		_occupiedCells.Remove(ore.Cell);
		_oreNodes.Remove(ore);
	}

	// A big golden burst at the character's position, played when the player
	// successfully prestiges.
	public void SpawnPrestigeBurst()
	{
		var objectLayer = GetNode<Node2D>("ObjectLayer");
		Vector2 pos = (_characterNode as Node2D)?.GlobalPosition ?? GridToScreen(Cols / 2, Rows / 2);
		Effects.SpawnBurst(objectLayer, pos, new Color(1f, 0.85f, 0.2f), amount: 40, scale: 2f, lifetime: 1.0f);
	}
}
