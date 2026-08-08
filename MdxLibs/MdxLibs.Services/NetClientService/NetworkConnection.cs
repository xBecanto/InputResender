using System;
using System.Collections.Generic;
using System.Threading;

namespace MdxLibs.Services.NetClientService {
	/// <summary>Main abstraction of the network communication. Represents a device that can send and receive messages between two specific network points.</summary>
	public sealed class NetworkConnection {
		/// <summary>Reference to the target network point. Strongly advised against using this for communication itself. Use <see cref="Send(byte[])"/> instead. Use this for diagnostics and logging.</summary>
		public INetPoint TargetEP { get; private set; }
		/// <summary>Reference to the local network device. Strongly advised against using this for communication itself. Use <see cref="Send(byte[])"/> instead. Use this for diagnostics and logging.</summary>public 
		public INetDevice LocalDevice { get; private set; }
		/// <summary>Event that is triggered when a message is received from the target network point.</summary>
		public event INetDevice.MessageHandler OnReceive;
		/// <summary>Event that is triggered when the connection is closed.</summary>
		public event Action<INetDevice, INetPoint> OnClosed;
		public bool WasClosed { get; private set; }

		/// <summary>Private delegate for sending messages, created by local device that created this connection.</summary>
		public delegate bool MessageSender ( NetMessagePacket data );
		private readonly MessageSender Sender;
		private readonly Queue<NetMessagePacket> watingMessages = new ();
		private volatile bool isClosing = false;

		public struct NetworkInfo {
			public readonly NetworkConnection Connection;
			public readonly INetDevice.MessageHandler Receiver;
			public NetworkInfo ( NetworkConnection conn, INetDevice.MessageHandler recv ) {
				Connection = conn;
				Receiver = recv;
			}
		}

		/// <summary>Basic constructor for creating a connection. This should not be used directly, but rather through <see cref="INetDevice.Connect(INetPoint, INetDevice.MessageHandler, int)"/>.</summary>
		/// <param name="localDevice">Network device that created this connection.</param>
		/// <param name="targetEP">Network point that this connection is connected to.</param>
		/// <param name="senderAct">Action that will be used to send messages to the target network point. Is created by the local device that created this connection.</param>
		/// <returns>Connection instance and receiver delegate. NetDevice that created this connection should use the receiver delegate to pass received messages to this connection.</returns>
		public static NetworkInfo Create ( INetDevice localDevice, INetPoint targetEP, MessageSender senderAct ) {
			NetworkConnection conn = new ( localDevice, targetEP, senderAct );
			return new ( conn, conn.AcceptMessage);
		}

		private NetworkConnection ( INetDevice localDevice, INetPoint targetEP, MessageSender senderAct ) {
			if ( localDevice == null ) throw new ArgumentNullException ( nameof ( localDevice ) );
			if ( targetEP == null ) throw new ArgumentNullException ( nameof ( targetEP ) );
			if ( senderAct == null ) throw new ArgumentNullException ( nameof ( senderAct ) );

			LocalDevice = localDevice;
			TargetEP = targetEP;
			Sender = senderAct;
		}

		private INetDevice.ProcessResult AcceptMessage ( NetMessagePacket message ) {
			if ( !message.IsFor ( LocalDevice.EP ) ) return INetDevice.ProcessResult.Skiped;
			if (message.SignalType == INetDevice.SignalMsgType.Disconnect) {
				Close ();
				return INetDevice.ProcessResult.Confirmed;
			}
			lock ( watingMessages ) {
				watingMessages.Enqueue ( message );
			}
			OnReceive?.Invoke ( message );
			return INetDevice.ProcessResult.Accepted;
		}

		/// <summary>Synchronous non-blocking message receiver. Will return null if there are no messages to receive.</summary>
		/// <param name="timeout">Time in milliseconds to wait for a message. If set to 0, will return immediately. If set to -1, will wait indefinitely.</param>
		public NetMessagePacket Receive ( int timeout = 0 ) {
			/*lock ( watingMessages ) {
				if ( watingMessages.Count < 1 ) return null;
				return watingMessages.Dequeue ();
			}*/
			if ( timeout < 0 ) timeout = int.MaxValue;
			var start = DateTime.Now;
			while ( true ) {
				lock ( watingMessages ) {
					if ( watingMessages.Count > 0 ) return watingMessages.Dequeue ();
				}
				double diff = (DateTime.Now - start).TotalMilliseconds;
				if ( diff > timeout ) return null;
			}
		}

		/// <summary>Instructs local device to send data to the target network point.</summary>
		public bool Send ( HMessageHolder data ) {
			if ( Sender == null ) throw new InvalidOperationException ( "Connection is closed" );
			NetMessagePacket packet = new ( data, TargetEP, LocalDevice.EP );
			return Sender ( packet );
		}

		/// <summary>Instructs local device to send given signal to the target network point.</summary>
		public bool Send ( INetDevice.SignalMsgType signalType ) {
			if ( Sender == null ) throw new InvalidOperationException ( "Connection is closed" );
			NetMessagePacket packet = NetMessagePacket.CreateSignal ( signalType, LocalDevice.EP, TargetEP );
			return Sender ( packet );
		}

		/// <summary>Closes the connection. After this, no more data can be sent or received. This doesn't have to close the underlying device, but closing the device will terminate all of its connections.</summary>
		/// <param name="caller">This parameter is used to prevent infinite recursion. When called by the device, it should pass itself as the caller, otherwise it should be left null.</param>
		public void Close ( MessageSender caller = null ) {
			if ( (TargetEP == null | LocalDevice == null) ) {
				if ( caller == Sender ) return;
				else throw new InvalidOperationException ( "Connection is already closed" );
			}
			if ( isClosing ) return;
			isClosing = true;

			var msg = NetMessagePacket.CreateSignal ( INetDevice.SignalMsgType.Disconnect, LocalDevice.EP, TargetEP );
			Sender ( msg );
			Thread.Sleep ( 1 );
			if ( caller != Sender && LocalDevice.IsConnected ( TargetEP ) )
				LocalDevice.UnregisterConnection ( this );
			if ( OnReceive != null ) {
				foreach ( var handler in OnReceive.GetInvocationList () ) {
					OnReceive -= (INetDevice.MessageHandler)handler;
				}
			}
			//var oldEP = TargetEP;
			//var oldDev = LocalDevice;
			WasClosed = true;
			OnClosed?.Invoke ( LocalDevice, TargetEP );
			TargetEP = null;
			LocalDevice = null;
			isClosing = false;
		}

		public override string ToString () => $"{LocalDevice}-->{TargetEP}";
		/// <summary>Compare if two connections are between same endpoints. Those might still be different instances with different event registrations. To check this consider ReferenceEquals</summary>
		public override bool Equals ( object obj ) => obj is NetworkConnection conn && conn.LocalDevice == LocalDevice && conn.TargetEP == TargetEP;
		public override int GetHashCode () => HashCode.Combine ( LocalDevice, TargetEP );
		/// <inheritdoc cref="Equals(object)"/>
		public static bool operator == ( NetworkConnection left, NetworkConnection right ) => left is null ? right is null : left.Equals ( right );
		public static bool operator != ( NetworkConnection left, NetworkConnection right ) => !(left == right);
	}


	public class NetMessagePacket {
		public readonly HMessageHolder Data;
		public readonly INetPoint TargetEP;
		public readonly INetPoint SourceEP;
		public readonly INetDevice.SignalMsgType SignalType;
		public readonly INetDevice.NetworkError Error;
		public readonly DateTime TimeStamp;

		public NetMessagePacket ( HMessageHolder data, INetPoint targetEP, INetPoint sourceEP ) {
			if ( targetEP == null ) throw new ArgumentNullException ( nameof ( targetEP ) );
			if ( sourceEP == null ) throw new ArgumentNullException ( nameof ( sourceEP ) );
			if ( data == null ) throw new ArgumentNullException ( nameof ( data ) );

			Data = data;
			TargetEP = targetEP;
			SourceEP = sourceEP;
			SignalType = ParseSignalMessage ( data );
			Error = INetDevice.NetworkError.None;
			TimeStamp = DateTime.Now;
		}

		public NetMessagePacket ( INetDevice.NetworkError error, INetPoint targetEP, INetPoint sourceEP ) : this ( null, targetEP, sourceEP ) {
			Error = error;
		}

		static INetDevice.SignalMsgType ParseSignalMessage ( HMessageHolder data ) {
			if ( data == null || data.Length != INetDevice.SignalMsgSize ) return INetDevice.SignalMsgType.None;
			if ( data[0] != 0xAA ) return INetDevice.SignalMsgType.None;
			if ( Enum.TryParse<INetDevice.SignalMsgType> ( data[1].ToString (), out var msgType ) ) return msgType;
			else return INetDevice.SignalMsgType.None;
		}

		public bool IsError => Error != INetDevice.NetworkError.None;
		public bool IsFor ( INetPoint ep, bool exact = true ) => IsFor ( TargetEP, ep, exact );
		public bool IsFrom ( INetPoint ep, bool exact = true ) => IsFor ( ep, SourceEP, exact );

		private static bool IsFor ( INetPoint src, INetPoint dst, bool exact = true ) {
			if ( src.Equals ( dst ) ) return true;
			if ( !exact ) return src.Port < 1;
			return false;
		}

		internal static NetMessagePacket CreateSignal ( INetDevice.SignalMsgType msgType, INetPoint src, INetPoint dst ) {
			if ( msgType == INetDevice.SignalMsgType.None ) return null;
			byte[] msgData = new byte[INetDevice.SignalMsgSize];
			msgData[0] = 0xAA;
			msgData[1] = (byte)msgType;
			for ( int i = 2; i < INetDevice.SignalMsgSize; i++ ) msgData[i] = 0xFF;
			return new ( new HMessageHolder ( HMessageHolder.MsgFlags.None, msgData), dst, src );
		}

		public override string ToString () {
			//System.Text.StringBuilder SB = new ();
			return $"{SourceEP}->{TargetEP}({SignalType}/{Error})@{TimeStamp.ToString ( "m:ss:fff" )}[{Data.Length}] {Data.Span.ToHex ()}";
			//foreach ( var b in Data ) SB.Append ( $" {b:X2}" );
			//return SB.ToString ();
		}
	}

	public static class NetConnectionExtensions {
		private static string InnerNull (string argName, string objType) => $"{argName} of the {objType} is null. Such {objType} should not be even possible to create. This indicates a serious error, both as a missing null-check in {objType} constructor and bad constructor call somewhere else.";
		private static string InnerMismatch (string argName, string objType, string expectedType) => $"{argName} of the {objType} is not of expected type {expectedType}. This can indicate mismatch between the expected and actual {argName.ToLower ()} formats.";

		public static T ValidSender<T> ( this NetMessagePacket packet ) where T : INetPoint {
			if ( packet == null )
				throw new ArgumentNullException ( nameof ( packet ) );

			if ( packet.SourceEP == null )
				throw new InvalidOperationException ( InnerNull ( nameof ( packet.SourceEP ), nameof ( packet ) ) );

			if (packet.SourceEP is not T sender)
				throw new InvalidOperationException ( InnerMismatch ( nameof ( packet.SourceEP ), nameof ( packet ), typeof ( T ).Name ) );

			return sender;
		}
		public static T ValidReceiver<T> ( this NetMessagePacket packet ) where T : INetPoint {
			if ( packet == null )
				throw new ArgumentNullException ( nameof ( packet ) );

			if ( packet.TargetEP == null )
				throw new InvalidOperationException ( InnerNull ( nameof ( packet.TargetEP ), nameof ( packet ) ) );

			if (packet.TargetEP is not T receiver)
				throw new InvalidOperationException ( InnerMismatch ( nameof ( packet.TargetEP ), nameof ( packet ), typeof ( T ).Name ) );
			return receiver;
		}

		public static T ValidTarget<T> (this NetworkConnection conn) where T : INetPoint {
			if ( conn == null )
				throw new ArgumentNullException ( nameof ( conn ) );
			if ( conn.TargetEP == null )
				throw new InvalidOperationException ( InnerNull ( nameof ( conn.TargetEP ), nameof ( conn ) ) );
			if (conn.TargetEP is not T target)
				throw new InvalidOperationException ( InnerMismatch ( nameof ( conn.TargetEP ), nameof ( conn ), typeof ( T ).Name ) );
			return target;
		}
		public static T ValidSource<T> (this NetworkConnection conn) where T : INetDevice {
			if ( conn == null )
				throw new ArgumentNullException ( nameof ( conn ) );
			if ( conn.LocalDevice == null )
				throw new InvalidOperationException ( InnerNull ( nameof ( conn.LocalDevice ), nameof ( conn ) ) );
			if ( conn.LocalDevice is not T target )
				throw new InvalidOperationException ( InnerMismatch ( nameof ( conn.LocalDevice ), nameof ( conn ), typeof ( T ).Name ) );
			return target;
		}
	}
}