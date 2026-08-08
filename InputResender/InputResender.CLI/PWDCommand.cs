using System;
using System.Collections.Generic;
using InputResender.Definitions;
using MdxLibs.Core;

namespace InputResender.CLI;
public class PWDCommand : DCommand_IRCore {
	public override string Description => "Command for Working Directory management";
	private static List<string> CommandNames = ["pwd"];
	private static List<(string, Type)> InterCommands = [
		("set", null)
		];

	public PWDCommand ( DInputResenderCore owner, string parentDsc = null )
		: base ( owner, parentDsc, CommandNames, InterCommands ) { }

	protected override CommandResult ExecIner ( CmdContext context ) {
		if ( context.Args.ArgC < context.ArgID + 1 )
			return new ( "HomePath=" + Owner.Fetch<Config> ().HomePath );

		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
			"set" => CallName + " set <Path>: Set the working directory\n\tPath: The new working directory path",
			_ => CallName + ": Show the current working directory"
		}, out var helpRes ) ) return helpRes;
		switch ( context.SubAction ) {
		case "set": {
			string path = context.Args.String ( context.ArgID + 1, "Path" );
			if ( string.IsNullOrEmpty ( path ) )
				return new ( "Path cannot be empty." );
			try {
				Owner.Fetch<Config> ().HomePath = path;
				return new ( $"Working directory set to '{Owner.Fetch<Config> ().HomePath}'." );
			} catch ( Exception ex ) {
				return new ( $"Failed to set working directory to '{path}': {ex.Message}" );
			}
		}
		default:
			return new ( $"Unknown subcommand '{context.SubAction}'." );
		}
	}
}