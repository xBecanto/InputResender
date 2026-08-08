using FluentAssertions;
using InputResender.CLI;
using InputResender.UnitTests.IntegrationTests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using InputResender.Definitions;
using InputResender.Definitions.Commands;
using InputResender.Variants.Commands;
using InputResender.Windows.Commands;
using InputResender.Windows.Input;
using MdxLibs.Core;
using MdxLibs.Services;
using MdxLibs.ServiceTests;
using SeClav.Commands;
using Xunit;
using Outputter = Xunit.Abstractions.ITestOutputHelper;
using SBld = System.Text.StringBuilder;

//[assembly: TestFramework ( "InputResender.UnitTests.ParallelXunitRunner", "InputResender.UnitTests" )]
namespace InputResender.UnitTests;
public abstract class GlobalCommandListTest {
	Outputter Output;

	public GlobalCommandListTest ( Outputter output ) => Output = output;

	[Fact]
	public void CommandsCanBeCorrectlyDetected () {
		GlobalCommandList CommandList = new ();
		// Check potential correctness of CommandList data and check existance of some known commands and loaders at random (to ensure the reflection actually worked, not a full check that all commands are present).

		// Also should test that callname of subcommand is same as its registered callname in the parent.

		var L = CommandList.Loaders.Keys.Should ();
		L.NotBeNull ();
		L.NotBeEmpty ();
		L.OnlyHaveUniqueItems ();
		L.OnlyContain ( t => t != null );
		L.OnlyContain ( t => !t.IsAbstract );
		L.OnlyContain ( t => t.IsSubclassOf ( typeof ( DCommandLoader_IRCore ) ) );
		L.Contain ( GlobalCommandList.LoadersExamples );

		FluentAssertions.Collections.GenericDictionaryAssertions<IDictionary<Type, List<Type>>, Type, List<Type>> F = CommandList.Loaders.Should ();
		F.NotBeNull ();
		F.NotBeEmpty ();
		F.OnlyHaveUniqueItems ( t => t.Key );
		F.OnlyContain ( t => t.Value != null );
		F.ContainKeys ( CommandList.Loaders.Keys );
		F.OnlyHaveUniqueListValues ();
		var Fi = F.Subject.Values.SelectMany ( v => v ).Should ();
		Fi.OnlyContain ( t => t != null );
		Fi.OnlyContain ( t => !t.IsAbstract );
		Fi.OnlyContain ( t => t.IsSubclassOf ( typeof ( DCommand_IRCore ) ) );

		var T = CommandList.AllCallNames.Keys.Should ();
		T.NotBeNull ();
		T.NotBeEmpty ();
		T.OnlyContain ( t => t != null );
		T.OnlyContain ( t => !t.IsAbstract );
		T.OnlyContain ( t => t.IsSubclassOf ( typeof ( DCommand_IRCore ) ) );
		T.OnlyContain ( t => !t.IsSubclassOf ( typeof ( DCommandLoader_IRCore ) ) );
		T.OnlyHaveUniqueItems ();
		T.Contain ( GlobalCommandList.CommandTypeExamples );

		var N = CommandList.AllCallNames.Should ();
		N.NotBeNull ();
		N.NotBeEmpty ();
		N.OnlyHaveUniqueItems ( t => t.Key );
		N.OnlyContain ( t => t.Value != null );
		N.ContainKeys ( CommandList.AllCallNames.Keys );
		N.OnlyHaveUniqueListValues ();
		var U = N.Subject.Values.SelectMany ( v => v ).Should ();
		U.OnlyContain ( t => !string.IsNullOrWhiteSpace ( t ) );

		var C = CommandList.CommandList.Should ();
		C.NotBeNull ();
		C.NotBeEmpty ();
		C.OnlyHaveUniqueItems ( t => t.Key );
		C.OnlyContain ( t => t.Value != null );
		C.ContainKeys ( CommandList.AllBaseCommandTypes );
		C.OnlyHaveUniqueListValues ();
		var A = C.Subject.Values.SelectMany ( v => v ).Should ();
		A.OnlyContain ( t => !string.IsNullOrWhiteSpace ( t ) );

		foreach ( (Type type, string cmd) in GlobalCommandList.CommandsExamples ) {
			CommandList.CommandList.Should ().ContainKey ( type )
				.WhoseValue.Contains ( cmd );
		}

		// Test some specific commands with special properties
		AssertLoaderContainsCommand ( typeof ( FactoryCommandsLoader ), typeof ( DebugCommand ) );
		AssertLoaderContainsCommand ( typeof ( FactoryCommandsLoader ), typeof ( InputCommandsLoader ) );
		AssertLoaderContainsCommand ( typeof ( InputCommandsLoader ), typeof ( InputSimulatorCommand ) );
		AssertLoaderContainsCommand ( typeof ( TopLevelLoader ), typeof ( LowLevelInputCommand ) );
		AssertSpecificCallname ( typeof ( BasicCommands ), "safemode" );
		AssertSpecificCallname ( typeof ( ContextVarCommands ), "context" );
		AssertSpecificCallname ( typeof ( SeClavRunnerCommand ), "seclav" );
		AssertSpecificCallname ( typeof ( SeClavModuleManagerCommand ), "seclav module" );
		AssertSpecificCallname ( typeof ( CoreCreatorCommand ), "core create" );
		AssertSpecificCallname ( typeof ( LowLevelInputCommand ), "hook inpll" );

		AssertSpecificCommands ( typeof ( CoreManagerCommand )
			, ["core", "core act", "core typeof", "core list"]
			, typeof ( CoreCreatorCommand ) );
		AssertSpecificCommands ( typeof ( CoreCreatorCommand )
			, ["core new", "core create", "core new comp", "core create comp"]
			, typeof ( CoreManagerCommand ) );

		void AssertSpecificCallname (Type cmdType, string callName)
			=> CommandList.AllCallNames
			.Should ().ContainKey ( cmdType )
			.WhoseValue.Contains ( callName );
		void AssertLoaderContainsCommand (Type loaderType, Type cmdType)
			=> CommandList.Loaders
			.Should ().ContainKey ( loaderType )
			.WhoseValue.Contains ( cmdType );
		void AssertSpecificCommand (Type cmdType, string cmd)
			=> CommandList.CommandList
			.Should ().ContainKey ( cmdType )
			.WhoseValue.Contains ( cmd );
		void AssertNotSpecificCommand (Type cmdType, string cmd)
			=> CommandList.CommandList
			.Should ().ContainKey ( cmdType )
			.WhoseValue.All ( c => c != cmd );
		void AssertSpecificCommands (Type ownerT, string[] cmds, params Type[] notOwner) {
			foreach ( var cmd in cmds )
				AssertSpecificCommand ( ownerT, cmd );
			foreach ( var notT in notOwner )
				foreach ( var cmd in cmds )
					AssertNotSpecificCommand ( notT, cmd );
		}

		if ( Output != null ) {
			System.Text.StringBuilder SB = new ();
			SB.AppendLine ( $"Detected {CommandList.AllCallNames.Keys.Count} commands in {CommandList.Loaders.Keys.Count} loaders." );
			SB.AppendLine ( " -- LOADERS -- ");

			List<List<string>> infoBlocks = [];
			foreach ( var loader in CommandList.Loaders ) {
				List<string> info = [$" - {loader.Key.Name}:"];
				for ( int i = 0; i < loader.Value.Count; i++ ) {
					var cmdT = loader.Value[i];
					string cmdInfo = $" . . {cmdT.Name}";
					if ( CommandList.AllBaseCommandTypes.Contains ( cmdT ) ) cmdInfo += "*";
					cmdInfo += " : ";
					cmdInfo += string.Join ( ", ", CommandList.AllCallNames.GetValueOrDefault ( cmdT ) ?? ["NONE"] );
					info.Add ( cmdInfo );
				}
				infoBlocks.Add ( info );
			}
			var columns = infoBlocks.SplitToColumns ( 2, 1 );
			var blocks = columns.BuildColumBlocks ( "\t| ", "\n" );
			foreach ( var block in blocks ) SB.AppendLine ( block );

			SB.AppendLine ( "\n -- COMMANDS -- " );
			//foreach ( var cmd in CommandList.CommandList ) {
			//	SB.Append ( $"\n - {cmd.Key.Name}:" );
			//	if ( CommandList.AllBaseCommandTypes.Contains ( cmd.Key ) ) SB.Append ( " *" );

			//	foreach (var cmdS in cmd.Value ) SB.Append ( $"\n   - {cmdS}" );
			//}

			infoBlocks.Clear ();
			foreach ( var cmd in CommandList.CommandList ) {
				List<string> info = [$" - {cmd.Key.Name}:"];
				if ( CommandList.AllBaseCommandTypes.Contains ( cmd.Key ) ) info[0] += "*";
				for ( int i = 0; i < cmd.Value.Count; i++ ) {
					var cmdS = cmd.Value[i];
					info.Add ( $" . . {cmdS}" );
				}
				infoBlocks.Add ( info );
			}
			columns = infoBlocks.SplitToColumns ( 3, 1 );
			blocks = columns.BuildColumBlocks ( "\t| ", "\n" );
			foreach ( var block in blocks ) SB.AppendLine ( block );

			Output.WriteLine ( SB.ToString () );
		}
	}
}

public abstract class GlobalCommandTest : BaseIntegrationTest {
	internal static readonly GlobalCommandList CommandList = new ();
	public Outputter Output;
	/*
	HELP EXAMPLE:
	if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => $"sim mousemove [-x <Xaxis>] [-y <Yaxis>]: Simulate mouse move event\n\t-x <Xaxis> = 0: X axis movement\n\t-y <Yaxis> = 0: Y axis movement", out var helpRes ) ) return helpRes;

	if ( TryPrintHelp ( context.Args, context.ArgID + 1, () =context.SubAction switch {
		"act" => "Prints the name of the active core.",
		"typeof" => "Prints the type of the component of thactive core.",
		_ => null
	}, out var helpRes ) ) return new ( null, helpRes.Message );

	sim (mousemove|keydown|keyup|keypress)
	  - Can simulate user hardware input
		EndPoint: IP end point or InMemNet point" to contain "tEP set ".
		-x <Xaxis> = 0: X axis movement
		-x <Xaxis>: X axis movement
		-x: X axis movement
		--XAxis <Xaxis> = 0: X axis movement
	  - Can simulate user hardware input

	.*?[a-zA-Z]+? .*?(( \([a-zA-Z]+?(\|[a-zA-Z]+?)*\))|( <\S+?>))+?.*?((
	\s+ - [a-zA-Z]+.*)|(
	\s+[a-zA-Z]+: .*)|(
	\s+((-[a-z])|(--[A-Z][a-zA-Z]+))( <[a-zA-Z]+>)?( = \S+)?: \S.*))+
		*/
	protected const string latin = "[a-zA-Z_-]+";
	//protected const string alphanum = "[a-zA-Z0-9_-]+";
	protected const string token = "[a-zA-Z][a-zA-Z0-9_-]*";
	protected const string alphanumSpace = $"{token}( {token})*";
	protected const string arg1 = $" \\({token}(\\|{token})*\\)";
	protected const string arg2 = $" <\\S+?>";
	protected const string sw = $"((-[a-z])|(--[a-zA-Z][a-zA-Z]+)|(<{token}>))";
	protected const string SW = $"(?:{sw}|(\\[-[a-z]\\|--[A-Z][a-zA-Z]+\\]))";
	protected const string line1 = $"{callname}.*?(({arg1})|({arg2}))*"; // Not restrictive enough. Since this is getting realllly large, custom regex processor is getting more and more tempting.
	protected const string line2 = $"\\n\\s+- {alphanumSpace}.*";
	protected const string line3 = $"\\n\\s+{alphanumSpace}: .*";
	protected const string line5 = $"\\n\\s+[+>] .*";
	protected const string line4 = $"\\n\\s+{SW}({arg2})*( = \\S+)?: \\S.*";
	public static readonly Regex helpRegex = new ( $"^{line1}((: [\\S ]*)|({line2})|({line3})|({line5})|({line4}))+$", RegexOptions.ExplicitCapture );

	protected const string callname = $"Usage:( ({latin})(\\[\\.(\\|({latin}))+\\])?)+";
	public static readonly Regex callnameRegex = new ( callname );

	public GlobalCommandTest ( Outputter output ) : base ( null, output, GeneralInitCmds ) {
		Output = output;
	}

	public static IEnumerable<object[]> CommandIterator () {
		foreach ( var comm in CommandList.CommandList ) {
			foreach ( var cmd in comm.Value ) {
				yield return [cmd, comm.Key];
			}
		}
	}

	protected static void AssertSubcommandHelp (string helpMsg) {
		string[] lines = helpMsg.Replace ( "\r\n", "\n" ).Split ( '\n' );
		SBld SB = null;

		for ( int i = 0; i < lines.Length; i++ ) {
			if ( SB != null ) {
				if ( lines[i].StartsWith ( " > " ) )
					SB.AppendLine ( lines[i][3..] );
				else {
					AssertHelpMsg ( null, SB.ToString () );
					SB.Clear ();
					SB = null;
				}
			}
			if ( SB == null ) {
				if ( !lines[i].StartsWith ( " +" ) ) continue;
				lines[i].Should ().StartWith ( " + Usage: ", "no line other than start of sub-command should start with '+' sign" );
				SB = new ();
				SB.AppendLine ( lines[i][3..] );
			}
		}
		if ( SB != null ) AssertHelpMsg ( null, SB.ToString () );
	}

	protected static void AssertHelpMsg ( string command, string helpMsg ) {
		helpMsg = helpMsg.TrimEnd ();
		helpMsg.Should ().NotBeNullOrWhiteSpace ().And
			.MatchAnyRegex ( [helpRegex] );
		if ( !string.IsNullOrWhiteSpace ( command ) )
			helpMsg.Should ().MatchRegex ( callnameRegex, command.Split ( ' ' ) );
		AssertSubcommandHelp ( helpMsg );
	}
}

public class GlobalCommandsAreTestedTest ( Outputter output ) : GlobalCommandTest ( output ) {
	[Fact]
	public void AllCommandsAreTested () {
		// This test gathers all implementations of 'ACommand' automatically using reflection.
		List<Type> unregistered = new ();
		foreach ( var cmdCls in CommandList.AllCallNames.Keys )
			if ( !CommandList.CommandList.ContainsKey ( cmdCls ) ) unregistered.Add ( cmdCls );
		if ( unregistered.Any () ) throw new CommandNotTestedUnregisteredException ( unregistered, CommandList.CommandList.Keys.ToList () );

		List<Type> withoutTests = new ();
		foreach ( var cmdInfo in CommandList.CommandList )
			if ( !cmdInfo.Value.Any () ) withoutTests.Add ( cmdInfo.Key );
		if ( withoutTests.Any () ) throw new CommandWithoutTestsException ( withoutTests, CommandList.CommandList.Keys.ToList () );
	}
}

public class GlobalCommandsCanBeLoadedTest ( Outputter output ) : GlobalCommandTest ( output ) {
	[Fact]
	public void AllCommandsCanBeLoaded () {
		List<Type> loaded = [typeof ( FactoryCommandsLoader ), typeof ( BasicCommands ), typeof(TopLevelLoader)];
		List<Type> waiting = CommandList.AllCallNames.Keys.Select ( t => t ).ToList ();
		waiting.AddRange ( CommandList.Loaders.Keys );
		foreach ( var T in loaded ) waiting.Remove ( T );

		while ( waiting.Any () ) {
			bool changed = false;
			foreach ( var loader in CommandList.Loaders ) {
				if ( !loaded.Contains ( loader.Key ) ) continue;
				foreach ( var cmdT in loader.Value ) {
					if ( loaded.Contains ( cmdT ) ) continue;
					if ( !waiting.Contains ( cmdT ) ) continue;
					changed = true;
					loaded.Add ( cmdT );
					waiting.Remove ( cmdT );

				}
			}
			if ( !changed ) throw new CommandLoadingException ( waiting );
		}
	}

	/*[Fact]
	public void AllCommandsInHelp () {
		var res = cliWrapper.ProcessLine ( "help" );
		res.Should ().NotBeNull ().And.NotBeOfType<ErrorCommandResult> ();
		string h = res.Message;
		Regex re = new ( $"^ \\. {ascii} - .*$", RegexOptions.Multiline );
		h.Should ().MatchRegex ( re );
		GlobalCommandList CmdList = new ();
		foreach ( var cmdAr in CmdList.CommandList.Values ) {
			foreach ( string cmd in cmdAr ) {
				string callname = cmd.Split ( ' ', StringSplitOptions.RemoveEmptyEntries )[0];
				h.Should ().Contain ( $". {callname} - " );
			}
		}
	}*/
}

public class GlobalCommandsInHelpTest ( Outputter output ) : GlobalCommandTest ( output ) {
	Regex HelpListRegex = new ( $"^{latin} - .*$", RegexOptions.Multiline );

	[Theory]
	[MemberData ( nameof ( CommandIterator ) )]
	public void AllCommandsInHelp_P (string command, Type owner) {
		var res = cliWrapper.ProcessLine ( "help" );
		res.Should ().NotBeNull ().And.NotBeOfType<ErrorCommandResult> ();
		string h = res.Message;
		h.Should ().MatchRegex ( HelpListRegex );
		string callname = command.Split ( ' ', StringSplitOptions.RemoveEmptyEntries )[0];
		callname.Should ().NotBeNullOrWhiteSpace ().And
			.Subject.Length.Should ().BeGreaterThan ( 1 );
		h.Should ().ContainAny ( [$"{callname} ", $"{callname}[.|", $"|{callname}|", $"|{callname}]", $"{callname}: "], $"variant of command call name '{callname}' from '{owner.Name}' should be present in help list" );
	}
}

public class GlobalCommandsHelpAvailableTest ( Outputter output ) : GlobalCommandTest ( output ) {
	[Theory]
	[MemberData ( nameof ( CommandIterator ) )]
	public void HelpAvailable_P ( string command, Type owner ) {
		string helpMsg = null;
		command += ' ';
		foreach ( string hs in DCommand_CoreBase.HelpSwitches ) {
			string cmd = command + hs;
			if ( helpMsg == null ) Output?.WriteLine ( $" > {cmd}" );
			var res = cliWrapper.ProcessLine ( cmd );
			if (res is ErrorCommandResult err) Output?.WriteLine ( $"Error: {err.Message}" );
			res.Should ().NotBeNull ().And.NotBeOfType<ErrorCommandResult> ();

			if ( helpMsg == null ) {
				Output?.WriteLine ( res.Message );

				AssertHelpMsg ( command, res.Message );
				helpMsg = res.Message;
			} else res.Message.Should ().Be ( helpMsg );
		}
	}
}