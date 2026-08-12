using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using InputResender.Definitions;
using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using InputResender.Variants.Commands;
using InputResender.Variants.InputProcessing;
using MdxLibs.Core;
using MdxLibs.Definitions;
using Xunit;
using Xunit.Abstractions;

namespace InputResender.UnitTests.IntegrationTests;
public class ProcessorChainingTest : BaseIntegrationTest {
	private readonly List<HInputEventDataHolder> capturedEvents = [];
	private readonly List<InputData> TapperCaptures = [];
	private readonly List<InputData> InputProcessorCaptures = [];
	private readonly HookManagerCommand.SHookManager hookManager;
	private readonly MLowLevelInput llInput;

	private static void AssertPassthrough ( nint val ) => val.Should ().BeLessThan ( 0 );
	private static void AssertConsumed ( nint val ) => val.Should ().Be ( 1 );

	public ProcessorChainingTest ( ITestOutputHelper output )
		: base ( null, output, InitCmdsList () ) {
		if ( Core.CommandWorker == null ) new VCommandWorker ( Core );
		Core.CommandWorker.RegisterCallback ( ev => {
				capturedEvents.AddRange ( Core.Fetch<DInputSimulator> ().ParseCommand ( ev ) );
			}
		);
		DComponentJoiner.TryRegisterJoiner<DInputProcessor, DCommandWorker, InputData> (
			Core.Fetch<DComponentJoiner> ()
			, ( compJoiner, processor, data ) => {
				processor.Push ( data );
				return (true, null);
			}
		);

		cliWrapper.CmdProc.AddCommand ( new VInputProcessorCommand ( Core ), CommandProcessor.AddCmdBehavior.Overwrite
		);
		var res00 = cliWrapper.ProcessLine ( "tapper add --force A S D F Space CapsLock Scroll" );
		var res01 = cliWrapper.ProcessLine ( "tapper wait 600" );
		var res02 = cliWrapper.ProcessLine ( "tapper condition None" );
		var res03 = cliWrapper.ProcessLine ( "tapper mapping Single X---- T" );
		var res04 = cliWrapper.ProcessLine ( "tapper mapping Single -X--- A" );
		var res05 = cliWrapper.ProcessLine ( "inputproc add" );
		var res06 = cliWrapper.ProcessLine ( "load joiners" );
		var res07 = cliWrapper.ProcessLine ( "hook manager start" );
		var res08 = cliWrapper.ProcessLine ( "hook manager verbosity 0" );
		var res09 = cliWrapper.ProcessLine ( "hook add Fast Pipeline Keydown Keyup" );
		var res10 = cliWrapper.ProcessLine ( "pipeline new getInput SHookManager DInputMerger VInputProcessor origin" );
		var res11 = cliWrapper.ProcessLine ( "pipeline new execTapper VInputProcessor VTapperInput" );
		var res12 = cliWrapper.ProcessLine ( "pipeline new tapperBackup VInputProcessor DCommandWorker" );
		var res13 = cliWrapper.ProcessLine ( "pipeline new logTapper VTapperInput DCommandWorker" );
		Core.Fetch<VTapperInput> ().Callback = TapperCaptures.Add;
		Core.Fetch<VInputProcessor> ().Callback = InputProcessorCaptures.Add;
		Core.Fetch<DInputSimulator> ().AllowRecapture = true;
		Core.Fetch<VComponentJoiner> ().OnPipelineFinishLog += ( s ) => Output.WriteLine ( s + "\n - - - - - - - -\n" );
		hookManager = Core.Fetch<HookManagerCommand.SHookManager> ();
		llInput = Core.Fetch<MLowLevelInput> ();
		llInput.CaptureEvents = true;
	}

	[Theory]
	[InlineData ( KeyCode.A, KeyCode.T )] // Converted by Tapper
	[InlineData ( KeyCode.W, KeyCode.W )] // Ignored by Tapper
	public void PipelineSetupCorrectly ( KeyCode inputKey, KeyCode expectedKey ) {
		AssertExec ( $"sim keypress {inputKey}", $"Sent 2 keyboard input (keypress) events." );
		AssertConsumed ( 0, true );
		AssertFinalKeypress ( 0, true, expectedKey );
	}

	[Theory]
	[InlineData ( typeof(VTapperInput), KeyCode.T )] // Converted by Tapper
	[InlineData ( typeof(DCommandWorker), KeyCode.A )] // Send directly to CommandWorker
	public void VInputProcessor_CanSelectTarget (Type target, KeyCode expectedKey) {
		cliWrapper.ProcessLine ( "pipeline new directExec VInputProcessor DCommandWorker" );
		cliWrapper.ProcessLine ( $"inputproc target {target.Name}" );
		cliWrapper.ProcessLine ( "sim keypress A" );
		AssertConsumed ( 0, true );
		AssertFinalKeypress ( 0, true, expectedKey );
	}

	[Theory]
	[InlineData ( KeyCode.A, KeyCode.T )] // Processed directly by Tapper
	[InlineData ( KeyCode.B, KeyCode.T )] // Processed after remap by Tapper
	[InlineData ( KeyCode.T, KeyCode.T )] // Not processed by Tapper
	[InlineData ( KeyCode.M, KeyCode.M )] // Not processed by Tapper
	public void VInputProcessor_CanRemapKeys (KeyCode inputKey, KeyCode expectedKey) {
		cliWrapper.ProcessLine ( "inputproc remap B A" );
		cliWrapper.ProcessLine ( $"sim keypress {inputKey}" );
		AssertConsumed ( 0, true );
		AssertFinalKeypress ( 0, true, expectedKey );
	}

	[Fact]
	public void VInputProcessor_OnHold_OnRelease () {
		cliWrapper.ProcessLine ( "pipeline delete tapperBackup" ).Should ().NotBeOfType<ErrorCommandResult> ();
		cliWrapper.ProcessLine ( "inputproc hold H" );
		cliWrapper.ProcessLine ( "inputproc release R" );
		cliWrapper.ProcessLine ( "sim keypress A" ); // Not processed - no hold
		capturedEvents.Should ().HaveCount ( 0 );
		cliWrapper.ProcessLine ( "sim keydown R" );
		capturedEvents.Should ().HaveCount ( 0 );
		cliWrapper.ProcessLine ( "sim keypress A" ); // Still not processed
		capturedEvents.Should ().HaveCount ( 0 );
		cliWrapper.ProcessLine ( "sim keydown H" );
		capturedEvents.Should ().HaveCount ( 0 );
		cliWrapper.ProcessLine ( "sim keypress A" ); // Still not processed - not released
		capturedEvents.Should ().HaveCount ( 0 );
		cliWrapper.ProcessLine ( "sim keyup R" );
		capturedEvents.Should ().HaveCount ( 0 );
		cliWrapper.ProcessLine ( "sim keypress A" ); // Processed - hold
		AssertFinalKeypress ( 0, true, KeyCode.T );
		cliWrapper.ProcessLine ( "sim keyup H" );
		AssertFinalKeypress ( 0, true, KeyCode.T );
		cliWrapper.ProcessLine ( "sim keypress A" ); // Not processed - hold released
		AssertFinalKeypress ( 0, true, KeyCode.T );
	}

	private static HInputEventDataHolder[] CreateInput ( DInputReader owner, params (KeyCode Key, bool Pressed)[] events ) {
		var ret = new List<HInputEventDataHolder> ();
		foreach ( var ev in events ) {
			ret.Add ( new HKeyboardEventDataHolder ( owner, 1, (int)ev.Key, ev.Pressed ? 1 : 0, ev.Pressed ? 1 : -1 ) );
		}
		return ret.ToArray ();
	}

	private void AssertFinalKeypress ( int id, bool final, KeyCode key ) {
		if ( final ) capturedEvents.Should ().HaveCount ( id + 2 );
		else capturedEvents.Should ().HaveCountGreaterThanOrEqualTo ( id + 2 );
		var events = capturedEvents.Skip(id).Take(2).ToArray();
		events.Should ().HaveCount ( 2 );
		InputSimulationTest.AssertKeyEvent ( events[0], key, true );
		InputSimulationTest.AssertKeyEvent ( events[1], key, false );
	}

	private void AssertConsumed ( int id, bool final ) {
		if ( final ) llInput.CapturedEvents.Should ().HaveCount ( id + 2 );
		else llInput.CapturedEvents.Should ().HaveCountGreaterThanOrEqualTo ( id + 2 );
		AssertConsumed ( llInput.CapturedEvents[id].ret );
		AssertConsumed ( llInput.CapturedEvents[id + 1].ret );
	}
}