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

		// Soft shadow under the ore to ground it on the tile.
		Lighting.AttachDropShadow(this, 4f, 34f);
	}

	public void Setup(string typeName, Dictionary data)
	{
		OreType = typeName;
		OreData = data;
		MaxHp = data["hp"].AsSingle();
		CurrentHp = MaxHp;

		string texturePath = data["texture"].AsString();
		if (ResourceLoader.Exists(texturePath))
			_oreSprite.Texture = Lighting.GetLitTexture(GD.Load<Texture2D>(texturePath));

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
		// Reward scales with the prestige multiplier. The coin-boost doubling is
		// applied inside AddCoins, so we don't double-count it here.
		int amount = (int)(OreData["amount"].AsSingle() * gm.PrestigeMultiplier);
		string reward = OreData["reward"].AsString();

		if (reward == "gems")
			gm.AddGems(amount);
		else
			gm.AddCoins(amount);

		if (OreType == "meteor")
			Effects.SpawnBurst(GetParent(), GlobalPosition, new Color(1f, 0.65f, 0.15f), amount: 40, scale: 2f, lifetime: 1.0f);
		else
			Effects.SpawnBurst(GetParent(), GlobalPosition, reward == "gems" ? new Color(0.55f, 0.85f, 1f) : new Color(0.85f, 0.7f, 0.35f));

		Visible = false;

		if (OreType == "meteor")
		{
			if (World != null)
				World.RemoveMeteor(this);
			QueueFree();
			return;
		}

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
