using Godot;
using Godot.Collections;

public partial class Ore : Node2D
{
	[Signal]
	public delegate void OreClickedEventHandler(Ore ore);

	public string OreType { get; private set; } = "";
	public Dictionary OreData { get; private set; } = new();
	public float CurrentHp { get; private set; }
	public float MaxHp { get; private set; }
	public bool IsDead { get; private set; }

	// Grid cell this ore currently occupies, and a back-reference to the World
	// so it can ask for a new random spot when it respawns.
	public Vector2I Cell { get; set; }
	public World World { get; set; }

	private Sprite2D _oreSprite;
	private ProgressBar _hpBar;
	private Area2D _clickArea;

	public override void _Ready()
	{
		_oreSprite = GetNode<Sprite2D>("OreSprite");
		_hpBar = GetNode<ProgressBar>("HPBar");
		_clickArea = GetNode<Area2D>("ClickArea");

		_clickArea.InputPickable = true;
		_clickArea.InputEvent += OnClick;
	}

	public void Setup(string typeName, Dictionary data)
	{
		OreType = typeName;
		OreData = data;
		MaxHp = data["hp"].AsSingle();
		CurrentHp = MaxHp;

		string texturePath = data["texture"].AsString();
		if (ResourceLoader.Exists(texturePath))
			_oreSprite.Texture = GD.Load<Texture2D>(texturePath);

		if (_hpBar != null)
		{
			_hpBar.MaxValue = MaxHp;
			_hpBar.Value = CurrentHp;
		}
	}

	private void OnClick(Node viewport, InputEvent @event, long shapeIdx)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			EmitSignal(SignalName.OreClicked, this);
	}

	public void TakeDamage(float amount)
	{
		if (IsDead) return;

		float dmg = amount;
		if (GameManager.Instance.IsLucky() && GD.Randf() < 0.25f)
			dmg *= 2;

		CurrentHp = Mathf.Max(0f, CurrentHp - dmg);
		if (_hpBar != null) _hpBar.Value = CurrentHp;
		Shake();
		if (CurrentHp <= 0f) _ = BreakAndRespawn();
	}

	private void Shake()
	{
		float baseX = Position.X;
		var tween = CreateTween();
		tween.TweenProperty(this, "position:x", baseX + 3, 0.05);
		tween.TweenProperty(this, "position:x", baseX - 3, 0.05);
		tween.TweenProperty(this, "position:x", baseX, 0.05);
	}

	private async System.Threading.Tasks.Task BreakAndRespawn()
	{
		IsDead = true;

		var gm = GameManager.Instance;
		int bonus = gm.CoinBoost ? 2 : 1;
		int amount = (int)(OreData["amount"].AsSingle() * (1f + gm.PickaxeLevel * 0.4f) * bonus);

		if (OreData["reward"].AsString() == "gems")
			gm.AddGems(amount);
		else
			gm.AddCoins(amount);

		Visible = false;
		await ToSignal(GetTree().CreateTimer(GD.RandRange(3.0, 6.0)), SceneTreeTimer.SignalName.Timeout);

		// Respawn at a random free spot instead of the same place.
		if (World != null)
			Position = World.RequestRespawnPosition(this);

		CurrentHp = MaxHp;
		if (_hpBar != null) _hpBar.Value = CurrentHp;
		IsDead = false;
		Visible = true;
	}
}
