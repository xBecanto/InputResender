using System;
using System.Collections.Generic;
using MdxLibs.Core;

namespace MdxLibs.Definitions.Commands;
public class ExternalLoaderCommand : DCommand_MdxCore {
	private static List<string> CommandNames = ["externals"];
	private static List<(string, Type)> InterCommands = [("load", null)];
	public override string Description => "Loads external modules.";
	public ExternalLoaderCommand ( DMdxCore owner, string parentDsc = null )
		: base ( owner, parentDsc, CommandNames, InterCommands ) { }

	protected override CommandResult ExecIner ( CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
			"load" => $"{CallName} load <path> <loader>: Load external modules into the system.\n\tpath: The path to the external module file.\n\tloader: Name of the ACommandLoader type that will get loaded into command processor in active core.",
			_ => null
		}, out var helpRes ) ) return helpRes;

		var core = GetCore<DMdxCore> ( context );
		if ( core == null ) return new CommandResult ( "Core not found in context." );
		var loader = core.Fetch<DExternalLoader> ();
		loader ??= new MExternalLoader ( core );

		switch ( context.SubAction ) {
		case "load": {
			var fileManager = GetActiveCore<DMdxCore> ().FileManager;
			string path = FileManagerCommand.FindPath ( context, fileManager, 1 );
			if ( !fileManager.FileService.Exists ( path ) ) return new CommandResult ( $"File not found: {path}" );

			string loaderName = context.Args.String ( context.ArgID + 2, "Loader name", 1, true );
			var targetProcessor = core.Fetch<CommandProcessor> ();
			if ( targetProcessor == null ) return new CommandResult ( "Command processor not found in active core." );

			var extCmdLoader = loader.LoadExternal ( path, loaderName, context.Sub ().Args );
			if ( extCmdLoader == null ) return new CommandResult ( "Failed to load external command loader." );
			targetProcessor.AddCommand ( extCmdLoader, CommandProcessor.AddCmdBehavior.Throw );
			return new CommandResult ( $"Successfully loaded external command loader {loaderName} from {path}." );
		}
		default: return new CommandResult ( $"Unknown subcommand '{context.SubAction}'." );
		}
	}
}