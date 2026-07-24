using Components.Interfaces.Commands;
using Components.Interfaces.SeClav.Parsing;
using Components.Library;
using SeClav;

namespace Components.Interfaces.SeClav;
public class SCL_REPL : DCommand<DMainAppCore> {
	public override string Description => "SeClav interactive shell";

	private static List<string> CommandNames = ["repl", "scli"];
	private static List<(string, Type)> InterCommands = [
		("reload", null),
		("write", null),
		("safemode", null),
		("read", null),
	];

	//private Func<string, IModuleInfo> moduleLoader;
	private SCLParsing parser;
	private SCLRuntimeExpandable runtime;
	private SCLRunner runner;
	private ISCLDebugInfo lastDebugInfo;
	private bool safeMode = true;
	private static readonly IReadOnlyList<SIdVal> emptyArgList = [];
	private int lastPC = 0;
	private List<string> execLog = [];

	public SCL_REPL ( DMainAppCore owner, string parentDsc = null ) : base ( owner, parentDsc, CommandNames, InterCommands ) {
		parser = null;
	}

	protected override CommandResult ExecIner ( CommandProcessor<DMainAppCore>.CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
			"reload" => CallName
				+ " reload: Reloads the REPL, reassigns relevant dependencies from actual state."
			, "write"    => CallName + " write [-p|--pause] <Command>: Process new line\n\rpause:Do not auto-execute\n\rCommand: One line of SeClav code."
			, "safemode" => CallName + " safemode on|off: Should execute in debug or release mode."
			, "read"	 => CallName + " read <varName>: Print a value of a given variable\n\rvarName: Name of the requested variable."
			, _          => null
		}, out var helpRes ) ) return helpRes;

		switch ( context.SubAction ) {
		case "reload": {
			var sclRunner = GetActiveCore<DMainAppCore> ()?.Fetch<SeClavRunnerCommand> ();
			if ( sclRunner == null )
				return new ErrorCommandResult ( "Could not find any active SeClavRunnerCommand" );

			parser = new (sclRunner.ModuleManager.ModuleLoader);
			lastPC = 0;
			return new ("SeClav REPL reloaded");
		}
		case "safemode": {
			safeMode = context.Args.Present ( "on" );
			return new ($"Safe mode {(safeMode ? "enabled" : "disabled")}.");
		}
		case "watchdog": {
			if ( runner == null )
				return new ErrorCommandResult ( "REPL is not initialized. Please run 'reload' first." );
			string subaction = context.Args.String ( context.ArgID + 1, "Watchdog subaction", 2, true );
			switch ( subaction ) {
			case "reset":
				runner.IncreaseWatchdog ( 1024 );
				return new ($"Watchdog reset. Current value: {runner.WatchdogMax}.");
			case "value":
				return new ($"Current watchdog value: {runner.WatchdogMax}.");
			default: return new ErrorCommandResult ($"Unknown watchdog subaction '{subaction}'." );
			}
		}
		case "write": {
			if (parser == null) return new ErrorCommandResult ( "REPL is not initialized. Please run 'reload' first." );
			if (context.Args.ArgC <= context.ArgID + 1) return new ErrorCommandResult ( "No command provided to write." );

			context.Args.RegisterSwitch ( 'p', "pause" );
			context.Args.RegisterSwitch ( 'f', "finalize" );
			context.Args.RegisterSwitch ( 'r', "relaxed" );
			System.Text.StringBuilder sb = new ();
			for (int i = context.ArgID + 1; i < context.Args.ArgC; i++) {
				sb.Append ( context.Args.String ( i, "" ) );
				if (i < context.Args.ArgC - 1) sb.Append ( ' ' );
			}

			try { parser.ProcessLine ( sb.ToString () ); }
			catch ( SCLDuplicateUsingException e ) { return new ($"Warning: Module '{e.ModuleName}' is already imported. Ignoring duplicate import."); }
			catch ( Exception e ) { return new ErrorCommandResult ( new ($"Error processing line: {e.Message}"), e ); }

			if ( context.Args.Present ( "--pause" ) )
				return new ($"Line processed and paused. Use 'write' without --pause to execute.");

			try {
				bool finalize = context.Args.Present ( "--finalize" );
				bool relaxed = context.Args.Present ( "--relaxed" );
				if ( runtime == null ) {
					lastDebugInfo = parser.GetResultWithDebugInfo ( finalize, relaxed );
					runtime = new (lastDebugInfo.Script);
					runner = new (lastDebugInfo.Script, 1024);
				} else {
					lastDebugInfo = parser.GetResultWithDebugInfo ( finalize, relaxed );
					runtime.Expand ( lastDebugInfo.Script );
					runner.UpdateScript ( lastDebugInfo.Script );
				}
			}
			catch ( Exception e ) { return new ErrorCommandResult ( new ($"Error expanding runtime: {e.Message}"), e ); }

			if ( safeMode ) {
				runner.ExecuteSafe ( runtime, emptyArgList, ref lastPC, ref execLog );
				System.Text.StringBuilder output = new ( $"Processed '{sb}' in safe mode. Execution log:\n" );
				for ( int i = 0; i < execLog.Count; i++ ) output.Append ( $"[{i}] {execLog[i]}\n" );
				execLog.Clear ();
				return new (output.ToString ());
			} else {
				runner.Execute ( runtime, emptyArgList, ref lastPC );
				return new ( $"Processed '{sb}'.");
			}
		}
		case "read": {
			if ( parser == null )
				return new ErrorCommandResult ( "REPL is not initialized. Please run 'reload' first." );
			if ( context.Args.ArgC <= context.ArgID + 1 )
				return new ErrorCommandResult ( "No variable name provided to read." );

			string varName = context.Args.String ( context.ArgID + 1, "" );
			if ( runtime == null )
				return new ErrorCommandResult ( "Runtime is not initialized. Please run 'write' first." );

			//var value = runtime.GetVariableValue ( varName );
			//lastDebugInfo.VarNames
			var possible = lastDebugInfo.VarNames.Where ( kvp => kvp.Value == varName ).ToList ();
			if ( possible.Count == 0 ) return new ErrorCommandResult ($"Variable '{varName}' not found." );
			if ( possible.Count > 1 )
				return new ErrorCommandResult ( $"Variable '{varName}' is ambiguous. Found {possible.Count} matches." );

			try {
				var value = runtime.SafeGetVar ( possible[0].Key.Generic );
				if ( value == null ) return new ErrorCommandResult ($"Variable '{varName}' not found." );

				return new ($"Variable '{varName}' has value: {value}");
			}
			catch ( Exception e ) {
				return new ErrorCommandResult ( new ($"Error retrieving variable '{varName}': {e.Message}"), e );
			}
		}
		default: return new CommandResult ( $"Unknown subcommand '{context.SubAction}'." );
		}
	}
}