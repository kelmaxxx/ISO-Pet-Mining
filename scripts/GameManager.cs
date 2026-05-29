using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	public int Coins { get; private set; } = 0;
	public int Gems { get; private set; } = 0;

	public int PickaxeLevel { get; private set; } = 1;
	public int PickaxeCost { get; private set; } = 50;

	public Array<Dictionary> EquippedPets { get; private set; } = new();
	public int MaxPets { get; private set; } = 5;

	public bool CoinBoost { get; private set; } = false;
	public double CoinBoostEnd { get; private set; } = 0.0;
	public bool LuckyBoost { get; private set; } = false;
	public double LuckyBoostEnd { get; private set; } = 0.0;

	[Signal] public delegate void CoinsChangedEventHandler(int amount);
	[Signal] public delegate void GemsChangedEventHandler(int amount);
	[Signal] public delegate void PetAddedEventHandler(Dictionary petData);
	[Signal] public delegate void PickaxeUpgradedEventHandler(int level, int nextCost);

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		GD.Randomize();
	}

	private double Now() => Time.GetTicksMsec() / 1000.0;

	public void AddCoins(int amount)
	{
		int mult = (CoinBoost && Now() < CoinBoostEnd) ? 2 : 1;
		Coins += amount * mult;
		EmitSignal(SignalName.CoinsChanged, Coins);
	}

	public void AddGems(int amount)
	{
		Gems += amount;
		EmitSignal(SignalName.GemsChanged, Gems);
	}

	public bool SpendCoins(int amount)
	{
		if (Coins >= amount)
		{
			Coins -= amount;
			EmitSignal(SignalName.CoinsChanged, Coins);
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
			return true;
		}
		return false;
	}

	public bool UpgradePickaxe()
	{
		if (SpendCoins(PickaxeCost))
		{
			PickaxeLevel += 1;
			PickaxeCost = (int)(PickaxeCost * 2.2);
			EmitSignal(SignalName.PickaxeUpgraded, PickaxeLevel, PickaxeCost);
			return true;
		}
		return false;
	}

	public float GetClickDamage() => PickaxeLevel * 6.0f;

	public float GetPetDps()
	{
		float total = 0f;
		foreach (var pet in EquippedPets)
		{
			if (pet.TryGetValue("power", out var p))
				total += p.AsSingle();
		}
		return total * PickaxeLevel * 0.3f;
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
				return pet;
			}
		}
		return pool[0];
	}
}
