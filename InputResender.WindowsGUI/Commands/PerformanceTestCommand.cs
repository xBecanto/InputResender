using Components.Implementations;
using Components.Interfaces;
using Components.Interfaces.Commands;
using Components.Interfaces.SeClav;
using Components.Library;
using InputResender.Commands;
using InputResender.Services;
using SeClav;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace InputResender.WindowsGUI.Commands;
public class PerformanceTestCommand : DCommand<DMainAppCore> {
	public override string Description => "Performance tests for input processing subsystems";
	protected override bool PrintHelpOnEmpty => true;

	private static List<string> CommandNames = ["perf"];
	private static List<(string, Type)> InterCommands = [
		("sim-hook", null),
		("sim-pipeline", null),
		("cmd-hook", null),
		("emulated-roundtrip", null),
		("scl-startup", null),
		("scl-throughput", null),
		("net-receiver", null),
		("net-sender", null),
	];


	public PerformanceTestCommand ( DMainAppCore owner, string parentDsc = null )
		: base ( owner, parentDsc, CommandNames, InterCommands ) { }

	protected override CommandResult ExecIner ( CommandProcessor<DMainAppCore>.CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
			"sim-hook"            => CallName + " sim-hook [count] [--real]: Direct: simulate input and capture via basic hook\n\tcount: Repetitions (default 50)\n\t--real: Use VWinLowLevelLibs instead of MLowLevelInput",
			"sim-pipeline"        => CallName + " sim-pipeline [count] [--real]: Direct: simulate input through DInputReader->DInputMerger->DInputProcessor\n\tcount: Repetitions (default 50)\n\t--real: Use VWinLowLevelLibs instead of MLowLevelInput",
			"cmd-hook"            => CallName + " cmd-hook [count] [--real]: Command: simulate via 'sim' commands and capture in SHookManager\n\tcount: Repetitions (default 50)\n\t--real: Use VWinLowLevelLibs instead of MLowLevelInput\n\tNote: VInputSimulator.SimulateDelay is set to -1 for this test.",
			"emulated-roundtrip"  => CallName + " emulated-roundtrip [count]: Full mock roundtrip via pipeline system: DInputReader->DInputMerger->DInputProcessor->DDataSigner->DPacketSender (MPacketSender loopback)->DDataSigner->DInputSimulator\n\tcount: Repetitions (default 50)",
			"scl-startup"         => CallName + " scl-startup [count]: SCL: create runtime + assign @in vars + execute + read @out vars per iteration\n\tcount: Repetitions (default 50)",
			"scl-throughput"      => CallName + " scl-throughput [count]: SCL: execute a 5-state FSM script (each state: 10 ADD + 5 COMPARE, loops 11× before advancing)\n\tcount: Repetitions (default 50)",
			"net-receiver"        => CallName + " net-receiver: Real roundtrip RECEIVER — sets up 3 pipelines (receive/decrypt/simulate, capture/merge/process, process/encrypt/send) and prints listening IPs. Stays alive until process exits.",
			"net-sender"          => CallName + " net-sender <ip:port> [count]: Real roundtrip SENDER — connects to receiver, measures per-iteration latency\n\tip:port: Target receiver address (e.g. 192.168.1.1:45256)\n\tcount: Repetitions (default 10). Stats: avg, median, worst 1%.",
			_                     => Help
		}, out var helpRes ) ) return helpRes;

		context.Args.RegisterSwitch ( 'r', "real" );
		int count = context.Args.Int ( context.ArgID + 1, "count", true ) ?? 50;
		bool useReal = context.Args.Present ( "--real" );

		return context.SubAction switch {
			"sim-hook"           => RunSimHook ( context, count, useReal ),
			"sim-pipeline"       => RunSimPipeline ( context, count, useReal ),
			"cmd-hook"           => RunCmdHook ( context, count, useReal ),
			"emulated-roundtrip" => RunEmulatedRoundtrip ( context, count ),
			"scl-startup"        => RunSclStartup ( context, count ),
			"scl-throughput"     => RunSclThroughput ( context, count ),
			"net-receiver"       => RunNetReceiver ( context ),
			"net-sender"         => RunNetSender ( context ),
			_ => new CommandResult ( $"Unknown subcommand '{context.SubAction}'." )
		};
	}

	private CommandResult RunSimHook ( CommandProcessor<DMainAppCore>.CmdContext context, int count, bool useReal ) {
		int total = count * 2; // KeyDown + KeyUp per iteration
		int captured = 0;
		CountdownEvent countdown = useReal ? new CountdownEvent ( total ) : null;

		var testCore = CreateTestCore ( useReal, DMainAppCore.CompSelect.LLInput | DMainAppCore.CompSelect.InputReader );
		var reader = testCore.Fetch<DInputReader> ();
		var hookInfo = new HHookInfo ( reader, 0, VKChange.KeyDown, VKChange.KeyUp );
		var hookKeys = reader.SetupHook ( hookInfo, ( _, e ) => {
			Interlocked.Increment ( ref captured );
			if ( countdown is { IsSet: false } ) countdown.Signal ();
			return true;
		}, null );
		foreach ( var kvp in hookKeys ) hookInfo.AddHookID ( kvp.Value, kvp.Key );

		var sw = Stopwatch.StartNew ();
		for ( int i = 0; i < count; i++ ) {
			reader.SimulateInput ( new HKeyboardEventDataHolder ( reader, hookInfo, (int)KeyCode.A, VKChange.KeyDown ), true );
			reader.SimulateInput ( new HKeyboardEventDataHolder ( reader, hookInfo, (int)KeyCode.A, VKChange.KeyUp ), true );
		}
		bool completed = countdown?.Wait ( TimeSpan.FromSeconds ( 10 ) ) ?? true;
		sw.Stop ();
		reader.Clear ();
		countdown?.Dispose ();
		return FormatResult ( "sim-hook", total, captured, sw.ElapsedMilliseconds, completed ? null : "TIMEOUT" );
	}

	private CommandResult RunSimPipeline ( CommandProcessor<DMainAppCore>.CmdContext context, int count, bool useReal ) {
		// Doesn't actually execute pipelines, just emulates that by manually calling the components
		int total = count * 2;
		int processed = 0;
		CountdownEvent countdown = useReal ? new CountdownEvent ( total ) : null;

		var selector = DMainAppCore.CompSelect.LLInput | DMainAppCore.CompSelect.InputReader
			| DMainAppCore.CompSelect.InputMerger | DMainAppCore.CompSelect.InputProcessor;
		var testCore = CreateTestCore ( useReal, selector );
		var reader = testCore.Fetch<DInputReader> ();
		var merger = testCore.Fetch<DInputMerger> ();
		var processor = testCore.Fetch<DInputProcessor> ();

		var hookInfo = new HHookInfo ( reader, 0, VKChange.KeyDown, VKChange.KeyUp );
		var hookKeys = reader.SetupHook ( hookInfo, ( _, e ) => {
			var merged = merger.ProcessInput ( e );
			processor.ProcessInput ( merged ?? [] );
			Interlocked.Increment ( ref processed );
			if ( countdown is { IsSet: false } ) countdown.Signal ();
			return true;
		}, null );
		foreach ( var kvp in hookKeys ) hookInfo.AddHookID ( kvp.Value, kvp.Key );

		var sw = Stopwatch.StartNew ();
		for ( int i = 0; i < count; i++ ) {
			reader.SimulateInput ( new HKeyboardEventDataHolder ( reader, hookInfo, (int)KeyCode.A, VKChange.KeyDown ), true );
			reader.SimulateInput ( new HKeyboardEventDataHolder ( reader, hookInfo, (int)KeyCode.A, VKChange.KeyUp ), true );
		}
		bool completed = countdown?.Wait ( TimeSpan.FromSeconds ( 10 ) ) ?? true;
		sw.Stop ();
		reader.Clear ();
		countdown?.Dispose ();
		return FormatResult ( "sim-pipeline", total, processed, sw.ElapsedMilliseconds, completed ? null : "TIMEOUT" );
	}

	private CommandResult RunCmdHook ( CommandProcessor<DMainAppCore>.CmdContext context, int count, bool useReal ) {
		if ( useReal )
			return new CommandResult (
				"[perf/cmd-hook] --real flag has no effect here; cmd-hook uses the active core as-is."
			);

		var cmdProc = context.CmdProc;
		var aggregatedEvents = new List<string> ();
		cmdProc.SetVar ( "hookEvents", aggregatedEvents );

		var sim = cmdProc.Owner?.Fetch<VInputSimulator> ();
		if ( sim == null )
			throw new InvalidOperationException (
				"Active core does not have a VInputSimulator component, which is required for the cmd-hook test."
			);

		int prevDelay = sim.SimulateDelay;
		bool verbose = sim.Verbose;
		bool resend = sim.AllowRecapture;

		sim.SimulateDelay = -1;
		sim.Verbose = false;
		sim.AllowRecapture = true;

		cmdProc.ProcessLine ( "hook manager start" );
		cmdProc.ProcessLine ( "hook add fast Aggregate KeyDown KeyUp" );

		var sw = Stopwatch.StartNew ();
		for ( int i = 0; i < count; i++ ) {
			cmdProc.ProcessLine ( "sim keydown A" );
			cmdProc.ProcessLine ( "sim keyup A" );
		}

		sw.Stop ();

		sim.SimulateDelay = prevDelay;
		sim.Verbose = verbose;
		sim.AllowRecapture = resend;

		int totalCaptured = aggregatedEvents.Sum ( s => s.Split ( ' ', StringSplitOptions.RemoveEmptyEntries ).Length
		);
		return FormatResult ( "cmd-hook", count * 2, totalCaptured, sw.ElapsedMilliseconds );
	}

	private CommandResult RunEmulatedRoundtrip ( CommandProcessor<DMainAppCore>.CmdContext context, int count ) {
		// Doesn't perform full round-trip, only processing. Needs to go all the way through MPacketSender
		int total = count * 2;
		int received = 0;

		var selector = DMainAppCore.CompSelect.LLInput | DMainAppCore.CompSelect.InputReader
			| DMainAppCore.CompSelect.InputMerger | DMainAppCore.CompSelect.InputProcessor
			| DMainAppCore.CompSelect.DataSigner | DMainAppCore.CompSelect.PacketSender
			| DMainAppCore.CompSelect.ComponentJoiner;
		var testCore = new DMainAppCoreFactory { PreferMocks = true }.CreateVMainAppCore ( selector );

		var simulator = new VInputSimulator ( testCore );
		simulator.SimulateDelay = -1;

		var reader = testCore.Fetch<DInputReader> ();
		var joiner = testCore.Fetch<DComponentJoiner> ();
		var s1 = testCore.Fetch<DPacketSender> () as MPacketSender;

		testCore.Fetch<DDataSigner> ().Key = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

		DMainAppCoreFactory.AddJoiners ( testCore );

		joiner.RegisterPipeline (
			new ComponentSelector ( componentType: typeof ( DInputReader ) ),
			new ComponentSelector ( componentType: typeof ( DInputMerger ) ),
			new ComponentSelector ( componentType: typeof ( DInputProcessor ) ) );
		joiner.RegisterPipeline (
			new ComponentSelector ( componentType: typeof ( DInputProcessor ) ),
			new ComponentSelector ( componentType: typeof ( DDataSigner ) ),
			new ComponentSelector ( componentType: typeof ( DPacketSender ) ) );
		joiner.RegisterPipeline (
			new ComponentSelector ( componentType: typeof ( DPacketSender ) ),
			new ComponentSelector ( componentType: typeof ( DDataSigner ) ),
			new ComponentSelector ( componentType: typeof ( DInputSimulator ) ),
			new ComponentSelector ( componentType: typeof ( DInputSimulator ) ) );

		// Remote MPacketSender in a throwaway core acts as the network peer
		var tempCore = new DMainAppCoreFactory { PreferMocks = true }.CreateVMainAppCore ( DMainAppCore.CompSelect.None );
		var s2 = MPacketSender.Fetch ( 0, tempCore );
		s1.Connect ( s2 );

		// When s2 receives a packet, increment counter and trigger the receive pipeline on s1
		s2.OnReceive += ( msg, _ ) => {
			Interlocked.Increment ( ref received );
			DComponentJoiner.TrySend ( s1, null, msg, msg.Data );
			return DPacketSender.CallbackResult.None;
		};

		var hookInfo = new HHookInfo ( reader, 0, VKChange.KeyDown, VKChange.KeyUp );
		var hookKeys = reader.SetupHook ( hookInfo, ( _, e ) => {
			DComponentJoiner.TrySend ( reader, null, e );
			return true;
		}, null );
		foreach ( var kvp in hookKeys ) hookInfo.AddHookID ( kvp.Value, kvp.Key );

		var sw = Stopwatch.StartNew ();
		for ( int i = 0; i < count; i++ ) {
			reader.SimulateInput ( new HKeyboardEventDataHolder ( reader, hookInfo, (int)KeyCode.A, VKChange.KeyDown ), true );
			reader.SimulateInput ( new HKeyboardEventDataHolder ( reader, hookInfo, (int)KeyCode.A, VKChange.KeyUp ), true );
		}
		sw.Stop ();

		reader.Clear ();
		return FormatResult ( "emulated-roundtrip", total, received, sw.ElapsedMilliseconds );
	}

	private CommandResult RunSclStartup ( CommandProcessor<DMainAppCore>.CmdContext context, int count ) {
		const string scriptName = "perf-scl-startup";
		const string scriptCode =
			"@using BasicModule\n" +
			"@in Int a\n" +
			"@in Int b\n" +
			"@out Int sum\n" +
			"@out Int cmpResult\n" +
			"sum = ADD_INT a b\n" +
			"cmpResult = COMPARE_INT sum b\n";

		try {
			context.CmdProc.ProcessLine ( "load sclModules" ); // ignore result — modules may already be registered
			context.CmdProc.ProcessLine ( $"seclav parse {scriptName} --force --inline=\"{scriptCode}\"" );
		}
		catch ( Exception _ ) { }

		SeClavRunnerCommand sclCmd;
		try { sclCmd = (SeClavRunnerCommand)context.CmdProc.GetCommandInstance<SeClavRunnerCommand> (); }
		catch { return new CommandResult ( "[perf/scl-startup] SeClavRunnerCommand not available in this command processor." ); }
		var script = sclCmd.TryGetParsedScript ( scriptName );
		if ( script == null )
			return new CommandResult ( "[perf/scl-startup] Script parse failed. Ensure 'load sclModules' and SeClavRunnerCommand are available." );

		int executed = 0;

		var sw = Stopwatch.StartNew ();
		for ( int i = 0; i < count; i++ ) {
			int locI = i;
			var runtime = new SCLRuntimeHolder ( script );
			runtime.SetExternVar<BasicValueIntDef> ( "a", intDef => new BasicValueInt ( intDef, locI ), null );
			runtime.SetExternVar<BasicValueIntDef> ( "b", intDef => new BasicValueInt ( intDef, locI + 1 ), null );
			runtime.Execute ( false );
			if ( runtime.TryGetOutputVar ( "sum", null, out _ ) )
				executed++;
		}
		sw.Stop ();

		return FormatResult ( "scl-startup", count, executed, sw.ElapsedMilliseconds );
	}

	private CommandResult RunSclThroughput ( CommandProcessor<DMainAppCore>.CmdContext context, int count ) {
		const string scriptName = "perf-scl-throughput";
		string scriptCode = BuildThroughputScript ();

		try {
			context.CmdProc.ProcessLine ( "load sclModules" ); // ignore result
			context.CmdProc.ProcessLine ( $"seclav parse {scriptName} --force --inline=\"{scriptCode}\"" );
		}
		catch ( Exception _ ) { }

		var sclCmd = context.CmdProc.GetCommandInstance<SeClavRunnerCommand> () as SeClavRunnerCommand;
		if ( sclCmd == null )
			return new CommandResult ( "[perf/scl-throughput] SeClavRunnerCommand not available." );
		var script = sclCmd.TryGetParsedScript ( scriptName );
		if ( script == null )
			return new CommandResult ( "[perf/scl-throughput] Script parse failed. Ensure 'load sclModules' and SeClavRunnerCommand are available." );

		// 5 states × 11 loop iters × (1 counter ADD + 9 data ADDs + 5 COMPAREs + 1 loop-control COMPARE) = 5 × 11 × 16
		const int opsPerExec = 5 * 11 * 16;
		var runtime = new SCLRuntimeHolder ( script );

		var sw = Stopwatch.StartNew ();
		for ( int i = 0; i < count; i++ )
			runtime.Execute ( false );
		sw.Stop ();

		int opsPerMs = sw.ElapsedMilliseconds > 0 ? count * opsPerExec / (int)sw.ElapsedMilliseconds : 0;
		return FormatResult ( "scl-throughput", count, count, sw.ElapsedMilliseconds, $"{opsPerExec} ops/call, {opsPerMs} ops/ms" );
	}

	private static string BuildThroughputScript () {
		var sb = new StringBuilder ();
		sb.AppendLine ( "@using BasicModule" );
		sb.AppendLine ( "Int counter = 0" );
		sb.AppendLine ( "Int a = 1" );
		sb.AppendLine ( "Int b = 2" );
		sb.AppendLine ();

		string[] nextStates = ["State1", "State2", "State3", "State4", "Done"];
		for ( int i = 0; i < 5; i++ ) {
			// Non-accepting state; all transitions are explicit emits so implicit SUSPEND is never reached
			sb.AppendLine ( $"--> State{i} -loop-> State{i} -next-> {nextStates[i]}" );

			// 10 ADD_INT: counter increment + 9 data operations
			sb.AppendLine ( "counter = ADD_INT counter 1" );
			for ( int j = 1; j < 10; j++ )
				sb.AppendLine ( j % 2 != 0 ? "a = ADD_INT a b" : "b = ADD_INT a b" );

			// 5 COMPARE_INT for data work
			sb.AppendLine ( "COMPARE_INT a b" );
			sb.AppendLine ( "COMPARE_INT b a" );
			sb.AppendLine ( "COMPARE_INT counter a" );
			sb.AppendLine ( "COMPARE_INT a counter" );
			sb.AppendLine ( "COMPARE_INT b counter" );

			// Loop control: re-enter same state while counter < 11, then advance
			sb.AppendLine ( "COMPARE_INT counter 11" );
			sb.AppendLine ( "?< emit loop" );
			sb.AppendLine ( "counter = 0" );
			sb.AppendLine ( "emit next" );
			sb.AppendLine ();
		}

		// Accepting Done state: TERMINATE restores State0 in InactivePCs for the next Execute() call
		sb.AppendLine ( "--> [Done]" );
		return sb.ToString ();
	}

	private static DMainAppCore CreateTestCore ( bool useReal, DMainAppCore.CompSelect selector ) {		if ( !useReal )
			return new DMainAppCoreFactory { PreferMocks = false }.CreateVMainAppCore ( selector );

		return new VMainAppCore (
			_ => null,
			core => new VWinLowLevelLibs ( core ),
			core => new VInputReader_KeyboardHook ( core ),
			core => new VInputMerger ( core ),
			core => new VInputProcessor ( core ),
			_ => null, _ => null, _ => null, _ => null, _ => null, _ => null, _ => null,
			selector
		);
	}

	private static CommandResult FormatResult ( string testName, int simulated, int captured, long elapsedMs, string extra = null ) {
		double avgUs = simulated > 0 && elapsedMs > 0 ? elapsedMs * 1000.0 / simulated : 0;
		double eps = elapsedMs > 0 ? simulated * 1000.0 / elapsedMs : 0;
		string msg = $"[perf/{testName}] N={simulated} Captured={captured} | Total={elapsedMs}ms Avg={avgUs:F2}µs Throughput={eps:F0} ev/s";
		if ( extra != null ) msg += $" [{extra}]";
		return new CommandResult ( msg );
	}

	private CommandResult RunNetReceiver ( CommandProcessor<DMainAppCore>.CmdContext context ) {
		var sb = new StringBuilder ();
		// Set up the active core via commands: load joiners, set password, register echo pipeline, wire recv callback, print IPs.
		foreach ( var cmd in new[] {
			"load joiners",
			"core new comp mlowlevelinput",
			"core new comp vpacketsender",
			"hook manager start",
			"hook add fast Pipeline KeyDown KeyUp",
			"password add perf-roundtrip",
			"pipeline new perf-recv NetworkCallbacks DDataSigner DInputSimulator DInputSimulator",
			"pipeline new perf-process DInputReader DInputMerger DInputProcessor",
			"pipeline new perf-send DInputProcessor DDataSigner DPacketSender",
			"network callback recv pipeline",
			"network hostlist",
		} ) {
			var res = context.CmdProc.ProcessLine ( cmd );
			if ( !string.IsNullOrWhiteSpace ( res?.Message ) )
				sb.AppendLine ( res.Message.TrimEnd () );
		}
		sb.AppendLine ( "Core state was altered, please restart after the test is finished." );
		return new CommandResult ( sb.ToString ().TrimEnd () );
	}

	private CommandResult RunNetSender ( CommandProcessor<DMainAppCore>.CmdContext context ) {
		string targetAddr = context.Args.String ( context.ArgID + 1, "ip:port", 8, true );
		if ( string.IsNullOrWhiteSpace ( targetAddr ) )
			return new CommandResult ( "[perf/net-sender] Missing <ip:port>. Usage: perf net-sender <ip:port> [count]" );
		int count = context.Args.Int ( context.ArgID + 2, "count", true ) ?? 4;

		VRoundtripTrigger roundtripTrigger = new (context.CmdProc.Owner);

		foreach ( var cmd in new[] {
			"load joiners",
			"core new comp mlowlevelinput",
			"core new comp vpacketsender",
			"hook manager start",
			"hook add fast Pipeline KeyDown KeyUp",
			"password add perf-roundtrip",
			"pipeline new perf-sim DRoundtripTrigger DInputSimulator DInputSimulator",
			"pipeline new perf-capture DInputReader DInputMerger DInputProcessor",
			"pipeline new perf-send DInputProcessor DDataSigner DPacketSender",
			"pipeline new perf-recv NetworkCallbacks DDataSigner DInputSimulator DRoundtripTrigger",
			$"target set {targetAddr}",
		} ) context.CmdProc.ProcessLine ( cmd );

		var core = context.CmdProc.GetVar<DMainAppCore> ( CoreManagerCommand<DMainAppCore>.ActiveCoreVarName );
		if (core == null)
			throw new InvalidOperationException ( "[perf/net-sender] Active core not found in command processor." );
		var sender = core.Fetch<DPacketSender> ();
		var reader = core.Fetch<DInputReader> ();
		if ( sender == null || reader == null )
			return new CommandResult ( "[perf/net-sender] Required components (DPacketSender or DInputReader) not found in active core." );

		InputData testDataDown = new (this, KeyCode.A, true);
		InputData testDataUp = new (this, KeyCode.A, false);
		//var hookInfo = new HHookInfo ( reader, 0, VKChange.KeyDown );
		//var testEvent = new HKeyboardEventDataHolder ( reader, hookInfo, (int)KeyCode.A, VKChange.KeyDown );
		var ackSignal = new ManualResetEventSlim ( false );
		DPacketSender.OnReceiveHandler ackHandler = ( _, __ ) => { ackSignal.Set (); return DPacketSender.CallbackResult.None; };
		sender.OnReceive += ackHandler;

		var timesMs = new List<double> ( count );
		try {
			for ( int i = 0; i < count; i++ ) {
				ackSignal.Reset ();
				long t0 = Stopwatch.GetTimestamp ();
				DComponentJoiner.TrySend ( reader, null, i % 2 == 0 ? testDataDown : testDataUp );
				bool received = ackSignal.Wait ( TimeSpan.FromSeconds ( 10 ) );
				double ms = (Stopwatch.GetTimestamp () - t0) * 1000.0 / Stopwatch.Frequency;
				timesMs.Add ( received ? ms : -1.0 );
			}
		} finally {
			sender.OnReceive -= ackHandler;
			ackSignal.Dispose ();
		}

		var result = FormatNetRoundtripResult ( "net-sender", count, timesMs );
		return new CommandResult ( result.Message + "\nCore state was altered, please restart." );
	}

	private static CommandResult FormatNetRoundtripResult ( string testName, int total, List<double> timesMs ) {
		var received = timesMs.Where ( t => t >= 0 ).OrderBy ( t => t ).ToList ();
		int timeouts = total - received.Count;
		if ( received.Count == 0 )
			return new CommandResult ( $"[perf/{testName}] All {total} iterations timed out." );

		double avg = received.Average ();
		double median = received.Count % 2 == 1
			? received[received.Count / 2]
			: (received[received.Count / 2 - 1] + received[received.Count / 2]) / 2.0;
		int worst1pctCount = Math.Max ( 1, received.Count / 100 );
		double worst1pct = received.Skip ( received.Count - worst1pctCount ).Average ();

		string timeoutStr = timeouts > 0 ? $" Timeouts={timeouts}" : "";
		return new CommandResult (
			$"[perf/{testName}] N={received.Count}/{total}{timeoutStr} | Avg={avg:F2}ms Median={median:F2}ms Worst1%={worst1pct:F2}ms"
		);
	}
}