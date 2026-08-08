using System;
using System.Collections.Generic;
using InputResender.Definitions;
using InputResender.Windows.Input;
using MdxLibs.Core;

namespace InputResender.Windows.Commands;
public class TopLevelLoader : DCommandLoader_IRCore {
	readonly ConsoleManager consoleManager;

	public TopLevelLoader ( DInputResenderCore owner, ConsoleManager console = null)
		: base ( owner, "TopLevel") { consoleManager = console; }

	private static Dictionary<Type, Func<DInputResenderCore, DCommand_CoreBase>> NewCommandList ( TopLevelLoader self ) => new () {
		{ typeof(CLI.FactoryCommandsLoader), ( core ) => new CLI.FactoryCommandsLoader ( core )},
		{ typeof ( WindowsCommands ), ( core ) => new WindowsCommands ( core, self.consoleManager )},
		{ typeof ( PerformanceTestCommand ), ( core ) => new PerformanceTestCommand ( core )},
	};
	private static Dictionary<Type, (string, Func<DCommand_CoreBase, DCommand_CoreBase>)> NewSubCommandList = new () {
		{ typeof (LowLevelInputCommand), ("hook", ( parent ) => {
			RegisterSubCommand ( parent, new LowLevelInputCommand ( parent) );
			return null;
		}) },
	};

	protected override Dictionary<Type, Func<DInputResenderCore, DCommand_CoreBase>> NewCommands_IR => NewCommandList ( this );

	protected override Dictionary<Type, (string, Func<DCommand_CoreBase, DCommand_CoreBase>)> NewSubCommands_IR => NewSubCommandList;
}