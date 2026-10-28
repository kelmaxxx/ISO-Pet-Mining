using Godot;
using Godot.Collections;

public partial class HUD : Control
{
	private Label _coinsLabel;
	private Label _gemsLabel;
	private Label _prestigeLabel;
	private Label _prestigeCostLabel;
	private Label _speedCostLabel;
	private Label _maxPetsCostLabel;

	private Control _shopPopup;
	private Control _hatchPopup;
	private PetInventory _petInventory;
	private Control _hatchReveal;
	private TextureRect _revealSprite;
	private Label _revealName;
	private Label _revealStats;

	private const string PrestigeCostLabelPath =
		"ShopPopup/CenterContainer/PanelContainer/MarginContainer/VBoxContainer/PrestigeButton/PrestigeCostLabel";

	public override void _Ready()
	{
		_coinsLabel = GetNode<Label>("TopBar/CoinsLabel");
		_gemsLabel = GetNode<Label>("TopBar/GemsLabel");
		_prestigeLabel = GetNode<Label>("TopBar/PrestigeLabel");
		_prestigeCostLabel = GetNode<Label>(PrestigeCostLabelPath);
		_speedCostLabel = GetNode<Label>("ShopPopup/CenterContainer/PanelContainer/MarginContainer/VBoxContainer/SpeedUpgradeButton/SpeedCostLabel");
		_maxPetsCostLabel = GetNode<Label>("ShopPopup/CenterContainer/PanelContainer/MarginContainer/VBoxContainer/MaxPetsUpgradeButton/MaxPetsCostLabel");

		_shopPopup = GetNode<Control>("ShopPopup");
		_hatchPopup = GetNode<Control>("HatchPopup");
		_shopPopup.Visible = false;
		_hatchPopup.Visible = false;

		_petInventory = GetNode<PetInventory>("PetInventory");
		_hatchReveal = GetNode<Control>("HatchReveal");
		_revealSprite = GetNode<TextureRect>("HatchReveal/CenterContainer/PanelContainer/MarginContainer/VBoxContainer/Sprite");
		_revealName = GetNode<Label>("HatchReveal/CenterContainer/PanelContainer/MarginContainer/VBoxContainer/NameLabel");
		_revealStats = GetNode<Label>("HatchReveal/CenterContainer/PanelContainer/MarginContainer/VBoxContainer/StatsLabel");
		_hatchReveal.Visible = false;

		var gm = GameManager.Instance;
		gm.CoinsChanged += UpdateCoins;
		gm.GemsChanged += UpdateGems;
		gm.PrestigeChanged += UpdatePrestige;
		gm.SpeedLevelChanged += UpdateSpeedUpgrade;
		gm.MaxPetsLevelChanged += UpdateMaxPetsUpgrade;

		UpdateCoins(gm.Coins);
		UpdateGems(gm.Gems);
		UpdatePrestige(gm.PrestigeLevel, gm.PrestigeMultiplier, gm.PrestigeCost);
		UpdateSpeedUpgrade(gm.SpeedLevel, gm.GetSpeedUpgradeCost());
		UpdateMaxPetsUpgrade(gm.MaxPetsLevel, gm.GetMaxPetsUpgradeCost());
	}

	private void UpdateCoins(int amount)
	{
		_coinsLabel.Text = $"Coins: {amount}";
	}

	private void UpdateGems(int amount)
	{
		_gemsLabel.Text = $"Gems: {amount}";
	}

	private void UpdatePrestige(int level, float multiplier, int nextCost)
	{
		_prestigeLabel.Text = $"Prestige {level} (x{multiplier:0.0})";
		_prestigeCostLabel.Text = $"Rebirth: {nextCost} coins";
	}

	private void UpdateSpeedUpgrade(int level, int nextCost)
	{
		if (nextCost == -1)
			_speedCostLabel.Text = $"Speed Level: {level} (MAX)";
		else
			_speedCostLabel.Text = $"Speed Lvl: {level} | Cost: {nextCost} coins";
	}

	private void UpdateMaxPetsUpgrade(int level, int nextCost)
	{
		if (nextCost == -1)
			_maxPetsCostLabel.Text = $"Max Pets Level: {level} (MAX)";
		else
			_maxPetsCostLabel.Text = $"Max Pets Lvl: {level} | Cost: {nextCost} gems";
	}

	// ----- opened by buildings in the world -----

	public void OpenShop()
	{
		_hatchPopup.Visible = false;
		_shopPopup.Visible = true;
	}

	public void OpenHatchery()
	{
		_shopPopup.Visible = false;
		_hatchPopup.Visible = true;
	}

	public void OnCloseShop()
	{
		_shopPopup.Visible = false;
	}

	public void OnCloseHatch()
	{
		_hatchPopup.Visible = false;
	}

	// ----- shop buttons -----

	public void OnPrestigePressed()
	{
		if (GameManager.Instance.Prestige())
		{
			var world = GetTree().CurrentScene as World;
			world?.SpawnPrestigeBurst();
		}
	}

	public void OnCoinBoostPressed()
	{
		GameManager.Instance.ActivateCoinBoost();
	}

	public void OnLuckyBoostPressed()
	{
		GameManager.Instance.ActivateLuckyBoost();
	}

	public void OnSpeedUpgradePressed()
	{
		GameManager.Instance.BuySpeedUpgrade();
	}

	public void OnMaxPetsUpgradePressed()
	{
		GameManager.Instance.BuyMaxPetsUpgrade();
	}

	// ----- hatchery buttons -----

	public void OnBasicEggPressed()
	{
		HatchEgg("basic");
	}

	public void OnGemEggPressed()
	{
		HatchEgg("gem");
	}

	private void HatchEgg(string eggType)
	{
		var gm = GameManager.Instance;
		if (gm.EquippedPets.Count >= gm.MaxPets)
		{
			ShowAnnouncement($"Pet roster full ({gm.MaxPets}) — upgrade Max Pets in the Shop");
			return;
		}
		var pet = gm.RollEgg(eggType);
		if (pet.Count == 0) return;
		AudioManager.Instance.PlayHatch();
		var world = GetTree().CurrentScene as World;
		world?.SpawnPet(pet);
		_hatchPopup.Visible = false;
		ShowHatchReveal(pet);
	}

	public void OnPetsPressed()
	{
		_shopPopup.Visible = false;
		_hatchPopup.Visible = false;
		_petInventory.Open();
	}

	public void ShowHatchReveal(Dictionary pet)
	{
		string icon = pet.TryGetValue("icon", out var i) ? i.AsString() : "dog";
		string rarity = pet.TryGetValue("rarity", out var r) ? r.AsString() : "common";
		float power = pet.TryGetValue("power", out var p) ? p.AsSingle() : 0f;

		_revealSprite.Texture = PlaceholderArt.PetFrames(icon, rarity).GetFrameTexture("idle", 0);
		_revealName.Text = pet.TryGetValue("name", out var n) ? n.AsString() : "Pet";
		_revealStats.Text = $"{rarity.Capitalize()} · Power {power:0.#}";
		_revealStats.AddThemeColorOverride("font_color", PetInventory.RarityColor(rarity));

		_hatchReveal.Visible = true;
		_revealSprite.Scale = new Vector2(0.4f, 0.4f);
		CreateTween().TweenProperty(_revealSprite, "scale", Vector2.One, 0.3)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	public void OnCloseHatchReveal()
	{
		_hatchReveal.Visible = false;
		_hatchPopup.Visible = true;
	}

	public void ShowAnnouncement(string text)
	{
		var label = new Label();
		label.Text = text;
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.AddThemeFontSizeOverride("font_size", 18);
		label.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.0f)); // Gold color
		
		// Position at the top center
		label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide, Control.LayoutPresetMode.Minsize, 20);
		label.OffsetTop = 50;
		AddChild(label);

		var tween = CreateTween();
		label.Modulate = new Color(1, 1, 1, 0);
		tween.TweenProperty(label, "modulate:a", 1.0f, 0.3);
		tween.TweenInterval(2.5);
		tween.TweenProperty(label, "modulate:a", 0.0f, 0.5);
		tween.TweenCallback(Callable.From(label.QueueFree));
	}
}
