using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace InputResender.Services.NetClientService {
	public abstract class ANetDevice<EPT> : INetDevice where EPT : class, INetPoint {
		private Dictionary<EPT, NetworkConnection.NetworkInfo> Connections = new ();
		private Action<NetworkConnection, INetPoint, bool> ConnAccepter;

		public string DeviceName { get; set; } = string.Empty;
		public INetPoint EP { get => locEP; }
		public event Action<INetDevice, INetPoint> OnClosed;

		public IReadOnlyDictionary<INetPoint, NetworkConnection> ActiveConnections => Connections.ToDictionary ( kvp => kvp.Key as INetPoint, kvp => kvp.Value.Connection );
		public bool IsConnected ( INetPoint ep ) => Connections.ContainsKey ( ep as EPT );

		public readonly List<(NetMessagePacket, string)> LastErrors = new ();
		private Dictionary<EPT, Connector<EPT>> OpenConnRequests = new ();

		public List<string> Logbook = [];
		public string GetLog () {
			lock ( Logbook ) return string.Join ( "\n", Logbook );
		}
		protected void Note ( string msg ) {
			lock ( Logbook ) Logbook.Add ( msg );
		}

		protected EPT locEP { get; private set; }
		protected abstract ANetDeviceLL<EPT> boundedLLDevice { get; }
		protected abstract void BindLL ( EPT ep );
		/// <summary>This method is called right after sending message. It can be used to attempt securing some higher level continuity.</summary>
		protected virtual void Waiter ( NetMessagePacket msg, bool sentStatus ) { }

		protected virtual bool LogRecvError ( NetMessagePacket msg, string dsc ) {
			LastErrors.Add ( (msg, dsc) );
			return false;
		}

		public void AcceptAsync ( Action<NetworkConnection, INetPoint, bool> callback, CancellationToken ct ) {
			if ( EP == null ) throw new InvalidOperationException ( "Not bound" );
			if ( callback == null ) throw new ArgumentNullException ( nameof ( callback ) );
			if ( ConnAccepter != null ) throw new InvalidOperationException ( "Already accepting" );

			ConnAccepter = callback;
			ct.Register ( () => ConnAccepter = null );
		}

		public void Bind ( INetPoint ep ) {
			if ( ep == null ) throw new ArgumentNullException ( nameof ( ep ) );
			if ( EP != null ) throw new InvalidOperationException ( $"Already bounded to {EP}" );
			if ( ep is not EPT ) throw new ArgumentException ( $"{nameof ( ep )} is not of type {nameof ( EPT )}" );
			locEP = ep as EPT;
			BindLL ( locEP );
			if ( boundedLLDevice == null ) throw new InvalidOperationException ( "LLDevice was not properly bound" );
		}

		protected abstract void InnerClose ();
		public void Close () {
			if ( locEP == null ) throw new InvalidOperationException ( "Not bound" );
			OnClosed?.Invoke ( this, locEP );
			InnerClose ();
			if ( boundedLLDevice != null ) throw new InvalidOperationException ( "LLDevice was not properly closed" );
			locEP = null;
		}

		NetworkConnection INetDevice.Connect ( INetPoint ep, INetDevice.MessageHandler recvAct, int timeout, bool canReconnect ) {
			// Argument check
			if ( ep == null ) throw new ArgumentNullException ( nameof ( ep ) );
			if ( locEP == null ) throw new InvalidOperationException ( "Not bound" );
			if ( ep is not EPT targInMemEP ) throw new ArgumentException ( $"{nameof ( ep )} is not of type {nameof ( EPT )}" );

			var connRequest = Connector<EPT>.Connect ( this, boundedLLDevice, targInMemEP, Send, canReconnect );
			OpenConnRequests.Add ( targInMemEP, connRequest );
			try {
				var connInfo = connRequest.Wait ( new CancellationTokenSource ( timeout ).Token );
				Connections.Add ( targInMemEP, connInfo );
				OpenConnRequests.Remove ( targInMemEP );
				return connInfo.Connection;
			} catch ( Exception e ) {
				OpenConnRequests.Remove ( targInMemEP );
				throw;
			}
		}
		public void UnregisterConnection ( NetworkConnection connection ) {
			if ( connection == null ) throw new ArgumentNullException ( nameof ( connection ) );
			if ( connection.TargetEP is not EPT targEP ) throw new ArgumentException ( $"{nameof ( connection )} is not of type {nameof ( EPT )}" );
			if ( !Connections.TryGetValue ( targEP, out var connInfo ) ) throw new InvalidOperationException ( $"Connection to {targEP} not found" );
			if ( connInfo.Connection != connection ) throw new InvalidOperationException ( "Connection mismatch" );

			Connections.Remove ( targEP );
			//var msg = NetMessagePacket.CreateSignal ( INetDevice.SignalMsgType.Disconnect, EP, targEP );
			//boundedLLDevice.Send ( msg.Data, targEP );
			connection.Close ( Send );
		}

		private bool Send ( NetMessagePacket msg ) {
			if ( msg == null ) return false;
			if ( msg.TargetEP == null ) return false;
			if ( msg.SourceEP != EP ) return false;
			var res = boundedLLDevice.Send ( (byte[])msg.Data, msg.TargetEP as EPT );
			Waiter ( msg, res );
			return res;
		}

		protected bool ReceiveMsg ( NetMessagePacket msg ) {
			if ( msg == null ) return LogRecvError ( msg, "Given message is null" );
			if ( msg.Data == null ) return LogRecvError ( msg, "Data of given message is null" );
			if ( msg.TargetEP == null ) return LogRecvError ( msg, "TargetEP of given message is null" );
			if ( msg.TargetEP != EP ) return LogRecvError ( msg, "TargetEP of given message is not this device" );
			if ( msg.SourceEP == null ) return LogRecvError ( msg, "SourceEP of given message is null" );
			var remoteEP = msg.SourceEP as EPT;

			NetworkConnection.NetworkInfo connInfo;
			bool canReconnect = msg.SignalType == INetDevice.SignalMsgType.Reconnect;
			if ( msg.SignalType == INetDevice.SignalMsgType.Connect || canReconnect ) {
				// Incoming request for new connection, the devices aren't sure at this point about existance or state of the other
				if ( ConnAccepter == null ) return LogRecvError ( msg, "Not accepting connections" );
				if ( OpenConnRequests.ContainsKey ( remoteEP ) ) return LogRecvError ( msg, "Already waiting for this connection request" );

				if ( Connections.TryGetValue ( remoteEP, out var conn ) ) {
					if ( canReconnect && conn.Connection.TargetEP.Equals ( remoteEP ) && conn.Connection.LocalDevice == this )
						ConnAccepter.Invoke ( conn.Connection, conn.Connection.TargetEP, canReconnect );
					if ( !canReconnect ) return LogRecvError ( msg, "Connection already exists" );
				} else {
					conn = NetworkConnection.Create ( this, remoteEP, Send );
					Connections.Add ( remoteEP, conn );

					// Accept connection on this side
					ConnAccepter.Invoke ( conn.Connection, null, canReconnect );
				}

				// Send confirmation to requester
				var accMsg = NetMessagePacket.CreateSignal ( INetDevice.SignalMsgType.Confirm, EP, msg.SourceEP );
				boundedLLDevice.Send ( (byte[])accMsg.Data, remoteEP );

				return true;

			} else if ( msg.SignalType == INetDevice.SignalMsgType.Confirm ) {
				return false; // This should be handled by Connector, which should have registred own receiver
							  // This method still could be called due to uncertainty in receiver call order. Thus ignore and let other (hopefully Connectors receiver) handle this.

			} else if ( msg.SignalType == INetDevice.SignalMsgType.Disconnect ) {
				if (!Connections.TryGetValue ( remoteEP, out connInfo ) ) return LogRecvError ( msg, "No connection to source" );
				Connections.Remove ( remoteEP );
				connInfo.Connection.Close ( Send );
				return true;

			} else if ( Connections.TryGetValue ( remoteEP, out connInfo ) ) {
				switch ( connInfo.Receiver ( msg ) ) {
				case INetDevice.ProcessResult.Closed: throw new NotImplementedException ();
				case INetDevice.ProcessResult.Confirmed: return true;
				case INetDevice.ProcessResult.Accepted: return true;
				case INetDevice.ProcessResult.Skiped: return LogRecvError ( msg, "Message was skipped by receiver" );
				default: return LogRecvError ( msg, "Unknown ProcessResult" );
				}

			} else return LogRecvError ( msg, "No connection to source" );
		}

		public string GetInfo () {
			return $"(T<{GetType ().Name}> locEP'{locEP}'|boundLL'{boundedLLDevice}')";
			//System.Text.StringBuilder sb = new ();
			//sb.Append ( GetType () );
			//return sb.ToString ();
		}

		public override string ToString () {
			if ( string.IsNullOrEmpty ( DeviceName ) ) return $"NetDevice@{locEP}";
			return $"(NetDevice '{DeviceName}' @{locEP})";
		}
	}
}