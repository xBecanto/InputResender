using System;
using System.Collections.Generic;
using System.Linq;
using InputResender.Definitions;
using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using InputResender.Variants.InputProcessing;
using MdxLibs.Core;
using MdxLibs.Definitions.Commands;

namespace InputResender.Variants.Commands;
public class VInputProcessorCommand : DCommand_IRCore {
	public override string Description => "Control the VInputProcessor component.";

	private static readonly List<string> CommandNames = ["inputproc"];
	private static readonly List<(string, Type)> InterCommands = [
		("status", null),
		("enable", null),
		("consume", null),
		("toggle", null),
		("hold", null),
		("release", null),
		("remap", null)
	];

	public VInputProcessorCommand ( DInputResenderCore owner, string parentDsc = null )
		: base ( owner, parentDsc, CommandNames, InterCommands ) { }

	protected override CommandResult ExecIner ( CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
			"add" => CallName + " add [-s|--soft] [-f|--force]: Add the SIP as input processor.\n\rsoft: If some InputProcessor is already registered, deactivate it instead of unregistering.\n\rforce: Force the new SIP to be the only existing InputProcessor by removing any already existing ones.",
			"status" => CallName + " status: Print the current VInputProcessor settings.",
			"enable" => CallName + " enable <on|off>: Enable or disable processing.",
			"consume" => CallName + " consume <pass|consume>: Set whether processed input should be consumed.",
			"toggle" => CallName + " toggle <Key>: Set the key used to toggle processing.",
			"hold" => CallName + " hold <Key>: Require the specified key to be held while processing.",
			"release" => CallName + " release <Key>: Require the specified key to be released while processing.",
			"target" => CallName + " target <Type>: Set the type of the final component in the processing pipeline.",
			"remap" => CallName + " remap <FromKey> <ToKey> [--clear]: Remap one key to another.",
			_ => null
		}, out var helpRes ) ) return helpRes;

		DInputResenderCore core = context.CmdProc.GetVar<DInputResenderCore> ( CoreManagerCommand.ActiveCoreVarName );
		if ( core == null ) return new ( "No active core found." );


		if ( context.SubAction == "add" ) {
			context.Args.RegisterSwitch ( 's', "soft" );
			context.Args.RegisterSwitch ( 'f', "force" );

			if ( context.Args.Present ( "--force" ) ) {
				while (true) {
					var existing = core.Fetch<DInputProcessor> ();
					if ( existing == null ) break;
					core.Unregister ( existing );
				}
			}

			var dProc = core.Fetch<DInputProcessor> ();
			if (dProc != null) {
				if ( context.Args.Present ( "--soft" ) ) dProc.PipelineEnabled = false;
			}
			dProc = new VInputProcessor ( core );
			return new ( "VInputProcessor assigned as Input Processor." );
		}

		var inputProcessor = core.Fetch<VInputProcessor> ();
		if ( inputProcessor == null )
			return new ErrorCommandResult ( "VInputProcessor is not registered. Use 'inputproc force' to register it." );

		try {
			switch ( context.SubAction ) {
			case "status":
				return new ( $"VInputProcessor status: ProcessingEnabled={inputProcessor.ProcessingEnabled}, ShouldConsume={inputProcessor.ShouldConsume}, Toggle={inputProcessor.Toggle}, OnHold={inputProcessor.OnHold}, OnRelease={inputProcessor.OnRelease}, Remaps={inputProcessor.Remap.Count}" );
			case "enable": {
				bool enabled = context.Args.Bool ( context.ArgID + 1, "Value", true);
				inputProcessor.ProcessingEnabled = enabled;
				return new ( enabled ? "Processing enabled." : "Processing disabled." );
			}
			case "consume": {
				if ( !TryParseConsume ( context, context.ArgID + 1, out var mode ) )
					return new ErrorCommandResult ( "Invalid consume value. Use 'consume', 'skip', or 'passthrough'." );
				inputProcessor.ShouldConsume = mode;
				return new ( $"Processed events will be marked for {mode}." );
			}
			case "toggle": {
				var key = context.Args.EnumC<KeyCode> ( context.ArgID + 1, "Key", true );
				inputProcessor.Toggle = key;
				return new ( $"Toggle key set to {key}." );
			}
			case "hold": {
				var key = context.Args.EnumC<KeyCode> ( context.ArgID + 1, "Key", true );
				inputProcessor.OnHold = key;
				return new ( $"OnHold key set to {key}." );
			}
			case "release": {
				var key = context.Args.EnumC<KeyCode> ( context.ArgID + 1, "Key", true );
				inputProcessor.OnRelease = key;
				return new ( $"OnRelease key set to {key}." );
			}
			case "target": {
				var typeName = context.Args.String ( context.ArgID + 1, "Type", 1, true );
				var typeSel = PipelineCommand.CreateSelector ( typeName, null, core );

				var sample = typeSel.Fetch ( core );
				TypeTree sampleTypeTree = new (sample);
				if (!sampleTypeTree.ToArray_VariantFirst ().Any( type => type.Name == typeName || type.FullName == typeName))
					return new ErrorCommandResult ( $"Component '{typeName}' not found in core." );

				inputProcessor.PipelineTarget = typeSel;
				return new ( $"Pipeline target set to {typeSel}." );
			}
			case "remap": {
				context.Args.RegisterSwitch ( 'c', "clear" );
				var fromKey = context.Args.EnumC<KeyCode> ( context.ArgID + 1, "FromKey", true );
				var toKey = context.Args.EnumC<KeyCode> ( context.ArgID + 2, "ToKey", true );
				if (context.Args.Present ( "--clear" ))
					inputProcessor.Remap.Clear ();
				if ( toKey == KeyCode.None ) inputProcessor.Remap.Remove ( fromKey );
				else inputProcessor.Remap[fromKey] = toKey;
				return new ( $"Remap set from {fromKey} to {toKey}." );
			}
			default: return new ErrorCommandResult ( $"Unknown sub-action '{context.SubAction}'." );
		}
		} catch ( Exception ex ) {
			return new ErrorCommandResult ( ex.Message );
		}
	}

	private static bool TryParseConsume ( CmdContext context, int argIndex, out DHookManager.ConsumingStatus mode ) {
		try {
			var raw = context.Args.String ( argIndex, "Mode", 1, true );
			if ( raw == null ) { mode = DHookManager.ConsumingStatus.Error; return false; }
			switch ( raw.Trim ().ToLowerInvariant () ) {
			case "c":
			case "consume":
				mode = DHookManager.ConsumingStatus.Consume;
				return true;
			case "p":
			case "pass":
			case "passthrough":
				mode = DHookManager.ConsumingStatus.Passthrough;
				return true;
			case "s":
			case "skip":
				mode = DHookManager.ConsumingStatus.Skip;
				return true;
			default:
				mode = DHookManager.ConsumingStatus.Error;
				return false;
		}
		} catch ( Exception ) {
			mode = DHookManager.ConsumingStatus.Error;
			return false;
		}
	}
}

