extends Node2D

const COLS := 8
const ROWS := 8
const TILE_W := 64
const TILE_H := 32
const GRID_ORIGIN_Y := 180  # pushes the grid down from the top of the screen

const OreScene := preload("res://scenes/Ore.tscn")
const CharacterScene := preload("res://scenes/Character.tscn")
const PetScene := preload("res://scenes/Pet.tscn")

# Texture paths — loaded lazily so missing files don't crash the game.
const TERRAIN_PATHS := {
	"grass": "res://assets/terrain/terrain_grass.png",
	"dirt":  "res://assets/terrain/terrain_dirt.png",
	"stone": "res://assets/terrain/terrain_stone.png",
}

const ORE_DATA := {
	"coal":    {"hp": 40,  "reward": "coins", "amount": 8,   "chance": 0.22, "texture": "res://assets/ores/ore_coal.png"},
	"iron":    {"hp": 80,  "reward": "coins", "amount": 25,  "chance": 0.16, "texture": "res://assets/ores/ore_iron.png"},
	"gold":    {"hp": 130, "reward": "coins", "amount": 80,  "chance": 0.10, "texture": "res://assets/ores/ore_gold.png"},
	"crystal": {"hp": 100, "reward": "gems",  "amount": 3,   "chance": 0.07, "texture": "res://assets/ores/ore_crystal.png"},
	"ruby":    {"hp": 200, "reward": "gems",  "amount": 10,  "chance": 0.04, "texture": "res://assets/ores/ore_ruby.png"},
}

# Cached loaded textures (key -> Texture2D or null)
var _terrain_cache := {}
var character_node: Node = null
var ore_nodes: Array = []


func _ready() -> void:
	randomize()
	_build_terrain()
	_spawn_ores()
	_spawn_character()


# --- Grid math ---

func grid_to_screen(col: int, row: int) -> Vector2:
	var screen_center_x := get_viewport().get_visible_rect().size.x / 2.0
	var x := screen_center_x + float(col - row) * (TILE_W / 2.0)
	var y := float(GRID_ORIGIN_Y) + float(col + row) * (TILE_H / 2.0)
	return Vector2(x, y)


# --- Loading helpers ---

func _load_terrain(type_name: String) -> Texture2D:
	if _terrain_cache.has(type_name):
		return _terrain_cache[type_name]
	var path: String = TERRAIN_PATHS.get(type_name, "")
	var tex: Texture2D = null
	if path != "" and ResourceLoader.exists(path):
		tex = load(path)
	_terrain_cache[type_name] = tex
	return tex


func _resolve_terrain(preferred: String) -> Texture2D:
	# Prefer requested type; fall back to grass if the sprite isn't drawn yet.
	var tex := _load_terrain(preferred)
	if tex != null:
		return tex
	return _load_terrain("grass")


# --- World construction ---

func _build_terrain() -> void:
	var tile_layer := $TileLayer
	var tile_types := ["grass", "dirt", "stone"]
	var weights := [0.7, 0.2, 0.1]

	for row in range(ROWS):
		for col in range(COLS):
			var type_name := _weighted_random(tile_types, weights)
			var tex := _resolve_terrain(type_name)
			if tex == null:
				continue  # nothing to draw yet
			var sprite := Sprite2D.new()
			sprite.texture = tex
			sprite.position = grid_to_screen(col, row)
			sprite.z_index = col + row
			tile_layer.add_child(sprite)


func _spawn_ores() -> void:
	var object_layer := $ObjectLayer
	for row in range(ROWS):
		for col in range(COLS):
			if randf() >= 0.35:
				continue
			var ore_type := _roll_ore_type()
			if ore_type == "":
				continue
			var data: Dictionary = ORE_DATA[ore_type]
			# Skip if the ore sprite hasn't been drawn yet — keeps the world clean.
			if not ResourceLoader.exists(String(data.texture)):
				continue
			var ore := OreScene.instantiate()
			var pos := grid_to_screen(col, row)
			ore.position = Vector2(pos.x, pos.y - 16)
			ore.z_index = col + row + 1
			object_layer.add_child(ore)
			ore.setup(ore_type, data)
			ore.add_to_group("ores")
			ore.ore_clicked.connect(_on_ore_clicked)
			ore_nodes.append(ore)


func _spawn_character() -> void:
	# Only spawn if a character spritesheet exists — otherwise Character.tscn would
	# instantiate an empty AnimatedSprite2D and just sit invisible.
	if not ResourceLoader.exists("res://assets/character/character_idle.png"):
		return
	var object_layer := $ObjectLayer
	character_node = CharacterScene.instantiate()
	character_node.position = grid_to_screen(COLS / 2, ROWS / 2)
	character_node.z_index = 999
	object_layer.add_child(character_node)


func _roll_ore_type() -> String:
	var r := randf()
	var cum := 0.0
	for key in ORE_DATA.keys():
		cum += float(ORE_DATA[key].chance)
		if r < cum:
			return key
	return ""


func _weighted_random(items: Array, weights: Array) -> String:
	var r := randf()
	var cum := 0.0
	for i in range(items.size()):
		cum += float(weights[i])
		if r < cum:
			return items[i]
	return items[0]


func _on_ore_clicked(ore_node: Node) -> void:
	# Immediate click damage feels more responsive than waiting for the character.
	ore_node.take_damage(GameManager.get_click_damage())
	if character_node and character_node.has_method("set_target"):
		character_node.set_target(ore_node)
	for pet in get_tree().get_nodes_in_group("pets"):
		pet.target_ore = ore_node


# --- Depth sorting for isometric view ---

func _process(_delta: float) -> void:
	for child in $ObjectLayer.get_children():
		child.z_index = int(child.position.y)


# --- Pet spawn helper (called by HUD/egg system) ---

func spawn_pet(pet_data: Dictionary) -> void:
	if not ResourceLoader.exists("res://scenes/Pet.tscn"):
		return
	var pet := PetScene.instantiate()
	var col := COLS / 2 + randi_range(-2, 2)
	var row := ROWS / 2 + randi_range(-2, 2)
	pet.position = grid_to_screen(col, row)
	$ObjectLayer.add_child(pet)
	pet.add_to_group("pets")
	pet.setup(pet_data)
