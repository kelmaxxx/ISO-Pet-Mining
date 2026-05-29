using Godot;
using Godot.Collections;
using System.Threading.Tasks;

// C# equivalent of Ore.gd — kept side-by-side for learning.
// The scene Ore.tscn still points at Ore.gd; switch it in the editor
// (Inspector > Script) when you want to try this version live.
public partial class Ore : Node2D
{
	[Signal]
	public delegate void OreClickedEventHandler(Ore ore);

	public string OreType { get; private set; } = "";
	public Dictionary OreData { get; private set; } = new();
	public float CurrentHp { get; private set; }
	public float MaxHp { get; private set; }
	public bool IsDead { get; private set; }

	private Sprite2D _oreSprite;
	private ProgressBar _hpBar;
	private Area2D _clickArea;

	// GameManager is still GDScript, so we talk to it through Variant Call/Get.
	// Once GameManager.gd is migrated, this becomes a typed field.
	private Node _gameManager;

	public override void _Ready()
	{
		_oreSprite = GetNode<Sprite2D>("OreSprite");
		_hpBar = GetNode<ProgressBar>("HPBar");
		_clickArea = GetNode<Area2D>("ClickArea");
		_gameManager = GetNode("/root/GameManager");

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
		if (_gameManager.Call("is_lucky").AsBool() && GD.Randf() < 0.25f)
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

	private async Task BreakAndRespawn()
	{
		IsDead = true;

		bool coinBoost = _gameManager.Get("coin_boost").AsBool();
		int pickaxeLevel = _gameManager.Get("pickaxe_level").AsInt32();
		int bonus = coinBoost ? 2 : 1;
		int amount = (int)(OreData["amount"].AsSingle() * (1f + pickaxeLevel * 0.4f) * bonus);

		if (OreData["reward"].AsString() == "gems")
			_gameManager.Call("add_gems", amount);
		else
			_gameManager.Call("add_coins", amount);

		Visible = false;
		await ToSignal(GetTree().CreateTimer(GD.RandRange(3.0, 6.0)), SceneTreeTimer.SignalName.Timeout);
		CurrentHp = MaxHp;
		if (_hpBar != null) _hpBar.Value = CurrentHp;
		IsDead = false;
		Visible = true;
	}
}
