using Components.Library;
using InputResender.Services;
using InputResender.Services.NetClientService;

namespace Components.Interfaces;
/// <summary>Thread unsafe!</summary>
public class MPacketSender : DPacketSender, INetPoint, INetDevice {
	static Dictionary<int, MPacketSender> AllSenders = new ();
	Dictionary<MPacketSender, NetworkConnection.NetworkInfo> Conns = new ();

	static int NextID = 42;
	int portID;

	private MPacketSender ( CoreBase owner, int id ) : base ( owner ) { portID = id; }
	public static MPacketSender Fetch (int id, CoreBase owner = null) {
		lock (AllSenders) {
			if (id < 1) {
				if ( owner == null ) throw new ArgumentNullException ( nameof ( owner ), "Cannot create new MPacketSender without specifying owner!" );
				else id = NextID++;
			}
			if ( AllSenders.TryGetValue ( id, out var mPacketSender ) ) return mPacketSender;
			else {
				mPacketSender = new MPacketSender ( owner, id );
				AllSenders.Add ( id, mPacketSender );
				return mPacketSender;
			}
		}
	}

	public override IReadOnlyList<IReadOnlyList<INetPoint>> EPList => new []{ this }.AsReadonly2D ();

	private List<(string, Exception)> errors = new ();
	public override IReadOnlyCollection<(string msg, Exception e)> Errors => errors;

	public override int Connections => Conns.Count;

	public override int ComponentVersion => 1;

	string INetPoint.DscName { get; set; }
	string INetDevice.DeviceName { get; set; }
	string INetPoint.Address => $"MPSender#{portID}";
	int INetPoint.Port => portID;
	public override string ToString () => $"MPacketSender#{portID} ({((INetPoint)this).DscName})";

	string INetPoint.NetworkAddress => "MPSender";

	public bool DoNetworksMatch ( INetPoint other ) => other is MPacketSender; // All MPacketSenders are as of now considered to be in the same network.

	string INetPoint.FullNetworkPath => ((INetPoint)this).Address;

	int INetPoint.PrefixLength => 0;

	IReadOnlyDictionary<INetPoint, NetworkConnection> INetDevice.ActiveConnections => (IReadOnlyDictionary<INetPoint, NetworkConnection>)Conns.AsReadOnly ();

	INetPoint INetDevice.EP => this;

	public override event OnReceiveHandler OnReceive;
	public override event Action<NetworkConnection, INetPoint> OnNewConn;
	public override event Action<string, Exception> OnError;

	public event Action<INetDevice, INetPoint> OnClosed;

	public override void Connect ( INetPoint ep, bool canReconnect = false ) => ((INetDevice)this).Connect ( ep, null );
	public override void Destroy () => ((INetDevice)this).Close ();
	public override void Disconnect ( INetPoint ep ) => ((INetDevice)this).UnregisterConnection ( Conns[(MPacketSender)ep].Connection );
	public override string GetEPInfo ( INetPoint ep ) => ((INetDevice)this).GetInfo ();
	public override bool IsEPConnected ( INetPoint ep ) => Conns.ContainsKey ( (MPacketSender)ep );
	public override INetPoint OwnEP ( int TTL, int network ) => this;

	public List<string> Logbook = [];
	public string GetLog () {
		lock ( Logbook ) return string.Join ( "\n", Logbook );
	}
	protected void Note ( string msg ) {
		lock ( Logbook ) Logbook.Add ( msg );
	}

	public override void Recv ( NetMessagePacket data ) {
		bool proc = false;
		List<OnReceiveHandler> removedCBs = new ();
		OnReceive?.Invoke ( data, false );
		// This doesn't support buffering nor requesting listening cancelation, if not explicitly required (e.g. by tests), this should be enough for mocks
	}

	public override void Send ( HMessageHolder data ) {
		foreach ( var conn in Conns )
			SendPriv ( new NetMessagePacket ( data, conn.Key, this ) );
	}

	void INetDevice.AcceptAsync ( Action<NetworkConnection, INetPoint, bool> callback, CancellationToken ct ) {
		if ( portID < 1 ) throw new InvalidOperationException ( "Not bound" );
		ArgumentNullException.ThrowIfNull ( callback, nameof ( callback ) );

		Action<NetworkConnection, INetPoint> onNewConn = ( conn, netPoint ) => callback ( conn, netPoint, false );
		OnNewConn += onNewConn;
		ct.Register ( () => OnNewConn -= onNewConn );
	}

	void INetDevice.Bind ( INetPoint ep ) {
		ArgumentNullException.ThrowIfNull ( nameof ( ep ) );
		if (ep is not MPacketSender mps) throw new Exception ( $"Expected MPacketSender to bind to, but {ep.GetType ().Name} was given.");

		if ( portID > 0 ) return;
		if ( mps.portID < 0 ) return;
		portID = mps.portID;
	}

	void INetDevice.Close () {
		var keys = Conns.Keys.ToHashSet ();
		foreach ( var key in keys ) {
			((INetDevice)this).UnregisterConnection ( Conns[key].Connection );
		}
		lock(AllSenders) {
			AllSenders.Remove ( portID );
			Conns.Clear ();
			Conns = null;
		}
	}

	NetworkConnection INetDevice.Connect ( INetPoint ep, INetDevice.MessageHandler recvAct, int timeout, bool canReconnect ) {
		ArgumentNullException.ThrowIfNull ( ep );
		if ( ep is not MPacketSender mp ) throw new InvalidCastException ( $"Unexpected object type: {ep.GetType ().Name}." );
		if ( mp == this ) throw new Exception ( "Connecting to self is not allowed!" );

		if (Conns.TryGetValue ( mp, out var conn ) ) return conn.Connection;
		var connInfo = NetworkConnection.Create ( this, mp, SendPriv );
		Conns.Add ( mp, connInfo );

		mp.Connect ( this );

		OnNewConn?.Invoke ( connInfo.Connection, null );
		return connInfo.Connection;
	}

	string INetDevice.GetInfo () => $"(T<MPacketSender> locEP'{portID}')";

	bool INetDevice.IsConnected ( INetPoint ep ) => ep is MPacketSender mp ? Conns.ContainsKey ( mp ) : false;

	void INetDevice.UnregisterConnection ( NetworkConnection connection ) {
		var keys = Conns.Where ( kvp => kvp.Value.Connection == connection ).ToList ();
		foreach ( var kvp in keys ) {
			Conns.Remove ( kvp.Key );
			var targDev = kvp.Value.Connection.TargetEP as MPacketSender;
			var Reversed = targDev.Conns.FirstOrDefault ( kvp => kvp.Value.Connection.LocalDevice == targDev && kvp.Value.Connection.TargetEP == this ).Value.Connection;
			if ( Reversed != null ) {
				(targDev as INetDevice).UnregisterConnection ( Reversed );
			}
		}
	}

	private bool SendPriv ( NetMessagePacket packet) {
		if ( packet == null ) return false;
		if ( packet.TargetEP is not MPacketSender mp ) return false;
		if ( !Conns.TryGetValue ( mp, out var connInfo ) ) return false;
		if ( connInfo.Connection == null ) return false;
		mp.Recv ( packet );
		return true;
		//return connInfo.Receiver?.Invoke ( packet ) == INetDevice.ProcessResult.Accepted;
	}


	public override StateInfo Info => new MStateInfo ( this );

	public class MStateInfo : DStateInfo {
		public new MPacketSender Owner => (MPacketSender)base.Owner;
		public MStateInfo (MPacketSender owner) : base (owner) {

		}

		protected override string[] GetBuffers () => Array.Empty<string> ();
		protected override string[] GetConnections () => Array.Empty<string> ();
	}
}









	/* List<MPacketSender> ConnList = new List<MPacketSender> ();
	Queue<HMessageHolder> MsgQueue = new Queue<HMessageHolder> ();

	private event Action<MPacketSender> OnNewConnLocal;
	public override event Action<INetPoint> OnNewConn {  add => OnNewConnLocal += value; remove => OnNewConnLocal -= value; }

	public MPacketSender ( CoreBase owner ) : base ( owner ) { }

	public override event Action<string, Exception> OnError { add { } remove { } }
	public override int ComponentVersion => 1;
	public override SimpleNetPoint OwnEP ( int TTL, int network ) => this;
	public override int Connections => ConnList.Count;
	public override IReadOnlyList<IReadOnlyList<SimpleNetPoint>> EPList { get => new []{ this }.AsReadonly2D (); }
	public override IReadOnlyCollection<(string msg, Exception e)> Errors { get => new List<(string msg, Exception e)> ().AsReadOnly (); }
	public override bool IsEPConnected ( object ep ) => ConnList.Contains ( ep );
	private readonly List<OnReceiveHandler> OnReceiveHandlers = new ();

	public override void Connect ( INetPoint ep ) {
		if ( ep is not SimpleNetPoint mpSender ) throw new InvalidCastException ( $"Unexpected object type: {ep.GetType ().Name}." );
		if ( mpSender == this ) throw new InvalidOperationException ( "Cannot connect to self" );
		if ( ConnList.Contains ( mpSender ) ) throw new InvalidOperationException ( $"Already connected to {mpSender.Name}" );
		if ( mpSender.ConnList.Contains ( this ) ) throw new InvalidOperationException ( $"{mpSender.Name} is already connected to this" );

		ConnList.Add ( mpSender );
		mpSender.ConnList.Add ( this );
	}
	public override void Disconnect ( INetPoint ep ) {
		if ( ep is not SEP mpSender ) throw new InvalidCastException ( $"Unexpected object type: {ep.GetType ().Name}." );
		if ( !ConnList.Remove ( mpSender ) ) throw new InvalidOperationException ( $"No active connection to {ep}" );
		if ( !mpSender.ConnList.Remove ( this ) ) throw new InvalidOperationException ( $"{mpSender.Name} is not connected to this" );
	}
	public override event OnReceiveHandler OnReceive {
		add {
			if ( value == null ) return;
			while ( MsgQueue.Count > 0 ) {
				var ret = value ( MsgQueue.Dequeue (), false );
				if ( ret.HasFlag ( CallbackResult.Stop ) ) {
					// Caller does no longer want to receive
					return;
				}
			}
			if ( OnReceiveHandlers.Contains ( value ) ) return;
			OnReceiveHandlers.Add ( value );
		}
		remove {
			if ( value == null ) return;
			if ( !OnReceiveHandlers.Remove ( value ) )
				throw new InvalidOperationException ( $"Callback {value.Method.AsString ()} not found in OnReceiveHandlers" );
		}
	}
	public override void Send ( HMessageHolder data ) {
		foreach ( MPacketSender receiver in ConnList )
			receiver.Recv ( (byte[])data );
	}
	public override void Recv ( byte[] data ) {
		bool proc = false;
		var msg = (HMessageHolder)data;
		List<OnReceiveHandler> removedCBs = new ();
		foreach ( var Callback in OnReceiveHandlers ) {
			var ret = Callback ( msg, proc );
			proc |= !ret.HasFlag ( CallbackResult.Skip );
			if ( !ret.HasFlag (CallbackResult.Stop) ) removedCBs.Add ( Callback );
		}
		foreach ( var Callback in removedCBs ) OnReceive -= Callback;
		if (!proc) {
			if (MsgQueue.Count >= 16 ) MsgQueue.Dequeue ();
			MsgQueue.Enqueue ( msg );
		}
	}

	public override string GetEPInfo ( object ep ) {
		if ( ep is not MPacketSender mp ) return $"Object '{ep}' is not valid EP ({nameof ( MPacketSender )}).";
		if ( mp == this ) return $"{nameof ( MPacketSender )} loopback ({this})";
		return $"{ep} ({(ConnList.Contains ( ep ) ? "Connected" : "Not connected")}";
	}

	public override void Destroy () {
		ConnList.Clear (); MsgQueue.Clear ();
		OnReceiveHandlers.Clear (); MsgQueue = null; ConnList = null;
	}

	public override StateInfo Info => new VStateInfo ( this );
	public class VStateInfo : DStateInfo {
		public new MPacketSender Owner => (MPacketSender)base.Owner;
		public VStateInfo ( MPacketSender owner ) : base ( owner ) {
			//LocalCallback = Owner.Callback.Method.AsString ();
			LocalCallbacks = new string[Owner.OnReceiveHandlers.Count];
			for ( int i = 0; i < Owner.OnReceiveHandlers.Count; i++ )
				LocalCallbacks[i] = Owner.OnReceiveHandlers[i].Method.AsString ();
		}

		public string[] LocalCallbacks;

		protected override string[] GetConnections () {
			int N = Owner.ConnList.Count;
			string[] ret = new string[N];
			for ( int i = 0; i < N; i++ )
				ret[i] = $"MPacketSender {{{Owner.ConnList[i].Name}}}";
			return ret;
		}
		protected override string[] GetBuffers () {
			int N = Owner.MsgQueue.Count;
			string[] ret = new string[N];
			int ID = 0;
			foreach ( var msg in Owner.MsgQueue )
				ret[ID++] = msg.Span.ToHex ();
			return ret;
		}
		public override string AllInfo () => $"{base.AllInfo ()}{BR}Callback:{BR}{string.Join (", ", LocalCallbacks)}";
	}
}*/