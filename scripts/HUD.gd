extends Control

@onready var coins_label: Label = $TopBar/CoinsLabel
@onready var gems_label: Label = $TopBar/GemsLabel
@onready var pick_label: Label = $BottomBar/UpgradePickButton/PickLabel
@onready var upgrade_button: Button = $BottomBar/UpgradePickButton


func _ready() -> void:
	GameManager.coins_changed.connect(_update_coins)
	GameManager.gems_changed.connect(_update_gems)
	GameManager.pickaxe_upgraded.connect(_update_pick)
	_update_coins(GameManager.coins)
	_update_gems(GameManager.gems)
	_update_pick(GameManager.pickaxe_level, GameManager.pickaxe_cost)


func _update_coins(amount: int) -> void:
	coins_label.text = "Coins: %d" % amount


func _update_gems(amount: int) -> void:
	gems_label.text = "Gems: %d" % amount


func _update_pick(level: int, next_cost: int) -> void:
	pick_label.text = "Pick Lv%d (Next: %d)" % [level, next_cost]


func _on_upgrade_pick_pressed() -> void:
	GameManager.upgrade_pickaxe()


func _on_coin_boost_pressed() -> void:
	GameManager.activate_coin_boost()


func _on_lucky_boost_pressed() -> void:
	GameManager.activate_lucky_boost()


func _on_basic_egg_pressed() -> void:
	var pet := GameManager.roll_egg("basic")
	if pet.is_empty():
		return
	var world := get_tree().current_scene
	if world and world.has_method("spawn_pet"):
		world.spawn_pet(pet)
