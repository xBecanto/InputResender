using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MdxLibs.Services.NetClientService {
	public class UDPDevice : ANetDevice<IPNetPoint> {
		public UDPDeviceLL LLDevice { get; private set; }
		protected override ANetDeviceLL<IPNetPoint> boundedLLDevice => LLDevice;
		private readonly NetClientList.LogFcn Logger;

		public UDPDevice ( NetClientList.LogFcn logger ) { Logger = logger; }

		protected override void BindLL ( IPNetPoint ep ) {
			if ( LLDevice != null ) throw new InvalidOperationException ( "Already bound" );
			LLDevice = new UDPDeviceLL ( ep, ReceiveMsg, Logger );
		}

		protected override void InnerClose () {
			if ( LLDevice == null ) return;
			LLDevice.Close ();
			LLDevice = null;
		}

		override protected void Waiter ( NetMessagePacket msg, bool sentStatus ) {
			if (sentStatus && msg.SignalType == INetDevice.SignalMsgType.Disconnect) {
				Thread.Sleep ( 1 );
			}
		}
	}

	public class UDPDeviceLL : ANetDeviceLL<IPNetPoint> {
		private UdpClient Client;
		private Task RecvTask;
		private readonly NetClientList.LogFcn Logger;

		public UDPDeviceLL ( IPNetPoint ep, Func<NetMessagePacket, bool> receiver, NetClientList.LogFcn logger ) : base ( ep, receiver ) {
			Logger = logger;
			var llEP = ep.LowLevelEP ();
			try {
				Client = new UdpClient ( llEP );
			} catch ( Exception e ) {
				throw new Exception ( $"Problem starting UDPClient on {llEP}: {e.Message}", e );
			}
			RecvTask = Task.Run ( RecvLoop );
			Task.Delay ( 1 ).Wait ();
		}

		private void RecvLoop () {
			IPEndPoint? remoteEP = new ( IPAddress.Any, 0 );
			byte[] data;
			while ( true ) {
				try {
					data = Client.Receive ( ref remoteEP );
				} catch ( SocketException e ) {
					if ( e.SocketErrorCode == SocketError.Interrupted ) break;
					else throw;
				} catch ( Exception e ) {
					Logger?.Invoke ( null, $"Error receiving on {this}: {e.Message}" );
					break;
				}
				if ( data == null ) break;
				try {
					var msg = new NetMessagePacket ( (HMessageHolder)data, LocalEP, new IPNetPoint ( remoteEP ) );
					var status = ReceiveMsg ( msg );
					Logger?.Invoke ( msg, $"Received on {this}. ({status})" );
				} catch ( Exception e ) {
					Logger?.Invoke ( null, $"Error receiving on {this}: {e.Message}" );
				}
			}
		}

		protected override ErrorType InnerSend ( byte[] data, IPNetPoint ep ) {
			if ( Client == null ) throw new InvalidOperationException ( "Client is closed" );
			if ( ep == null ) throw new ArgumentNullException ( nameof ( ep ) );
			if ( data == null ) throw new ArgumentNullException ( nameof ( data ) );
			if ( ep is not IPNetPoint ) throw new ArgumentException ( $"{nameof ( ep )} is not of type {nameof ( IPNetPoint )}" );
			//OnMessage?.Invoke ( $"Sent on {this}: {new NetMessagePacket ( data, LocalEP, ep )}" );
			int sent = Client.Send ( data, data.Length, ep.LowLevelEP () );
			//Thread.Sleep ( 1 );
			return sent == data.Length ? ErrorType.None : ErrorType.Unknown;
		}

		public void Close () {
			Client.Close ();
			Client.Dispose ();
			Client = null;
			RecvTask.Wait ();
			RecvTask.Dispose ();
			RecvTask = null;
		}
	}
}