using System;
using System.Collections.Generic;
using InputResender.Definitions;
using MdxLibs.Core;

namespace InputResender.CLI; 
public class DebugCommand : DCommand_IRCore {
	override public string Description => "Debugging commands";

	private static List<string> CommandNames = ["debug"];
	private static List<(string, Type)> InterCommands = [("throw", null), ("break", null)];

	public DebugCommand ( DInputResenderCore owner, DCommand_IRCore parent = null )
		: base ( owner, parent?.CallName, CommandNames, InterCommands ) {
	}

	protected override CommandResult ExecIner ( CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
			"throw" => CallName + " throw: Throws an exception",
			"break" => CallName + " break <where>: Setup a breakpoint\n\twhere: now/here - setup breakpoint inside the call, next - setup breakpoint just before entering next command",
			_ => null
		}, out var helpRes ) ) return helpRes;
		switch ( context.SubAction ) {
		case "throw": throw new Exception ( "Debug command throw" );
		case "break": {
			string where = context.Args.String ( context.ArgID + 1, "Where to place the breakpoint", 3, true );
			switch ( where ) {
			case "now":
			case "here":
				System.Diagnostics.Debugger.Break ();
				return new ($"Breakpoint should be processed.");
			case "next":
				context.CmdProc.BreakpointNext = true;
				return new ($"Breakpoint set for next command.");
			default: return new ($"Invalid breakpoint location '{where}'.");
			}
		}
		default: return new ( $"Invalid action '{context.SubAction}'." );
		}
	}
}