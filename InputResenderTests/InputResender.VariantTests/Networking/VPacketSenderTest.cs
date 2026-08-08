using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentAssertions;
using InputResender.DefinitionTests.Networking;
using InputResender.Variants.Networking;
using MdxLibs.Services.NetClientService;
using MdxLibs.Services.NetClientService.InMemNet;
using Xunit.Abstractions;

namespace InputResender.VariantTests.Networking;
public class VPacketSenderTest : DPacketSenderTest<VPacketSender> {
	public VPacketSenderTest ( ITestOutputHelper outputHelper ) : base ( outputHelper ) { }
	public override VPacketSender GenerateTestObject () => new VPacketSender ( OwnerCore );

	protected override bool IsErrorCritical ( string msg, Exception e, VPacketSender Aobj, object AEP, VPacketSender Bobj, object BEP ) {
		if ( msg.StartsWith ( "Failed to add " )
			&& msg.EndsWith ( " as a valid local EP" )
			&& !msg.Contains ( BEP.ToString () )
			&& !msg.Contains ( AEP.ToString () )
			) return false;
		return true;
	}

	protected override IEnumerable<INetPoint> GetLocalPoint ( VPacketSender testObj, int port ) {
		List<INetPoint> allEPs = new ();
		foreach ( var network in testObj.EPList )
			foreach ( var ep in network )
				if ( !allEPs.Contains ( ep ) ) allEPs.Add ( ep );


		InMemNetPoint IMEP = allEPs.OfType<InMemNetPoint> ().FirstOrDefault ( ep => true, null );
		IMEP.Should ().NotBeNull ();
		yield return IMEP;

		Output?.WriteLine ( $"All loopback devices: {string.Join ( ", ", allEPs.OfType<IPNetPoint> ().Where ( ep => ep.LowLevelEP ().Address.Equals ( IPAddress.Loopback ) ) )}" );
		IPNetPoint localhostEP = allEPs.OfType<IPNetPoint> ().FirstOrDefault ( ep => ep.LowLevelEP ().Address.Equals ( IPAddress.Loopback ), null );
		localhostEP.Should ().NotBeNull ();
		yield return localhostEP;

		Output?.WriteLine ( $"All local devices: {string.Join ( ", ", allEPs.OfType<IPNetPoint> ().Where ( ep => IsLocal ( ep.LowLevelEP () ) ) )}" );
		IPNetPoint privateNetEP = allEPs.OfType<IPNetPoint> ().FirstOrDefault ( ep => IsLocal ( ep.LowLevelEP () ), null );
		privateNetEP.Should ().NotBeNull ();
		yield return privateNetEP;
	}

	private static bool IsLocal ( IPEndPoint ep ) {
		byte[] bytes = ep.Address.GetAddressBytes ();
		if ( bytes.Length == 4 ) {
			if ( bytes[0] == 10 ) return true;
			if ( bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31 ) return true;
			if ( bytes[0] == 192 && bytes[1] == 168 ) return true;
			if ( bytes[0] == 169 && bytes[1] == 254 ) return true;
			return false;
		} else throw new System.NotImplementedException ();
	}
}