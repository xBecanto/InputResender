using System;
using System.Collections.Generic;
using System.Linq;

namespace MdxLibs.Core;

public abstract class DCommandLoader_CoreBase : DCommand_CoreBase {
	public const string BaseLoadCmdName = "load-cmd";
	private string CmdGroupName { get; init; }
	public override string Description => $"Dynamically add '{CmdGroupName}' commands to the command processor";
	public override string Help => $"{parentCommandHelp} {CallName}";
	public DCommandLoader_CoreBase ( CoreBase owner, string cmdGroupName)
		: base ( owner, null, [BaseLoadCmdName + '-' + cmdGroupName], [] ) => CmdGroupName = BaseLoadCmdName + '-' + cmdGroupName;

	protected abstract IReadOnlyCollection<Func<CoreBase, DCommand_CoreBase>> NewCommands { get; }
	protected virtual IReadOnlyCollection<(string, Func<DCommand_CoreBase, DCommand_CoreBase>)> NewSubCommands => null;

	// Note that created command might not be actually added to the context
	protected sealed override CommandResult ExecIner ( CmdContext context ) {
		string ret = string.Empty;
		Dictionary<string, DCommand_CoreBase> commands = new ();
		Dictionary<string, Func<DCommand_CoreBase, DCommand_CoreBase>> subCommands = new ();
		Dictionary<string, DCommandLoader_CoreBase> cmdLoaders = new ();
		Queue<DCommandLoader_CoreBase> newLoaders = new ();

		PushCmds ( this );

		while ( newLoaders.Any () ) {
			var cmdLoader = newLoaders.Dequeue ();
			if ( cmdLoader == null ) continue;
			if ( cmdLoaders.ContainsKey ( cmdLoader.CmdGroupName ) ) continue;
			cmdLoaders.Add ( cmdLoader.CmdGroupName, cmdLoader );
			PushCmds ( cmdLoader );
		}

		foreach ( var cmd in commands ) {
			context.CmdProc.AddCommand ( cmd.Value, CommandProcessor.AddCmdBehavior.Skip );
			if ( !string.IsNullOrEmpty ( ret ) ) ret += Environment.NewLine;
			ret += cmd.Value.CallName;
		}

		foreach ( var subCmd in subCommands ) {
			context.CmdProc.ModifyCommand ( subCmd.Key, subCmd.Value );
		}

		return new CommandResult ( ret );

		void PushCmds ( DCommandLoader_CoreBase loader ) {
			if ( loader.NewCommands != null ) {
				foreach ( var cmdAdder in loader.NewCommands ) {
					DCommand_CoreBase cmd = cmdAdder ( Owner );
					if ( cmd == null ) continue;
					if ( commands.ContainsKey ( cmd.CallName ) ) continue;

					if ( cmd is DCommandLoader_CoreBase loaderCmd ) newLoaders.Enqueue ( loaderCmd );
					else commands.Add ( cmd.CallName, cmd );
				}
			}
			if ( loader.NewSubCommands != null ) {
				foreach ( var subCmdAdder in loader.NewSubCommands ) {
					if ( subCmdAdder.Item2 == null ) continue;
					if ( !subCommands.ContainsKey ( subCmdAdder.Item1 ) )
						subCommands.Add ( subCmdAdder.Item1, subCmdAdder.Item2 );
				}
			}
		}
	}
}