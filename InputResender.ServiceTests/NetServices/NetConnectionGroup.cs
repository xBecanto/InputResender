using System;
using System.Collections.Generic;
using System.Threading;
using InputResender.Services;
using FluentAssertions;
using InputResender.Services.NetClientService;
using System.Threading.Tasks;

namespace InputResender.ServiceTests.NetServices;
/// <summary>Manages connections between two groups of devices. Fully connected between groups, no connections within groups. A always requests connections, B always accepts.</summary>
public class NetConnectionGroup {
	protected class Element {
		public INetDevice Device;
		public INetPoint EP;
		public string Name;

		public Element ( string name, INetPoint ep ) {
			(EP = ep).DscName = Name = name;
			Device = NetworkDeviceFactory.CreateDevice ( EP );
			Device.DeviceName = Name;
			Device.Should ().NotBeNull ();
		}
	}

	public const int PacketSize = 4;
	protected Element[] A, B;
	protected CancellationTokenSource CTS;
	protected Dictionary<(INetPoint, INetPoint), NetworkConnection> Conns = new ();

	private static void SendMessage ( NetworkConnection connA, NetworkConnection connB, int info, System.Func<int, int> conv ) {
		byte[] msgData = new byte[PacketSize];
		msgData[0] = (byte)info;
		for ( int i = 1; i < PacketSize; i++ ) msgData[i] = (byte)conv ( i );
		connA.Send ( new HMessageHolder ( HMessageHolder.MsgFlags.None, msgData ) ).Should ().BeTrue ();
		var msgFromA = connB.Receive ( 250 );
		msgFromA.Should ().NotBeNull ();
		msgFromA.Data.InnerMsg.Should ().BeEquivalentTo ( msgData ).And.NotBeSameAs ( msgData );
		msgFromA.Error.Should ().Be ( INetDevice.NetworkError.None );
		msgFromA.SourceEP.Should ().Be ( connA.LocalDevice.EP );
		msgFromA.TargetEP.Should ().Be ( connA.TargetEP );
		msgFromA.SignalType.Should ().Be ( INetDevice.SignalMsgType.None );
		msgFromA.IsFor ( connA.TargetEP ).Should ().BeTrue ();
		msgFromA.IsFor ( connA.LocalDevice.EP ).Should ().BeFalse ();
		msgFromA.IsFrom ( connA.LocalDevice.EP ).Should ().BeTrue ();
		msgFromA.IsFrom ( connA.TargetEP ).Should ().BeFalse ();
	}

	public NetConnectionGroup(string epType, int An, int Bn ) {
		A = new Element[An];
		B = new Element[Bn];
		var EPs = NetworkConnectionHappyFlowTest.GetNetPoints ( epType, An + Bn );
		for ( int i = 0; i < An; i++ ) A[i] = new Element ( $"Sender_A#{i}", EPs[i] );
		for ( int i = 0; i < Bn; i++ ) B[i] = new Element ( $"Receiver_B#{i}", EPs[An + i] );
		CTS = new CancellationTokenSource ();
		foreach ( var b in B ) b.Device.AcceptAsync ( AcceptConn, CTS.Token );
	}

	private void AcceptConn ( NetworkConnection conn, INetPoint oldTarget ) {
		if ( conn == null ) throw new ArgumentNullException ( nameof ( conn ) );
		if ( Conns.ContainsKey ( (conn.LocalDevice.EP, conn.TargetEP) ) ) throw new InvalidOperationException ( "Connection already exists" );
		Conns[(conn.LocalDevice.EP, conn.TargetEP)] = conn;
	}

	public void Connect () {
		foreach ( var a in A ) {
			int connCnt = 0;
			foreach ( var b in B ) {
				var conn = a.Device.Connect ( b.EP, null );
				Conns[(a.EP, b.EP)] = conn;
				connCnt++;
				a.Device.ActiveConnections.Should ().ContainKey ( b.EP ).And.HaveCount ( connCnt );
				a.Device.ActiveConnections[b.EP].Should ().BeSameAs ( conn );
			}
		}

		foreach (var conn in Conns) {
			var rev = (conn.Key.Item2, conn.Key.Item1);
			Conns.Should ().ContainKey ( rev );
		}
	}

	public void SendTo (int info ) {
		foreach ( var a in A ) {
			foreach ( var b in B ) SendMessage ( Conns[(a.EP, b.EP)], Conns[(b.EP, a.EP)], info, (pos) => pos + 1 );
		}
	}
	public void SendFrom (int info ) {
		foreach ( var b in B ) {
			foreach ( var a in A ) SendMessage ( Conns[(b.EP, a.EP)], Conns[(a.EP, b.EP)], info, (pos) => PacketSize - pos );
		}
	}

	public void CloseConnection () {
		Dictionary<NetworkConnection, NetworkCloseWatcher> watchers = new ( ReferenceEqualityComparer.Instance );
		foreach ( var conn in Conns ) watchers[conn.Value] = new NetworkCloseWatcher ( conn.Value );
		foreach ( var a in A) {
			foreach ( var b in B ) {
				CloseConn ( a.Device, b.Device, true );
				CloseConn ( b.Device, a.Device, false );
			}
		}

		void CloseConn (INetDevice Ad, INetDevice Bd, bool callStop) {
			var conn = Conns[(Ad.EP, Bd.EP)];
			if ( callStop ) conn.Close ();
			Task.Delay ( 10 ).Wait ();
			watchers[conn].Assert ();
			conn.LocalDevice.Should ().BeNull ();
			conn.TargetEP.Should ().BeNull ();
			Ad.ActiveConnections.Should ().NotContainKey ( Bd.EP );
			var key = (Ad.EP, Bd.EP);
			Conns[key] = null;
			Conns.Remove ( key );
		}
	}

	public void CloseDevices () {
		CTS.Cancel ();
		CTS.Dispose ();
		foreach ( var a in A ) Close ( a );
		foreach ( var b in B ) Close ( b );

		void Close ( Element e ) {
			e.Device.Close ();
			e.Device.EP.Should ().BeNull ();
			e.Device.ActiveConnections.Should ().BeEmpty ();
			e.Device = null;
			e.EP = null;
		}
	}
}

public class OneToMConnection : NetConnectionGroup {
	public NetworkConnection AtoB => Conns[(A[0].EP, B[0].EP)];
	public INetDevice Sender => A[0].Device;
	public INetPoint SenderEP => A[0].EP;
	public OneToMConnection ( string epType, int M ) : base ( epType, 1, M ) { }
}

public class BidirectionalConnection : NetConnectionGroup {
	public NetworkConnection AtoB => Conns[(A[0].EP, B[0].EP)];
	public NetworkConnection BtoA => Conns[(B[0].EP, A[0].EP)];
	public INetDevice Sender => A[0].Device;
	public INetDevice Receiver => B[0].Device;
	public INetPoint SenderEP => A[0].EP;
	public INetPoint ReceiverEP => B[0].EP;

	public BidirectionalConnection ( string epType ) : base ( epType, 1, 1 ) { }
}