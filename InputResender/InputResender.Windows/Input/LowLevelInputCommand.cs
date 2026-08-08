using System.Collections.Generic;
using System.Linq;
using InputResender.Definitions;
using MdxLibs.Core;

namespace InputResender.Windows.Input;
public class LowLevelInputCommand : DCommand_IRCore {
	override public string Description => "Low level hook (Win) access.";
	//override public string Help => $"Usage: {parentCommandHelp} inpll list <Selector> - {Description}\n\tSelector: (events|hooks|errors)";
	protected override bool PrintHelpOnEmpty => true;

	private static List<string> CommandNames = ["inpll"];
	private static List<(string, System.Type)> InterCommands = [("list", null)];

	public LowLevelInputCommand ( DCommand_CoreBase parent )
		: base ( parent.Owner as DInputResenderCore, parent.CallName, CommandNames, InterCommands ) {
	}

	override protected CommandResult ExecIner ( CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => CallName + " list <Selector>\n\tSelector: (events|hooks|errors)", out var helpRes ) ) return helpRes;

		VWinLowLevelLibs LLInput = context.CmdProc.Owner.Fetch<VWinLowLevelLibs> ();
		if ( LLInput == null ) return new ( "Low level input library not available." );

		switch ( context.SubAction ) {
		default: return new ( $"Invalid action '{context.SubAction}'." );
		case "list": {
			if (TryPrintHelp(context.Args, context.ArgID + 1, () => $"{CallName} list <Selector>\n\tSelector: (events|hooks|errors)", out helpRes)) return helpRes;
			switch ( context[1, "Selector"] ) {
			default: return new ( $"Invalid selector '{context[1]}'." );
			case "events":
				string retEv = string.Join ( ", ", VWinLowLevelLibs.EventList.Select ( e => $"{e.nCode}|{e.changeCode} = {e.inputData}" ) );
				VWinLowLevelLibs.EventList.Clear ();
				return new ( retEv );
			case "hooks":
				return new ( string.Join ( ", ", (LLInput.Info as VWinLowLevelLibs.VStateInfo)?.Hooks ) );
			case "errors":
				string retEr = string.Join ( ", ", LLInput.ErrorList );
				LLInput.ErrorList.Clear ();
				return new ( retEr );
			}
		}
		}
	}
}