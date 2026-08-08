using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using System.Text.RegularExpressions;
using MdxLibs.Services.NetClientService.InMemNet;
using MdxLibs.Definitions;
using MdxLibs.Definitions.Commands;
using MdxLibs.Services;
using System;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using InputResender.Definitions.Commands;
using InputResender.Definitions.Networking;
using MdxLibs.Services.NetClientService;

namespace InputResender.UnitTests.IntegrationTests;
public class NetworkConnectionTest {
	protected List<string> Errors = new ();
	protected readonly BaseIntegrationTest sender, receiver;
	protected static readonly Regex InMemNetReg = new ( ".*(IMN#\\d+:\\d+)" );
	protected readonly ITestOutputHelper Output;

	public NetworkConnectionTest (ITestOutputHelper output) {
		Output = output;
		sender = InitTestObj( output, "sender", Errors );
		receiver = InitTestObj( output, "receiver", Errors );
	}

	public static BaseIntegrationTest InitTestObj ( ITestOutputHelper output, string objName, List<string> errorList ) {
		BaseIntegrationTest ret = new ( objName + ":: ", output, BaseIntegrationTest.InitCmdsList ( "core new comp vpacketSender --force", "hook manager start" ) );
		var core = ret.cliWrapper.CmdProc.Owner;
		core.Name = $"{objName} ({core.Name})";
		if ( errorList != null )
			core.OnError += msg => errorList.Add ( $"{objName}:: {msg}" );
		return ret;
	}

	/// <summary>Cmd 'network hostlist' should return at least one INM endpoint, extract and return it</summary>
	public static InMemNetPoint GetEP (BaseIntegrationTest obj) {
		var res = obj.cliWrapper.ProcessLine ( "network hostlist" );
		res.Should ().NotBeNull ();
		res.Message.Should ().NotBeNullOrWhiteSpace ().And.MatchRegex ( InMemNetReg );
		var regexMatch = InMemNetReg.Match ( res.Message );
		regexMatch.Success.Should ().BeTrue ();
		InMemNetPoint.TryParse ( regexMatch.Value, out var ret ).Should ().BeTrue ();
		return ret;
	}

	private void SetNetworkCallback ( BaseIntegrationTest obj, Action<object> onNewConn, DPacketSender.OnReceiveHandler onRecv ) {
		obj.cliWrapper.CmdProc.SetVar ( NetworkCallbacks.NEWCONNCBVarName, onNewConn );
		obj.cliWrapper.CmdProc.SetVar ( NetworkCallbacks.RECVCBVarName, onRecv );
		obj.AssertExec ( "network callback newconn fcn", "Callback set to Fcn." );
		obj.AssertExec ( "network callback recv fcn", "Callback set to Fcn." );
	}

	private NetworkConnection AssertConn (BlockingCollection<object> connList, INetPoint sender, INetPoint receiver) {
		CancellationTokenSource cts = new ( 200 );
		NetworkConnection conn;
		try {
			var tmp = connList.Take ( cts.Token );
			if (tmp is not NetworkConnection)
				throw new Exception ( $"Expected {typeof(NetworkConnection)},\n     got {tmp.GetType ()}" );
			conn = tmp as NetworkConnection;
		} catch ( OperationCanceledException e ) {
			throw new Exception ( $"Timeout waiting for connection{Environment.NewLine}{string.Join ( Environment.NewLine + "  ", Errors )}", e );
		} finally { cts.Dispose (); }
		conn.Should ().NotBeNull ();
		conn.LocalDevice.EP.Should ().Be ( sender );
		conn.TargetEP.Should ().Be ( receiver );
		return conn;
	}

	private byte[] AssertMessage (BlockingCollection<(NetMessagePacket, bool)> recvList, string expMsg, INetPoint from, INetPoint to ) {
		CancellationTokenSource cts = new ( 200 );
		NetworkConnection conn;
		NetMessagePacket msg;
		try {
			(msg, _) = recvList.Take ( cts.Token );
		} catch ( OperationCanceledException e ) {
			throw new Exception ( $"Timeout waiting for message{Environment.NewLine}{string.Join ( Environment.NewLine + "  ", Errors )}", e );
		} finally { cts.Dispose (); }
		msg.Should ().NotBeNull ();
		msg.Data.InnerMsg.Should ().Equal ( Encoding.UTF8.GetBytes ( expMsg ) );
		//msg.Sender.Should ().Be ( from );
		//msg.Target.Should ().Be ( to );
		msg.IsFrom ( from ).Should ().BeTrue ();
		msg.IsFor ( to ).Should ().BeTrue ();
		msg.IsFor ( from ).Should ().BeFalse ();
		msg.IsFrom ( to ).Should ().BeFalse ();
		return msg.Data.InnerMsg;
	}

	[Fact]
	public void HappyFlow () {
		var EPA = GetEP ( sender );
		var EPB = GetEP ( receiver );
		EPA.Should ().NotBeNull ();
		EPB.Should ().NotBeNull ();
		EPA.Should ().NotBeSameAs ( EPB ).And.NotBe ( EPB );
		EPA.DscName = "A";
		EPB.DscName = "B";
		Output.WriteLine ( $"Using EPs: {EPA} and {EPB}" );

		BlockingCollection<object> connsA = new (), connsB = new ();
		BlockingCollection<(NetMessagePacket, bool)> recvA = new (), recvB = new ();
		SetNetworkCallback ( sender,( obj ) => {
			connsA.Add ( obj );
			}, ( data, wasProcessed ) => {
				recvA.Add ( (data, wasProcessed) );
				return DPacketSender.CallbackResult.None;
			}
			);
		SetNetworkCallback ( receiver, ( obj ) => {
			connsB.Add ( obj );
			}, ( data, wasProcessed ) => {
				recvB.Add ( (data, wasProcessed) );
				return DPacketSender.CallbackResult.None;
			}
			);

		// Connect A to B
		// Assert existing connection on both sides
		// Send simple textual message both ways to test the connection
		// Set up callbacks for hooks
		// Send command to simulate keypress

		var statusA = sender.cliWrapper.ProcessLine ( $"network info " + EPA.FullNetworkPath );
		var statusB = receiver.cliWrapper.ProcessLine ( $"network info " + EPB.FullNetworkPath );
		Output.WriteLine ( statusA.Message );
		Output.WriteLine ( statusB.Message );

		sender.AssertExec ( $"target set {EPB.FullNetworkPath}", $"Target set to InMemNet point {EPB}" );
		var A_B = AssertConn ( connsA, EPA, EPB );
		var B_A = AssertConn ( connsB, EPB, EPA );

		sender.AssertExec ( $"conns send {EPB.FullNetworkPath} \"Hello, World!\"", $"Sent 13 bytes to '{EPB}'." );
		AssertMessage ( recvB, "Hello, World!", EPA, EPB );

		sender.AssertExec ( "target set none", "Target disconnected." );

	}
}