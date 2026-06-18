using System;
using System.Collections.Generic;
using System.Threading;

namespace InputResender.Services.NetClientService {
	internal sealed class Connector<EPT> where EPT : class, INetPoint {
		public readonly ANetDevice<EPT> Device;
		public readonly ANetDeviceLL<EPT> DeviceLL;
		public readonly EPT Target;
		private readonly ManualResetEvent Signal;
		public readonly NetworkConnection.MessageSender Sender;
		public readonly INetDevice.SignalMsgType SignalType;
		public NetworkConnection.NetworkInfo Connection { get; private set; }

		public Connector ( ANetDevice<EPT> dev, ANetDeviceLL<EPT> devLL, EPT targ, NetworkConnection.MessageSender sender, bool canReconnect = false ) {
			Device = dev;
			DeviceLL = devLL;
			Target = targ;
			Sender = sender;
			Signal = new ManualResetEvent ( false );
			SignalType = canReconnect ? INetDevice.SignalMsgType.Reconnect : INetDevice.SignalMsgType.Connect;
		}
		private NetMessagePacket CreateConnRequestSignal () => NetMessagePacket.CreateSignal ( SignalType, Device.EP, Target );

		private void Notify ( NetworkConnection.NetworkInfo conn ) {
			if ( Connection.Connection != null ) throw new InvalidOperationException ( "Already connected" );
			Connection = conn;
			Interlocked.MemoryBarrierProcessWide ();
			Signal.Set ();
		}
		public NetworkConnection.NetworkInfo Wait ( CancellationToken? ct ) {
			var packet = CreateConnRequestSignal ();

			DeviceLL.OnReceive += OnReceive;
			DeviceLL.Send ( (byte[])packet.Data, packet.TargetEP as EPT );

			int res;
			if ( ct == null ) res = Signal.WaitOne ( TimeSpan.FromMinutes ( 10 ) ) ? 0 : 1;
			else res = WaitHandle.WaitAny ( new WaitHandle[] { Signal, ct.Value.WaitHandle } );
			DeviceLL.OnReceive -= OnReceive;

			if ( res == 1 ) {
				lock (ActiveAttempts) {
					// Any message won't be received now and needs to be removed manually to allow another attempt
					ActiveAttempts.Remove ( Target );
				}
				throw new OperationCanceledException ( $"Connection request to {Target} timed out ({(ct == null ? "no success notification" : "cancelled")})" );
			}
			Signal.Dispose ();
			return Connection;
		}

		private static Dictionary<EPT, Connector<EPT>> ActiveAttempts = new ();

		public static Connector<EPT> Connect ( ANetDevice<EPT> dev, ANetDeviceLL<EPT> devLL, EPT targ, NetworkConnection.MessageSender sender, bool canReconnect = false ) {
			if ( dev == null ) throw new ArgumentNullException ( nameof ( dev ) );
			if ( targ == null ) throw new ArgumentNullException ( nameof ( targ ) );
			if ( dev.EP == null ) throw new InvalidOperationException ( "Device not bound" );
			if ( dev.EP != devLL.LocalEP ) throw new InvalidOperationException ( "Device and LL device are not bound to same EP" );

			Connector<EPT> req = new ( dev, devLL, targ, sender, canReconnect );
			lock ( ActiveAttempts ) {
				if ( ActiveAttempts.ContainsKey ( targ ) ) throw new InvalidOperationException ( "Already connecting" );
				ActiveAttempts.Add ( targ, req );
				return req;
			}
		}

		private static bool OnReceive ( NetMessagePacket msg ) {
			Connector<EPT> req;
			if ( msg == null ) return false;
			if ( msg.SignalType != INetDevice.SignalMsgType.Confirm ) return false;
			if ( msg.SourceEP is not EPT srcEP ) return false;

			lock ( ActiveAttempts ) {
				if ( !ActiveAttempts.TryGetValue ( srcEP, out req ) ) return false;
				ActiveAttempts.Remove ( srcEP );
			}

			// Create connection
			var conn = NetworkConnection.Create ( req.Device, msg.SourceEP, req.Sender );
			req.Notify ( conn );
			return true;
		}
	}
}