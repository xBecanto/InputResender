using System.Net;
using Components.Interfaces;
using Components.Library;
using InputResender.Services;
using InputResender.Services.NetClientService;
using NetList = InputResender.Services.NetworkFinderService;
using InputResender.Services.NetClientService.InMemNet;
using System.Collections.Concurrent;

namespace Components.Implementations {
	/// <summary>This serves mostly as a translation layer between the DPacketSender and the NetClientList</summary>
	public class VPacketSender : DPacketSender {
		public const int MaxBufferSize = 32;
		private object lockObj = new ();
		private static int DefPort = 45256;
		public readonly int Port;
		readonly NetClientList Clients;
		List<(string msg, Exception e)> errors;
		public IReadOnlyDictionary<INetPoint, NetworkConnection> ActiveConns;
		private readonly BlockingCollection<NetMessagePacket> PacketBuffer;
		private readonly List<OnReceiveHandler> OnReceiveHandlers = new ();
		private List<INetPoint[]> NetList;

		public override event Action<NetworkConnection, INetPoint> OnNewConn;

		private event Action<string, Exception> OnErrorLocal;
		public override event Action<string, Exception> OnError { add => OnErrorLocal += value; remove => OnErrorLocal -= value; }

		public override string GetEPInfo ( INetPoint ep ) {
			if ( ep is not INetPoint NP ) return $"Object '{ep}' is not valid EP.";
			(int i, int j) = FindEP ( ep );
			if ( i < 0 ) return $"EndPoint '{NP}' is not registered under this component.";
			var EP = NetList[i][j];

			if ( !Clients.OwnedDevices.TryGetValue ( EP, out var netDevice ) ) return $"No client associated with '{EP}'[{i},{j}]";
			return $"{GetType ().Name} '{this}' (ttl:[{i},{j}]) :: {netDevice}:{netDevice.GetInfo ()}";
		}

		private (int, int) FindEP (object ep) {
			for (int i = 0; i < NetList.Count; i++ ) {
				for (int j = 0; j < NetList[i].Length; j++ ) {
					if ( ep.Equals ( NetList[i][j] ) ) return (i, j);
				}
			}
			return (-1, -1);
		}

		public VPacketSender ( CoreBase owner, int port = -1, Action<string, Exception> onErrorSub = null ) : base ( owner ) {
			if ( !Owner.IsRegistered ( nameof ( DLogger ) ) ) new VLogger ( Owner );
			errors = new List<(string msg, Exception e)> ();
			if ( onErrorSub != null ) OnError += onErrorSub;
			lock (lockObj) {
				Port = port < 0 ? DefPort++ : port;
			}
			NetList = new NetList ().ToList ( Port );
			NetList.Insert ( 0, INetPoint.NextAvailable<InMemNetPoint> ( 1, Port ) );
			PacketBuffer = new ( MaxBufferSize );
			Clients = new ();
			Clients.OnLog += (packet, msg) => Owner.PushDelayedMsg (packet == null ? $"{msg} (no packet)" : $"{msg} {packet.SourceEP}->{packet.TargetEP}");
			ActiveConns = Clients.Connections;
			//Clients.AddEP ( InMemNetPoint.NextAvailable ( DefPort ) );

			foreach ( var network in NetList ) {
				// Assuming that NetworkList returns only local addresses (i.e. bindable)
				foreach ( var node in network ) {
					// Skip if already added
					if ( Clients.OwnedDevices.Keys.Any ( ep => node.Equals ( ep ) ) ) continue;
					try {
						Clients.AddEP ( node );
						Owner.Fetch<DLogger> ()?.Log ( $"Added {node} '{node.DscName}' as a valid local EP" );
					} catch ( Exception e ) {
						PrintError ( $"Failed to add {node} as a valid local EP", e );
					}
				}
			}
			Clients.AcceptAcync ( OnNewConnection );
		}

		private void OnNewConnection (NetworkConnection conn, INetPoint oldTarget) {
			conn.OnReceive += LocalReceiver;
			OnNewConn?.Invoke ( conn, oldTarget );
		}
		private void PrintError (string msg, Exception e) {
			errors.Add ( (msg, e) );
			Owner.Fetch<DLogger> ()?.Log ( $"ERROR: {msg} ({e.Message})" );
			OnErrorLocal?.Invoke ( msg, e );
		}

		public override int ComponentVersion => 1;
		public override int Connections => Clients.Connections.Count;
		public override IReadOnlyCollection<(string msg, Exception e)> Errors => errors.AsReadOnly ();
		public override IReadOnlyList<IReadOnlyList<INetPoint>> EPList => NetList.AsReadOnly ();
		public override bool IsEPConnected ( INetPoint ep ) => ep is INetPoint iep ? Clients.Connections.ContainsKey ( iep ) : false;

		public override INetPoint OwnEP ( int TTL, int network = 0 ) => NetList[network][TTL];

		public override void Connect ( INetPoint epObj, bool canReconnect = false ) {
			if ( epObj == null ) throw new ArgumentNullException ( nameof ( epObj ) );
			NetworkConnection conn = null;
			for (int i = 0; i < 5; i++ ) {
				try {
					conn = Clients.Connect ( epObj, canReconnect: canReconnect );
					break;
				} catch (OperationCanceledException e) {
					if ( i == 4 ) throw new InvalidOperationException ( $"Failed to connect to {epObj}", e );
				}
			}
			
			if ( conn == null ) PrintError ( "Failed to connect", null );
			//else ActiveConns.Add ( epObj, conn );
			conn.OnReceive += LocalReceiver;
		}
		public override void Disconnect ( INetPoint epObj ) {
			if ( epObj == null ) throw new ArgumentNullException ( nameof ( epObj ) );
			if ( !Clients.Connections.TryGetValue ( epObj, out var conn ) )
				throw new InvalidOperationException ( $"No active connection to {epObj}" );
			conn.OnReceive -= LocalReceiver;
			conn.Close ();
			//ActiveConns.Remove ( epObj );
		}

		private INetDevice.ProcessResult LocalReceiver ( NetMessagePacket msg ) {
			lock ( PacketBuffer ) {
				//if ( Receiver != null ) return Receiver ( msg.Data ) ? INetDevice.ProcessResult.Accepted : INetDevice.ProcessResult.Skiped;
				foreach ( var handler in OnReceiveHandlers ) {
					var ret = handler ( msg, false );
					if ( ret.HasFlag ( CallbackResult.Skip ) ) return INetDevice.ProcessResult.Skiped;
					if ( !ret.HasFlag ( CallbackResult.Stop ) ) return INetDevice.ProcessResult.Accepted;
				}
				if ( DComponentJoiner.TrySend ( this, null, msg, msg.Data ) > 0 )
					return INetDevice.ProcessResult.Accepted;
				if ( PacketBuffer.Count >= MaxBufferSize ) PacketBuffer.Take ();
				PacketBuffer.Add ( msg );
				return INetDevice.ProcessResult.Accepted;
			}
		}

		public override event OnReceiveHandler OnReceive {
			add {
				if ( value == null ) return;
				lock (PacketBuffer) {
					List<NetMessagePacket> declined = new ();
					while ( PacketBuffer.Count > 0 ) {
						var msg = PacketBuffer.Take ();
						var ret = value ( msg, false );
						if ( !ret.HasFlag ( CallbackResult.Skip) ) declined.Add ( msg );
						if ( ret.HasFlag( CallbackResult.Stop) ) return; // Caller does no longer want to receive (might be due to a full buffer, error or received specific message)
					}
					foreach ( var msg in declined ) PacketBuffer.Add ( msg );
					OnReceiveHandlers.Add ( value );
				}
			}
			remove {
				if ( value == null ) return;
				lock ( PacketBuffer ) {
					OnReceiveHandlers.Remove ( value );
				}
			}
		}
		public override void Recv ( NetMessagePacket data ) => throw new NotImplementedException ();
		public override void Send ( HMessageHolder data ) {
			if ( Owner.LogFcn != null ) {
				System.Text.StringBuilder SB = new ();
				SB.AppendLine ( $"Sending data[{data.Size}]" );
				foreach ( var conn in ActiveConns ) SB.AppendLine ( $"  {conn.Value}" );
				Owner.LogFcn.Invoke ( SB.ToString () );
			}
			foreach ( var conn in ActiveConns ) conn.Value.Send ( data );
			Task.Delay ( 10 ).Wait ();
			Owner.FlushDelayedMsgs<DMainAppCore> ();
		}

		public override string ToString () {
			var SB = new System.Text.StringBuilder ();
			var lst = ActiveConns;
			int N = lst.Count;
			SB.Append ( $"(IP:{Port}=>{{" );
			var targs = ActiveConns.Values.Select ( ep => ep.TargetEP.ToString () ).ToArray ();
			SB.Append ( targs.Any () ? string.Join ( ", ", targs ) : "none" );
			SB.Append ( '}' );
			return SB.ToString ();
		}

		public static IPAddress IPv4 ( byte A, byte B, byte C, byte D ) => new IPAddress ( new byte[] { A, B, C, D } );

		public override void Destroy () {
			foreach ( var conn in ActiveConns ) conn.Value.Close ();
			//ActiveConns.Clear ();
			Clients.Close ();
			PacketBuffer.Dispose ();
			errors.Clear ();
		}

		public override StateInfo Info => new VStateInfo ( this );

		public class VStateInfo : DStateInfo {
			public new VPacketSender Owner => (VPacketSender)base.Owner;
			public VStateInfo ( VPacketSender owner ) : base ( owner ) {
				Clients = owner.Clients.ToString ();
			}

			public readonly string Clients;

			protected override string[] GetBuffers () {
				lock ( Owner.PacketBuffer ) {
					NetMessagePacket[] bufferCopy = Owner.PacketBuffer.ToArray ();
					return bufferCopy.Where ( p => p != null )
						.Select ( p => $"{p.SourceEP}->{p.TargetEP}[t:{p.SignalType}|e:{p.Error}] {((byte[])p.Data).ToHex ()}" ).ToArray ();
				}
			}
			protected override string[] GetConnections () {
				List<string> ret = new ();
				foreach ( var conn in Owner.ActiveConns ) {
					ret.Add ( $"{conn.Key}: {conn.Value}" );
				}
				return ret.ToArray ();
			}
			public override string AllInfo () => $"{base.AllInfo ()}{BR}Clients: {Clients}";
		}
	}
}