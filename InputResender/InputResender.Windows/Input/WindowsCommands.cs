using System.Collections.Generic;
using System.Windows.Forms;
using InputResender.Definitions;
using InputResender.Definitions.InputProcessing;
using MdxLibs.Core;

namespace InputResender.Windows.Input;
public class WindowsCommands : DCommand_IRCore {
	readonly ConsoleManager consoleManager;

	override public string Description => "Offers access to Windows specific functionalities";
	protected override bool PrintHelpOnEmpty => true;

	private static List<string> CommandNames = ["windows"];
	private static List<(string, System.Type)> InterCommands = [
		  ("load", null),
		  ("msgs", null),
	 ];

	public WindowsCommands ( DInputResenderCore owner, ConsoleManager console ) : base ( owner, null, CommandNames, InterCommands ) {
		consoleManager = console;
	}

	protected override CommandResult ExecIner ( CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
			"load" => $"{context.ParentAction} windows load [--force]: Loads Windows specific dependencies",
			"msgs" => $"{context.ParentAction} windows msgs <start|stop>: Starts processing windows message events",
			_ => $"Unknown action '{context.SubAction}'",
		}, out var helpRes ) ) return helpRes;
		switch ( context.SubAction ) {
		case "load":
			context.Args.RegisterSwitch ( 'F', "force", null );
			var actCore = context.CmdProc.GetVar<CoreBase> ( CoreManagerCommand.ActiveCoreVarName );
			if ( actCore.IsRegistered<DLowLevelInput> () ) {
				var llInput = actCore.Fetch<DLowLevelInput> ();
				if ( llInput.GetType () == typeof ( VWinLowLevelLibs ) ) {
					return new CommandResult ( "Windows dependencies already loaded." );
				}
				if ( context.Args.Present ( "--force" ) ) {
					do {
						actCore.Unregister ( actCore.Fetch<DLowLevelInput> () );
					} while ( actCore.IsRegistered<DLowLevelInput> () );
				} else return new CommandResult ( $"LowLevelInput is already registered with variant {llInput.GetType ().Name}. To override, use '--force' hint." );
			}
			new VWinLowLevelLibs ( actCore );
			return new CommandResult ( "Windows dependencies loaded." );
		case "msgs":
			if ( consoleManager == null ) return new ( "No ConsoleManager registered" );
			switch (context[1, "action"]) {
			default: return new ( $"Invalid action for windows msgs: {context[1]}" );
			case "start":
				consoleManager.OnIdle += MsgPeek;
				return new ( "Message loop now processing" );
			case "stop":
				consoleManager.OnIdle -= MsgPeek;
				return new ( "Messages no longer being processed" );
			}
		default: return new CommandResult ( "Invalid action." );
		}
	}

	private static void MsgPeek () {
		Application.DoEvents ();
	}
}