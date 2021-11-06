using Godot;
using System;
using System.Threading.Tasks;
using System.Threading;

public class RPS : Control{
  #region datastore
  public byte move, wins, losses, laststate, bestof;
  public bool localstate;
  public RichTextLabel score, llabel, rlabel;
  public BaseButton button;
  public CancellationTokenSource dc;
  #endregion
  #region signals
  [Signal]
  public delegate void _send(byte info);
  [Signal]
  public delegate void _result(string path);
  [Signal]
  public delegate void _disp_results();
  [Signal]
  public delegate void _set_state(bool state);
  #endregion
  public override void _Ready(){
    score = GetNode<RichTextLabel>(new NodePath("ScoreLabel"));
    llabel = GetNode<RichTextLabel>(new NodePath("LocalLabel"));
    rlabel = GetNode<RichTextLabel>(new NodePath("RemoteLabel"));
    button = GetNode<BaseButton>(new NodePath("Send"));
    dc = new CancellationTokenSource();
    bestof = 3;
    _reset();
  }
  public void _reset(){
    localstate = false;
    move = wins = losses = laststate = 0;
    score.Text = score.Text.Substr(0, 8) + wins + "-" + losses;
    llabel.Text = llabel.Text.Substr(0,12);
    rlabel.Text = rlabel.Text.Substr(0, 13);
    button.Disabled = true;
    EmitSignal(nameof(_set_state), false);
  }
  //On disconnect, any lingering tasks should be removed.
  public void _dc(){dc.Cancel();}
  public void _Resolve(byte o){
    string message = "???";
    switch (o){
        case 10:
        message = "Rock.";
        break;
        case 11:
        message = "Paper.";
        break;
        case 12:
        message = "Scissors.";
        break;
        default:
        break;
    }
    rlabel.Text = rlabel.Text.Substr(0, 13) + message;
    int r = move - o;
    byte b = 0;
    switch (r){
      case -2: // R - S
      case 1: // P - R, S - P
      b = 35; // WIN
      break;
      case -1: // R - P, P - S
      case 2: // S - R
      b = 33; // LOSS
      break;
      case 0: // X - X
      b = 34; // TIE
      break;
      default:
      b = 55; // Error
      break;
    }
    laststate = b;
    EmitSignal(nameof(_send), b);
  }
  //Move strings are stored in the scene info.
  public void _on_Move_pressed(byte i, string m){
    llabel.Text = llabel.Text.Substr(0,12) + m;
    move = i;
    button.Disabled = false;
  }
  public void _on_send_pressed(){
    EmitSignal(nameof(_send), 5);
    localstate = true;
    button.Disabled = true;
    EmitSignal(nameof(_set_state), true);
  }
  public async void _on_roundstart(){
    try{await Task.Run(
      async () => 
      {while(!localstate){await Task.Delay(100);}
      EmitSignal(nameof(_send), move);
      }, dc.Token);}
    catch(System.Threading.Tasks.TaskCanceledException){}
  }
  public async void _check(bool a, byte res){
    try{await Task.Run( async () => {
      //This is here so that you can actually see what move your opponent played
      //Because I'm not enough of an artist to do good fun little animations
      await Task.Delay(1000);
      while(laststate == 0){await Task.Delay(100);}
      //THE FUNDAMENTAL LOGIC:
      //In the event of a tie, check and make sure your opponent agrees.
      //In the event of a win or loss, check and make sure your opponent doesn't think *they* won / lost.
      //Theoretically, you could cheat by always reporting wins.
      //But if it goes through this, yout opponent's results will disagree
      if((res == laststate) ^ a){GD.Print("Results verified.");
      switch(res){
        case 33:
        wins++;
        break;
        case 35:
        losses++;
        break;
        case 34:
        break;
        default:
        GD.Print("Something Unusual Happened.");
        break;
      }
      //Fire off the internal filename for the graphic we want to show, then show it.
      if(losses == ((bestof + 1) / 2)){EmitSignal(nameof(_result), "res://Messages/Loser.png"); EmitSignal(nameof(_disp_results));}
      else if(wins == ((bestof + 1) / 2)){EmitSignal(nameof(_result), "res://Messages/Winner.png"); EmitSignal(nameof(_disp_results));}
      //Otherwise, update the display
      else{score.Text = score.Text.Substr(0, 8) + wins + "-" + losses;}}
    else{GD.Print("Results are not verified, discarding.");
    }}
      ,dc.Token);}
    catch(System.Threading.Tasks.TaskCanceledException){}
    localstate = false;
    laststate = move = 0;
    llabel.Text = llabel.Text.Substr(0,12);
    rlabel.Text = rlabel.Text.Substr(0, 13);
    EmitSignal(nameof(_set_state), false);
  }
  public void _on_set_bestof(byte amount){bestof = amount;}
}
