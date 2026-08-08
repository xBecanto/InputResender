using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace MdxLibs.Services.NetClientService {
	public class NetworkFinderService : IEnumerable<NetworkFinderService.Network> {
		public class Node {
			public IPAddress IPAddress;
			//public int ListeningPort;
			public int NetworkID;
			public int TTL;
			public Node ( int netID, int ttl, IPAddress ip ) { IPAddress = ip; NetworkID = netID; TTL = ttl; }
			public override string ToString () => $"{IPAddress}[{NetworkID}#{TTL}]";
		}
		public class Network {
			public Node[] Nodes;
			public string Name;
			public int NetworkID;
			public Network ( int netID, string name, params IPAddress[] ips ) {
				NetworkID = netID;
				Name = name;
				Nodes = new Node[ips.Length];
				for ( int i = 0; i < ips.Length; i++ ) Nodes[i] = new Node ( netID, i, ips[i] );
			}
			public int Count { get => Nodes.Length; }
			public Node LastNode { get => Nodes[Nodes.Length - 1]; }
			public Node this[int id] => Nodes[id.Crop(0, Nodes.Length)];
		}

		public List<INetPoint[]> ToList ( int port ) {
			int N = NetworkCnt;
			List<INetPoint[]> ret = new ( N );
			for ( int i = 0; i < N; i++ ) {
				int C = this[i].Count;
				ret.Add ( new INetPoint[C] );
				for ( int a = 0; a < C; a++ ) {
					ret[i][a] = INetPoint.NextAvailable<IPNetPoint> ( port, this[i][a].IPAddress.ToString () );
					ret[i][a].DscName = $"{this[i].Name}({this[i][a].TTL})";
					//ret[i][a] = new IPNetPoint ( this[i][a].IPAddress, port ) { DscName = $"{this[i].Name}({this[i][a].TTL})" };
				}
			}
			return ret;
		}

		public IPEndPoint[][] GetAllEPs ( int port ) => AddressesToEPs ( FindNetworks (), port );
		protected Network[] networks = null;

		public int NetworkCnt { get { if ( networks == null ) RefreshBuffer (); return networks.Length; } }
		public Network this[int id] { get { if ( networks == null ) RefreshBuffer (); return networks[id.Crop ( 0, networks.Length )]; } }

		//public IPEndPoint EP (int TTL, int network) { Node node = this[network][TTL]; return new IPEndPoint ( node.IPAddress, node.ListeningPort ); }
		public IPEndPoint[][] AddressesToEPs ( Network[] addresses, int port ) {
			int N = addresses.Length;
			IPEndPoint[][] ret = new IPEndPoint[N][];
			for ( int i = 0; i < N; i++ ) {
				int C = addresses[i].Count;
				ret[i] = new IPEndPoint[C];
				for ( int a = 0; a < C; a++ ) {
					ret[i][a] = new IPEndPoint ( addresses[i][a].IPAddress, port );
				}
			}
			return ret;
		}

		/// <summary>WARNING! Only very basic implementation!</summary>
		public void RefreshBuffer () {
			List<Network> ret = new ();
			var interfaces = NetStatsServise.GetAllNetworkInterfaces ();
			foreach ( var inf in interfaces ) {
				var info = inf.GetIPProperties ();
				var unicasts = info.UnicastAddresses;
				IPAddress newAddr = null;
				foreach ( var addr in unicasts ) {
					if ( addr.Address.AddressFamily != AddressFamily.InterNetwork ) continue;
					if ( addr.Address.Equals ( IPAddress.Loopback ) ) continue;
					newAddr = addr.Address;
				}
				if ( newAddr == null ) continue;
				Network network = new Network ( ret.Count, inf.Name, IPAddress.Loopback, newAddr );
				try {
					// I really wonder what this is supposed to do, since only loopback seems to have gateway
					// Maybe it was just copied 1:1 from internet, worked fine so not modified? Who knows...
					var gateways = info.GatewayAddresses;
					if ( gateways.Count > 0 ) ret.Insert ( 0, network );
					else ret.Add ( network );
				} catch { ret.Add ( network ); }
			}
			networks = ret.ToArray ();
		}

		public Network[] FindNetworks () { if (networks == null) RefreshBuffer (); return networks; }

		public Node FindNetwork ( IPAddress addr ) {
			if ( NetworkCnt < 1 ) return null; // Will also refresh network list, if out-of-date
			if ( IPAddress.IsLoopback ( addr ) ) return networks[0][0];
			byte[] abAr = addr.GetAddressBytes ();
			if ( abAr[0] == 192 && abAr[1] == 168 ) {
				for ( int i = 0; i < networks.Length; i++ ) {
					if ( abAr[2] == networks[i][1].IPAddress.GetAddressBytes ()[2] ) return networks[i][1];
				}
			}
			return networks[0].LastNode;
		}

		public IEnumerator<Network> GetEnumerator() {
			int N = NetworkCnt;
			for ( int i = 0; i < N; i++ ) yield return networks[i];
		}
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
