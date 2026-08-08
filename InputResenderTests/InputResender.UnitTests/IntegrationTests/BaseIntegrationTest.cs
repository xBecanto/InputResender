using FluentAssertions;
using System.Collections.Concurrent;
using InputResender.CLI;
using System.Collections.Generic;
using System.Windows.Forms;
using System;
using System.Threading.Tasks;
using Xunit.Abstractions;
using InputResender.Definitions;
using InputResender.Variants;
using InputResender.Windows.Commands;
using MdxLibs.Core;
using MdxLibs.DefinitionTests;

namespace InputResender.UnitTests.IntegrationTests;
public class BaseIntegrationTest : IDisposable {
	public static string[] InitCmdsList (params string[] extras) {
		List<string> ret = ["loadall", "safemode off", "core new", "core migrate act own skipSame", "core destroy", "core activate", "core new comp mpacketsender", .. extras]; //, "hook manager start" };
		return [.. ret];
	}
	public static readonly string[] GeneralInitCmds = InitCmdsList ();

	public static void ConsumeMessages () => Application.DoEvents ();
	public static bool ActiveWait ( int msTime, int msSafe, Func<bool> waiter, int minIters = 2 ) {
		DateTime end = DateTime.Now + TimeSpan.FromMilliseconds ( msTime );
		bool ret = waiter == null;
		while ( DateTime.Now < end || minIters-->=0 ) {
			if (waiter != null ) {
				if ( waiter () ) {
					if (msSafe > 0 ) {
						// Try to wait a little bit more if some excess messages arrive
						Task.Delay ( msSafe ).Wait ();
						ConsumeMessages ();
						waiter ().Should ().BeTrue ();
					}
					ret = true;
					break;
				}
			}
			ConsumeMessages ();
			Task.Delay ( 1 ).Wait ();
		}
		ConsumeMessages ();
		return ret;
	}
	public static void ActiveAssert ( int msTime, Action waiter ) {
		DateTime end = DateTime.Now + TimeSpan.FromMilliseconds ( msTime );
		while ( DateTime.Now < end ) {
			ConsumeMessages ();
			waiter ();
			Task.Delay ( 1 ).Wait ();
		}
	}

	private readonly BlockingCollection<string> StdIn, StdOut, StdErr;
	public readonly ConsoleManager console;
	public readonly CliWrapper cliWrapper;
	public readonly DInputResenderCore Core;
	public readonly List<CommandResult> CommandResults = new ();
	public ITestOutputHelper Output;
	public string Pre;

	//public CoreBase Core => cliWrapper.CmdProc?.Owner;

	public BaseIntegrationTest ( string pre, ITestOutputHelper output, params string[] initCmds ) {
		Pre = pre ?? string.Empty;
		Output = output;
		StdIn = new ();
		StdOut = new ();
		StdErr = new ();
		console = new ( StdOut.Add, StdIn.Take, Write, null, null );
		Core = DInputResenderCoreFactory.CreateDefault ();
		Core.FileManager.FileService = new MockFileService ();
		Core.FileManager.FileManagerWrapper = new FileManager_AutoAccept ( Core );
		cliWrapper = new ( Core, console );
		new Config ( "virtual", new ("password"), Core );

		cliWrapper.CmdProc.SetVar ( CliWrapper.CLI_VAR_NAME, cliWrapper );
		cliWrapper.CmdProc.AddCommand (
			new BasicCommands ( Core, console.WriteLine, console.Clear
				, () => throw new System.NotImplementedException ()
			), CommandProcessor.AddCmdBehavior.Skip
		);
		cliWrapper.CmdProc.AddCommand ( new TopLevelLoader ( Core ), CommandProcessor.AddCmdBehavior.Skip );
		//cliWrapper.CmdProc.AddCommand ( new FactoryCommandsLoader () );
		//cliWrapper.CmdProc.AddCommand ( new InputCommandsLoader () );
		//cliWrapper.CmdProc.AddCommand ( new SeClavCommandLoader () );

		foreach ( string cmd in initCmds ){
			var res = cliWrapper.ProcessLine ( cmd );
			if (res is ErrorCommandResult errRes )
				throw new Exception ( $"Error in command '{cmd}': {errRes.Message}\n{errRes.Exception}" );
			CommandResults.Add ( res );
		}

		if ( Core != null ) Core.OnError += WriteError;
	}

	public void Dispose () {
		if ( Core != null ) Core.OnError -= WriteError;
		cliWrapper.CmdProc.Dispose ();
		StdIn.Dispose ();
		StdOut.Dispose ();
		StdErr.Dispose ();
	}

	private void WriteError ( string text ) {
		StdErr.Add ( text );
		Output?.WriteLine ( Pre + text );
	}

	private void Write ( string text ) {
		if ( !StdOut.TryTake ( out string oldLine ) ) oldLine = string.Empty;
		oldLine += text;
		StdOut.Add ( oldLine );
	}

	private void AssertResultType ( CommandResult res ) {
		try {
			res.Should ().NotBeNull ().And.BeOfType<CommandResult> ();
		} catch ( Exception ex ) {
			if (res is ErrorCommandResult errRes) {
				Core?.PushDelayedError ( errRes.Message, ex );
				throw res.Exception;
			}
			Core?.FlushDelayedMsgs ( cliWrapper.Console.WriteLine );
			throw;
		}
	}

	public CommandResult AssertExec ( string cmd, string expRes, params string[] notRes ) {
		var res = cliWrapper.ProcessLine ( cmd );
		AssertResultType ( res );
		foreach ( string nr in notRes ?? [] )
			res.Message.Should ().NotBe ( nr ).And.NotContain ( nr );
		res.Message.Should ().Be ( expRes, $"The result of command '{cmd}', which is:\n\"{res.Message}\"\n  is expected to be\n\"{expRes}\"" );
		return res;
	}

	public CommandResult AssertExecByRegex ( string cmd, string regex, params string[] notRes ) {
		var res = cliWrapper.ProcessLine ( cmd );
		AssertResultType ( res );
		foreach ( string nr in notRes ?? [] )
			res.Message.Should ().NotBe ( nr ).And.NotContain ( nr );
		res.Message.Should ().MatchRegex ( regex );
		return res;
	}

	public ErrorCommandResult AssertExecError ( string cmd, string expRes, params string[] notRes ) {
		var res = cliWrapper.ProcessLine ( cmd );
		res.Should ().NotBeNull ().And.BeOfType<ErrorCommandResult> ();
		foreach ( string nr in notRes ?? [] )
			res.Message.Should ().NotBe ( nr ).And.NotContain ( nr );
		res.Message.Should ().Be ( expRes );
		return res as ErrorCommandResult;
	}
	public ErrorCommandResult AssertExecErrorByRegex ( string cmd, string regex, params string[] notRes ) {
		var res = cliWrapper.ProcessLine ( cmd );
		res.Should ().NotBeNull ().And.BeOfType<ErrorCommandResult> ();
		foreach ( string nr in notRes ?? [] )
			res.Message.Should ().NotBe ( nr ).And.NotContain ( nr );
		res.Message.Should ().MatchRegex ( regex );
		return res as ErrorCommandResult;
	}
}