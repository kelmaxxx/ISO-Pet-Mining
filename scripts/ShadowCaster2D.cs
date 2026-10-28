using Godot;

// A flattened, darkened copy of an actor's current sprite frame, sitting on the
// ground and sliding away from the sun. Add it as a child of the actor, next to
// the Sprite2D/AnimatedSprite2D it should copy.
public partial class ShadowCaster2D : Node2D
{
	[Export] public Vector2 GroundOffset = Vector2.Zero;
	[Export] public float Opacity = 0.32f;
	[Export] public float Flatten = 0.35f;
	[Export] public float Length = 8f;

	// Set before adding to the tree, or leave null to use the first sprite sibling.
	public Node2D Source { get; set; }

	public Sprite2D Shadow { get; private set; }

	private Sprite2D _sprite;
	private AnimatedSprite2D _animated;
	private bool _sourceIsParent;

	public override void _Ready()
	{
		Shadow = new Sprite2D
		{
			Name = "Shadow",
			LightMask = 0,   // keep point lights from brightening the silhouette
			ZIndex = -1,     // behind the owner's visuals
			ZAsRelative = true,
		};
		AddChild(Shadow);

		Source ??= FindSource();
		_sourceIsParent = Source == GetParent();
		if (Source is Sprite2D sprite)
		{
			_sprite = sprite;
		}
		else if (Source is AnimatedSprite2D animated)
		{
			_animated = animated;
			animated.FrameChanged += Refresh;
			animated.AnimationChanged += Refresh;
		}

		Refresh();
	}

	public override void _Process(double delta)
	{
		if (Shadow == null) return;

		float stretch = Mathf.Lerp(1.6f, 0.6f, DayNightCycle.Daylight) * Length;
		Shadow.Position = GroundOffset + DayNightCycle.SunDirection * stretch;
		Shadow.Modulate = new Color(0f, 0f, 0f, Opacity * DayNightCycle.ShadowStrength);

		if (_sprite != null)
		{
			Shadow.FlipH = _sprite.FlipH;
			Shadow.FlipV = _sprite.FlipV;
		}
		else if (_animated != null)
		{
			Shadow.FlipH = _animated.FlipH;
			Shadow.FlipV = _animated.FlipV;
		}
	}

	public void Refresh()
	{
		if (Shadow == null) return;

		Texture2D texture = null;
		float scaleX = 1f;
		float scaleY = 1f;

		if (_sprite != null)
		{
			texture = _sprite.Texture;
			scaleX = _sprite.Scale.X;
			scaleY = _sprite.Scale.Y;
		}
		else if (_animated != null)
		{
			texture = _animated.SpriteFrames?.GetFrameTexture(_animated.Animation, _animated.Frame);
			scaleX = _animated.Scale.X;
			scaleY = _animated.Scale.Y;
		}

		// A CanvasTexture would re-light the shadow; only its diffuse is wanted.
		if (texture is CanvasTexture canvas) texture = canvas.DiffuseTexture;

		Shadow.Visible = texture != null;
		if (texture == null) return;

		// If the caster itself is under the source, its scale is inherited already.
		Vector2 inherited = _sourceIsParent ? Vector2.One : new Vector2(scaleX, scaleY);
		Shadow.Texture = texture;
		Shadow.Scale = new Vector2(inherited.X, inherited.Y * Flatten);
	}

	private Node2D FindSource()
	{
		Node parent = GetParent();
		if (parent is Sprite2D sprite) return sprite;
		if (parent is AnimatedSprite2D animated) return animated;

		foreach (var child in parent.GetChildren())
		{
			if (child == this) continue;
			if (child is Sprite2D childSprite) return childSprite;
			if (child is AnimatedSprite2D childAnimated) return childAnimated;
		}
		return null;
	}

	// Creates the caster as the first child of the actor so it draws underneath.
	public static ShadowCaster2D Attach(Node2D owner, Node2D source, Vector2 groundOffset)
	{
		var caster = new ShadowCaster2D
		{
			Name = "ShadowCaster",
			Source = source,
			GroundOffset = groundOffset,
		};
		owner.AddChild(caster);
		owner.MoveChild(caster, 0);
		return caster;
	}
}
