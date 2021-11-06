using Godot;
using System;

public class BestOf : Control
{
  public byte bestof;
  public LineEdit line;
  public RichTextLabel disp;
  [Signal]
  public delegate void _set_bestof(byte amount);
  [Signal]
  public delegate void _send_many(byte[] data);
  public override void _Ready(){
    line = GetNode<LineEdit>(new NodePath("LineEdit"));
    disp = GetNode<RichTextLabel>(new NodePath("Number"));
  }
  public void _on_LineEdit_text_entered(string s){
    int a = 3;
    if(int.TryParse(s, out a)){
      //Look if you want to do best of 255 be my guest
      a = Mathf.Clamp(a,1,255);
      //A Best Of should only ever be odd, enforcing
      a += (a % 2) - 1;
      line.Text = a.ToString();
      bestof = (byte)a;
      disp.Text = bestof.ToString();
      byte[] data = {20,bestof};
      //Communicate with the game script
      EmitSignal(nameof(_set_bestof), bestof);
      //Communicate with the internet
      EmitSignal(nameof(_send_many), data);
  }}
  //Receive info, from the internet
  public void _on_set_bestof(byte amount){bestof = amount;
    EmitSignal(nameof(_set_bestof), bestof);
    disp.Text = bestof.ToString();}
}
