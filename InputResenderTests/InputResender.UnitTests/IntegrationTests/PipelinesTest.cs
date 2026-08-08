using MdxLibs.Definitions;
using Xunit;
using System;
using Xunit.Abstractions;
using System.Threading;
using System.Collections.Generic;
using FluentAssertions;
using MdxLibs.Services.NetClientService;
using System.Diagnostics;
using InputResender.Definitions;
using InputResender.Definitions.InputProcessing;
using InputResender.Definitions.Networking;
using InputResender.Services;
using InputResender.Variants;
using InputResender.Variants.Commands;
using InputResender.Windows.Input;
using MdxLibs.Core;

namespace InputResender.UnitTests.IntegrationTests;
[Collection ( "WinInputTests" )]
public class PipelinesTest {
	// This test is alternative to NetworkConnectionTest, using core pipelines to connect sending data and simulating input (basically content of tests NetworkConnectionTest and InputSimulationTest)

	protected readonly BaseIntegrationTest sender, receiver;
	protected readonly ITestOutputHelper Output;

	public PipelinesTest ( ITestOutputHelper output ) {
		Output = output;
		sender = NetworkConnectionTest.InitTestObj ( output, "sender", null );
		receiver = NetworkConnectionTest.InitTestObj ( output, "receiver", null );
		var senderCore = sender.cliWrapper.CmdProc.GetVar<DInputResenderCore> ( CoreManagerCommand.ActiveCoreVarName );
		var receiverCore = receiver.cliWrapper.CmdProc.GetVar<DInputResenderCore> ( CoreManagerCommand.ActiveCoreVarName );
		sender.cliWrapper.ProcessLine ( "core new comp vinputsimulator" );
		receiver.cliWrapper.ProcessLine ( "core new comp vinputsimulator" );
		sender.cliWrapper.ProcessLine ( "core new comp vpacketsender --force" );
		receiver.cliWrapper.ProcessLine ( "core new comp vpacketsender --force" );
		senderCore.OnMessage += SenderPrintMsg;
		receiverCore.OnMessage += ReceiverPrintMsg;
		senderCore.LogFcn = SenderLogMsg;
		receiverCore.LogFcn = ReceiverLogMsg;
		DInputResenderCoreFactory.AddJoiners ( senderCore );
		DInputResenderCoreFactory.AddJoiners ( receiverCore );
		sender.cliWrapper.ProcessLine ( "password add testPassword" );
		receiver.cliWrapper.ProcessLine ( "password add testPassword" );
		sender.cliWrapper.ProcessLine ( "windows load --force" );
		receiver.cliWrapper.ProcessLine ( "windows load --force" );
		sender.cliWrapper.ProcessLine ( "windows msgs start" );
		receiver.cliWrapper.ProcessLine ( "windows msgs start" );
	}

	private void SenderPrintMsg (string s) {
		Output.WriteLine ( $"SENDER: {s}" );
	}
	private void SenderLogMsg ( string s ) {
		Output.WriteLine ( $"SENDER (log): {s}" );
	}
	private void ReceiverPrintMsg ( string s ) {
		Output.WriteLine ( $"RECEIVER: {s}" );
	}
	private void ReceiverLogMsg (string s) {
		Output.WriteLine ( $"RECEIVER (log): {s}" );
	}

	[Fact]
	public void Process_Send_Simulate_Pipeline () {
		var joiner = FetchJoiner ( sender );
		joiner.RegisterJoiner ( typeof ( DInputProcessor ), typeof ( DDataSigner ),
			"Overloaded InputProcessor<InputData> to DataSigner (for testing)", ( j, obj ) => {
			// This joiner will change keyDown to keyUp and vise versa
			// This is to 1) illustrate the option to overwrite joiner with some logic
			//   and 2) to separate hooks for sender and receiver in this test
			string objType = obj.GetType ().Name + " - " + obj.GetType ().FullName;
			if ( obj is not InputData data ) return (false, null);

			var input = obj as InputData;
			if ( input.Key != KeyCode.P ) return (false, null);
			if ( input.X < 1 ) return (false, null); // Ignore releasing button
			//input.X = 0;
			input.Cmnd = InputData.Command.KeyRelease;
			var signer = j.Owner.Fetch<DDataSigner> ();
			if ( signer == null ) return (false, null);
			byte[] bin = input.Serialize ();
			HMessageHolder msg = new ( HMessageHolder.MsgFlags.None, bin );
			return (true, signer.Encrypt ( msg ));
		}, true );

		RegisterPipeline ( sender, typeof ( DInputProcessor ), typeof ( DDataSigner ), typeof ( DPacketSender ) );
		RegisterPipeline ( receiver, typeof ( DPacketSender ), typeof ( DDataSigner ), typeof ( DInputSimulator ), typeof ( DInputSimulator ) );
		RegisterPipeline ( sender, typeof ( DInputReader ), typeof ( DInputMerger ), typeof ( DInputProcessor ) );
		receiver.Core.Fetch<DInputSimulator> ().AllowRecapture = true;

		// Set up sender hook to fire the sending pipeline (hook needs only to be created,p
		//    only the return value is important here (i.e. should the event be consumed or passed on).
		//    The pipeline should be triggered automatically when processing the input event)
		List<HInputEventDataHolder> sentCaptures = [];
		sender.cliWrapper.CmdProc.SetVar ( HookManagerCommand.INPHOOKCBVarName, ( HInputEventDataHolder data ) => {
			lock ( sentCaptures ) {
				sentCaptures.Add ( data );
			}
			return false;
		} );
		sender.AssertExecByRegex ( "hook add Delayed -c Fcn KeyDown"
			, InputSimulationTest.HookAddRegex ( "KeyDown", DHookManager.CBType.Delayed, "Fcn" )
			, "No hooks added." );

		var EPA = NetworkConnectionTest.GetEP ( sender );
		var EPB = NetworkConnectionTest.GetEP ( receiver );
		// Registering OnCallback for receiver shouldn't be needed since pipeline should handle it
		EPA.DscName = "A"; EPB.DscName = "B";
		sender.cliWrapper.ProcessLine ( $"target set {EPB.FullNetworkPath}" );

		// Set up receiver hook to check if input is received
		AutoResetEvent receiverHookWaiter = new ( false );
		List<(HInputEventDataHolder, StackTrace)> stackTrace = [];

		List<HInputEventDataHolder> receiverHookCaptures = [];
		receiver.cliWrapper.CmdProc.SetVar(HookManagerCommand.INPHOOKCBVarName, (HInputEventDataHolder data) => {
			lock (stackTrace) {
				receiverHookCaptures.Add ( data );
				receiverHookWaiter.Set ();
				stackTrace.Add ( (data, new StackTrace ()) );
			}
			return false;
		} );
		receiver.AssertExecByRegex ( "hook add delayed -c Fcn KeyUp", InputSimulationTest.HookAddRegex ( "KeyUp", DHookManager.CBType.Delayed, "Fcn" ), "No hooks added." );
		BaseIntegrationTest.ConsumeMessages ();
		lock ( VWinLowLevelLibs.EventList )
			VWinLowLevelLibs.EventList.Clear ();

		var senderLLLibs = sender.Core.Fetch<VWinLowLevelLibs> () ?? new VWinLowLevelLibs ( sender.Core );
		var receiverLLLibs = receiver.Core.Fetch<VWinLowLevelLibs> () ?? new VWinLowLevelLibs ( receiver.Core );

		var senderProbe = senderLLLibs.InstallProbe ( false, [VKChange.KeyDown, VKChange.KeyUp], KeyCode.P );
		var receiverProbe = receiverLLLibs.InstallProbe ( false, [VKChange.KeyDown, VKChange.KeyUp], KeyCode.P );

		sender.AssertExec ( "sim keydown P", "Sent 1 key down events." );
		List<(DateTime, int)> log = [];
		try {
			int timeout = System.Diagnostics.Debugger.IsAttached ? 300 * 1000 : 800;
			BaseIntegrationTest.ActiveWait ( timeout, 50, () => {
				log.Add ( (DateTime.Now, receiverHookCaptures.Count) );
				return receiverHookCaptures.Count > 0;
			} ).Should ().BeTrue ();
			receiverHookCaptures.Should ().HaveCount ( 1 );
			InputSimulationTest.AssertKeyEvent ( receiverHookCaptures[0], KeyCode.P, false );
		} catch ( Exception ex ) {
			Output.WriteLine ( $"Active wait log:" );
			foreach ( var (time, cnt) in log )
				Output.WriteLine ( $" . {time} := {cnt}" );
			Output.WriteLine ( "... error list ..." );
			var llInput = receiver.Core.Fetch<DLowLevelInput> ();
			llInput.PrintErrors ( Output.WriteLine );

			Output.WriteLine ( " ... capture log ..." );
			foreach ( var ev in VWinLowLevelLibs.EventList )
				Output.WriteLine ( $"{ev.Item1} {ev.Item2} {ev.Item3}" );
			Output.WriteLine ( " ... sender probe raw log ..." );
			foreach ( var ev in senderProbe.RawEvents )
				Output.WriteLine ( $"nCode: {ev.Item1}, wParam: {ev.Item2}, lParam: {ev.Item3}, eID: {ev.Item4}" );
			foreach ( var ev in senderProbe.Events )
				Output.WriteLine ( $"Consumed: {ev.Item1}, Change: {ev.Item2}, Key: {ev.Item3}, eID: {ev.Item4}" );
			Output.WriteLine ( " ... receiver probe raw log ..." );
			foreach ( var ev in receiverProbe.RawEvents )
				Output.WriteLine ( $"nCode: {ev.Item1}, wParam: {ev.Item2}, lParam: {ev.Item3}, eID: {ev.Item4}" );
			foreach ( var ev in senderProbe.Events )
				Output.WriteLine ( $"Consumed: {ev.Item1}, Change: {ev.Item2}, Key: {ev.Item3}, eID: {ev.Item4}" );
			Output.WriteLine ( " ... ... ..." );

			Output.WriteLine (ex.ToString ());
			Output.WriteLine ( "" );
			Output.WriteLine ( "EndPoint A 'Sender' logbook:" );
			lock ( EPA.Logbook ) Output.WriteLine ( string.Join ( "\n", EPA.Logbook ) );
			Output.WriteLine ( "EndPoint B 'Receiver' logbook:" );
			lock ( EPB.Logbook ) Output.WriteLine ( string.Join ( "\n", EPB.Logbook ) );
			Output.WriteLine ( "LL_Device of A 'Sender' stack trace:" );
			Output.WriteLine ( EPA.ListeningDevice.BoundedInMemDeviceLL.GetLog () );
			Output.WriteLine ( "LL_Device of B 'Receiver' stack trace:" );
			Output.WriteLine ( EPB.ListeningDevice.BoundedInMemDeviceLL.GetLog () );
			throw;
		} finally {
			sender.AssertExecByRegex ( "hook remove KeyDown", InputSimulationTest.HookRemoveRegex ( "KeyDown" ), "No hooks removed." );
			receiver.AssertExecByRegex ( "hook remove KeyUp", InputSimulationTest.HookRemoveRegex ( "KeyUp" ), "No hooks removed." );
			sender.AssertExec ( "target set none", "Target disconnected." );
		}
	}

	private void RegisterPipeline ( BaseIntegrationTest agent, params Type[] types ) {
		var joiner = FetchJoiner ( agent );
		ComponentSelector[] selectors = new ComponentSelector[types.Length];
		for ( int i = 0; i < types.Length; i++ ) {
			selectors[i] = new ComponentSelector ( componentType: types[i] );
		}
		joiner.RegisterPipeline ( selectors );
	}
	private DComponentJoiner FetchJoiner (BaseIntegrationTest agent) {
		var Core = agent.cliWrapper.CmdProc.GetVar<DInputResenderCore> ( CoreManagerCommand.ActiveCoreVarName );
		var joiner = Core.Fetch<DComponentJoiner> ();
		joiner.Should ().NotBeNull ();
		return joiner;
	}
}