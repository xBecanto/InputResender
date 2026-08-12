using System;
using System.Collections.Generic;
using InputResender.Definitions;
using InputResender.Definitions.InputProcessing;
using InputResender.Variants.InputProcessing;
using MdxLibs.Core;

namespace InputResender.Variants.Commands;
public class ScriptedInputProcessorCommand : DCommand_IRCore {
	public override string Description => "Command to access scripted-based input processor.";

	private static List<string> CommandNames = ["SIP"];
	private static List<(string, Type)> InterCommands = [
		("status", null)
		, ("add", null)
		, ("assign", null)
		, ("safemode", null)
		];

	public ScriptedInputProcessorCommand ( DInputResenderCore owner, string parentDsc = null )
		: base ( owner, parentDsc, CommandNames, InterCommands ) {
	}

	protected override CommandResult ExecIner ( CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
			"status" => CallName + " status: Get the current status of the Scripted Input Processor.",
			"add" => CallName + " add [-s|--soft] [-f|--force]: Add the SIP as input processor.\n\rsoft: If some InputProcessor is already registered, deactivate it instead of unregistering.\n\rforce: Force the new SIP to be the only existing InputProcessor by removing any already existing ones.",
			"assign" => CallName + " assign <ScriptName> [-s|--safe]: Assign a compiled script to the SIP.\n\tScriptName: Name of the script to assign, compiled with 'seclav parse <ScriptFile>'.",
			"safemode" => CallName + " safemode: Enable or disable safe mode for the SIP.",
			_ => null,
		}, out var helpRes ) ) return helpRes;

		//var SCLcmder = context.CmdProc.GetCommandInstance<Interfaces.Commands.SeClavRunnerCommand> ();
		switch ( context.SubAction ) {
		case "status": {
			DInputResenderCore core = context.CmdProc.GetVar<DInputResenderCore> ( CoreManagerCommand.ActiveCoreVarName );
			if ( core == null ) return new CommandResult ( "No active core found." );
			var sip = core.Fetch<DInputProcessor> ();
			if ( sip == null || sip is not VScriptedInputProcessor scriptedSIP )
				return new CommandResult ( "Input Processor is not a SIP." );
			if ( scriptedSIP.Script == null )
				return new CommandResult ( "SIP running, no skript assigned." );
			return new CommandResult ( $"SIP running, assigned {(scriptedSIP.Script.IsUsingModule ( SIP_SCL_Module.ModuleName ) ? "integratable" : "non-integratable")} script '{scriptedSIP.Script.ScriptName}'." );
		}
		case "add": {
			DInputResenderCore core = context.CmdProc.GetVar<DInputResenderCore> ( CoreManagerCommand.ActiveCoreVarName );
			if ( core == null ) return new CommandResult ( "No active core found." );

			context.Args.RegisterSwitch ( 's', "soft" );
			context.Args.RegisterSwitch ( 'f', "force" );

			if ( context.Args.Present ( "--force" ) ) {
				while (true) {
					var existing = core.Fetch<DInputProcessor> ();
					if ( existing == null ) break;
					core.Unregister ( existing );
				}
			}

			var sip = core.Fetch<DInputProcessor> ();
			if (sip != null) {
				if ( sip is VScriptedInputProcessor )
					return new CommandResult ( "Input Processor is already SIP." );

				if ( context.Args.Present ( "--soft" ) ) sip.PipelineEnabled = false;
			}
			var newSIP = new VScriptedInputProcessor ( core );
			return new CommandResult ( "SIP assigned as Input Processor." );
		}
		case "assign": {
			context.Args.RegisterSwitch ( 's', "safe", "Run in safe mode" );
			string scriptName = context.Args.String ( context.ArgID + 1, "ScriptName" );
			if ( string.IsNullOrEmpty ( scriptName ) )
				return new CommandResult ( "ScriptName cannot be empty." );

			DInputResenderCore core = context.CmdProc.GetVar<DInputResenderCore> ( CoreManagerCommand.ActiveCoreVarName );
			if ( core == null ) return new CommandResult ( "No active core found." );
			var sip = core.Fetch<DInputProcessor> ();
			if ( sip == null || sip is not VScriptedInputProcessor scriptedSIP )
				return new CommandResult ( "Input Processor is not a SIP." );

			var sclCmd = context.CmdProc.GetCommandInstance<SeClav.Commands.SeClavRunnerCommand> ();
			if ( sclCmd == null )
				return new CommandResult ( "SeClavRunnerCommand not found in Command Processor." );
			var SCLcmder = sclCmd as SeClav.Commands.SeClavRunnerCommand;

			var parsedScript = SCLcmder.TryGetParsedScript ( scriptName );
			if ( parsedScript == null )
				return new CommandResult ( $"No parsed script found with name '{scriptName}'. Please parse it first using 'seclav parse <ScriptFile>'." );

			scriptedSIP.AssignScript ( parsedScript );
			scriptedSIP.ExecSafeMode = context.Args.Present ( "--safe" );
			return new CommandResult ( $"Script '{scriptName}' assigned to SIP." );
		}
		case "safemode": {
			DInputResenderCore core = context.CmdProc.GetVar<DInputResenderCore> ( CoreManagerCommand.ActiveCoreVarName );
			if ( core == null ) return new CommandResult ( "No active core found." );
			var sip = core.Fetch<DInputProcessor> ();
			if ( sip == null || sip is not VScriptedInputProcessor scriptedSIP )
				return new CommandResult ( "Input Processor is not a SIP." );

			string val = context.Args.String ( context.ArgID + 1, "Safemode value", 1, true )?.ToLower ();
			switch ( val ) {
				case "t": case "on":
					scriptedSIP.ExecSafeMode = true;
					return new CommandResult ( "SIP safe mode enabled." );
				case "f": case "off":
					scriptedSIP.ExecSafeMode = false;
					return new CommandResult ( "SIP safe mode disabled." );
				default: return new CommandResult ( $"Invalid safemode value '{val}'. Use 'on' or 'off'." );
			}
		}
		default: return new CommandResult ( $"Unknown sub-action '{context.SubAction}'." );
		}
	}
}
