using Components.Implementations;
using Components.Implementations.UserApps;
using Components.Interfaces.Commands;
using Components.Library;
using Components.Library.ComponentSystem;
using InputResender.Commands;
using InputResender.WebUI.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using Components.Interfaces;

namespace InputResender.CLI; 
public class FactoryCommandsLoader : ACommandLoader<DMainAppCore> {
	private static Dictionary<Type, Func<DMainAppCore, DCommand<DMainAppCore>>> NewCommandList = new () {
		{ typeof(CoreManagerCommand<DMainAppCore>), ( core ) => new CoreManagerCommand<DMainAppCore> ( core ) },
		{ typeof(ConnectionManagerCommand), ( core ) => new ConnectionManagerCommand ( core ) },
		{ typeof(ComponentCommandLoader), ( core ) => new ComponentCommandLoader ( core ) },
		{ typeof(ContextVarCommands<DMainAppCore>), ( core ) => new ContextVarCommands<DMainAppCore> ( core ) },
		{ typeof(InputCommandsLoader), ( core ) => new InputCommandsLoader ( core ) },
		{ typeof(SeClavCommandLoader), ( core ) => new SeClavCommandLoader ( core ) },
		{ typeof(DebugCommand), ( core ) => new DebugCommand ( core ) },
		{ typeof(PWDCommand), ( core ) => new PWDCommand ( core ) },
		{ typeof(AutoCmdsCommand), ( core ) => new AutoCmdsCommand ( core ) },
		{ typeof(LoaderCommand), ( core ) => new LoaderCommand ( core ) },
		{ typeof(BlazorManagerCommand), ( core ) => new BlazorManagerCommand ( core ) },
		{ typeof(ExternalLoaderCommand), ( core ) => new ExternalLoaderCommand ( core ) },
		{ typeof(FileManagerCommand), ( core ) => new FileManagerCommand ( core ) },
	};
	private static Dictionary<Type, (string, Func<DCommand<DMainAppCore>, DCommand<DMainAppCore>>)> NewSubCommandList = new () {
		{ typeof (CoreCreatorCommand), ("core", ( parent ) => {
			RegisterSubCommand ( parent, new CoreCreatorCommand ( parent.Owner, parent.CallName ) );
			return null;
		} ) },
	};

	public FactoryCommandsLoader ( DMainAppCore owner ) : base ( owner, "generalCmds" ) { }
	protected override IReadOnlyCollection<Func<DMainAppCore, DCommand<DMainAppCore>>> NewCommands => NewCommandList.Values.Select<Func<DMainAppCore, DCommand<DMainAppCore>>, Func<DMainAppCore, DCommand<DMainAppCore>>>( f => core => f((DMainAppCore)core) ).ToList();

	protected override IReadOnlyCollection<(string, Func<DCommand<DMainAppCore>, DCommand<DMainAppCore>>)> NewSubCommands => NewSubCommandList.Values;
}

public class InputCommandsLoader : ACommandLoader<DMainAppCore> {
	public InputCommandsLoader ( DMainAppCore owner ) : base ( owner, "inputCmds" ) { }
	private static Dictionary<Type, Func<DMainAppCore, DCommand<DMainAppCore>>> NewCommandList = new () {
		{ typeof(InputSimulatorCommand), ( core ) => new InputSimulatorCommand ( core ) },
		{ typeof(HookManagerCommand), ( core ) => new HookManagerCommand ( core ) },
		{ typeof(ScriptedInputProcessorCommand), ( core ) => new ScriptedInputProcessorCommand ( core ) },
		{ typeof(VTapperInputCommand), ( core ) => new VTapperInputCommand ( core ) },
		{ typeof(VTapperLearner), ( core ) => new VTapperLearner ( core ) },
	};
	protected override IReadOnlyCollection<Func<DMainAppCore, DCommand<DMainAppCore>>> NewCommands
		=> NewCommandList.Values.Select<Func<DMainAppCore, DCommand<DMainAppCore>>, Func<DMainAppCore, DCommand<DMainAppCore>>>( f
			=> core => f((DMainAppCore)core) ).ToList();
}

public class SeClavCommandLoader : ACommandLoader<DMainAppCore> {
	public SeClavCommandLoader ( DMainAppCore owner ) : base ( owner, "seclavCmds" ) { }
	private static Dictionary<Type, Func<DMainAppCore, DCommand<DMainAppCore>>> NewCommandList = new () {
		{ typeof(SeClavRunnerCommand), ( core ) => new SeClavRunnerCommand ( core ) },
	};
	protected override IReadOnlyCollection<Func<DMainAppCore, DCommand<DMainAppCore>>> NewCommands
		=> NewCommandList.Values.Select<Func<DMainAppCore, DCommand<DMainAppCore>>, Func<DMainAppCore, DCommand<DMainAppCore>>>( f
			=> core => f((DMainAppCore)core) ).ToList();
}



public class LoaderCommand : DCommand<DMainAppCore> {
	public override string Description => "Loads various components, commands, data or configurations.";

	private static List<string> CommandNames = ["load"];
	private static List<(string, Type)> InterCommands = [
		("sclModules", null)
		, ("joiners", null)
		];
	public LoaderCommand ( DMainAppCore owner, string parentDsc = null )
		: base ( owner, parentDsc, CommandNames, InterCommands ) {
	}

	protected override CommandResult ExecIner ( CommandProcessor<DMainAppCore>.CmdContext context ) {
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
				new Components.Interfaces.SeClav.SCL_BasicModule ()
				, new SCL_Module ()
				];
			foreach ( var module in knownModules ) {
				try { sclRunner.ModuleManager.RegisterModule ( module ); }
				catch ( Exception _ ) { }
			}
			return new CommandResult ( $"Loaded {knownModules.Count} SeClav modules:\n" + string.Join ( "\n", knownModules.Select ( m => $"- {m.Name}: {m.Description}" ) ) );
		}
		case "joiners": {
			var core = context.CmdProc.Owner;
			if ( core is not Components.Interfaces.DMainAppCore dCore )
				return new CommandResult ( "Current core is not a DMainAppCore." );
			DMainAppCoreFactory.AddJoiners ( dCore );
			return new CommandResult ( "Loaded known component joiners into the system." );
		}
		default:
			return new CommandResult ( $"Unknown subcommand '{context.SubAction}'." );
		}
	}
}