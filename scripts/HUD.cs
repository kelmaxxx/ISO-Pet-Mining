using Godot;
using Godot.Collections;

public partial class HUD : Control
{
	private Label _coinsLabel;
	private Label _gemsLabel;
	private Label _pickLabel;
	private Button _upgradeButton;

	public override void _Ready()
	{
		_coinsLabel = GetNode<Label>("TopBar/CoinsLabel");
		_gemsLabel = GetNode<Label>("TopBar/GemsLabel");
		_pickLabel = GetNode<Label>("BottomBar/UpgradePickButton/PickLabel");
		_upgradeButton = GetNode<Button>("BottomBar/UpgradePickButton");

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

	public void OnBasicEggPressed()
	{
		var pet = GameManager.Instance.RollEgg("basic");
		if (pet.Count == 0) return;
		var world = GetTree().CurrentScene as World;
		world?.SpawnPet(pet);
	}
}
