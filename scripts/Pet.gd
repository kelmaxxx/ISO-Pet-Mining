extends CharacterBody2D

var pet_data: Dictionary = {}
var target_ore: Node = null
var attack_timer: float = 0.0
var move_speed: float = 60.0

@onready var sprite: AnimatedSprite2D = $Sprite


func setup(data: Dictionary) -> void:
	pet_data = data
	# Optional: load per-pet spritesheet here based on data.icon
	# var sf_path := "res://assets/pets/pet_%s.tres" % data.icon
	# if ResourceLoader.exists(sf_path):
	#     sprite.sprite_frames = load(sf_path)


func _physics_process(delta: float) -> void:
	if target_ore == null or not is_instance_valid(target_ore) or target_ore.is_dead:
		target_ore = _find_nearest_ore()
		if target_ore == null:
			velocity = Vector2.ZERO
			_safe_play("walk")
			return

	var target_pos: Vector2 = target_ore.global_position
	var dist := global_position.distance_to(target_pos)

	if dist > 25.0:
		var dir := (target_pos - global_position).normalized()
		velocity = dir * move_speed
		move_and_slide()
		_safe_play("walk")
		sprite.flip_h = dir.x < 0
	else:
		velocity = Vector2.ZERO
		attack_timer += delta
		if attack_timer >= 1.0:
			attack_timer = 0.0
			_safe_play("attack")
			var power := float(pet_data.get("power", 5))
			target_ore.take_damage(power * GameManager.pickaxe_level * 0.3)


func _find_nearest_ore() -> Node:
	var ores := get_tree().get_nodes_in_group("ores")
	var nearest: Node = null
	var best_dist := 9999.0
	for ore in ores:
		if ore.is_dead:
			continue
		var d := global_position.distance_to(ore.global_position)
		if d < best_dist:
			best_dist = d
			nearest = ore
	return nearest


func _safe_play(anim_name: String) -> void:
	if sprite and sprite.sprite_frames and sprite.sprite_frames.has_animation(anim_name):
		if sprite.animation != anim_name:
			sprite.play(anim_name)
