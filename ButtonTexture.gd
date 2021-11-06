extends TextureRect
const initpos = Vector2(13,11)
const newpos = Vector2(13,34)
func _on_TextureButton_button_down():
  rect_position = newpos
func _on_TextureButton_button_up():
  rect_position = initpos
