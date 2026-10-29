using Godot;

// Temporary: captures a frame of the world and dumps generated normal maps.
public partial class Screenshot : Node
{
	public override async void _Ready()
	{
		var world = GD.Load<PackedScene>("res://scenes/World.tscn").Instantiate<Node2D>();
		AddChild(world);
		for (int i = 0; i < 10; i++)
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		var img = GetViewport().GetTexture().GetImage();
		img.SavePng("C:/Users/msu/AppData/Local/Temp/opencode/iso_before.png");

		DumpNormal("res://assets/terrain/terrain_grass.png", "grass");
		DumpNormal("res://assets/ores/ore_gold.png", "gold");
		DumpNormal("res://assets/ores/ore_ruby.png", "ruby");
		DumpNormal("res://assets/buildings/shop.png", "shop");

		GD.Print("SHOT SAVED");
		GetTree().Quit();
	}

	private static void DumpNormal(string path, string name)
	{
		var lit = Lighting.GetLitTexture(GD.Load<Texture2D>(path)) as CanvasTexture;
		lit?.NormalTexture?.GetImage()?.SavePng($"C:/Users/msu/AppData/Local/Temp/opencode/normal_{name}.png");
	}
}
