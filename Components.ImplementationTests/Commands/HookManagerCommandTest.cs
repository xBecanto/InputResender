using Components.Implementations;
using Components.Interfaces;
using Components.Library;
using InputResender.Commands;
using Xunit;
using FluentAssertions;
using System.Collections.Generic;

namespace Components.ImplementationTests.Commands;
public class TestableHookManagerCommand : HookManagerCommand {
	public TestableHookManagerCommand ( DMainAppCore owner ) : base ( owner ) { }

	// Access to registered callbacks for testing  
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




public class HookManagerCommandTest : CommandTestBaseVCore {
	private const DHookManager.CBType FAST = DHookManager.CBType.Fast;
	private const DHookManager.CBType DELAYED = DHookManager.CBType.Delayed;
	private readonly TestableHookManagerCommand testableCommand;

	public HookManagerCommandTest () : base ( owner => new TestableHookManagerCommand ( owner )
		, DMainAppCore.BasicSelection | DMainAppCore.CompSelect.LLInput
	) {
		// Get the command instance that was added to the processor
		testableCommand = (TestableHookManagerCommand)CmdProc.GetCommandInstance<TestableHookManagerCommand> ();
	}

	[Fact]
	public void TestAutoCmdConfiguration () {
		var result = CmdProc.ProcessLine ( "hook autocmd fast movement W A S D" );
		result.Should ().BeOfType<CommandResult> ();
		result.Message.Should ().Contain ( "AutoCmd configured for group 'movement' with 4 keys" );

		CmdProc.ProcessLine ( "hook autocmd fast combat Space Enter" );

		var hookManager = testableCommand.GetHookManagerForCore ( Owner, FAST, 0 );
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
		var result = CmdProc.ProcessLine ( "hook filter fast consume Escape Tab" );
		result.Should ().BeOfType<CommandResult> ();
		result.Message.Should ().Contain ( "Filter configured to consume 2 keys" );

		CmdProc.ProcessLine ( "hook filter fast pass F1 F2 F3" );

		var hookManager = testableCommand.GetHookManagerForCore ( Owner, FAST, 0 );
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
		SetVCore ( DMainAppCore.BasicSelection | DMainAppCore.CompSelect.LLInput );
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
		CmdProc.ProcessLine ( "hook manager start" );

		var result = CmdProc.ProcessLine ( "hook add fast AutoCmd keydown" );
		result.Should ().BeOfType<CommandResult> ();
		result.Message.Should ().Contain ( "Hooks added" );

		var hookManager = testableCommand.GetHookManagerForCore ( Owner, FAST, 0 );
		hookManager.CbFcn.Should ().Be ( HookManagerCommand.CallbackFcn.AutoCmd );
		hookManager.AssignedCallbackType.Should ().Be ( FAST );

		var callbacks = testableCommand.GetRegisteredCallbacks ();
		callbacks.Should ().NotBeEmpty ();
		callbacks.Values.First ().type.Should ().Be ( FAST );
	}

	[Fact]
	public void TestAutoCmdCallbackExecution () {
		CmdProc.ProcessLine ( "hook manager start" );
		CmdProc.ProcessLine ( "hook add fast AutoCmd keydown" );
		CmdProc.ProcessLine ( "hook autocmd fast testGroup W" );

		testableCommand.AssertConsume ( FAST, KeyCode.W, VKChange.KeyDown );
		testableCommand.AssertPassthrough ( FAST, KeyCode.Z, VKChange.KeyDown );
	}

	[Fact]
	public void TestFilterCallbackExecution () {
		CmdProc.ProcessLine ( "hook manager start" );
		CmdProc.ProcessLine ( "hook add fast Filter keydown" );
		CmdProc.ProcessLine ( "hook filter fast consume Escape" );
		CmdProc.ProcessLine ( "hook filter fast pass F1" );

		testableCommand.AssertConsume ( FAST, KeyCode.Escape, VKChange.KeyDown );
		testableCommand.AssertPassthrough ( FAST, KeyCode.F1, VKChange.KeyDown );
		testableCommand.AssertPassthrough ( FAST, KeyCode.Z, VKChange.KeyDown );
	}

	[Fact]
	public void TestDelayedCallbackPassesThrough () {
		CmdProc.ProcessLine ( "hook manager start" );
		CmdProc.ProcessLine ( "hook add delayed Filter keydown" );
		CmdProc.ProcessLine ( "hook filter delayed consume Escape" );

		var hookManager = testableCommand.GetHookManagerForCore ( Owner, DELAYED, 0 );
		hookManager.AssignedCallbackType.Should ().Be ( DELAYED );
		testableCommand.AssertPassthrough ( DELAYED, KeyCode.Escape, VKChange.KeyDown );
	}

	[Fact]
	public void TestMultipleCoreSupport () {
		var factory = new DMainAppCoreFactory ();
		var core1 = factory.CreateVMainAppCore ( DMainAppCore.CompSelect.InputReader );
		var core2 = factory.CreateVMainAppCore ( DMainAppCore.CompSelect.InputProcessor );

		CmdProc.SetVar ( CoreManagerCommand<DMainAppCore>.ActiveCoreVarName, core1 );
		CmdProc.ProcessLine ( "hook autocmd fast group1 W" );
		CmdProc.SetVar ( CoreManagerCommand<DMainAppCore>.ActiveCoreVarName, core2 );
		CmdProc.ProcessLine ( "hook autocmd fast group2 A" );

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
				CmdProc.ProcessLine ( "hook filter fast invalid_action W" );
			}
		);
		exception.Message.Should ().Contain ( "Invalid filter action 'invalid_action'" );

		Assert.Throws<ArgumentException> ( () => { CmdProc.ProcessLine ( "hook autocmd fast group InvalidKey" ); } );
	}

	/*
	[Fact]
	public void TestCleanupFunctionality () {
		SetVCore ( DMainAppCore.BasicSelection | DMainAppCore.CompSelect.LLInput );
		var core = GetActiveCore ();

		// Setup hook
		CmdProc.ProcessLine ( "hook manager start" );
		CmdProc.ProcessLine ( "hook add fast Print keydown" );

		// Verify hook callback is registered
		var hookManager = testableCommand.GetHookManagerForCore ( core );
		hookManager.hookCallback.Should ().NotBeNull ();

		// Test cleanup
		var cleanupResult
			= testableCommand.ExecCleanup ( new CommandProcessor<DMainAppCore>.CmdContext ( CmdProc, "cleanup" ) );
		cleanupResult.Should ().BeOfType<CommandResult> ();
		cleanupResult.Message.Should ().Contain ( "Hook callback in active core unregistered" );
	}*/

	[Fact]
	public void TestFastVsDelayedCallbackSeparation () {
		SetVCore ( DMainAppCore.CompSelect.All );
		var core = GetActiveCore ();

		// Configure different settings for fast and delayed callbacks
		ExecuteCommand ( "hook manager start" );

		// Install fast filter callback
		ExecuteCommand ( "hook add fast Filter keydown" );
		ExecuteCommand ( "hook filter fast consume Escape" );

		// Install delayed pipeline callback
		ExecuteCommand ( "hook add delayed Pipeline keydown" );

		// Verify we have separate hook managers
		var fastHookManager = testableCommand.GetHookManagerForCore ( core, FAST, 0 );
		var delayedHookManager = testableCommand.GetHookManagerForCore ( core, DELAYED, 0 );

		fastHookManager.Should ().NotBeSameAs ( delayedHookManager );
		fastHookManager.CbFcn.Should ().Be ( HookManagerCommand.CallbackFcn.Filter );
		delayedHookManager.CbFcn.Should ().Be ( HookManagerCommand.CallbackFcn.Pipeline );

		// Verify callback types are correctly assigned
		fastHookManager.AssignedCallbackType.Should ().Be ( FAST );
		delayedHookManager.AssignedCallbackType.Should ().Be ( DELAYED );

		// Test fast callback behavior - Filter should consume Escape
		var escapeEvent = new HKeyboardEventDataHolder (
			fastHookManager,
			new HHookInfo ( fastHookManager, 0, VKChange.KeyDown ),
			(int)KeyCode.Escape,
			VKChange.KeyDown
		);

		bool fastResult = fastHookManager.HookCallback ( escapeEvent );
		fastResult.Should ().BeFalse (); // Filter should consume the Escape key
	}

	[Fact]
	public void TestMultipleDeviceSupport () {
		SetVCore ( DMainAppCore.CompSelect.All );
		var core = GetActiveCore ();

		// Test that different device IDs create separate hook managers
		var device0Manager = testableCommand.GetHookManagerForCore ( core, FAST, 0 );
		var device1Manager = testableCommand.GetHookManagerForCore ( core, FAST, 1 );

		device0Manager.Should ().NotBeSameAs ( device1Manager );
		device0Manager.AssignedDeviceID.Should ().Be ( 0 );
		device1Manager.AssignedDeviceID.Should ().Be ( 1 );
	}

	// Helper methods
	private DMainAppCore GetActiveCore () {
		return CmdProc.GetVar<DMainAppCore> ( CoreManagerCommand<DMainAppCore>.ActiveCoreVarName );
	}

	private CommandResult ExecuteCommand ( string command ) { return CmdProc.ProcessLine ( command ); }
}