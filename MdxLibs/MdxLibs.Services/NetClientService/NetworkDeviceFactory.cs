using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MdxLibs.Services.NetClientService {
	public static class NetworkDeviceFactory {
		/// <summary>Create net point that can 'listen' on given net point. Will bind to it on creation, if <paramref name="autoBind"/></summary>
		public static INetDevice CreateDevice ( INetPoint ep, NetClientList.LogFcn logger = null ) {
			INetDevice ret;
			if ( ep is InMemNet.InMemNetPoint ) ret = new InMemNet.InMemDevice ();
			else if (ep is IPNetPoint ) ret = new UDPDevice ( logger );
			else throw new NotImplementedException ( "Other types of INetPoint are not implemented yet." );

			ret.Bind ( ep );
			return ret;
		}
	}

	public static class NetworkPointFactory {
		public static INetPoint CreatePoint (string address) {
			throw new NotImplementedException ( "This method of INetPoint creation is not implemented yet." );
			if (address.StartsWith ("IMN#")) {
				//var ret = new InMemNet.InMemNetPoint ();
			}
		}
	}
}
