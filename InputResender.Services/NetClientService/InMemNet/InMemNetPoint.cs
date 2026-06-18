using System;
using System.Collections.Generic;

namespace InputResender.Services.NetClientService.InMemNet {
	public class InMemNetPoint : INetPoint {
		private static Dictionary<string, InMemNetPoint> ReservedPoints = new ();
		private static Dictionary<string, InMemNetPoint> BoundedPoints = new ();
		public InMemDevice ListeningDevice { get; private set; }
		public string DscName { get; set; }

		public readonly int ID;
		private readonly int _port;

		/// <inheritdoc />
		public string Address => GetAddress ( ID );
		/// <inheritdoc />
		public int Port => _port;
		/// <inheritdoc />
		public string NetworkAddress => $"IMN#{ID}";
		/// <inheritdoc />
		public int PrefixLength => 24;
		/// <inheritdoc />
		public string FullNetworkPath => GetKey ( ID, _port );

		public List<string> Logbook = [];
		protected void Note (string msg) {
			lock ( Logbook ) Logbook.Add ( msg );
		}

		public bool DoNetworksMatch ( INetPoint other ) => other is InMemNetPoint point && ID == point.ID;

		private InMemNetPoint ( int id, int port, bool reserve = true ) {
			ID = id;
			_port = port;
			if (reserve) {
				lock ( ReservedPoints ) {
					if ( !ReservedPoints.ContainsKey ( GetKey ( id, port ) ) )
						ReservedPoints.Add ( GetKey ( id, port ), this );
					// Maybe it should warn, that other netpoint already reserved this address?
				}
			}
		}

		public static InMemNetPoint CreateNonreserverd ( int id, int port ) {
			if ( id < 0 ) throw new ArgumentOutOfRangeException ( nameof ( id ) );
			if ( port < 1 || port > 65535 ) throw new ArgumentOutOfRangeException ( nameof ( port ) );
			return new InMemNetPoint ( id, port, false );
		}

		public void Bind ( InMemDevice device ) {
			if ( device == null ) throw new ArgumentNullException ( nameof ( device ) );
			if ( ListeningDevice != null ) throw new InvalidOperationException ( $"Already bounded to {ListeningDevice}" );
			ListeningDevice = device;
			BoundedPoints.Add ( GetKey ( ID, _port ), this );
		}
		public void Close (InMemDevice owner) {
			if ( ListeningDevice != owner ) throw new InvalidOperationException ( $"Not bounded to {owner}" );
			if ( ListeningDevice == null ) throw new InvalidOperationException ( $"Not bounded" );
			var key = GetKey ( ID, _port );
			if ( !BoundedPoints.ContainsKey ( key ) ) throw new InvalidOperationException ( $"Not bounded" );
			if ( !BoundedPoints.Remove ( key ) )
				throw new InvalidOperationException ( $"Failed to remove {this}" );
			ListeningDevice = null;
		}

		private static string GetAddress (int id) => $"IMN#{id}";
		private static string GetKey (int id, int port) => $"{GetAddress ( id )}:{port}";
		public override string ToString () => GetKey ( ID, _port ) + (string.IsNullOrWhiteSpace ( DscName ) ? "" : $"({DscName})");
		public static bool TryParse ( string ss, out InMemNetPoint IMEP ) {
			IMEP = null;
			if ( string.IsNullOrWhiteSpace ( ss ) ) return false;
			if ( !ss.StartsWith ( "IMN#" ) ) return false;
			int sep = ss.IndexOf ( ':' );
			if ( sep < 0 ) return false;
			if ( !int.TryParse ( ss[4..sep], out int id ) ) return false;
			if ( !int.TryParse ( ss[(sep + 1)..], out int port ) ) return false;

			if ( BoundedPoints.TryGetValue ( GetKey ( id, port ), out IMEP ) ) return true;
			if ( ReservedPoints.TryGetValue ( GetKey ( id, port ), out IMEP ) ) return true;
			IMEP = new InMemNetPoint ( id, port, false );
			return true;
		}

		public bool SendHere ( byte[] data, InMemNetPoint senderEP ) {
			if ( ListeningDevice == null ) throw new InvalidOperationException ( $"Not listening" );
			if ( ListeningDevice.BoundedInMemDeviceLL == null ) throw new InvalidOperationException ( $"Listening device not bound properly" );
			NetMessagePacket msg = new ( (HMessageHolder)data, this, senderEP );
			Note ( $"Requested to send {data.Length} bytes from {senderEP} to {this}" );
			var status = ListeningDevice.BoundedInMemDeviceLL.ReceiveMsg ( msg );
			switch (status) {
			case INetDevice.ProcessResult.Accepted: return true;
			case INetDevice.ProcessResult.Confirmed: return true;
			case INetDevice.ProcessResult.Closed: return true;
			case INetDevice.ProcessResult.Skiped: return false;
			default: throw new InvalidOperationException ( $"Unknown status {status}" );
			}
		}

		/// <summary>Finds next available InMemNetPoint. That is a combination of <paramref name="id"/> and <paramref name="port"/> that is not yet used by any created InMemNetPoint. The returned InMemNetPoint is reserved and will not be returned by this method again. This method is thread-safe.</summary>
		public static InMemNetPoint NextAvailable ( int id = 873, int port = 1 ) {
			if ( id < 1 ) id = 873;
			if ( port < 1 ) port = 1;
			lock ( ReservedPoints ) {
				while ( ReservedPoints.ContainsKey ( GetKey ( id, port ) ) ) port++;
				return new ( id, port );
			}
		}

		public override bool Equals ( object obj ) => obj is InMemNetPoint point && ID == point.ID && _port == point._port;
		public override int GetHashCode () => HashCode.Combine ( ID, _port );
		public static bool operator == ( InMemNetPoint left, InMemNetPoint right ) => left.Equals ( right );
		public static bool operator != ( InMemNetPoint left, InMemNetPoint right ) => !left.Equals ( right );
	}
}