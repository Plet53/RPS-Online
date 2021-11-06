using Godot;
using System;

public class ResultTexture : TextureRect
{
  public void _on_result(string path){Texture = GD.Load<Texture>(path);}
}
