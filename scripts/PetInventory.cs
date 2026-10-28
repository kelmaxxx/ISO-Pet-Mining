using Godot;

// Popup roster built from GameManager.EquippedPets. Rebuilt every time it opens
// so it never shows stale counts after a hatch.
public partial class PetInventory : Control
{
	private Label _capacityLabel;
	private VBoxContainer _petList;

	public override void _Ready()
	{
		_capacityLabel = GetNode<Label>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/CapacityLabel");
		_petList = GetNode<VBoxContainer>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/ScrollContainer/PetList");
		Visible = false;
	}

	public void Open()
	{
		Refresh();
		Visible = true;
	}

	public void Close()
	{
		Visible = false;
	}

	private void Refresh()
	{
		foreach (var child in _petList.GetChildren())
		{
			_petList.RemoveChild(child);
			child.QueueFree();
		}

		var gm = GameManager.Instance;
		_capacityLabel.Text = $"Pets: {gm.EquippedPets.Count} / {gm.MaxPets} active";

		foreach (var pet in gm.EquippedPets)
		{
			string rarity = pet.TryGetValue("rarity", out var r) ? r.AsString() : "common";
			string name = pet.TryGetValue("name", out var n) ? n.AsString() : "Pet";
			float power = pet.TryGetValue("power", out var p) ? p.AsSingle() : 0f;

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 10);
			row.AddChild(new ColorRect
			{
				Color = RarityColor(rarity),
				CustomMinimumSize = new Vector2(8, 8),
				SizeFlagsVertical = SizeFlags.ShrinkCenter,
			});
			row.AddChild(new Label { Text = name, SizeFlagsHorizontal = SizeFlags.ExpandFill });
			row.AddChild(new Label
			{
				Text = rarity.Capitalize(),
				Modulate = RarityColor(rarity),
				CustomMinimumSize = new Vector2(70, 0),
				HorizontalAlignment = HorizontalAlignment.Right,
			});
			row.AddChild(new Label
			{
				Text = $"Power {power:0.#}",
				CustomMinimumSize = new Vector2(70, 0),
				HorizontalAlignment = HorizontalAlignment.Right,
			});
			row.AddChild(new Label
			{
				Text = "Active",
				Modulate = new Color(0.45f, 0.85f, 0.45f),
				CustomMinimumSize = new Vector2(50, 0),
				HorizontalAlignment = HorizontalAlignment.Right,
			});
			_petList.AddChild(row);
		}
	}

	public static Color RarityColor(string rarity) => rarity switch
	{
		"rare" => new Color(0.30f, 0.65f, 1.00f),
		"epic" => new Color(0.72f, 0.38f, 1.00f),
		"legendary" => new Color(1.00f, 0.80f, 0.20f),
		_ => new Color(0.75f, 0.76f, 0.82f),
	};
}
