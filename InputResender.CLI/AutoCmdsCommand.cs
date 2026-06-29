using Components.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Components.Interfaces;

namespace InputResender.CLI;
public class AutoCmdsCommand : DCommand<DMainAppCore> {
	public override string Description => "Manage automatic commands that run on specific events";
	protected override bool PrintHelpOnEmpty => true;
	private static List<string> CommandNames = ["auto", "autocmd", "autocmds"];
	private static List<(string, Type)> InterCommands = [
		("list", null),
		("run", null),
		("load", null)
		];
	public AutoCmdsCommand ( DMainAppCore owner, string parentDsc = null )
		: base ( owner, parentDsc, CommandNames, InterCommands ) { }

	private Dictionary<string, string[]> LoadedCmds = [];

	protected override CommandResult ExecIner ( CommandProcessor<DMainAppCore>.CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
			"list" => CallName + " list: List all automatic commands",
			"run" => CallName + " run <Name>: Run command group\n\tName: The name of the command group to run",
			"load" => CallName + " load <Path>: Load automatic commands from a file (currently not implemented)\n\tPath: The path to the file containing the commands",
			_ => Help
		}, out var helpRes ) ) return helpRes;
		switch ( context.SubAction ) {
		case "list": {
			context.Args.RegisterSwitch ( 'c', "compact" );
			var ACnames = Owner.Fetch<Config> ().ValidAutostartNames.ToArray ();
			if ( ACnames.Length == 0 )
				return new CommandResult ( "No automatic commands configured." );
			System.Text.StringBuilder sb = new ();
			bool verbose = !context.Args.Present ( "--compact" );
			sb.AppendLine ( "Automatic command groups:" );
			for ( int i = 0; i < ACnames.Length; i++ ) {
				sb.AppendLine ( $"[{i}] {ACnames[i]}:" );
				if ( verbose ) {
					var cmds = Owner.Fetch<Config> ().FetchAutoCommands ( ACnames[i] ).ToList ();
					if ( cmds == null || cmds.Count == 0 ) {
						sb.AppendLine ( "\t<No commands>" );
						continue;
					}
					for ( int j = 0; j < cmds.Count; j++ ) {
						sb.AppendLine ( $"\t#{j,2} |> {cmds[j]}" );
					}
				}
			}
			return new CommandResult ( sb.ToString () );
		}
		case "load": {
			context.Args.RegisterSwitch ( 'n', "node" );
			string name = context.Args.String ( context.ArgID + 1, "Path", 1, true );
			var fileManager = GetActiveCore<DMainAppCore> ().FileManager;
			string homePath = Components.Interfaces.Commands.FileManagerCommand.GetHomePath ( context.CmdProc );
			homePath = fileManager.FileService.GetAssetPath ( homePath, name, InputResender.Services.FileAccessService.SearchOptions.All );
			if ( string.IsNullOrWhiteSpace ( homePath ) )
				return new CommandResult ( $"Could not find file: {name}." );
			string content = fileManager.GetWrapperOrSelf ().ReadFile ( homePath );
			XmlDocument xmlDoc = new ();
			try {
				xmlDoc.LoadXml ( content );
				XmlElement root = xmlDoc.DocumentElement;
				XmlNode node = root;
				if ( context.Args.Present ( "--node" ) ) {
					string nodeName = context.Args.String ( context.ArgID + 2, "NodeName", 1, true );
					node = root.SelectSingleNode ( $"//{nodeName}" );
					if ( node == null ) return new ($"Could not find node: {nodeName}.");
				}

				int loaded = Config.LoadAutoCommands ( node, LoadedCmds );
				return new ($"Loaded {loaded} commands from {node.Name}.");
			}
			catch ( Exception e ) {
				return new ( e.Message );
			}
		} case "run": {
			string name = context.Args.String ( context.ArgID + 1, "Name" );
			if ( string.IsNullOrEmpty ( name ) )
				return new CommandResult ( "Name cannot be empty." );

			Config config = Owner.Fetch<Config> ();
			var cmds = config.FetchAutoCommands ( name );
			if ( cmds == null || cmds.Count == 0 )
				return new CommandResult ( $"No commands found for group '{name}'." );
			var cliWrapper = context.CmdProc.GetVar<CliWrapper> ( CliWrapper.CLI_VAR_NAME );
			int done = 0;
			foreach ( var cmd in cmds ) {
				if ( cmd.StartsWith ( '#' ) ) continue;

				Dictionary<string, (string, string)> ifAssign = [];
				Dictionary<string, (string, string)> ifNotAssign = [];

				string Command = cmd;
				while ( true ) {
					if ( Command.StartsWith ( "?", out Command ) ) {
						Command = Command.ExtractPrefix ( ' ', out string condition, 1, 1 );
						if ( condition.ContainsPrefix ( '=', out string prefix, out string suffix, 1, 1 ) ) {
							if ( suffix != config.GetEnv ( prefix ) ) continue;
						} else if ( config.GetEnv ( condition ) == null ) continue;

					} else if ( Command.StartsWith ( "=", out Command ) ) {
						Command = Command.ExtractPrefix ( ' ', out string assignVal, 5, 1 );
						assignVal = assignVal.ExtractPrefix ( ':', out string content, 1, 3 );
						assignVal = assignVal.ExtractPrefix ( '=', out string assignName, 1, 1 );
						ifAssign[content] = (assignName, assignVal);

					} else if ( Command.StartsWith ( "!", out Command ) ) {
						Command = Command.ExtractPrefix ( ' ', out string assignVal, 5, 1 );
						assignVal = assignVal.ExtractPrefix ( ':', out string content, 1, 3 );
						assignVal = assignVal.ExtractPrefix ( '=', out string assignName, 1, 1 );
						ifNotAssign[content] = (assignName, assignVal);
					} else
						break;
				}

				CommandResult cmdRes = null;
				cmdRes = Owner.Fetch<Config> ().PrintAutoCommands
					? cliWrapper.ProcessLine ( Command, true )
					: cliWrapper.CmdProc.ProcessLine ( Command );

				foreach ( var (content, (envName, envVal)) in ifAssign ) {
					if ( cmdRes.Message.Contains ( content ) ) config.SetEnv ( envName, envVal );
				}

				foreach ( var (content, (envName, envVal)) in ifNotAssign ) {
					if ( !cmdRes.Message.Contains ( content ) ) config.SetEnv ( envName, envVal );
				}

				done++;
			}
			return new ( $"Executed {done} commands from group '{name}'." );
		}
		default:
			return new ( $"Unknown subcommand '{context.SubAction}'." );
		}
	}
}