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

	private static readonly System.Collections.Generic.Dictionary<string, string> TerrainPaths = new()
	{
		["grass"] = "res://assets/terrain/terrain_grass.png",
		["dirt"]  = "res://assets/terrain/terrain_dirt.png",
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
		["coal"]    = new OreInfo { Hp = 40,  Reward = "coins", Amount = 8,  Chance = 0.22f, Texture = "res://assets/ores/ore_coal.png" },
		["iron"]    = new OreInfo { Hp = 80,  Reward = "coins", Amount = 25, Chance = 0.16f, Texture = "res://assets/ores/ore_iron.png" },
		["gold"]    = new OreInfo { Hp = 130, Reward = "coins", Amount = 80, Chance = 0.10f, Texture = "res://assets/ores/ore_gold.png" },
		["crystal"] = new OreInfo { Hp = 100, Reward = "gems",  Amount = 3,  Chance = 0.07f, Texture = "res://assets/ores/ore_crystal.png" },
		["ruby"]    = new OreInfo { Hp = 200, Reward = "gems",  Amount = 10, Chance = 0.04f, Texture = "res://assets/ores/ore_ruby.png" },
	};

	private readonly System.Collections.Generic.Dictionary<string, Texture2D> _terrainCache = new();
	private Node _characterNode;
	private readonly List<Ore> _oreNodes = new();

	public override void _Ready()
	{
		GD.Randomize();
		BuildTerrain();
		SpawnOres();
		SpawnCharacter();
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
					Texture = tex,
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
					["hp"]      = info.Hp,
					["reward"]  = info.Reward,
					["amount"]  = info.Amount,
					["chance"]  = info.Chance,
					["texture"] = info.Texture,
				};
				ore.Setup(oreType, data);
				ore.AddToGroup("ores");
				ore.OreClicked += OnOreClicked;
				_oreNodes.Add(ore);
			}
		}
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
		}
		objectLayer.AddChild(_characterNode);
	}

	private string RollOreType()
	{
		float r = GD.Randf();
		float cum = 0f;
		foreach (var kv in OreDataTable)
		{
			cum += kv.Value.Chance;
			if (r < cum) return kv.Key;
		}
		return "";
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
}
