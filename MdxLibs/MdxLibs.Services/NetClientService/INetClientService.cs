using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MdxLibs.Services.NetClientService;
using ClientType = MdxLibs.Services.NetClientService.ClientType;

namespace MdxLibs.Services {
	public class INetClientServiceGroup : IDisposable {
		protected readonly BlockingCollection<MessageResult> PacketBuffer;
		public int Available { get => PacketBuffer.Count; }
		protected readonly CancellationTokenSource cts;
		protected readonly HashSet<INetClientService> clients;
		public bool Listening { get; private set; }
		public int Count { get => clients.Count; }

		public INetClientServiceGroup () {
			PacketBuffer = new ();
			cts = new ();
			clients = new ();
		}
		public void Dispose () {
			Stop ();
			cts.Dispose ();
			//cts = null;
			PacketBuffer.Dispose ();
			//PacketBuffer = null;
			clients.Clear ();
			//clients = null;
		}

		public MessageResult WaitForRecv ( CancellationToken cancelToken ) {
			Listen ();
			return PacketBuffer.Take ( cancelToken );
		}

		public bool Send ( byte[] data, object ep = null, bool attemptDirect = true ) {
			lock ( cts ) {
				foreach ( var client in clients ) {
					if ( client.Send ( data, ep, attemptDirect ) ) return true;
				}
				return false;
			}
		}

		public void Listen () {
			lock ( cts ) {
				if ( PacketBuffer == null ) throw new ObjectDisposedException ( nameof ( INetClientService ) );
				if ( Listening ) return;
				Listening = true;
				foreach ( var client in clients ) client.StartListen ( Callback, cts.Token );
			}
		}
		public void Stop () {
			lock ( cts ) {
				cts.Cancel ();
				foreach ( var client in clients ) client.WaitClose ();
			}
		}
		public bool AddClient ( Func<INetClientService[]> creator ) {
			lock ( cts ) {
				var obj = creator ();
				if ( obj == null ) return false;
				foreach ( var client in obj ) {
					if ( client == null ) continue;
					clients.Add ( client );
					if ( Listening ) client.StartListen ( Callback, cts.Token );
				}
				return true;
			}
		}
		public bool RemoveClient (INetClientService client) {
			lock ( cts ) {
				if ( !clients.Remove ( client ) ) return false;
				client.WaitClose ();
				return true;
			}
		}

		public override string ToString () => $"NetClientGroup ({clients.Count} clients) with {Available} packets.";

		public void Callback ( MessageResult result ) {
			if ( result == null ) return;
			PacketBuffer.Add ( result );
		}
	}

	public interface INetClientService {
		bool Send ( byte[] data, object ep, bool attemptDirect = true, object viaEP = null );
		void StartListen ( Action<MessageResult> callback, CancellationToken cancelToken );
		void WaitClose ( bool lockRequested = true );
		ClientType ServiceType { get; }
		object EP { get; }

		public static INetClientService Create ( ClientType type, IPEndPoint EP ) {
			return type switch {
				ClientType.UDP => new UDPClientService ( EP ),
				_ => null,
			};
		}
	}

	public class UDPClientService : INetClientService {
		public ClientType ServiceType => ClientType.UDP;
		public object EP => new IPEndPoint (localEP.Address, localEP.Port);
		private UdpClient UdpClient;
		private readonly IPEndPoint localEP;
		private readonly IPAddress localNetwork;
		Task recvTask;
		private Action<MessageResult> currentCallback;

		public UDPClientService ( IPEndPoint ep ) {
			localEP = ep;
			localNetwork = ep.Address.GetNetworkAddr ();
			UdpClient = new UdpClient ( localEP );
		}

		public bool Send ( byte[] data, object ep, bool attemptDirect = true, object viaEP = null ) {
			if ( ep is not IPEndPoint ) return false;
			IPEndPoint targ = ep as IPEndPoint;
			if ( attemptDirect && targ.Address.Equals ( localEP.Address ) && targ.Port == localEP.Port ) {
				byte[] dataClone = new byte[data.Length];
				data.CopyTo ( dataClone, 0 );
				MessageResult msg = new ( dataClone, ep, localEP, true );
				lock ( this ) {
					if ( currentCallback != null ) {
						currentCallback ( msg );
						return true;
					}
				}
			}
			var targNet = targ.Address.GetNetworkAddr ();
			if ( !targNet.Equals ( localNetwork ) ) {
				if ( viaEP is not IPEndPoint ) return false;
				IPEndPoint via = viaEP as IPEndPoint;
				targNet = via.Address.GetNetworkAddr ();
				if ( !targNet.Equals ( localNetwork ) ) return false;
			}
			lock ( this ) {
				UdpClient ??= new ( localEP );
				int sent = UdpClient.Send ( data, targ );
				return sent > 0;
			}
		}
		public void StartListen ( Action<MessageResult> callback, CancellationToken cancelToken ) {
			lock ( this ) {
				if ( recvTask != null ) WaitClose ( false );
				UdpClient ??= new ( localEP );
				currentCallback = callback;
				recvTask = Task.Run ( () => RecvTask (), cancelToken );
				cancelToken.Register ( () => WaitClose ( true ) );
			}
		}
		public void WaitClose ( bool lockRequested = true ) {
			if ( lockRequested ) {
				lock ( this ) { Exec (); }
			} else Exec ();
			void Exec () {
				if ( recvTask != null ) {
					UdpClient.Close ();
					recvTask.Wait ();
					recvTask.Dispose ();
					recvTask = null;
					UdpClient.Dispose ();
				}
			}
		}
		private void RecvTask () {
			IPEndPoint distEP = new IPEndPoint ( IPAddress.Any, 0 );
			while ( true ) {
				try {
					var data = UdpClient.Receive ( ref distEP );
					MessageResult result = new ( data, distEP, localEP, distEP == localEP );
					currentCallback ( result );
				} catch ( SocketException ) {
					currentCallback ( new ( MessageResult.Type.Closed, localEP ) );
					return;
				}
			}
		}

		public override string ToString () => $"UDP Client for ({localEP}) - {(recvTask == null ? "closed" : "listening")}";
	}
}