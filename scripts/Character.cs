using Godot;

public partial class Character : CharacterBody2D
{
	public Ore TargetOre { get; set; }
	private float _attackTimer = 0f;
	private float _moveSpeed = 80f;

	private AnimatedSprite2D _sprite;

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("Sprite");
		SafePlay("idle");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (TargetOre == null || !IsInstanceValid(TargetOre) || TargetOre.IsDead)
		{
			TargetOre = null;
			Velocity = Vector2.Zero;
			SafePlay("idle");
			return;
		}

		Vector2 targetPos = TargetOre.GlobalPosition;
		float dist = GlobalPosition.DistanceTo(targetPos);

		if (dist > 30f)
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
			if (_attackTimer >= 0.5f)
			{
				_attackTimer = 0f;
				SafePlay("attack");
				TargetOre.TakeDamage(GameManager.Instance.GetClickDamage());
			}
		}
	}

	public void SetTarget(Ore ore)
	{
		TargetOre = ore;
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
