using System.Collections.Generic;
using System.Threading;
using System;
using InputResender.Services.NetClientService;
using System.Xml;
using InputResender.Services.NetClientService.InMemNet;
using System.Net;
using System.Linq;

/*
Allows communication over multiple networks seamlessly, each with different NetworkDevice and specific NetPoint, not necessarily of different type.
Example scenario:
- 1 InMemNetDevice for communication within the same process
- 1 NamedPipeNetDevice for communication within the same machine (not implemented, example only, might just use localhost)
- UDPNetDevice on 192.168.50.1 for communication with VirtualBox VMs
- UDPNetDevice and TCPNetDevice on 192.168.1.1 for communication with local network
- TCPNetDevice on 213.194.245.251 for hosting public server
- BluetoothNetDevice for communication with Bluetooth devices (might be avoided. Only BT device in interest is TapStrap, which can offers API, futher research needed)
*/

namespace InputResender.Services;
public class NetClientList {
	private Dictionary<INetPoint, INetDevice> Devices;
	private Dictionary<INetPoint, NetworkConnection> Conns;
	public IReadOnlyDictionary<INetPoint, INetDevice> OwnedDevices;
	public IReadOnlyDictionary<INetPoint, NetworkConnection> Connections;
	/// <summary>Set of valid local EPs that this device can be bound to</summary>
	//private HashSet<INetPoint> AvailablePoints;
	//public IReadOnlySet<INetPoint> AvailableEPs => Devices.Keys.ToHashSet ();
	private CancellationTokenSource AccepterCT;
	private Action<NetworkConnection, INetPoint> ConnAcceptCallback;

	public NetClientList () {
		Devices = new ();
		Conns = new ();
		//AvailablePoints = new ();
		OwnedDevices = Devices.AsReadOnly ();
		Connections = Conns.AsReadOnly ();
	}

	public delegate void LogFcn ( NetMessagePacket packet, string msg );
	public event LogFcn OnLog;
	private void Log ( NetMessagePacket packet, string msg ) => OnLog?.Invoke ( packet, msg );

	public void AddEP (INetPoint ep) {
		if ( ep == null ) throw new ArgumentNullException ( nameof ( ep ) );
		if ( Devices.ContainsKey ( ep ) ) throw new InvalidOperationException ( $"EP {ep} already added" );
		
		var locDev = NetworkDeviceFactory.CreateDevice ( ep, Log );
		if ( AccepterCT != null ) locDev.AcceptAsync ( ConnAccepter, AccepterCT.Token );
		Devices.Add ( ep, locDev );
	}
	public void RemoveEP ( INetPoint ep ) {
		if ( ep == null ) throw new ArgumentNullException ( nameof ( ep ) );
		if ( !Devices.ContainsKey ( ep ) ) throw new InvalidOperationException ( $"EP {ep} not found" );
		Devices[ep].Close ();
		Devices.Remove ( ep );
	}

	// Even though we probably want to accept rigth from start, this will allow to change the connection accepting callback during runtime
	public CancellationTokenSource AcceptAcync ( Action<NetworkConnection, INetPoint> callback ) {
		// Start accepting new connections. When callback is null, the caller doesn't want to be notified about new connections (probably is guided by other events and checking the Connections dictionary is enough)
		// To stop accepting, call Cancel on the returned CancellationTokenSource

		if ( AccepterCT != null) {
			// Cancel previous accepting. It's easier to just stop previous one and start new than reassign all callbacks for this AND the mid/low-level devices
			AccepterCT.Cancel ();
			AccepterCT = null;
		}

		AccepterCT = new ();
		foreach (var dev in Devices ) dev.Value.AcceptAsync ( ConnAccepter, AccepterCT.Token );
		AccepterCT.Token.Register ( () => ConnAcceptCallback = null );
		ConnAcceptCallback = callback;
		return AccepterCT;
	}

	public List<INetPoint> SelectProperLocalPoints ( INetPoint target ) {
		// Select one of the provided addresses from which a connection to target could be established
		// Currently, only selection whitin the same network is supported

		if ( target == null ) throw new ArgumentNullException ( nameof ( target ) );
		var netAddr = target.NetworkAddress;
		List<INetPoint> validEPs = [];
		foreach ( var EP in Devices.Keys ) {
			if ( target.InSameNetwork ( EP ) ) validEPs.Add ( EP );
		}

		return validEPs;
	}

	private void ConnAccepter ( NetworkConnection conn, INetPoint oldTarget, bool reconnectRequest = false ) {
		ArgumentNullException.ThrowIfNull ( conn );
		if ( Conns.ContainsKey ( conn.TargetEP ) ) {
			if ( reconnectRequest )
				return; // Connection already exists on our side, no data are changed (currently)
			else
				throw new InvalidOperationException ( $"Connection to {conn.TargetEP} already exists" );
		}

		Conns.Add ( conn.TargetEP, conn );
		ConnAcceptCallback?.Invoke ( conn, oldTarget );
		conn.OnClosed += ( _, ep ) => Conns.Remove ( ep );
	}

	public NetworkConnection Connect ( INetPoint targEP, int timeout = 1000, bool canReconnect = false ) {
		if ( targEP == null ) throw new ArgumentNullException ( nameof ( targEP ) );
		if ( Conns.ContainsKey ( targEP ) ) throw new InvalidOperationException ( $"Connection to {targEP} already exists" );

		List<INetPoint> locEPs = SelectProperLocalPoints ( targEP );
		if ( locEPs?.Count == 0 ) throw new InvalidOperationException ( $"No local point available for {targEP}\nAvailable are: {string.Join ( " | ", Devices.Keys )}" );

		foreach ( var VARIABLE in locEPs) {
			 // Try to connect from each of the valid local EPs until one succeeds. This is needed, because even if there are multiple valid local EPs, some of them might be already used for other connections, or just not working for some reason
			 try {
				 var conn = Devices[VARIABLE].Connect ( targEP, null, timeout, canReconnect );
				 ConnAccepter ( conn, null );
				 return conn;
			 } catch ( Exception ) {
				 continue;
			 }
		}
		throw new InvalidOperationException ( $"Failed to connect to {targEP} from any of the valid local EPs: {string.Join ( " | ", locEPs )}" );
	}
	public void UnregisterConnection ( NetworkConnection connection ) {
		if ( connection == null ) throw new ArgumentNullException ( nameof ( connection ) );
		if ( !Conns.ContainsValue ( connection ) ) throw new InvalidOperationException ( $"Connection {connection} not found" );
		connection.Close ();
		Conns.Remove ( connection.TargetEP );
	}

	public void Close () {
		foreach ( var conn in Conns ) conn.Value.Close ();
		foreach ( var dev in Devices ) dev.Value.Close ();
		Conns.Clear ();
		Devices.Clear ();
	}

	public static string Serialize ( INetPoint[] EPs ) {
		XmlDocument doc = new ();
		XmlElement root = doc.CreateElement ( "NetClientList" );
		doc.AppendChild ( root );
		foreach ( var dev in EPs ) {
			XmlElement devElem;
			if ( dev is InMemNetPoint inMemEP ) {
				devElem = doc.CreateElement ( "InMemNetDevice" );
				devElem.SetAttribute ( "ID", inMemEP.ID.ToString () );
				devElem.SetAttribute ( "Port", inMemEP.Port.ToString () );
			} else if ( dev is IPNetPoint ipEP ) {
				devElem = doc.CreateElement ( "IPNetDevice" );
				var llEP = ipEP.LowLevelEP ();
				devElem.SetAttribute ( "Address", llEP.Address.ToString () );
				devElem.SetAttribute ( "Port", llEP.Port.ToString () );
			} else continue; // Not recognized EP type

			root.AppendChild ( devElem );
		}
		return doc.OuterXml;
	}
	public static INetPoint[] Deserialize ( string xml ) {
		XmlDocument doc = new ();
		doc.LoadXml ( xml );
		XmlNode root = doc.SelectSingleNode ( "NetClientList" );
		if ( root == null ) throw new XmlException ( "Root element not found" );

		List<INetPoint> EPs = new ();
		foreach ( XmlNode dev in root.ChildNodes ) {
			if ( dev.Name == "InMemNetDevice" ) {
				if ( !int.TryParse ( dev.Attributes["ID"].Value, out int ID ) ) throw new XmlException ( "ID not found" );
				if ( !int.TryParse ( dev.Attributes["Port"].Value, out int Port ) ) throw new XmlException ( "Port not found" );
				EPs.Add ( InMemNetPoint.CreateNonreserverd ( ID, Port ) );
			} else if ( dev.Name == "IPNetDevice" ) {
				if ( !IPAddress.TryParse ( dev.Attributes["Address"].Value, out IPAddress Address ) ) throw new XmlException ( "Address not found" );
				if ( !int.TryParse ( dev.Attributes["Port"].Value, out int Port ) ) throw new XmlException ( "Port not found" );
				EPs.Add ( new IPNetPoint ( Address, Port ) );
			}
		}
		return EPs.ToArray ();
	}
}