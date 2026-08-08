using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using InputResender.Definitions;
using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using InputResender.Variants;
using InputResender.Variants.Commands;
using MdxLibs.Core;
using MdxLibs.Services;
using Xunit;
using Xunit.Abstractions;

namespace InputResender.UnitTests.IntegrationTests.CommandTests;
public class TestableHookManagerCommand ( DInputResenderCore owner ) : HookManagerCommand ( owner ) {
	public Dictionary<DictionaryKey, (DHookManager.CBType type, HCallbackHolder<DHookManager.HookCallback> cbHolder)>
		GetRegisteredCallbacks ()
		=> RegisteredCallbacks;

	private bool ExecCallback ( DHookManager.CBType cbtype, KeyCode keyCode, VKChange vkChange ) {
		var hookManager = GetComp ( Owner, cbtype, 0 );
		var inputEvent = new HKeyboardEventDataHolder ( hookManager
			, new HHookInfo ( hookManager, 0, vkChange )
			, (int)keyCode
			, vkChange
		);
		return hookManager.HookCallback ( inputEvent );
	}

	public void AssertPassthrough ( DHookManager.CBType cbtype, KeyCode keyCode, VKChange vkChange )
		=> ExecCallback ( cbtype, keyCode, vkChange ).Should ().BeTrue ();

	public void AssertConsume ( DHookManager.CBType cbtype, KeyCode keyCode, VKChange vkChange )
		=> ExecCallback ( cbtype, keyCode, vkChange ).Should ().BeFalse ();

	public SHookManager GetHookManagerForCore ( CoreBase core, DHookManager.CBType callbackType, int deviceID )
		=> GetComp ( core, callbackType, deviceID );
}




public class HookManagerCommandTest : BaseIntegrationTest, IDisposable {
	private const DHookManager.CBType FAST = DHookManager.CBType.Fast;
	private const DHookManager.CBType DELAYED = DHookManager.CBType.Delayed;
	private readonly TestableHookManagerCommand testableCommand;

	public HookManagerCommandTest ( ITestOutputHelper output )
		: base ( null, output, InitCmdsList ( "hook manager start" ) ) {
		cliWrapper.CmdProc.AddCommand ( new TestableHookManagerCommand ( Core ), CommandProcessor.AddCmdBehavior.Overwrite );
		testableCommand = (TestableHookManagerCommand)cliWrapper.CmdProc.GetCommandInstance<TestableHookManagerCommand> ();
	}

	public void Dispose () {
		Core.Close ();
	}

	[Fact]
	public void TestAutoCmdConfiguration () {
		AssertExec ( "hook autocmd fast movement W A S D", "AutoCmd configured for group 'movement' with 4 keys: W A S D." );
		AssertExec ( "hook autocmd fast combat Space Enter", "AutoCmd configured for group 'combat' with 2 keys: Space Enter." );

		var hookManager = testableCommand.GetHookManagerForCore ( Core, FAST, 0 );
		hookManager.AutoCmdMap.Should ().ContainKey ( KeyCode.W );
		hookManager.AutoCmdMap.Should ().ContainKey ( KeyCode.A );
		hookManager.AutoCmdMap.Should ().ContainKey ( KeyCode.S );
		hookManager.AutoCmdMap.Should ().ContainKey ( KeyCode.D );
		hookManager.AutoCmdMap.Should ().ContainKey ( KeyCode.Space );
		hookManager.AutoCmdMap.Should ().ContainKey ( KeyCode.Enter );

		hookManager.AutoCmdMap[KeyCode.W].Should ().Be ( "movement" );
		hookManager.AutoCmdMap[KeyCode.Space].Should ().Be ( "combat" );
	}

	[Fact]
	public void TestFilterConfiguration () {
		AssertExec ( "hook filter fast consume Escape Tab", "Filter configured to consume 2 keys: Escape Tab." );
		AssertExec ( "hook filter fast pass F1 F2 F3", "Filter configured to pass 3 keys: F1 F2 F3." );

		var hookManager = testableCommand.GetHookManagerForCore ( Core, FAST, 0 );
		hookManager.FilterMap.Should ().ContainKey ( KeyCode.Escape );
		hookManager.FilterMap.Should ().ContainKey ( KeyCode.Tab );
		hookManager.FilterMap.Should ().ContainKey ( KeyCode.F1 );
		hookManager.FilterMap.Should ().ContainKey ( KeyCode.F2 );
		hookManager.FilterMap.Should ().ContainKey ( KeyCode.F3 );

		hookManager.FilterMap[KeyCode.Escape].Should ().BeTrue (); // consume
		hookManager.FilterMap[KeyCode.Tab].Should ().BeTrue ();    // consume
		hookManager.FilterMap[KeyCode.F1].Should ().BeFalse ();    // pass
		hookManager.FilterMap[KeyCode.F2].Should ().BeFalse ();    // pass
		hookManager.FilterMap[KeyCode.F3].Should ().BeFalse ();    // pass
	}

	/*[Fact]
	public void TestSclConfiguration () {
		SetVCore ( DInputResenderCore.BasicSelection | DInputResenderCore.CompSelect.LLInput );
		var core = GetActiveCore ();

		// Configure SCL script for specific keys
		var result = CmdProc.ProcessLine ( "hook scl testScript.scl Q E R" );
		result.Should ().BeOfType<CommandResult> ();
		result.Message.Should ().Contain ( "SCL script 'testScript.scl' configured for 3 keys" );

		// Verify internal state
		var hookManager = testableCommand.GetHookManagerForCore ( core );
		hookManager.SclScriptMap.Should ().ContainKey ( KeyCode.Q );
		hookManager.SclScriptMap.Should ().ContainKey ( KeyCode.E );
		hookManager.SclScriptMap.Should ().ContainKey ( KeyCode.R );

		hookManager.SclScriptMap[KeyCode.Q].Should ().Be ( "testScript.scl" );
		hookManager.SclScriptMap[KeyCode.E].Should ().Be ( "testScript.scl" );
		hookManager.SclScriptMap[KeyCode.R].Should ().Be ( "testScript.scl" );
	}*/

	[Fact]
	public void TestHookInstallationWithCallbackTypes () {
		AssertExecByRegex ( "hook add fast AutoCmd keydown", HookAddRegex ( "Fast", "AutoCmd", "KeyDown" ) );

		var hookManager = testableCommand.GetHookManagerForCore ( Core, FAST, 0 );
		hookManager.CbFcn.Should ().Be ( HookManagerCommand.CallbackFcn.AutoCmd );
		hookManager.AssignedCallbackType.Should ().Be ( FAST );

		var callbacks = testableCommand.GetRegisteredCallbacks ();
		callbacks.Should ().NotBeEmpty ();
		callbacks.Values.First ().type.Should ().Be ( FAST );
	}

	private string HookAddRegex(string type, string action, string vkChange) => $"Hooks added under key '\\(\\S+\\)' \\(MockLLHook#\\(\\S+\\)@\\d+<{vkChange}>\\) for {type} callback type for action {action}\\.";

	[Fact]
	public void TestAutoCmdCallbackExecution () {
		AssertExecByRegex ( "hook add fast AutoCmd keydown", HookAddRegex("Fast", "AutoCmd", "KeyDown") );
		AssertExec ( "hook autocmd fast testGroup W", "AutoCmd configured for group 'testGroup' with 1 keys: W." );
		AssertExec ( "auto add testGroup ((print \"TestGroup triggered!\"))", "Added 1 command to group 'testGroup'." );

		testableCommand.AssertConsume ( FAST, KeyCode.W, VKChange.KeyDown );
		testableCommand.AssertPassthrough ( FAST, KeyCode.Z, VKChange.KeyDown );
	}

	[Fact]
	public void TestFilterCallbackExecution () {
		AssertExecByRegex ( "hook add fast Filter keydown", HookAddRegex ( "Fast", "Filter", "KeyDown" ) );
		AssertExec ( "hook filter fast consume Escape", "Filter configured to consume 1 keys: Escape." );
		AssertExec ( "hook filter fast pass F1", "Filter configured to pass 1 keys: F1." );

		testableCommand.AssertConsume ( FAST, KeyCode.Escape, VKChange.KeyDown ); // Filter - consume
		testableCommand.AssertPassthrough ( FAST, KeyCode.F1, VKChange.KeyDown ); // FIlter - pass
		testableCommand.AssertPassthrough ( FAST, KeyCode.Z, VKChange.KeyDown ); // Ignored
	}

	[Fact]
	public void TestDelayedCallbackPassesThrough () {
		AssertExecByRegex ( "hook add delayed Filter keydown", HookAddRegex ( "Delayed", "Filter", "KeyDown" ) );
		AssertExec ( "hook filter delayed consume Escape", "Filter configured to consume 1 keys: Escape." );

		var hookManager = testableCommand.GetHookManagerForCore ( Core, DELAYED, 0 );
		hookManager.AssignedCallbackType.Should ().Be ( DELAYED );
		testableCommand.AssertPassthrough ( DELAYED, KeyCode.Escape, VKChange.KeyDown );
	}

	[Fact]
	public void TestMultipleCoreSupport () {
		var factory = new DInputResenderCoreFactory ();
		var core1 = factory.CreateVMainAppCore ( DInputResenderCore.CompSelect.InputReader );
		var core2 = factory.CreateVMainAppCore ( DInputResenderCore.CompSelect.InputProcessor );

		cliWrapper.CmdProc.SetVar ( CoreManagerCommand.ActiveCoreVarName, core1 );
		AssertExec ( "hook autocmd fast group1 W", "AutoCmd configured for group 'group1' with 1 keys: W." );

		cliWrapper.CmdProc.SetVar ( CoreManagerCommand.ActiveCoreVarName, core2 );
		AssertExec ( "hook autocmd fast group2 A", "AutoCmd configured for group 'group2' with 1 keys: A." );

		var hookManager1 = testableCommand.GetHookManagerForCore ( core1, FAST, 0 );
		var hookManager2 = testableCommand.GetHookManagerForCore ( core2, FAST, 0 );

		hookManager1.AutoCmdMap.Should ().ContainKey ( KeyCode.W );
		hookManager1.AutoCmdMap.Should ().NotContainKey ( KeyCode.A );
		hookManager1.AutoCmdMap[KeyCode.W].Should ().Be ( "group1" );

		hookManager2.AutoCmdMap.Should ().ContainKey ( KeyCode.A );
		hookManager2.AutoCmdMap.Should ().NotContainKey ( KeyCode.W );
		hookManager2.AutoCmdMap[KeyCode.A].Should ().Be ( "group2" );
	}

	[Fact]
	public void TestInvalidCommands () {
		var exception = Assert.Throws<ArgumentException> ( () => {
				AssertExec ( "hook filter fast invalid_action W", "" );
			}
		);
		exception.Message.Should ().Contain ( "Invalid filter action 'invalid_action'" );

		Assert.Throws<ArgumentException> ( () => {
			AssertExec ( "hook autocmd fast group InvalidKey", "" );
		} );
	}

	/*
	[Fact]
	public void TestCleanupFunctionality () {
		SetVCore ( DInputResenderCore.BasicSelection | DInputResenderCore.CompSelect.LLInput );
		var core = GetActiveCore ();

		// Setup hook
		CmdProc.ProcessLine ( "hook manager start" );
		CmdProc.ProcessLine ( "hook add fast Print keydown" );

		// Verify hook callback is registered
		var hookManager = testableCommand.GetHookManagerForCore ( core );
		hookManager.hookCallback.Should ().NotBeNull ();

		// Test cleanup
		var cleanupResult
			= testableCommand.ExecCleanup ( new CommandProcessor<DInputResenderCore>.CmdContext ( CmdProc, "cleanup" ) );
		cleanupResult.Should ().BeOfType<CommandResult> ();
		cleanupResult.Message.Should ().Contain ( "Hook callback in active core unregistered" );
	}*/

	[Fact]
	public void TestFastVsDelayedCallbackSeparation () {
		AssertExecByRegex ( "hook add fast Filter keydown", HookAddRegex ( "Fast", "Filter", "KeyDown" ) );
		AssertExec ( "hook filter fast consume Escape", "Filter configured to consume 1 keys: Escape." );

		// The hook was already created for Fast/KeyDown, will be reused
		AssertExec ( "hook add delayed Pipeline keydown", "No hooks added." );

		var fastHookManager = testableCommand.GetHookManagerForCore ( Core, FAST, 0 );
		var delayedHookManager = testableCommand.GetHookManagerForCore ( Core, DELAYED, 0 );

		fastHookManager.Should ().NotBeSameAs ( delayedHookManager );
		fastHookManager.CbFcn.Should ().Be ( HookManagerCommand.CallbackFcn.Filter );
		delayedHookManager.CbFcn.Should ().Be ( HookManagerCommand.CallbackFcn.Pipeline );

		fastHookManager.AssignedCallbackType.Should ().Be ( FAST );
		delayedHookManager.AssignedCallbackType.Should ().Be ( DELAYED );

		var escapeEvent = new HKeyboardEventDataHolder (
			fastHookManager,
			new HHookInfo ( fastHookManager, 0, VKChange.KeyDown ),
			(int)KeyCode.Escape,
			VKChange.KeyDown
		);

		bool fastResult = fastHookManager.HookCallback ( escapeEvent );
		fastResult.Should ().BeFalse ();
	}

	[Fact]
	public void TestMultipleDeviceSupport () {
		var device0Manager = testableCommand.GetHookManagerForCore ( Core, FAST, 0 );
		var device1Manager = testableCommand.GetHookManagerForCore ( Core, FAST, 1 );

		device0Manager.Should ().NotBeSameAs ( device1Manager );
		device0Manager.AssignedDeviceID.Should ().Be ( 0 );
		device1Manager.AssignedDeviceID.Should ().Be ( 1 );
	}
}