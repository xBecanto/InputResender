using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using InputResender.Variants.Commands;
using InputResender.Windows.Input;
using Xunit;
using Xunit.Abstractions;
using static InputResender.UnitTests.IntegrationTests.InputSimulationTest;

namespace InputResender.UnitTests.IntegrationTests;
public class WindowsInputSimulationTest : InputSimulationTest {
	public WindowsInputSimulationTest ( ITestOutputHelper output ) : base ( output ) { }
	protected override string[] InitCmds => ["hook manager start", "windows load --force", "windows msgs start"];
}

[Collection ( "WinInputTests" )]
public abstract class InputSimulationTest : BaseIntegrationTest {
	public const string DictKeyRegex = "\\([+-]?[a-zA-Z0-9\\+\\*@#$%&\\?€£¤]+\\)";

	public static string HookAddRegex ( string inputs, DHookManager.CBType type, string action )
		=> $@"Hooks added under key '{DictKeyRegex}' \(WinHook#{DictKeyRegex}\[13\]:0<{inputs}>\) for {type} callback type for action {action}\.";
	public static string HookRemoveRegex ( string inputs )
		=> $@"Hooks removed \(WinHook#{DictKeyRegex}\[13\]:0<{inputs}>\)";
	protected abstract string[] InitCmds { get; }
	private bool HookRet = true; // Please: docs containing what this 'bool' return type means!!!
	List<HInputEventDataHolder> hookCaptures = [];
	AutoResetEvent hookWaiter = null;

	public InputSimulationTest ( ITestOutputHelper output ) : base ( null, output, GeneralInitCmds ) {
		foreach ( string cmd in InitCmds ) cliWrapper.ProcessLine ( cmd );
		cliWrapper.CmdProc.SetVar ( HookManagerCommand.INPHOOKCBVarName, (object)TestHook );
	}

	[Fact]
	public void SimKeyDown () {
		// In VWinLowLevelLibs (both):
		//	private const int WH_KEYBOARD_LOW_LEVEL = 13;
		//	private const int WH_MOUSE_LOW_LEVEL = 14;
		// Hook info generated as: hookInfo[j++] = core.Fetch<DInputReader> ().PrintHookInfo ( hook );
		AssertExecByRegex ( "hook add delayed Fcn -c KeyDown", HookAddRegex ( "KeyDown", DHookManager.CBType.Delayed, "Fcn" ), "No hooks added." );
		// Manual waiting for windows events will probably be needed here
		ConsumeMessages ();
		hookCaptures.Clear ();
		hookWaiter = new ( false );
		AssertExec ( "sim keydown E", "Sent 1 key down events." );
		ActiveWait ( 50, 1, () => hookCaptures.Count > 0 ).Should ().BeTrue ();
		hookCaptures.Should().HaveCount ( 1 );
		AssertKeyEvent ( hookCaptures[0], KeyCode.E, true );
		hookCaptures.Clear ();
		AssertExecByRegex ( "hook remove KeyDown", HookRemoveRegex ( "KeyDown" ), "No hooks removed." );

		// Create probe, wait until it receives at least one event, check it wasn't picked by previous hook.
		var probe = cliWrapper.CmdProc.Owner.Fetch<VWinLowLevelLibs> ().InstallProbe
			( true, [VKChange.KeyDown], KeyCode.E );
		AssertExec ( "sim keydown E", "Sent 1 key down events." );
		ActiveWait ( 50, 1, () => probe.Events.Count > 0 ).Should ().BeTrue ();
		hookCaptures.Should ().HaveCount ( 0 );
	}

	[Fact]
	public void SimKeyEvents () {
		AssertExecByRegex ( "hook add delayed Fcn -c KeyDown KeyUp", HookAddRegex ( "KeyDown, KeyUp", DHookManager.CBType.Delayed, "Fcn" ), "No hooks added." );
		ConsumeMessages ();
		hookCaptures.Clear ();
		hookWaiter = new ( false );
		AssertExec ( "sim keydown F", "Sent 1 key down events." );
		AssertExec ( "sim keyup F", "Sent 1 key up events." );
		AssertExec ( "sim keypress G", "Sent 2 keyboard input (keypress) events." );
		ActiveWait ( 50, 1, () => hookCaptures.Count > 3 ).Should ().BeTrue ();
		hookCaptures.Should ().HaveCount ( 4 );
		AssertKeyEvent ( hookCaptures[0], KeyCode.F, true );
		AssertKeyEvent ( hookCaptures[1], KeyCode.F, false );
		AssertKeyEvent ( hookCaptures[2], KeyCode.G, true );
		AssertKeyEvent ( hookCaptures[3], KeyCode.G, false );
		hookCaptures.Clear ();
		AssertExecByRegex ( "hook remove KeyDown KeyUp", HookRemoveRegex ( "KeyDown, KeyUp" ), "No hooks removed." );

		var probe = cliWrapper.CmdProc.Owner.Fetch<VWinLowLevelLibs> ().InstallProbe
			( true, [VKChange.KeyDown, VKChange.KeyUp], KeyCode.F, KeyCode.G );
		AssertExec ( "sim keydown E", "Sent 1 key down events." );
		ActiveWait ( 50, 1, () => probe.Events.Count > 0 ).Should ().BeTrue ();
		hookCaptures.Should ().HaveCount ( 0 );
	}

	private void HookProbeCombinationTest (params HTAct[] acts ) {
		string testName = string.Join ( "→", acts.Select ( a => a.ToString ().Replace ( nameof ( HTAct ), "$" ).Replace ( "$.", null ) ) );
		TestStatus status = new ( this, testName );
		foreach ( var act in acts ) {
			switch ( act ) {
			case HTAct.AddHook: status.AddHook (); break;
			case HTAct.AddProbe: status.AddProbe (); break;
			case HTAct.RemHook: status.RemoveHook (); break;
			case HTAct.RemProbe: status.RemoveProbe (); break;
			case HTAct.RemAllProbes: status.RemoveAllProbes (); break;
			}
		}
		status.Clear ();
	}

	public enum HTAct { None, AddHook, AddProbe, RemHook, RemProbe, RemAllProbes }

	[Theory]
	// Assert that each action can be executed alone
	[InlineData ( HTAct.AddHook )]
	[InlineData ( HTAct.AddProbe )]
	public void BasicScenario ( HTAct act ) => HookProbeCombinationTest ( act );

	[Theory]
	// Assert that any object can be added and removed
	[InlineData ( HTAct.AddHook, HTAct.RemHook )]
	[InlineData ( HTAct.AddProbe, HTAct.RemProbe )]

	// Test that two objects (hook, probe) can work together
	[InlineData ( HTAct.AddHook, HTAct.AddProbe )]
	[InlineData ( HTAct.AddProbe, HTAct.AddHook )]
	[InlineData ( HTAct.AddProbe, HTAct.AddProbe )]
	public void HookProbe2Combo ( HTAct a1, HTAct a2 ) => HookProbeCombinationTest ( a1, a2 );

	[Theory]
	// Test that hook(or probe) works correctly with two probes
	[InlineData ( HTAct.AddHook, HTAct.AddProbe, HTAct.AddProbe )]
	[InlineData ( HTAct.AddProbe, HTAct.AddHook, HTAct.AddProbe )]
	[InlineData ( HTAct.AddProbe, HTAct.AddProbe, HTAct.AddHook )]
	[InlineData ( HTAct.AddProbe, HTAct.AddProbe, HTAct.AddProbe )]

	// Test that any object works after removing another
	[InlineData ( HTAct.AddHook, HTAct.RemHook, HTAct.AddProbe)]
	[InlineData ( HTAct.AddProbe, HTAct.RemProbe, HTAct.AddHook )]
	[InlineData ( HTAct.AddProbe, HTAct.RemProbe, HTAct.AddProbe )]

	// Simple scenario for independence of removal order
	[InlineData ( HTAct.AddHook, HTAct.AddProbe, HTAct.RemHook )]
	[InlineData ( HTAct.AddProbe, HTAct.AddHook, HTAct.RemProbe )]
	public void HookProbe3Combo ( HTAct a1, HTAct a2, HTAct a3 ) => HookProbeCombinationTest ( a1, a2, a3 );

	[Theory]
	// Add and remove - test independence of order
	[InlineData ( HTAct.AddHook, HTAct.AddProbe, HTAct.RemHook, HTAct.RemProbe )]
	[InlineData ( HTAct.AddHook, HTAct.AddProbe, HTAct.RemProbe, HTAct.RemHook )]
	[InlineData ( HTAct.AddProbe, HTAct.AddHook, HTAct.RemProbe, HTAct.RemHook )]
	[InlineData ( HTAct.AddProbe, HTAct.AddHook, HTAct.RemHook, HTAct.RemProbe )]
	[InlineData ( HTAct.AddProbe, HTAct.AddProbe, HTAct.RemProbe, HTAct.RemProbe )]
	[InlineData ( HTAct.AddProbe, HTAct.AddProbe, HTAct.RemProbe, HTAct.RemProbe )]

	[InlineData ( HTAct.AddHook, HTAct.RemHook, HTAct.AddProbe, HTAct.RemProbe )]
	[InlineData ( HTAct.AddProbe, HTAct.RemProbe, HTAct.AddHook, HTAct.RemHook )]
	[InlineData ( HTAct.AddProbe, HTAct.RemProbe, HTAct.AddProbe, HTAct.RemProbe )]
	public void HookProbe4Combo ( HTAct a1, HTAct a2, HTAct a3, HTAct a4 ) => HookProbeCombinationTest ( a1, a2, a3, a4 );

	[Theory]
	[InlineData ( HTAct.AddHook, HTAct.AddProbe, HTAct.AddProbe, HTAct.RemHook, HTAct.RemProbe )]
	[InlineData ( HTAct.AddHook, HTAct.AddProbe, HTAct.AddProbe, HTAct.RemProbe, HTAct.RemHook )]
	[InlineData ( HTAct.AddProbe, HTAct.AddHook, HTAct.AddProbe, HTAct.RemHook, HTAct.RemProbe )]
	[InlineData ( HTAct.AddProbe, HTAct.AddHook, HTAct.AddProbe, HTAct.RemProbe, HTAct.RemHook )]
	[InlineData ( HTAct.AddProbe, HTAct.AddProbe, HTAct.AddHook, HTAct.RemHook, HTAct.RemProbe )]
	[InlineData ( HTAct.AddProbe, HTAct.AddProbe, HTAct.AddHook, HTAct.RemProbe, HTAct.RemHook )]

	[InlineData ( HTAct.AddHook, HTAct.AddProbe, HTAct.RemProbe, HTAct.AddProbe, HTAct.RemHook )]
	[InlineData ( HTAct.AddHook, HTAct.AddProbe, HTAct.RemHook, HTAct.AddProbe, HTAct.RemProbe )]
	[InlineData ( HTAct.AddProbe, HTAct.AddHook, HTAct.RemProbe, HTAct.AddProbe, HTAct.RemHook )]
	[InlineData ( HTAct.AddProbe, HTAct.AddHook, HTAct.RemHook, HTAct.AddProbe, HTAct.RemProbe )]

	[InlineData ( HTAct.AddHook, HTAct.RemHook, HTAct.AddProbe, HTAct.AddProbe, HTAct.RemProbe )]
	[InlineData ( HTAct.AddProbe, HTAct.RemProbe, HTAct.AddHook, HTAct.AddProbe, HTAct.RemHook )]
	[InlineData ( HTAct.AddProbe, HTAct.RemProbe, HTAct.AddProbe, HTAct.AddHook, HTAct.RemProbe )]
	public void HookProbe5Combo ( HTAct a1, HTAct a2, HTAct a3, HTAct a4, HTAct a5 ) => HookProbeCombinationTest ( a1, a2, a3, a4, a5 );

	[Theory]
	[InlineData ( HTAct.AddHook, HTAct.AddProbe, HTAct.AddProbe, HTAct.AddProbe, HTAct.RemProbe, HTAct.RemHook )]
	[InlineData ( HTAct.AddHook, HTAct.AddProbe, HTAct.AddProbe, HTAct.AddProbe, HTAct.RemHook, HTAct.RemProbe )]
	[InlineData ( HTAct.AddHook, HTAct.AddProbe, HTAct.AddProbe, HTAct.RemHook, HTAct.AddProbe, HTAct.RemProbe )]
	[InlineData ( HTAct.AddHook, HTAct.AddProbe, HTAct.AddProbe, HTAct.RemProbe, HTAct.AddProbe, HTAct.RemHook )]
	[InlineData ( HTAct.AddHook, HTAct.AddProbe, HTAct.RemHook, HTAct.RemProbe, HTAct.AddProbe, HTAct.RemProbe )]
	[InlineData ( HTAct.AddHook, HTAct.RemHook, HTAct.AddProbe, HTAct.RemProbe, HTAct.AddProbe, HTAct.RemProbe )]

	[InlineData ( HTAct.AddProbe, HTAct.AddHook, HTAct.AddProbe, HTAct.RemProbe, HTAct.RemHook, HTAct.RemProbe )]
	[InlineData ( HTAct.AddProbe, HTAct.AddHook, HTAct.AddProbe, HTAct.RemHook, HTAct.RemProbe, HTAct.RemProbe )]
	[InlineData ( HTAct.AddProbe, HTAct.AddHook, HTAct.RemHook, HTAct.AddProbe, HTAct.RemProbe, HTAct.RemProbe )]
	[InlineData ( HTAct.AddProbe, HTAct.AddHook, HTAct.RemProbe, HTAct.AddProbe, HTAct.RemHook, HTAct.RemProbe )]
	[InlineData ( HTAct.AddProbe, HTAct.AddHook, HTAct.RemProbe, HTAct.RemHook, HTAct.AddProbe, HTAct.RemProbe )]
	public void HookProbe6Combo ( HTAct a1, HTAct a2, HTAct a3, HTAct a4, HTAct a5, HTAct a6 ) => HookProbeCombinationTest ( a1, a2, a3, a4, a5, a6 );

	public static void AssertKeypress (List<HInputEventDataHolder> caputers, ProbeHook probe, KeyCode key) {
		ActiveWait ( 50, 1, () => caputers.Count > 1 ).Should ().BeTrue ();
		if (probe != null) {
			AssertProbe ( probe, 0, key, VKChange.KeyDown );
			AssertProbe ( probe, 1, key, VKChange.KeyUp );
		}
		if (caputers != null) {
			AssertKeyEvent ( caputers[0], key, true );
			AssertKeyEvent ( caputers[1], key, false );
		}
	}

	public static void AssertKeyEvent ( HInputEventDataHolder e, KeyCode key, bool press ) {
		e.Should ().NotBeNull ()
		.And.BeOfType<HKeyboardEventDataHolder> ();
		var ke = e as HKeyboardEventDataHolder;
		if ( press ) ke.Pressed.Should ().BeGreaterThanOrEqualTo ( 1 );
		else ke.Pressed.Should ().BeLessThan ( 1 );
		e.InputCode.Should ().Be ( (int)key );
	}

	public static void AssertProbe (ProbeHook probe, int pos, KeyCode key, VKChange change) {
		probe.Events.Should ().HaveCountGreaterThan ( pos );
		var e = probe.Events[pos];
		e.Item2.Should ().Be ( change );
		e.Item3.Should ().Be ( key );
	}

	private bool TestHook ( HInputEventDataHolder e ) {
		hookCaptures.Add ( e );
		return false; // False means that input event should be consumed instead of resending to another hook
	}
}

class TestStatus {
	public readonly string TestName;
	private readonly BaseIntegrationTest Tester;
	private readonly ITestOutputHelper Output;
	static readonly KeyCode[] keys = [KeyCode.H, KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.M];
	static readonly int KN = keys.Length;
	int index = 0;
	List<(ProbeHook, List<(VKChange vk, KeyCode key)>)> Probes = new ();
	Tuple<List<HInputEventDataHolder>, List<(VKChange, KeyCode)>> HookCaptures = null;
	List<(DateTime, int, int)> LatestWaitLog = [];

	public TestStatus ( BaseIntegrationTest tester, string testName ) {
		TestName = testName;
		Tester = tester;
		Output = tester.Output;
		InputManagementService.ClearNotificationLog ();
	}

	private bool TestHook ( HInputEventDataHolder e ) {
		HookCaptures.Item1.Add ( e );
		return false; // False means that input event should be consumed instead of resending to another hook
	}

	KeyCode Key => keys[index % KN];

	private bool WaitFcn () {
		(int cnt, int tar) = HookCaptures != null
			? (HookCaptures.Item1.Count, HookCaptures.Item2.Count)
			: (Probes[0].Item1.RawEvents.Count, Probes[0].Item2.Count);
		LatestWaitLog.Add ( (DateTime.Now, cnt, tar) );
		return cnt >= tar;
	}



	public void SendKey () {
		if ( HookCaptures == null && Probes.Count < 1 ) {
			Output.WriteLine ( "Nothing to test" );
			return;
		}

		InputManagementService.PushNotification ( -1, null, index, (nint)Key, 0 );
		LLInputLogger.Log ( IntPtr.Zero, 'S', $" - Testing InputSimulation by sending a key {Key}, part of test {TestName} #{index}" );
		Output.WriteLine ( $" - Sending key {Key}" );
		foreach ( var probeInfo in Probes ) {
			probeInfo.Item2.Add ( (VKChange.KeyDown, Key) );
			probeInfo.Item2.Add ( (VKChange.KeyUp, Key) );
			probeInfo.Item1.SetExpectedEvents ( VKChange.KeyDown, Key, true );
			probeInfo.Item1.SetExpectedEvents ( VKChange.KeyUp, Key );
		}
		if ( HookCaptures != null ) {
			HookCaptures.Item2.Add ( (VKChange.KeyDown, Key) );
			HookCaptures.Item2.Add ( (VKChange.KeyUp, Key) );
		}
		Tester.AssertExec ( $"sim keypress {Key}", "Sent 2 keyboard input (keypress) events." );

		string checkLog = string.Empty;
		try {
			BaseIntegrationTest.ActiveWait ( 50, 1, WaitFcn ).Should ().BeTrue ();
			foreach ( var probeInfo in Probes ) {
				checkLog += $"Probe #{Probes.IndexOf ( probeInfo )} received {probeInfo.Item1.Events.Count} events, expected {probeInfo.Item2.Count}\n";

				int N = probeInfo.Item2.Count;
				probeInfo.Item1.Events.Should ().HaveCount ( N );
				probeInfo.Item1.RawEvents.Should ().HaveCountGreaterThanOrEqualTo ( N ); // There might be other events that were rejected only after the basic processing
				for ( int i = 0; i < N; i++ )
					AssertProbe ( probeInfo.Item1, i, probeInfo.Item2[i].key, probeInfo.Item2[i].vk );
			}

		} catch ( Exception e ) {
			PrintStatus ( $" == Status of #{index} test ==" );
			Output.WriteLine ( checkLog );
			Output.WriteLine ( $"Error during test #{index} of {TestName}: {e.Message}" );
			Output.WriteLine ( "\n === LL Messages HTML format ===" );
			Output.WriteLine ( LLInputLogger.AsString () );
			throw;
		}
	}

	private void PrintStatus ( string header, bool counts = true, bool globalCaptures = true, bool notifications = true, bool probeStatus = true ) {
		System.Text.StringBuilder SB = new ( header + Environment.NewLine );
		if ( counts ) {
			(int, int)[] probeEvents = new (int, int)[Probes.Count];
			(int, int) hookEvents = (-1, -1);
			for ( int i = 0; i < Probes.Count; i++ )
				probeEvents[i] = (Probes[i].Item1.Events.Count, Probes[i].Item2.Count);
			if ( HookCaptures != null )
				hookEvents = (HookCaptures.Item1.Count, HookCaptures.Item2.Count);

			SB.AppendLine ( $"Hook event count status: {hookEvents.Item1}, expected {hookEvents.Item2}" );
			for ( int i = 0; i < Probes.Count; i++ )
				SB.AppendLine ( $"Probe {i} event count status: {probeEvents[i].Item1}, expected {probeEvents[i].Item2}" );
		}

		var llInput = Tester.Core.Fetch<DLowLevelInput> ();
		if ( llInput.ErrorList.Count > 0 ) {
			SB.AppendLine ( " ... error list ..." );
			llInput.PrintErrors ( ( s ) => SB.AppendLine ( s ) );
		} else SB.AppendLine ( "No logged known errors" );

		if ( globalCaptures ) {
			SB.AppendLine ( " ... LLHooks capture log ..." );
			foreach ( var ev in VWinLowLevelLibs.EventList )
				SB.AppendLine ( $"{ev.Item1} {ev.Item2} {ev.Item3}" );
		}

		if ( HookCaptures == null ) SB.AppendLine ( " -- No hook active --\n" );
		else {
			SB.AppendLine ( " ... Hook captures ..." );
			foreach ( var ev in HookCaptures.Item1 )
				SB.AppendLine ( $"{ev}" );
		}

		if ( notifications ) {
			SB.AppendLine ( " ... Probe notification log ..." );
			InputManagementService.ReadNotifications ( SB );
			SB.AppendLine ();
		}

		if ( probeStatus ) {
			for ( int i = 0; i < Probes.Count; i++ ) {
				SB.AppendLine ( $" ... Probe {i} settings ..." );
				SB.AppendLine ( $" Is consuming: {Probes[i].Item1.Consume}" );
				SB.AppendLine ( $" VKChanges: {string.Join ( ", ", Probes[i].Item1.Changes )}" );
				SB.AppendLine ( $" Keys: {Probes[i].Item1.KeyMask.ToString ()}" );
				SB.AppendLine ( $" HookID: {Probes[i].Item1.HookID}" );
				SB.AppendLine ( $" ... Probe {i} raw log ..." );
				foreach ( var ev in Probes[i].Item1.RawEvents )
					SB.AppendLine ( $"nCode: {ev.Item1}, wParam: {ev.Item2}, lParam: {ev.Item3}, eID: {ev.Item4}" );
				SB.AppendLine ( $" ... Probe {i} log ..." );
				foreach ( var ev in Probes[i].Item1.Events )
					SB.AppendLine ( $"Consumed: {ev.Item1}, Change: {ev.Item2}, Key: {ev.Item3}, eID: {ev.Item4}" );
				SB.AppendLine ();
			}
		}

		Output.WriteLine ( SB.ToString () );
	}

	public void AddHook () {
		if ( HookCaptures != null ) throw new Exception ( "Cannot add multiple hooks" );
		HookCaptures = new ( [], [] );
		Tester.cliWrapper.CmdProc.SetVar ( HookManagerCommand.INPHOOKCBVarName, (object)TestHook );
		Tester.AssertExecByRegex ( "hook add delayed Fcn -c KeyDown KeyUp", HookAddRegex ( "KeyDown, KeyUp", DHookManager.CBType.Delayed, "Fcn" ), "No hooks added." );
		Output.WriteLine ( "Hook added" );
		UpdateConsuming ();
		Assert ();
	}
	public void AddProbe () {
		var probe = Tester.Core.Fetch<VWinLowLevelLibs> ().InstallProbe
			( HookCaptures == null, [VKChange.KeyDown, VKChange.KeyUp], keys );
		Probes.Add ( (probe, new ()) );
		Output.WriteLine ( $"Probe added ({Probes.Count})" );
		UpdateConsuming ();
		Assert ();
	}
	public void RemoveHook () {
		if ( HookCaptures == null ) throw new Exception ( "No hook to remove" );
		Tester.AssertExecByRegex ( "hook remove KeyDown KeyUp", HookRemoveRegex ( "KeyDown, KeyUp" ), "No hooks removed." );
		HookCaptures = null;
		Output.WriteLine ( "Hook removed" );
		UpdateConsuming ();
		Assert ();
	}
	public void RemoveProbe () {
		if ( Probes.Count < 1 ) throw new Exception ( "No probes to remove" );
		Probes[^1].Item1.Dispose ();
		Probes.RemoveAt ( Probes.Count - 1 );
		Output.WriteLine ( $"Probe removed ({Probes.Count})" );
		UpdateConsuming ();
		Assert ();
	}

	private void UpdateConsuming () {
		//if ( Probes.Count < 1 ) return;
		// The probes should never consume when any hook is active. This is to ensure that the hook is working correctly. Otherwise, the probe would consume the event and the hook would never see it.
		foreach ( var probe in Probes ) probe.Item1.Consume = HookCaptures == null;
		//Probes[^1].Item1.Consume = HookCaptures == null;
	}

	public void Assert () {
		SendKey ();
		LLInputLogger.Log ( 69, 'A', $" - Assert by sending key {Key}, part of test {TestName} #{index} was finished" );
		index++;
	}

	public void RemoveAllProbes () {
		while ( Probes.Count > 0 ) RemoveProbe ();
	}

	public void Clear () {
		if ( HookCaptures != null ) RemoveHook ();
		RemoveAllProbes ();
	}
}