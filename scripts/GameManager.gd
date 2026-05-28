extends Node

# Currency
var coins: int = 0
var gems: int = 0

# Upgrades
var pickaxe_level: int = 1
var pickaxe_cost: int = 50

# Pets
var equipped_pets: Array = []
var max_pets: int = 5

# Boosts (time-based, seconds since boot)
var coin_boost: bool = false
var coin_boost_end: float = 0.0
var lucky_boost: bool = false
var lucky_boost_end: float = 0.0

signal coins_changed(amount: int)
signal gems_changed(amount: int)
signal pet_added(pet_data: Dictionary)
signal pickaxe_upgraded(level: int, next_cost: int)


func _now() -> float:
	return Time.get_ticks_msec() / 1000.0


func add_coins(amount: int) -> void:
	var mult := 2 if (coin_boost and _now() < coin_boost_end) else 1
	coins += amount * mult
	coins_changed.emit(coins)


func add_gems(amount: int) -> void:
	gems += amount
	gems_changed.emit(gems)


func spend_coins(amount: int) -> bool:
	if coins >= amount:
		coins -= amount
		coins_changed.emit(coins)
		return true
	return false


func spend_gems(amount: int) -> bool:
	if gems >= amount:
		gems -= amount
		gems_changed.emit(gems)
		return true
	return false


func upgrade_pickaxe() -> bool:
	if spend_coins(pickaxe_cost):
		pickaxe_level += 1
		pickaxe_cost = int(pickaxe_cost * 2.2)
		pickaxe_upgraded.emit(pickaxe_level, pickaxe_cost)
		return true
	return false


func get_click_damage() -> float:
	return pickaxe_level * 6.0


func get_pet_dps() -> float:
	var total := 0.0
	for pet in equipped_pets:
		total += float(pet.get("power", 0))
	return total * pickaxe_level * 0.3


func activate_coin_boost() -> bool:
	if spend_gems(5):
		coin_boost = true
		coin_boost_end = _now() + 30.0
		return true
	return false


func activate_lucky_boost() -> bool:
	if spend_gems(8):
		lucky_boost = true
		lucky_boost_end = _now() + 30.0
		return true
	return false


func is_lucky() -> bool:
	return lucky_boost and _now() < lucky_boost_end


# --- Egg rolling ---

const EGG_POOLS := {
	"basic": [
		{"name": "Digger Dog",   "icon": "dog",   "power": 3,   "rarity": "common"},
		{"name": "Rock Rabbit",  "icon": "rabbit","power": 5,   "rarity": "common"},
		{"name": "Mine Cat",     "icon": "cat",   "power": 8,   "rarity": "rare"},
		{"name": "Stone Fox",    "icon": "fox",   "power": 14,  "rarity": "epic"},
		{"name": "Lava Bear",    "icon": "bear",  "power": 25,  "rarity": "legendary"},
	],
	"gem": [
		{"name": "Crystal Fox",  "icon": "fox",   "power": 20,  "rarity": "rare"},
		{"name": "Gem Wolf",     "icon": "wolf",  "power": 35,  "rarity": "rare"},
		{"name": "Sapphire Bear","icon": "bear",  "power": 55,  "rarity": "epic"},
		{"name": "Diamond Eagle","icon": "eagle", "power": 90,  "rarity": "legendary"},
		{"name": "Prism Dragon", "icon": "dragon","power": 150, "rarity": "legendary"},
	],
}

const EGG_COSTS := {
	"basic":     {"coins": 50,  "gems": 0},
	"gem":       {"coins": 0,   "gems": 10},
	"legendary": {"coins": 0,   "gems": 50},
}


func roll_egg(egg_type: String) -> Dictionary:
	var cost: Dictionary = EGG_COSTS.get(egg_type, EGG_COSTS["basic"])
	if cost.coins > 0 and not spend_coins(cost.coins):
		return {}
	if cost.gems > 0 and not spend_gems(cost.gems):
		return {}
	var pool: Array = EGG_POOLS.get(egg_type, EGG_POOLS["basic"])
	var weights := [0.45, 0.28, 0.15, 0.08, 0.04]
	var r := randf()
	var cum := 0.0
	for i in range(pool.size()):
		cum += weights[i]
		if r < cum:
			var pet: Dictionary = pool[i].duplicate()
			equipped_pets.append(pet)
			if equipped_pets.size() > max_pets:
				equipped_pets.pop_front()
			pet_added.emit(pet)
			return pet
	return pool[0]
