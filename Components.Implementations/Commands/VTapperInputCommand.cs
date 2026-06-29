using Components.Interfaces;
using Components.Library;
using InputResender.Commands;
using ModE = Components.Interfaces.InputData.Modifier;

namespace Components.Implementations;
public class VTapperInputCommand : DCommand<DMainAppCore> {
	public override string Description => "Control TapperInput processor component.";

	private static List<string> CommandNames = ["tapper"];
	private static List<(string, Type)> InterCommands = [
		("status", null),
		("force", null),
		("wait", null),
		("keys", null),
		("mapping", null),
		("condition", null),
		("verbosity", null)
	];

	public VTapperInputCommand ( DMainAppCore owner, string parentDsc = null )
		: base ( owner, parentDsc, CommandNames, InterCommands ) { }

	protected override CommandResult ExecIner ( CommandProcessor<DMainAppCore>.CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
			"status" => CallName + " status: Print current status of the TapperInput processor.",
			"force" => CallName + " force <Key1> <Key2> <Key3> <Key4> <Key5> <Shift> <Shift>: Force assign VTapperInput as input processor." + EnumPar<KeyCode> ( "Key1-5" ),
			"wait" => CallName + " wait <ms>: Set wait time between taps in milliseconds.\n\tms: Time in milliseconds",
			"keys" => CallName + " keys <Key1> <Key2> <Key3> <Key4> <Key5> <Shift> <Shift>: Assign 5 trigger keys." + EnumPar<KeyCode> ( "Key1-5" ),
			"condition" => CallName + " condition <Modifier>: Set modifier condition for triggering.\n\tModifier: Modifier key that must be held for trigger to activate." + EnumPar<ModE> ( "Modifier" ),
			"mapping" => CallName + " mapping <MapName> <Combo> <TargetKey> [Modifier]: Set a key mapping entry.\n\tMapName: {Single|Double|Triple|Shift|Switch}\n\tCombo: 5-char pattern of X and - (e.g. -X-X- means keys 2 and 4 pressed)" + EnumPar<KeyCode> ( "TargetKey" ) + EnumPar<ModE> ( "Modifier" ),
			"verbosity" => CallName + " verbosity <Level>: Set verbosity level of the TapperInput processor.\n\tlevel: {0|1} (0 = minimal output, 2 = all messages)",
			_ => null
		}, out var helpRes ) ) return helpRes;

		DMainAppCore core = context.CmdProc.GetVar<DMainAppCore> ( CoreManagerCommand<DMainAppCore>.ActiveCoreVarName );
		if ( core == null ) return new ( "No active core found." );

		switch ( context.SubAction ) {
		case "status": {
			var proc = core.Fetch<DInputProcessor> ();
			if ( proc == null || proc is not VTapperInput tapper )
				return new ( "Input Processor is not a VTapperInput." );

			var (keys, shiftK, switchK, mod) = tapper.GetTriggerKeys ();
			return new (
				$"VTapperInput active\n" +
				$"  WaitTime: {tapper.WaitTime}ms\n" +
				$"  TriggerKeys: {string.Join ( ", ", keys.Select ( k => k.ToString () ) )}\n" +
				$"  TriggerMod: {mod}\n" +
				$"  Shift: {shiftK}\n" +
				$"  Switch: {switchK}\n" +
				$"  State: {tapper.PrintState ()}"
			);
		}
		case "force": {
			KeyCode[] keys = LoadKeys ( context, context.ArgID + 1 );

			var existing = core.Fetch<DInputProcessor> ();
			if ( existing != null ) {
				if ( existing is VTapperInput )
					return new ( "Input Processor is already VTapperInput." );
				core.Unregister ( existing );
			}
			new VTapperInput ( core, keys, ModE.None );
			return new ( $"VTapperInput assigned with keys [{string.Join ( ", ", keys.Select ( k => k.ToString () ) )}]." );
		}
		case "wait": {
			int ms = context.Args.Int ( context.ArgID + 1, "WaitTime (ms)", true ).Value;
			var proc = core.Fetch<DInputProcessor> ();
			if ( proc == null || proc is not VTapperInput tapper )
				return new ( "Input Processor is not a VTapperInput." );
			tapper.WaitTime = ms;
			return new ( $"WaitTime set to {ms}ms." );
		}
		case "keys": {
			var proc = core.Fetch<DInputProcessor> ();
			if ( proc == null || proc is not VTapperInput tapper )
				return new ( "Input Processor is not a VTapperInput." );

			var keys = LoadKeys ( context, context.ArgID + 1 );
			tapper.SetKeys ( keys );
			return new ( $"TriggerKeys set to [{string.Join ( ", ", keys.Select ( k => k.ToString () ) )}]." );
		}
		case "condition": {
			var proc = core.Fetch<DInputProcessor> ();
			if ( proc == null || proc is not VTapperInput tapper )
				return new ( "Input Processor is not a VTapperInput." );

			var mod = context.Args.EnumC<ModE> ( context.ArgID + 1, "Modifier", true );
			tapper.SetTriggers ( mod );
			return new ( $"Trigger modifier condition set to {mod}." );
		}
		case "mapping": {
			var proc = core.Fetch<DInputProcessor> ();
			if ( proc == null || proc is not VTapperInput tapper )
				return new ( "Input Processor is not a VTapperInput." );

			string mapName = context[1, "MapName"];
			int mapID = Array.IndexOf ( VTapperInput.MappingNames, mapName );
			if ( mapID < 0 )
				return new ( $"Invalid mapping name '{mapName}'. Use: {string.Join ( ", ", VTapperInput.MappingNames )}." );

			string comboPattern = context[2, "Combo"];
			int comboIndex;
			try { comboIndex = ParseComboPattern ( comboPattern ); }
			catch ( ArgumentException ex ) { return new CommandResult ( ex.Message ); }

			var targetKey = context.Args.EnumC<KeyCode> ( context.ArgID + 3, "TargetKey", true );
			ModE mod = ModE.None;
			if ( context.Args.ArgC > context.ArgID + 4 )
				mod = context.Args.EnumC<ModE> ( context.ArgID + 4, "Modifier" );

			tapper.SetMapping ( mapID, comboIndex, targetKey, mod );
			return new ( $"Mapping {mapName}[{comboPattern}={comboIndex}] set to {targetKey}" + (mod != ModE.None ? $" + {mod}" : "") + "." );
		}
		case "verbosity": {
			int verbosity = context.Args.Int ( context.ArgID + 1, "Verbosity level", true ).Value;
			var tapperProcessor = core.Fetch<VTapperInput> ();
			tapperProcessor.Verbose = verbosity > 0;
			return new ( $"Verbosity set to {tapperProcessor.Verbose}." );
		}
		default: return new ( $"Unknown sub-action '{context.SubAction}'." );
		}
	}

	private static KeyCode[] LoadKeys ( CommandProcessor<DMainAppCore>.CmdContext context, int startID ) {
		var keys = new KeyCode[7];
		for ( int i = 0; i < 5; i++ ) {
			var key = context.Args.EnumC<KeyCode> ( startID + i, $"Key{i}", true );
			keys[i] = key;
		}
		if (context.Args.Present ( startID + 5 )) {
			if (!context.Args.Present ( startID + 6 )) {
				throw new ArgumentException ( "Shift and Switch key must be set together." );
			}

			keys[5] = context.Args.EnumC<KeyCode> ( startID + 5, "ShiftKey", true );
			keys[6] = context.Args.EnumC<KeyCode> ( startID + 6, "SwitchKey", true );
		} else {
			keys[5] = KeyCode.CapsLock;
			keys[6] = KeyCode.Scroll;
		}

		return keys;
	}

	internal static int ParseComboPattern ( string pattern ) {
		if ( pattern == null || pattern.Length != 5 )
			throw new ArgumentException ( "Combo pattern must be exactly 5 characters (X or -).", nameof ( pattern ) );
		int combo = 0;
		for ( int i = 0; i < 5; i++ ) {
			if ( pattern[i] == 'X' || pattern[i] == 'x' ) combo |= 1 << i;
			else if ( pattern[i] != '-' )
				throw new ArgumentException ( $"Invalid character '{pattern[i]}' at position {i}. Use 'X' or '-'.", nameof ( pattern ) );
		}
		return combo;
	}
}