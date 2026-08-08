using System;
using System.Collections.Generic;
using FluentAssertions;
using MdxLibs.Core;
using Xunit;
using Xunit.Abstractions;
using LoaderFcn = System.Func<MdxLibs.Core.CoreBase, MdxLibs.Core.DCommand_CoreBase>;

namespace MdxLibs.CoreTests;
internal class CommandAdderA1 : DCommandLoader_CoreBase {
	public const string A1 = "A1";
	public const string A2 = "A2";
	public const string B1 = "B1";
	public const string B1a = "B1a";
	public const string B1b = "B1b";
	public const string CmdName = "adder1A";
	public const string Cmd = BaseLoadCmdName + "-" + CmdName;

	public CommandAdderA1 ( CoreBaseMock owner ) : base ( owner, CmdName ) { }

	override protected IReadOnlyCollection<LoaderFcn> NewCommands => [
		( owner ) => new CommandA1 ( (CoreBaseMock)owner ),
		( owner ) => new CommandA2 ( (CoreBaseMock)owner ),
		( owner ) => new CommandAdderB1 ( (CoreBaseMock)owner ),
	];

	DCommand_CoreBase CommandRegister(DCommand_CoreBase parent) {
		if ( parent is CommandB1 cmdB1 ) RegisterSubCommand ( cmdB1, new CommandB1b ( (CoreBaseMock)Owner, cmdB1 ) );
		return null;
	}

	protected override IReadOnlyCollection<(string, Func<DCommand_CoreBase, DCommand_CoreBase>)> NewSubCommands => new List<(string, Func<DCommand_CoreBase, DCommand_CoreBase>)> {
		( B1, CommandRegister )
	};
	//() => ( B1, new CommandB1b ( Help ) ),
}
internal class CommandAdderB1 : DCommandLoader_CoreBase {
	public const string CmdName = "adder2A";
	public const string Cmd = CommandAdderA1.Cmd + "-" + CmdName;

	public CommandAdderB1 ( CoreBaseMock owner ) : base ( owner, CmdName ) { }

	override protected IReadOnlyCollection<LoaderFcn> NewCommands => [
		( owner ) => new CommandB1 ( (CoreBaseMock)owner ),
	];
}

internal abstract class ATestCommand : DCommand_CoreBase {
	public string CommandName { get; init; }
	override public string Description => $"Test command {CommandName.ToUpper ()}";
	public ATestCommand ( CoreBaseMock owner, string parentHelp, IReadOnlyList<string> cmdNames, IReadOnlyList<(string, Type)> interCmds )
		: base ( owner, parentHelp, cmdNames, interCmds ) => CommandName = cmdNames[0];

	override protected CommandResult ExecIner ( CmdContext context ) => new ( $"Test command {CallName.ToUpper ()} executed." );
}
internal class CommandA1 : ATestCommand {
	private static List<string> CommandNames = [CommandAdderA1.A1];
	private static List<(string, Type)> InterCommands = [];
	public CommandA1 ( CoreBaseMock owner ) : base ( owner, null, CommandNames, InterCommands ) { }
}
internal class CommandA2 : ATestCommand {
	private static List<string> CommandNames = [CommandAdderA1.A2];
	private static List<(string, Type)> InterCommands = [];
	public CommandA2 ( CoreBaseMock owner ) : base ( owner, null, CommandNames, InterCommands ) { }
}
internal class CommandB1 : ATestCommand {
	private static List<string> CommandNames = [CommandAdderA1.B1];
	private static List<(string, Type)> InterCommands = [
		(CommandB1a.CmdName, typeof(CommandB1a))
		];
	CommandB1a subB2;
	public CommandB1 ( CoreBaseMock owner ) : base ( owner, null, CommandNames, InterCommands ) {
		subB2 = new CommandB1a ( owner, this );
		RegisterSubCommand ( subB2, subB2.CommandName );
	}
}
internal class CommandB1a : ATestCommand {
	public const string CmdName = CommandAdderA1.B1a;
	private static List<string> CommandNames = [CmdName];
	private static List<(string, Type)> InterCommands = [];
	public CommandB1a ( CoreBaseMock owner, CommandB1 parent )
		: base ( owner, parent.CallName, CommandNames, InterCommands ) { }
}
internal class CommandB1b : ATestCommand {
	private static List<string> CommandNames = [CommandAdderA1.B1b];
	private static List<(string, Type)> InterCommands = [];
	public CommandB1b ( CoreBaseMock owner, CommandB1 parent )
		: base ( owner, parent.CallName, CommandNames, InterCommands ) { }
}

public class CommandCreatorTest {
	const string A1 = CommandAdderA1.A1;
	const string A2 = CommandAdderA1.A2;
	const string B1 = CommandAdderA1.B1;
	const string B1a = $"{B1} {CommandAdderA1.B1a}";
	const string B1b = $"{B1} {CommandAdderA1.B1b}";
	static string[] AllCmds = new string[] { A1, A2, B1, B1a, B1b };

	private readonly ITestOutputHelper Output;

	public CommandCreatorTest ( ITestOutputHelper output ) {
		Output = output;
	}

	[Fact]
	public void HappyFlowTest () {
		CoreBaseMock core = new ();
		CommandProcessor context = new ( core, Output.WriteLine );
		context.AddCommand ( new CommandAdderA1 ( core ), CommandProcessor.AddCmdBehavior.Throw ); // Adding only the 'root' loader
		context.ProcessLine ( A1 ).Should ().BeOfType<ErrorCommandResult> ().Subject.Message.Should ().ContainAll ( "ommand", A1, "not found" );

		context.ProcessLine ( CommandAdderA1.Cmd ).Should ().BeOfType<CommandResult> ().Subject.Message.Should ().ContainAll ( new[] { A1, A2, B1 } );

		foreach ( var cmd in AllCmds ) {
			var res = context.ProcessLine ( cmd );
			res.Should ().NotBeNull ();
			res.Should ().NotBeOfType<ErrorCommandResult> ();
			res.Message.Should ().Be ( $"Test command {cmd.ToUpper ()} executed." );
		}
	}
}