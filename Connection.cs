using Godot;
using System;
using System.Threading.Tasks;
using System.Threading;

public class Connection : Node
{
  #region datastore
  string target;
  SceneTree st;
  NetworkedMultiplayerENet net;
  bool host, up, lrm;
  UPNP u;
  CancellationTokenSource dc;
  ushort port, portcount;
  ushort[] ports;
  Control connect, connecting, bestof, game, disconnect, results;
  Godot.Timer hb;
  static int[] err = {1,2,3,4,5,6,7,8,9,10,12,13,16,17,18,19,20,21,22,23,24,25,26};
  #endregion
  #region signals
  [Signal]
  public delegate void _roundstart();
  [Signal]
  public delegate void _Resolve(byte move);
  [Signal]
  public delegate void _set_bestof(byte bestof);
  [Signal]
  public delegate void _check(bool a, byte state);
  [Signal]
  public delegate void _reset();
  [Signal]
  public delegate void _dc();
  #endregion
  public override void _Ready(){
    st = this.GetTree();
    net = new NetworkedMultiplayerENet();
    net.TransferMode = NetworkedMultiplayerENet.TransferModeEnum.Reliable;
    net.AllowObjectDecoding = false;
    portcount = 0; port = 7777; host = up = lrm = false;
    target = "127.0.0.1";
    connect = GetNode<Control>(new NodePath("Connect"));
    connecting = GetNode<Control>(new NodePath("Connecting"));
    disconnect = GetNode<Control>(new NodePath("Disconnect"));
    bestof = GetNode<Control>(new NodePath("BestOf"));
    game = GetNode<Control>(new NodePath("Game"));
    hb = GetNode<Godot.Timer>(new NodePath("HeartBeat"));
    results = GetNode<Control>(new NodePath("ResultsScreen"));
    dc = new CancellationTokenSource();
    ports = new ushort[8];
  }
  public void _on_Heartbeat(){net.Poll();}
  public void _on_UPNP_toggled(bool state){up = state;}
  public void _on_Address_text_entered(String s){target = s;}
  public void _on_PortEdit_text_entered(String s){if(!ushort.TryParse(s, out port)){port = 7777;}}
  #region network
  public void _on_toConnect_pressed(){
    connect.Visible = false;
    connecting.Visible = true;
    _establish(false);
  }
  public async void _on_toHost_pressed(){
    bool success = true;
    connect.Visible = false;
    connecting.Visible = true;
    if(up){
      try{
        //cheating to keep the UI responsive
        await Task.Run(() => {if(u == null){u = new UPNP();}
        if(u.Discover() == 0){
          if(u.GetDeviceCount() > 0){
          int i = u.AddPortMapping(port);
          foreach(int error in err){
            if(i == error){success = false; GD.Print("Could not establish outward gateway. " + i); break;}}
          //Allow attempts at other ports, cleanup on app exit
          if(success){
            if(portcount < 8){
              ports[portcount] = port;
              portcount++;}
          //Unless you've done too many
            else{
              GD.Print("Attempting to clear ports. " + portcount);
              u.DeletePortMapping(ports[7]);
              ports[7] = port;}
      }}}}, dc.Token);}
      catch(System.Threading.Tasks.TaskCanceledException){}}
    //If you're relying on UPNP to get out of network, and it doesn't, you can't host.
    if(success){
      _establish(true);}
      else{_disconnect();}
  }
  public void _establish(bool state){
    host = state;
    Error e;
    if(state){e = net.CreateServer(port, 1);}
    else{e = net.CreateClient(target, port);}
    st.NetworkPeer = net;
    //Signals need to be connected specifically from the scenetree for this to work.
    st.NetworkPeer.Connect("connection_failed", this, nameof(_cfail));
    if(state){st.NetworkPeer.Connect("peer_connected", this, nameof(_s_connect));
      st.NetworkPeer.Connect("peer_disconnected", this, nameof(_s_disconnect));}
    else{st.NetworkPeer.Connect("connection_succeeded", this, nameof(_connect));
      st.NetworkPeer.Connect("server_disconnected", this, nameof(_disconnect));}
    hb.Start();
    GD.Print(e);
    disconnect.Visible = true;
  }
  //arg stripping
  public void _s_connect(int d){_connect();}
  public void _connect(){
    bestof.Visible = true;
    connecting.Visible = false;
    net.RefuseNewConnections = true;
    EmitSignal(nameof(_set_bestof), 3);
  }
  public void _cfail(){
    AnimationPlayer a = GetNode<AnimationPlayer>(new NodePath("/AnimationPlayer"));
    a.Play("ConnectionFail");
    _disconnect();
  }
  public void _s_disconnect(int d){_disconnect();}
  public void _disconnect(){
    st.NetworkPeer.Disconnect("connection_failed", this, nameof(_cfail));
    if(host){st.NetworkPeer.Disconnect("peer_disconnected", this, nameof(_s_disconnect));
      st.NetworkPeer.Disconnect("peer_connected", this, nameof(_s_connect));}
    else{st.NetworkPeer.Disconnect("server_disconnected", this, nameof(_disconnect));
      st.NetworkPeer.Disconnect("connection_succeeded", this, nameof(_connect));}
    dc.Cancel();
    net.CloseConnection();
    hb.Stop();
    EmitSignal(nameof(_dc));
    lrm = false;
    bestof.Visible = false;
    game.Visible = false;
    connecting.Visible = false;
    disconnect.Visible = false;
    results.Visible = false;
    connect.Visible = true;
    net.RefuseNewConnections = false;
  }
  public void _send(byte info){
    byte[] b = {info};
    _send_many(b);
  }
  public void _send_many(byte[] data){
    GD.Print("sending: " + data[0]);
    Rpc(nameof(_receive), data);
  }
  //Messaging over synchronized objects, naturalized anticheat.
  //After all, you can't cheat in RPS if you don't know what your opponent is throwing.
  [Remote]
  public void _receive(byte[] data){
    GD.Print("receiving : " + data[0]);
    switch(data[0]){
      case 5:  // READY
      EmitSignal(nameof(_roundstart));
      break;
      case 10: // ROCK
      case 11: // PAPER
      case 12: // SCISSORS
      EmitSignal(nameof(_Resolve), data[0]);
      break;
      case 20: // BEST OF?
      //Theoretically this is the only time we need more than one bytes.
      //For now, at least.
      EmitSignal(nameof(_set_bestof), data[1]);
      break;
      case 33: // WIN
      case 35: // LOSS
      EmitSignal(nameof(_check), true, data[0]);
      break;
      case 34: // TIE
      EmitSignal(nameof(_check), false, data[0]);
      break;
      case 40: // GAME START
      _start();
      break;
      case 50: // REMATCH
      _rm();
      break;
      case 55: // ERROR
      GD.Print("Error");
      break;
      default: break;
    }
  }
  #endregion
  public void _disp_results(){results.Visible = true;
    game.Visible = false;}
  public void _rematch(){lrm = true; _send(50);}
  //You'll see this a lot, where I wait for "agreement" before proceeding
  //Cancellation Tokens let me clean things up in the event of disconnect
  //...and might be the reason things appcrash randomly
  public async void _rm(){
    try{await Task.Run(
      async () => {
        TextureRect rm = GetNode<TextureRect>(new NodePath("ResultsScreen/rm"));
        rm.Visible = true;
        while(!lrm){await Task.Delay(100);}
        rm.Visible = false;
        results.Visible = false;
        _start();
      }, dc.Token);}
    catch(System.Threading.Tasks.TaskCanceledException){}
  }
  public void _start(){
    bestof.Visible = false;
    game.Visible = true;
    EmitSignal(nameof(_reset));
  }
  public override void _Notification(int what){
    if((what == MainLoop.NotificationWmQuitRequest) || (what == MainLoop.NotificationCrash)){
      //If we've mapped anything via UPNP, we need to clean it up
      if(portcount > 0){
        //unless something wacky happened
        if(u.Discover() == 0){
          if(u.GetDeviceCount() > 0){
            for(int i = 0; i < portcount; i++){
              int j = u.DeletePortMapping(ports[i]);
              foreach(int error in err){
                if(j == error){GD.Print("Could not reset outward gateway. Check your router to ensure proper configuration. " + i); break;}
        }}}
        }else{GD.Print("Network configuration seems to have changed mid-game. Best of luck to you, tech wizard.");}}
    }
  }
}
