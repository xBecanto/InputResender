using System;
using System.Collections.Generic;
using System.Linq;
using MdxLibs.Core;
using MdxLibs.Definitions;
using MdxLibs.Services;
using MdxLibs.Definitions.Commands;

namespace SeClav.Commands;
public class SeClavRunnerCommand : DCommand_CoreBase {
	public readonly SeClavModuleManagerCommand ModuleManager;
	private readonly Dictionary<string, SCLScriptHolder> ParsedScripts;

	public SCLScriptHolder TryGetParsedScript ( string scriptName )
		=> ParsedScripts.GetValueOrDefault ( scriptName );

	public override string Description => "Parse and run SeClav scripts.";

	private static List<string> CommandNames = ["seclav"];
	private static List<(string, Type)> InterCommands = [
		("module", typeof(SeClavModuleManagerCommand))
		, ("list", null)
		, ("parse", null)
		, ("run", null)
		];

	public SeClavRunnerCommand ( CoreBase owner, string parentDsc = null ) : base ( owner, parentDsc, CommandNames, InterCommands ) {
		ModuleManager = new ( owner, CallName );
		ParsedScripts = new ();
	}

	protected override CommandResult ExecIner ( CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
			"parse" => CallName + " parse <Script>: Parse a SeClav script\n\tScript: The script to run, enclosed in quotes",
			"run" => CallName + " run <Script> [force]: Run a previously parsed SeClav script\n\tScript: The script to run, enclosed in quotes\n\tforce: Optional hint to force reparse if the script was already parsed",
			"list" => CallName + " list: List all parsed SeClav scripts",
			"module" => CallName + " module <Subcommand> [Options]: Manage valid modules for SeClav scripts\n\tSubcommand: {{list|add|remove|info}}\n\tOptions: Subcommand specific options",
			_ => null
		}, out var helpRes ) ) return helpRes;

		switch ( context.SubAction ) {
		case "list": return new CommandResult ( "Parsed Scripts:\n" +
			( ParsedScripts.Count == 0 ? "  (none)" :
			string.Join ( "\n", ParsedScripts.Keys.Select ( k => $"  - {k}" ) ) ) );
		case "module": return ModuleManager.Execute ( context.Sub () );
		case "parse": {
			string script = context.Args.String ( context.ArgID + 1, "Script" );
			if ( string.IsNullOrEmpty ( script ) )
				return new CommandResult ( "Script cannot be empty." );
			context.Args.RegisterSwitch ( 'f', "force" );
			context.Args.RegisterSwitch ( 'i', "inline" );
			bool alreadyContains = ParsedScripts.ContainsKey ( script );
			bool shouldForce = context.Args.Present ( "--force" );
			if ( alreadyContains && !shouldForce )
				return new ( $"Script '{script}' is already parsed. Use 'force' option to reparse." );

			//string code = context.Args.Present ( "--inline" )
			//	? context.Args.String ( "--inline", "code", 4 )
			//	: GetActiveCore<DInputResenderCore>().FileManager.GetWrapperOrSelf ().ReadFile ( script );
			string code;
			if ( context.Args.Present ( "--inline" ) ) code = context.Args.String ( "--inline", "code", 4 );
			else {
				var fileManager = GetActiveCore<DMdxCore> ().FileManager;
				string homePath = FileManagerCommand.GetHomePath ( context.CmdProc );
				code = fileManager.FileService.GetAssetPath ( homePath, script, FileAccessService.SearchOptions.AllExisting );
				if (string.IsNullOrWhiteSpace ( code ))
					return new ( $"Script '{script}' does not exist." );
				code = fileManager.GetWrapperOrSelf ().ReadFile ( code );
			}
			if ( string.IsNullOrEmpty ( code ) )
				return new ( $"Failed to load script from '{script}'." );

			SCLScriptHolder parsed = new ( code, script, ModuleManager.ModuleLoader, !context.CmdProc.SafeMode );
			ParsedScripts[script] = parsed;
			if ( parsed.Errors.Count > 0 ) {
				string errMsg = $"Script '{script}' parsed with {parsed.Errors.Count} errors:\n" + string.Join ( "\n", parsed.Errors.Select ( e => e.Item1 ) );
				return new CommandResult ( errMsg );
			}
			return new CommandResult ( $"Script '{script}' parsed successfully with {parsed.ParsedScript.Commands.Count} commands and {parsed.ParsedScript.DataTypes.Count} data types." );
		}
		case "run": {
			string script = context.Args.String ( context.ArgID + 1, "Script", shouldThrow: true );
			if ( string.IsNullOrEmpty ( script ) )
				return new CommandResult ( "Script cannot be empty." );
			if ( !ParsedScripts.TryGetValue ( script, out var parsed ) )
				return new CommandResult ( $"Script '{script}' is not parsed. Please parse it first." );
			if ( parsed.Errors.Count > 0 )
				return new CommandResult ( $"Script '{script}' has errors and cannot be run." );
			// Here should be the actual execution of the script. For now, just return a success message.
			return new CommandResult ( $"Script '{script}' executed successfully." );
		}
		default: return new CommandResult ( $"Unknown subcommand '{context.SubAction}'." );
		}
	}

	public override ComponentUIParametersInfo GetUIDescription () {
		var parsingSeparator = new UI_Separator.Factory ()
			.WithName ( "ParsingSeparator" )
			.WithLabel ( "Parsing" )
			.WithDescription ( "Separator for parsing section" )
			.Build ();
		var scriptPath = new UI_TextField.Factory ()
			.WithName ( "ScriptPath" )
			.WithLabel ( "Script Path" )
			.WithDescription ( "Path to the SeClav script to parse or run" )
			.WithInitialValue ( "-- Script Path --" )
			.ForceDynamic ()
			.Build<UI_TextField> ();
		var parseButton = new UI_ActionButton.Factory ()
			.WithOnClick ( () => {
					string cmd = $"{CallName} parse \"{scriptPath.Value}\"";
					var core = GetCore ( LastContext );
					string cmdRes = core.Fetch<CommandProcessor> ().ProcessLine ( cmd ).Message;
					scriptPath.ApplyValue ( cmdRes );
				}
			)
			.WithName ( "ParseButton" )
			.WithLabel ( "Parse Script" )
			.WithDescription ( "Parse the specified SeClav script" )
			.Build ();

		var parsedListing = new UI_ListView.Factory ()
			.WithName ( "ParsedScripts" )
			.WithLabel ( "Parsed Scripts" )
			.WithDescription ( "List of parsed SeClav scripts" )
			.WithPureUpdater ( () => ParsedScripts.Keys.ToList () )
			.Build ();
		var parsedScriptsSelector = new UI_DropDown.Factory ()
			.WithOptionUpdator ( () => ParsedScripts.Keys.ToList () )
			.WithName ( "ParsedScriptsSelector" )
			.WithLabel ( "Parsed Script Info" )
			.WithDescription ( "Select a parsed script to view its information" )
			.Build<UI_DropDown> ();
		var parsedScriptInfo = new UI_ListView.Factory ()
			.UpdatedByDropDown ( parsedScriptsSelector, ( selID ) => {
					if ( !ParsedScripts.TryGetValue ( parsedScriptsSelector.Value.options.ElementAt ( parsedScriptsSelector.Value.selID ), out var parsed ) )
						return ["Selected script is not available."];

					return [$"Name: {parsed.ScriptName}", $"Commands: {parsed.ParsedScript.Commands.Count}", $"Data Types: {parsed.ParsedScript.DataTypes.Count}", $"Errors: {parsed.Errors.Count}"];
				}
			)
			.WithName ( "ParsedScriptInfo" )
			.WithLabel ( "Parsed Script Information" )
			.WithDescription ( "Gets the info of the parsed script information." )
			.Build ();

		var managerSeparator = new UI_Separator.Factory ()
			.AsMajor ()
			.WithName ( "ManagerSeparator" )
			.WithLabel ( "Module Manager" )
			.WithDescription ( "Separator for module manager section" )
			.Build ();
		var managerUI = ModuleManager.GetUIDescription ();
		var ret = new ComponentUIParametersInfo.Factory ()
			.WithGroupID ( 0 )
			.WithComponentType ( GetType () )
			.AddParameters ( parsingSeparator, scriptPath, parseButton, parsedListing, parsedScriptsSelector, parsedScriptInfo, managerSeparator )
			.AddParameters ( managerUI.Parameters.ToArray () )
			.WithName ( "SeClav Runner Command" )
			.WithLabel ( "UI for SeClav Runner Command" )
			.WithDescription ( "Command for parsing and running SeClav scripts" )
			.Build () as ComponentUIParametersInfo;
		return ret;
	}
}

public class SeClavModuleManagerCommand : DCommand_CoreBase {
	private readonly Dictionary<string, IModuleInfo> loadedModules = [];
	private readonly Dictionary<string, IModuleInfo> availableModules = [];

	public override string Description => "Manage valid modules for SeClav scripts";

	private static List<string> CommandNames = ["module"];
	private static List<(string, Type)> InterCommands = [
		("list", null)
		, ("add", null)
		, ("remove", null)
		, ("info", null)
		];

	public SeClavModuleManagerCommand ( CoreBase owner, string parentDsc = null )
		: base ( owner, parentDsc, CommandNames, InterCommands ) {
		loadedModules = [];
		availableModules = [];
		// Maybe should contain command to search DLL for modules?
	}

	protected override CommandResult ExecIner ( CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
			"list" => CallName + " list: List all registered modules",
			"add" => CallName + " add <ModuleName>: Add a module to the valid modules list\n\tModuleName: Name of the module to add",
			"remove" => CallName + " remove <ModuleName>: Remove a module from the valid modules list\n\tModuleName: Name of the module to remove",
			"info" => CallName + " info <ModuleName>: Get information about a specific module\n\tModuleName: Name of the module to get info about",
			_ => null
		}, out var helpRes ) ) return helpRes;

		switch ( context.SubAction ) {
		case "list": {
			if ( loadedModules.Count == 0 )
				return new CommandResult ( "No modules loaded." );
			string res = "Loaded Modules:\n" + string.Join ( "\n", loadedModules.Keys );
			return new CommandResult ( res );
		}
		case "add": {
			string moduleName = context.Args.String ( context.ArgID + 1, "Module Name" );
			if ( string.IsNullOrEmpty ( moduleName ) )
				return new CommandResult ( "Module name cannot be empty." );
			if ( loadedModules.ContainsKey ( moduleName ) )
				return new CommandResult ( $"Module '{moduleName}' is already loaded." );
			if ( !availableModules.TryGetValue ( moduleName, out var module ) )
				return new CommandResult ( $"Module '{moduleName}' is not available." );
			loadedModules[moduleName] = module;
			return new CommandResult ( $"Module '{moduleName}' added." );
		}
		case "remove": {
			string moduleName = context.Args.String ( context.ArgID + 1, "Module Name" );
			if ( string.IsNullOrEmpty ( moduleName ) )
				return new CommandResult ( "Module name cannot be empty." );
			if ( !loadedModules.ContainsKey ( moduleName ) )
				return new CommandResult ( $"Module '{moduleName}' is not loaded." );
			loadedModules.Remove ( moduleName );
			return new CommandResult ( $"Module '{moduleName}' removed." );
		}
		case "info": {
			string moduleName = context.Args.String ( context.ArgID + 1, "Module Name" );
			if ( string.IsNullOrEmpty ( moduleName ) )
				return new CommandResult ( "Module name cannot be empty." );
			if ( !availableModules.TryGetValue ( moduleName, out var module ) )
				return new CommandResult ( $"Module '{moduleName}' is not available." );

			string res = $"Module Name: {module.Name}\nDescription: {module.Description}\nCommands: {string.Join ( ", ", module.Commands )}\nData Types: {string.Join ( ", ", module.DataTypes )}";
			return new CommandResult ( res );
		}
		default: return new CommandResult ( $"Unknown subcommand '{context.SubAction}'." );
		}
	}


	public void RegisterModule ( IModuleInfo module ) {
		ArgumentNullException.ThrowIfNull ( module );
		if ( availableModules.ContainsKey ( module.Name ) )
			throw new InvalidOperationException ( $"Module with name '{module.Name}' is already registered." );
		availableModules[module.Name] = module;
	}

	public IModuleInfo ModuleLoader ( string moduleName )
		=> availableModules.TryGetValue ( moduleName, out var module ) ? module : null;


	public override ComponentUIParametersInfo GetUIDescription () {
		var moduleList = new UI_ListView.Factory ()
			.WithName ( "ModuleList" )
			.WithLabel ( "Available Modules" )
			.WithDescription ( "List of available modules for SeClav scripts" )
			.WithPureUpdater ( () => availableModules.Keys.ToList () )
			.Build ();
		var separatorAboveInfo = new UI_Separator.Factory ()
			.WithName ( "SeparatorAboveInfo" )
			.WithLabel ( "Separator" )
			.WithDescription ( "Separator before module info" )
			.Build ();
		var moduleInfoSel = new UI_DropDown.Factory ()
			.WithOptionUpdator ( () => availableModules.Keys.ToList () )
			.WithName ( "ModuleInfoSel" )
			.WithLabel ( "Module Info" )
			.WithDescription ( "Select a module to view its information" )
			.Build<UI_DropDown> ();
		var moduleInfo = new UI_ListView.Factory ()
			.UpdatedByDropDown ( moduleInfoSel, ( selID ) => {
					if ( !availableModules.TryGetValue ( moduleInfoSel.Value.options.ElementAt ( moduleInfoSel.Value.selID ), out var module ) )
						return ["Selected module is not available."];
					return [$"Name: {module.Name}"
						, $"Description: {module.Description}"
						, $"Commands: {string.Join ( ", ", module.Commands )}"
						, $"Data Types: {string.Join ( ", ", module.DataTypes )}"];
				})
			.WithName ( "ModuleInfo" )
			.WithLabel ( "Module Information" )
			.WithDescription ( "Detailed information about the selected module" )
			.Build ();
		var ret = new ComponentUIParametersInfo.Factory ()
			.WithGroupID ( 0 )
			.WithComponentType ( GetType () )
			.AddParameters ( moduleList, separatorAboveInfo, moduleInfoSel, moduleInfo )
			.WithName ( "Input Simulator Command" )
			.WithLabel ( "UI for simulating user hardware input" )
			.WithDescription ( "Command for simulating user hardware input" )
			.Build () as ComponentUIParametersInfo;
		return ret;
	}
}