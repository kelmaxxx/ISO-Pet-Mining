using Godot;

public partial class Character : CharacterBody2D
{
	public Ore TargetOre { get; set; }
	private float _attackTimer = 0f;
	private float _moveSpeed => GameManager.Instance.GetPlayerSpeed();

	private AnimatedSprite2D _sprite;

	// 8 isometric directions, indexed clockwise starting from East (screen-space).
	// Build animations named like "walk_se", "idle_n", "attack_e" in the
	// SpriteFrames editor. Anything missing falls back automatically (see SafePlay).
	private static readonly string[] Dirs8 =
		{ "e", "se", "s", "sw", "w", "nw", "n", "ne" };

	// Remember the last facing so the character idles in the direction it stopped.
	private string _lastDir = "s";

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("Sprite");
		PlayDirectional("idle");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (TargetOre == null || !IsInstanceValid(TargetOre) || TargetOre.IsDead)
		{
			TargetOre = null;
			Velocity = Vector2.Zero;
			PlayDirectional("idle");
			return;
		}

		Vector2 targetPos = TargetOre.GlobalPosition;
		float dist = GlobalPosition.DistanceTo(targetPos);

		if (dist > 30f)
		{
			Vector2 dir = (targetPos - GlobalPosition).Normalized();
			Velocity = dir * _moveSpeed;
			MoveAndSlide();
			_lastDir = DirFromVector(dir);
			PlayDirectional("walk");
		}
		else
		{
			Velocity = Vector2.Zero;
			_attackTimer += (float)delta;
			if (_attackTimer >= 0.5f)
			{
				_attackTimer = 0f;
				PlayDirectional("attack");
				TargetOre.TakeDamage(GameManager.Instance.GetClickDamage());
			}
		}
	}

	public void SetTarget(Ore ore)
	{
		TargetOre = ore;
	}

	// Converts a movement vector into one of the 8 direction suffixes.
	// Godot screen-space: +X = east, +Y = south (down).
	private static string DirFromVector(Vector2 dir)
	{
		float angle = Mathf.PosMod(dir.Angle(), Mathf.Tau); // 0..2π
		int index = Mathf.RoundToInt(angle / (Mathf.Tau / 8f)) % 8;
		return Dirs8[index];
	}

	// Plays "<state>_<dir>" (e.g. "walk_se"). If that animation doesn't exist
	// yet, it falls back to the plain "<state>" (e.g. "walk"), then to "idle".
	// This lets the game run on the placeholder art until your 8-way sheets are in.
	private void PlayDirectional(string state)
	{
		string directional = $"{state}_{_lastDir}";
		if (HasAnim(directional)) { Play(directional); return; }
		if (HasAnim(state)) { Play(state); return; }
		if (HasAnim("idle")) Play("idle");
	}

	private bool HasAnim(string animName)
	{
		return _sprite != null
			&& _sprite.SpriteFrames != null
			&& _sprite.SpriteFrames.HasAnimation(animName);
	}

	private void Play(string animName)
	{
		if (_sprite.Animation != animName)
			_sprite.Play(animName);
	}
}
