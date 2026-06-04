using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Text.Json;

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	public int Coins { get; private set; } = 0;
	public int Gems { get; private set; } = 0;

	// --- Prestige / Rebirth (PS99-style) ---
	// Damage and coin rewards scale with the prestige multiplier. Rebirthing wipes
	// your coins but permanently raises the multiplier. Gems and pets are KEPT.
	public int PrestigeLevel { get; private set; } = 0;
	public int PrestigeCost { get; private set; } = 1000;
	public float PrestigeMultiplier => 1f + PrestigeLevel * 0.5f;

	// Flat damage knobs — there is no pickaxe level anymore. Damage = clicks + pets.
	private const float BaseClickDamage = 6f;
	private const float PetDpsFactor = 0.5f;

	public Array<Dictionary> EquippedPets { get; private set; } = new();
	public int MaxPets { get; private set; } = 5;

	public bool CoinBoost { get; private set; } = false;
	public double CoinBoostEnd { get; private set; } = 0.0;
	public bool LuckyBoost { get; private set; } = false;
	public double LuckyBoostEnd { get; private set; } = 0.0;

	[Signal] public delegate void CoinsChangedEventHandler(int amount);
	[Signal] public delegate void GemsChangedEventHandler(int amount);
	[Signal] public delegate void PetAddedEventHandler(Dictionary petData);
	[Signal] public delegate void PrestigeChangedEventHandler(int level, float multiplier, int nextCost);

	public override void _EnterTree()
	{
		Instance = this;
	}

	// ── Save / Load ──────────────────────────────────────────────────────────
	private const string SavePath = "user://save.json";

	public void SaveGame()
	{
		// Serialize pets as a plain list of string-keyed dicts so JSON can handle them.
		var petsData = new List<System.Collections.Generic.Dictionary<string, object>>();
		foreach (var pet in EquippedPets)
		{
			var p = new System.Collections.Generic.Dictionary<string, object>();
			foreach (var kv in pet)
				p[kv.Key.AsString()] = kv.Value.Obj;
			petsData.Add(p);
		}

		var data = new System.Collections.Generic.Dictionary<string, object>
		{
			["coins"]          = Coins,
			["gems"]           = Gems,
			["prestige_level"] = PrestigeLevel,
			["prestige_cost"]  = PrestigeCost,
			["pets"]           = petsData,
		};

		string json = JsonSerializer.Serialize(data);
		using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
		file.StoreString(json);
	}

	public void LoadGame()
	{
		if (!FileAccess.FileExists(SavePath)) return;
		using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
		string json = file.GetAsText();
		var data = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, JsonElement>>(json);
		if (data == null) return;

		if (data.TryGetValue("coins", out var c))          Coins          = c.GetInt32();
		if (data.TryGetValue("gems",  out var g))          Gems           = g.GetInt32();
		if (data.TryGetValue("prestige_level", out var pl)) PrestigeLevel = pl.GetInt32();
		if (data.TryGetValue("prestige_cost",  out var pc)) PrestigeCost  = pc.GetInt32();

		if (data.TryGetValue("pets", out var petsEl) && petsEl.ValueKind == JsonValueKind.Array)
		{
			EquippedPets.Clear();
			foreach (var petEl in petsEl.EnumerateArray())
			{
				var pet = new Dictionary();
				foreach (var prop in petEl.EnumerateObject())
				{
					switch (prop.Value.ValueKind)
					{
						case JsonValueKind.Number: pet[prop.Name] = prop.Value.GetSingle(); break;
						default:                   pet[prop.Name] = prop.Value.GetString(); break;
					}
				}
				EquippedPets.Add(pet);
			}
		}

		EmitSignal(SignalName.CoinsChanged, Coins);
		EmitSignal(SignalName.GemsChanged, Gems);
		EmitSignal(SignalName.PrestigeChanged, PrestigeLevel, PrestigeMultiplier, PrestigeCost);
		GD.Print($"[GameManager] Save loaded — coins:{Coins} gems:{Gems} prestige:{PrestigeLevel} pets:{EquippedPets.Count}");
	}

	// ── Idle Income ───────────────────────────────────────────────────────────
	// Pets passively earn coins over time even when the player is not clicking.
	private double _idleAccum = 0.0;

	public override void _Process(double delta)
	{
		_idleAccum += delta;
		if (_idleAccum >= 1.0)   // tick every second to avoid spamming signals
		{
			_idleAccum -= 1.0;
			float earned = GetPetDps();
			if (earned > 0f)
				AddCoins((int)earned);   // AddCoins already fires CoinsChanged + autosave
		}
	}

	public override void _Ready()
	{
		GD.Randomize();
		LoadGame();
	}

	private double Now() => Time.GetTicksMsec() / 1000.0;

	public void AddCoins(int amount)
	{
		int mult = (CoinBoost && Now() < CoinBoostEnd) ? 2 : 1;
		Coins += amount * mult;
		EmitSignal(SignalName.CoinsChanged, Coins);
		SaveGame();
	}

	public void AddGems(int amount)
	{
		Gems += amount;
		EmitSignal(SignalName.GemsChanged, Gems);
		SaveGame();
	}

	public bool SpendCoins(int amount)
	{
		if (Coins >= amount)
		{
			Coins -= amount;
			EmitSignal(SignalName.CoinsChanged, Coins);
			SaveGame();
			return true;
		}
		return false;
	}

	public bool SpendGems(int amount)
	{
		if (Gems >= amount)
		{
			Gems -= amount;
			EmitSignal(SignalName.GemsChanged, Gems);
			SaveGame();
			return true;
		}
		return false;
	}

	// Rebirth: wipe coins for a permanent multiplier. Keeps gems + pets, PS99-style.
	public bool Prestige()
	{
		if (Coins < PrestigeCost) return false;
		Coins = 0;
		PrestigeLevel += 1;
		PrestigeCost = (int)(PrestigeCost * 3);
		EmitSignal(SignalName.CoinsChanged, Coins);
		EmitSignal(SignalName.PrestigeChanged, PrestigeLevel, PrestigeMultiplier, PrestigeCost);
		SaveGame();
		return true;
	}

	// Damage comes only from clicks and pets now, scaled by the prestige multiplier.
	public float GetClickDamage() => BaseClickDamage * PrestigeMultiplier;

	public float GetPetDamage(float power) => power * PetDpsFactor * PrestigeMultiplier;

	public float GetPetDps()
	{
		float total = 0f;
		foreach (var pet in EquippedPets)
		{
			if (pet.TryGetValue("power", out var p))
				total += p.AsSingle();
		}
		return total * PetDpsFactor * PrestigeMultiplier;
	}

	public bool ActivateCoinBoost()
	{
		if (SpendGems(5))
		{
			CoinBoost = true;
			CoinBoostEnd = Now() + 30.0;
			return true;
		}
		return false;
	}

	public bool ActivateLuckyBoost()
	{
		if (SpendGems(8))
		{
			LuckyBoost = true;
			LuckyBoostEnd = Now() + 30.0;
			return true;
		}
		return false;
	}

	public bool IsLucky() => LuckyBoost && Now() < LuckyBoostEnd;

	private static readonly System.Collections.Generic.Dictionary<string, List<Dictionary>> EggPools = new()
	{
		["basic"] = new List<Dictionary>
		{
			new() { ["name"] = "Digger Dog",    ["icon"] = "dog",    ["power"] = 3,   ["rarity"] = "common" },
			new() { ["name"] = "Rock Rabbit",   ["icon"] = "rabbit", ["power"] = 5,   ["rarity"] = "common" },
			new() { ["name"] = "Mine Cat",      ["icon"] = "cat",    ["power"] = 8,   ["rarity"] = "rare" },
			new() { ["name"] = "Stone Fox",     ["icon"] = "fox",    ["power"] = 14,  ["rarity"] = "epic" },
			new() { ["name"] = "Lava Bear",     ["icon"] = "bear",   ["power"] = 25,  ["rarity"] = "legendary" },
		},
		["gem"] = new List<Dictionary>
		{
			new() { ["name"] = "Crystal Fox",   ["icon"] = "fox",    ["power"] = 20,  ["rarity"] = "rare" },
			new() { ["name"] = "Gem Wolf",      ["icon"] = "wolf",   ["power"] = 35,  ["rarity"] = "rare" },
			new() { ["name"] = "Sapphire Bear", ["icon"] = "bear",   ["power"] = 55,  ["rarity"] = "epic" },
			new() { ["name"] = "Diamond Eagle", ["icon"] = "eagle",  ["power"] = 90,  ["rarity"] = "legendary" },
			new() { ["name"] = "Prism Dragon",  ["icon"] = "dragon", ["power"] = 150, ["rarity"] = "legendary" },
		},
	};

	private static readonly System.Collections.Generic.Dictionary<string, (int Coins, int Gems)> EggCosts = new()
	{
		["basic"]     = (50, 0),
		["gem"]       = (0, 10),
		["legendary"] = (0, 50),
	};

	public Dictionary RollEgg(string eggType)
	{
		var cost = EggCosts.TryGetValue(eggType, out var c) ? c : EggCosts["basic"];
		if (cost.Coins > 0 && !SpendCoins(cost.Coins)) return new Dictionary();
		if (cost.Gems > 0 && !SpendGems(cost.Gems)) return new Dictionary();

		var pool = EggPools.TryGetValue(eggType, out var p) ? p : EggPools["basic"];
		float[] weights = { 0.45f, 0.28f, 0.15f, 0.08f, 0.04f };
		float r = GD.Randf();
		float cum = 0f;
		for (int i = 0; i < pool.Count; i++)
		{
			cum += weights[i];
			if (r < cum)
			{
				var pet = pool[i].Duplicate();
				EquippedPets.Add(pet);
				if (EquippedPets.Count > MaxPets)
					EquippedPets.RemoveAt(0);
				EmitSignal(SignalName.PetAdded, pet);
				SaveGame();
				return pet;
			}
		}
		return pool[0];
	}
}
