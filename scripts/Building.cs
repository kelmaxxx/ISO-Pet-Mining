using Godot;

// A clickable building placed on the isometric map (e.g. Shop, Hatchery).
// When the player clicks it, it emits BuildingClicked with its type string.
// The World listens for that and opens the matching popup UI.
public partial class Building : Node2D
{
	[Signal]
	public delegate void BuildingClickedEventHandler(string buildingType);

	public string BuildingType { get; private set; } = "shop";

	private Sprite2D _sprite;
	private ColorRect _placeholder;
	private Label _nameLabel;
	private Area2D _clickArea;

	public override void _Ready()
	{
		_sprite = GetNode<Sprite2D>("BuildingSprite");
		_placeholder = GetNode<ColorRect>("Placeholder");
		_nameLabel = GetNode<Label>("NameLabel");
		_clickArea = GetNode<Area2D>("ClickArea");

		_clickArea.InputPickable = true;
		_clickArea.InputEvent += OnClick;
	}

	// Called by World right after the building is added to the scene.
	// If the art file exists it uses the sprite, otherwise it falls back
	// to a colored placeholder so the building is still visible & clickable.
	public void Setup(string type, string label, string texturePath, Color placeholderColor)
	{
		BuildingType = type;
		_nameLabel.Text = label;

		if (texturePath != "" && ResourceLoader.Exists(texturePath))
		{
			_sprite.Texture = GD.Load<Texture2D>(texturePath);
			_sprite.Visible = true;
			_placeholder.Visible = false;
		}
		else
		{
			_sprite.Visible = false;
			_placeholder.Visible = true;
			_placeholder.Color = placeholderColor;
		}
	}

	private void OnClick(Node viewport, InputEvent @event, long shapeIdx)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			EmitSignal(SignalName.BuildingClicked, BuildingType);
	}
}
