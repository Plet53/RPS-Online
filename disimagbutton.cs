using Godot;
using System;

public class disimagbutton : TextureButton{
  public void _on_set_state(bool state){Disabled = state;
    //These link to the GDScript that handles the button graphics
    //Because it works.
    if(state){EmitSignal("button_down");}
    else{EmitSignal("button_up");}}
}
