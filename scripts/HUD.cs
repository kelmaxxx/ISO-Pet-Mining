using Godot;
using Godot.Collections;

public partial class HUD : Control
{
	private Label _coinsLabel;
	private Label _gemsLabel;
	private Label _pickLabel;

	private Control _shopPopup;
	private Control _hatchPopup;

	private const string PickLabelPath =
		"ShopPopup/CenterContainer/PanelContainer/MarginContainer/VBoxContainer/UpgradePickButton/PickLabel";

	public override void _Ready()
	{
		_coinsLabel = GetNode<Label>("TopBar/CoinsLabel");
		_gemsLabel = GetNode<Label>("TopBar/GemsLabel");
		_pickLabel = GetNode<Label>(PickLabelPath);

		_shopPopup = GetNode<Control>("ShopPopup");
		_hatchPopup = GetNode<Control>("HatchPopup");
		_shopPopup.Visible = false;
		_hatchPopup.Visible = false;

		var gm = GameManager.Instance;
		gm.CoinsChanged += UpdateCoins;
		gm.GemsChanged += UpdateGems;
		gm.PickaxeUpgraded += UpdatePick;

		UpdateCoins(gm.Coins);
		UpdateGems(gm.Gems);
		UpdatePick(gm.PickaxeLevel, gm.PickaxeCost);
	}

	private void UpdateCoins(int amount)
	{
		_coinsLabel.Text = $"Coins: {amount}";
	}

	private void UpdateGems(int amount)
	{
		_gemsLabel.Text = $"Gems: {amount}";
	}

	private void UpdatePick(int level, int nextCost)
	{
		_pickLabel.Text = $"Pick Lv{level} (Next: {nextCost})";
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

	public void OnUpgradePickPressed()
	{
		GameManager.Instance.UpgradePickaxe();
	}

	public void OnCoinBoostPressed()
	{
		GameManager.Instance.ActivateCoinBoost();
	}

	public void OnLuckyBoostPressed()
	{
		GameManager.Instance.ActivateLuckyBoost();
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
		var pet = GameManager.Instance.RollEgg(eggType);
		if (pet.Count == 0) return;
		var world = GetTree().CurrentScene as World;
		world?.SpawnPet(pet);
	}
}
