#define USE_UDP
#if !USE_UDP
#define USE_TCP
#endif

using System;
using System.Collections.Generic;

namespace InputResender.Services {
	public interface INetDevice {
		/// <summary>Name of the device. Used for logging and debugging.</summary>
		public string DeviceName { get; set; }
		public const string ExResponseData = "Response";
		delegate ProcessResult MessageHandler ( NetMessagePacket message );
		public const int SignalMsgSize = 4;
		public enum SignalMsgType {
			/// <summary>Not a signal, just a data message</summary>
			None = 0,
			/// <summary>Notification or command to close underlying device
			Close = 0xF0,
			/// <summary>Incoming connection request</summary>
			Connect = 0xA1,
			/// <summary>Request or notify connection close</summary>
			Disconnect = 0xA2,
			/// <summary>Incoming connection request that allows restoring old connections</summary>
			Reconnect = 0xA3,
			/// <summary>Generic confirmation</summary>
			Confirm = 0x33
		}
		public enum NetworkError { None = 0, Unknown = 1, ConnectionRefused = 2, ConnectionReset = 3, HostUnreachable = 4, TimedOut = 5 }
		public enum ProcessResult { Accepted, Skiped, Closed, Confirmed }
		void Bind ( INetPoint ep );
		void Close ();
		NetworkConnection Connect ( INetPoint ep, MessageHandler recvAct, int timeout = 1000, bool canReconnect = false );
		IReadOnlyDictionary<INetPoint, NetworkConnection> ActiveConnections { get; }
		bool IsConnected ( INetPoint ep );
		void AcceptAsync ( Action<NetworkConnection, INetPoint, bool> callback, System.Threading.CancellationToken ct );
		INetPoint EP { get; }
		void UnregisterConnection ( NetworkConnection connection );
		/// <summary>Checks, if this device is using same resources as <paramref name="device"/></summary>
		/// <returns>True when internal client is shared. False when at least one is null or are different.</returns>
		bool SharedWith ( INetDevice device ) => Equals ( device );
		/// <summary>Raised just before closing the device.</summary>
		event Action<INetDevice, INetPoint> OnClosed;

		public string GetInfo ();

		public string GetLog ();
	}

	public abstract class ANetDeviceLL<T> where T : INetPoint {
		private string LogInfo = "";
		public string GetLog() {
			lock ( LogInfo ) return LogInfo;
		}
		protected void Note ( string msg ) {
			lock ( LogInfo ) LogInfo += msg + "\n";
		}
		public enum ErrorType { None, Unknown, InvalidData, InvalidTarget, Unbound }
		public T LocalEP { get; }
		public bool Send ( byte[] data, T ep ) {
			Note ($"Requested to send {data.Length} bytes to {ep}" );
			var err = InnerSend ( data, ep );
			if ( err != ErrorType.None ) {
				LastError = err;
				return false;
			} else return true;
		}
		private ErrorType LastError = ErrorType.None;
		public ErrorType GetLastError () { var ret = LastError; LastError = ErrorType.None; return ret; }
		protected abstract ErrorType InnerSend ( byte[] data, T ep );


		protected ANetDeviceLL ( T ep, Func<NetMessagePacket, bool> receiver ) {
			if ( ep == null ) throw new ArgumentNullException ( nameof ( ep ) );
			Receivers.Add ( receiver );
			LocalEP = ep;
		}

		private List<Func<NetMessagePacket, bool>> Receivers = new ();
		public event Func<NetMessagePacket, bool> OnReceive {
			add => Receivers.Insert ( 0, value );
			remove => Receivers.Remove ( value );
		}

		protected INetDevice.ProcessResult ReceiveMsg ( NetMessagePacket msg ) {
			Note ( $"Received {msg.Data.Length} bytes from {msg.SourceEP}" );
			foreach ( var receiver in Receivers )
				if ( receiver ( msg ) )
					return INetDevice.ProcessResult.Accepted;
			return INetDevice.ProcessResult.Skiped;
		}

		public override string ToString () => $"{GetType ().Name}({LocalEP}){(LastError != ErrorType.None ? $"[{LastError}]" : "")}";
	}
}