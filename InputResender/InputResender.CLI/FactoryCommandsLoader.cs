using MdxLibs.Definitions.Commands;
using InputResender.WebUI.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using InputResender.Definitions;
using InputResender.Definitions.Commands;
using InputResender.Variants;
using InputResender.Variants.Commands;
using InputResender.Variants.InputProcessing;
using InputResender.Variants.UserApps;
using MdxLibs.Core;
using SeClav.Commands;
using SeClav.Modules;

namespace InputResender.CLI;
public class FactoryCommandsLoader : DCommandLoader_IRCore {
	private static Dictionary<Type, Func<DInputResenderCore, DCommand_CoreBase>> NewCommandList = new () {
		{ typeof(CoreManagerCommand), ( core ) => new CoreManagerCommand ( core ) },
		{ typeof(ConnectionManagerCommand), ( core ) => new ConnectionManagerCommand ( core ) },
		{ typeof(ComponentCommandLoader), ( core ) => new ComponentCommandLoader ( core ) },
		{ typeof(ContextVarCommands), ( core ) => new ContextVarCommands ( core ) },
		{ typeof(InputCommandsLoader), ( core ) => new InputCommandsLoader ( core ) },
		{ typeof(SeClavCommandLoader), ( core ) => new SeClavCommandLoader ( core ) },
		{ typeof(DebugCommand), ( core ) => new DebugCommand ( core ) },
		{ typeof(PWDCommand), ( core ) => new PWDCommand ( core ) },
		{ typeof(AutoCmdsCommand), ( core ) => new AutoCmdsCommand ( core ) },
		{ typeof(LoaderCommand), ( core ) => new LoaderCommand ( core ) },
		{ typeof(BlazorManagerCommand), ( core ) => new BlazorManagerCommand ( core ) },
		{ typeof(ExternalLoaderCommand), ( core ) => new ExternalLoaderCommand ( core ) },
		{ typeof(FileManagerCommand), ( core ) => new FileManagerCommand ( core ) },
		{ typeof(UpdateCommand), ( core ) => new UpdateCommand ( core ) },
	};
	private static Dictionary<Type, (string, Func<DCommand_CoreBase, DCommand_CoreBase>)> NewSubCommandList = new () {
		{ typeof (CoreCreatorCommand), ("core", ( parent ) => {
			RegisterSubCommand ( parent, new CoreCreatorCommand ( parent.Owner as DInputResenderCore, parent.CallName ) );
			return null;
		} ) },
	};

	public FactoryCommandsLoader ( DInputResenderCore owner ) : base ( owner, "generalCmds" ) { }
	protected override Dictionary<Type, Func<DInputResenderCore, DCommand_CoreBase>> NewCommands_IR => NewCommandList;

	protected override Dictionary<Type, (string, Func<DCommand_CoreBase, DCommand_CoreBase>)> NewSubCommands_IR => NewSubCommandList;
}

public class InputCommandsLoader : DCommandLoader_IRCore {
	public InputCommandsLoader ( DInputResenderCore owner ) : base ( owner, "inputCmds" ) { }
	private static Dictionary<Type, Func<DInputResenderCore, DCommand_CoreBase>> NewCommandList = new () {
		{ typeof(InputSimulatorCommand), ( core ) => new InputSimulatorCommand ( core ) },
		{ typeof(HookManagerCommand), ( core ) => new HookManagerCommand ( core ) },
		{ typeof(ScriptedInputProcessorCommand), ( core ) => new ScriptedInputProcessorCommand ( core ) },
		{ typeof(VTapperInputCommand), ( core ) => new VTapperInputCommand ( core ) },
		{ typeof(VTapperLearner), ( core ) => new VTapperLearner ( core ) },
	};
	protected override Dictionary<Type, Func<DInputResenderCore, DCommand_CoreBase>> NewCommands_IR => NewCommandList;
}

public class SeClavCommandLoader : DCommandLoader_IRCore {
	public SeClavCommandLoader ( DInputResenderCore owner ) : base ( owner, "seclavCmds" ) { }
	private static Dictionary<Type, Func<DInputResenderCore, DCommand_CoreBase>> NewCommandList = new () {
		{ typeof(SeClavRunnerCommand), ( core ) => new SeClavRunnerCommand ( core ) },
		{ typeof(SCL_REPL), ( core ) => new SCL_REPL ( core )},
	};
	protected override Dictionary<Type, Func<DInputResenderCore, DCommand_CoreBase>> NewCommands_IR => NewCommandList;
}



public class LoaderCommand : DCommand_IRCore {
	public override string Description => "Loads various components, commands, data or configurations.";

	private static List<string> CommandNames = ["load"];
	private static List<(string, Type)> InterCommands = [
		("sclModules", null)
		, ("joiners", null)
		];
	public LoaderCommand ( DInputResenderCore owner, string parentDsc = null )
		: base ( owner, parentDsc, CommandNames, InterCommands ) {
	}

	protected override CommandResult ExecIner ( CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
			"sclModules" => CallName + " sclModules: Load SeClav modules known to the system.",
			"joiners" => CallName + " joiners: Load known component joiners into the system.",
			_ => null
		}, out var helpRes ) ) return helpRes;

		switch ( context.SubAction ) {
		case "sclModules": {
			var sclCmd = context.CmdProc.GetCommandInstance<SeClavRunnerCommand> ();
			if ( sclCmd == null )
				return new CommandResult ( "SeClavRunnerCommand is not loaded." );
			if ( sclCmd is not SeClavRunnerCommand sclRunner )
				return new CommandResult ( "SeClavRunnerCommand is not of correct type." );

			List<SeClav.IModuleInfo> knownModules = [
				new SCL_BasicModule ()
				, new SIP_SCL_Module ()
				];
			foreach ( var module in knownModules ) {
				try { sclRunner.ModuleManager.RegisterModule ( module ); }
				catch ( Exception _ ) { }
			}
			return new CommandResult ( $"Loaded {knownModules.Count} SeClav modules:\n" + string.Join ( "\n", knownModules.Select ( m => $"- {m.Name}: {m.Description}" ) ) );
		}
		case "joiners": {
			var core = context.CmdProc.Owner;
			if ( core is not DInputResenderCore dCore )
				return new CommandResult ( "Current core is not a DInputResenderCore." );
			DInputResenderCoreFactory.AddJoiners ( dCore );
			return new CommandResult ( "Loaded known component joiners into the system." );
		}
		default:
			return new CommandResult ( $"Unknown subcommand '{context.SubAction}'." );
		}
	}
}