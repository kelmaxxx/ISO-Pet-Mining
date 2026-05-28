extends CharacterBody2D

var target_ore: Node = null
var attack_timer: float = 0.0
var move_speed: float = 80.0

@onready var sprite: AnimatedSprite2D = $Sprite


func _ready() -> void:
	_safe_play("idle")


func _physics_process(delta: float) -> void:
	if target_ore == null or not is_instance_valid(target_ore) or target_ore.is_dead:
		target_ore = null
		velocity = Vector2.ZERO
		_safe_play("idle")
		return

	var target_pos: Vector2 = target_ore.global_position
	var dist := global_position.distance_to(target_pos)

	if dist > 30.0:
		var dir := (target_pos - global_position).normalized()
		velocity = dir * move_speed
		move_and_slide()
		_safe_play("walk")
		sprite.flip_h = dir.x < 0
	else:
		velocity = Vector2.ZERO
		attack_timer += delta
		if attack_timer >= 0.5:
			attack_timer = 0.0
			_safe_play("attack")
			target_ore.take_damage(GameManager.get_click_damage())


func set_target(ore: Node) -> void:
	target_ore = ore


func _safe_play(anim_name: String) -> void:
	# Avoids errors during early development when not all animations exist yet.
	if sprite and sprite.sprite_frames and sprite.sprite_frames.has_animation(anim_name):
		if sprite.animation != anim_name:
			sprite.play(anim_name)
