extends Node2D

signal ore_clicked(ore_node)

var ore_type: String = ""
var ore_data: Dictionary = {}
var current_hp: float = 0.0
var max_hp: float = 0.0
var is_dead: bool = false

@onready var ore_sprite: Sprite2D = $OreSprite
@onready var hp_bar: ProgressBar = $HPBar
@onready var click_area: Area2D = $ClickArea


func _ready() -> void:
	click_area.input_pickable = true
	click_area.input_event.connect(_on_click)


func setup(type_name: String, data: Dictionary) -> void:
	ore_type = type_name
	ore_data = data
	max_hp = float(data.hp)
	current_hp = max_hp
	if ResourceLoader.exists(String(data.texture)):
		ore_sprite.texture = load(String(data.texture))
	if hp_bar:
		hp_bar.max_value = max_hp
		hp_bar.value = current_hp


func _on_click(_viewport: Node, event: InputEvent, _shape_idx: int) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		ore_clicked.emit(self)


func take_damage(amount: float) -> void:
	if is_dead:
		return
	var dmg := amount
	if GameManager.is_lucky() and randf() < 0.25:
		dmg *= 2
	current_hp = max(0.0, current_hp - dmg)
	if hp_bar:
		hp_bar.value = current_hp
	_shake()
	if current_hp <= 0.0:
		_break()


func _shake() -> void:
	var base_x := position.x
	var tween := create_tween()
	tween.tween_property(self, "position:x", base_x + 3, 0.05)
	tween.tween_property(self, "position:x", base_x - 3, 0.05)
	tween.tween_property(self, "position:x", base_x, 0.05)


func _break() -> void:
	is_dead = true
	var bonus := 2 if GameManager.coin_boost else 1
	var amount := int(float(ore_data.amount) * (1.0 + GameManager.pickaxe_level * 0.4) * bonus)
	if String(ore_data.reward) == "gems":
		GameManager.add_gems(amount)
	else:
		GameManager.add_coins(amount)
	visible = false
	await get_tree().create_timer(randf_range(3.0, 6.0)).timeout
	current_hp = max_hp
	if hp_bar:
		hp_bar.value = current_hp
	is_dead = false
	visible = true
