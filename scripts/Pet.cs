using Godot;
using Godot.Collections;

public partial class Pet : CharacterBody2D
{
	public Dictionary PetData { get; private set; } = new();
	public Ore TargetOre { get; set; }
	private float _attackTimer = 0f;
	private float _moveSpeed = 60f;

	private AnimatedSprite2D _sprite;

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("Sprite");
	}

	public void Setup(Dictionary data)
	{
		PetData = data;

		// Pets have no art files yet, so draw a stand-in from their icon + rarity.
		if (_sprite.SpriteFrames == null || !_sprite.SpriteFrames.HasAnimation("walk"))
		{
			_sprite.SpriteFrames = PlaceholderArt.PetFrames(
				data.TryGetValue("icon", out var icon) ? icon.AsString() : "",
				data.TryGetValue("rarity", out var rarity) ? rarity.AsString() : "common");
			_sprite.Scale = new Vector2(1.25f, 1.25f); // placeholder art is drawn small
		}
		SafePlay("walk");

		var frame = _sprite.SpriteFrames.GetFrameTexture("walk", 0);
		float height = frame?.GetHeight() ?? 64;
		float scale = _sprite.Scale.Y;
		Lighting.AttachDropShadow(this, (height * 0.5f - 12f) * scale, height * 0.42f * scale);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (TargetOre == null || !IsInstanceValid(TargetOre) || TargetOre.IsDead)
		{
			TargetOre = FindNearestOre();
			if (TargetOre == null)
			{
				Velocity = Vector2.Zero;
				SafePlay("walk");
				return;
			}
		}

		Vector2 targetPos = TargetOre.GlobalPosition;
		float dist = GlobalPosition.DistanceTo(targetPos);

		if (dist > 25f)
		{
			Vector2 dir = (targetPos - GlobalPosition).Normalized();
			Velocity = dir * _moveSpeed;
			MoveAndSlide();
			SafePlay("walk");
			_sprite.FlipH = dir.X < 0;
		}
		else
		{
			Velocity = Vector2.Zero;
			_attackTimer += (float)delta;
			if (_attackTimer >= 1.0f)
			{
				_attackTimer = 0f;
				SafePlay("attack");
				float power = PetData.TryGetValue("power", out var p) ? p.AsSingle() : 5f;
				TargetOre.TakeDamage(GameManager.Instance.GetPetDamage(power));
			}
		}
	}

	private Ore FindNearestOre()
	{
		var ores = GetTree().GetNodesInGroup("ores");
		Ore nearest = null;
		float bestDist = 9999f;
		foreach (var node in ores)
		{
			if (node is not Ore ore || ore.IsDead) continue;
			float d = GlobalPosition.DistanceTo(ore.GlobalPosition);
			if (d < bestDist)
			{
				bestDist = d;
				nearest = ore;
			}
		}
		return nearest;
	}

	private void SafePlay(string animName)
	{
		if (_sprite != null && _sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(animName))
		{
			if (_sprite.Animation != animName)
				_sprite.Play(animName);
		}
	}
}
