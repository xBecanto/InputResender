using Xunit;
using FluentAssertions;
using System.Collections.Concurrent;
using InputResender.CLI;
using System.Collections.Generic;
using System.Windows.Forms;
using System;
using System.Threading.Tasks;
using Xunit.Abstractions;
using System.Linq;
using InputResender.Definitions;
using InputResender.Variants;
using InputResender.Variants.InputProcessing;
using InputResender.Windows.Commands;
using MdxLibs.Core;
using MdxLibs.DefinitionTests;

namespace InputResender.UnitTests.SystemTests;
public class SystemTest_CriticalError ( string message ) : Exception ( message );

public abstract class BaseSystemTest : IDisposable {
	readonly CliWrapper MainCliWrapper;
	readonly StandardStream StdStream;
	readonly Task MainTask;
	readonly DInputResenderCore Core;
	readonly ITestOutputHelper Output;
	bool closing = false;

	readonly List<(string cmd, CommandResult res)> CmdResults = [];
	const int DelayMult = 4;
	const string StartCmd = "print \"Main Started!\"";

	protected FileManagerSystemTestWrapper FileManagerWrapper { get; private set; }

	protected BaseSystemTest ( ITestOutputHelper output, params string[] initCmds ) : base () {
		Output = output;
		StdStream = new ();
		foreach ( string cmd in initCmds )
			StdStream.InputLine ( cmd );
		StdStream.InputLine ( StartCmd );

		System.IO.DirectoryInfo di = new ( AppDomain.CurrentDomain.BaseDirectory );
		while ( di != null && !di.GetFiles ( "config.xml" ).Any () && !di.GetFiles ( "SIPtest.scl" ).Any () )
			di = di.Parent;
		if ( di == null )
			throw new Exception ( "Could not find config.xml or SIPtest.scl in any parent directory of the current directory." );

		Core = DInputResenderCoreFactory.CreateDefault ();
		FileManagerWrapper = new ( Core );
		Core.FileManager.FileManagerWrapper = FileManagerWrapper;
		Core.OnError += ( msg ) => {
			lock (CmdResults ) {
				CmdResults.Add ( ("<ERROR>", new ErrorCommandResult ( msg )) );
				StdStream.OutputLine ( $"<ERROR> {msg}" );
			}
		};
		MainCliWrapper = new ( Core, StdStream.ConsoleWrapper );
		MainCliWrapper.OnCommandProcessed += ( cmd, res ) => {
			lock ( CmdResults ) {
				CmdResults.Add ( (cmd, res) );
			}
		};

		Program.StartMain (
			[$"cfg={di.FullName.Replace ( "\\", "\\\\" )} pass=asdf"]
			, new TopLevelLoader ( Core, StdStream.ConsoleWrapper )
			, MainCliWrapper
		).Should ().BeTrue ( "Main program should start successfully." );

		MainTask = new ( () => {
			Program.MainRun ( MainCliWrapper );
		} );
		MainTask.Start ();

		WaitUntilCmd ( StartCmd, 250, false, false, [] );
		ClearOutput ();
	}

	public CommandResult WaitUntilCmd (string cmd, int maxTimeout, bool allowErrors, bool wrongExact, string[] wrongOuts) {
		DateTime end = DateTime.Now + TimeSpan.FromMilliseconds ( maxTimeout * DelayMult );
		while ( DateTime.Now < end ) {
			if ( MainTask.IsCompleted ) throw new Exception ( "Main task has completed unexpectedly." );
			if ( MainTask.IsFaulted ) throw new Exception ( "Main task has faulted unexpectedly.", MainTask.Exception );

			lock(CmdResults) {
				for ( int i = CmdResults.Count - 1; i >= 0; i-- ) {
					if ( !allowErrors && CmdResults[i].res is ErrorCommandResult ecr ) {
						if ( ecr.Exception != null ) throw ecr.Exception;
						else throw new ( $"Command '{CmdResults[i].cmd}' failed with error: {ecr.Message}" );
					}

					if (wrongOuts.Any( wo => wrongExact
							? string.Equals ( CmdResults[i].res.Message, wo, StringComparison.OrdinalIgnoreCase )
							: CmdResults[i].res.Message.Contains ( wo, StringComparison.OrdinalIgnoreCase )
						) ) {
						throw new ( $"Command '{CmdResults[i].cmd}' produced unexpected output: '{CmdResults[i].res.Message}'" );
					}

					if ( CmdResults[i].cmd == cmd ) return CmdResults[i].res;
				}
			}
			Task.Delay ( 20 ).Wait ();
		}
		Output.WriteLine ( $"Timed out while waiting for command '{cmd}' to be processed." );
		Output.WriteLine ( "Last known output:" );
		lock (CmdResults) {
			for ( int i = CmdResults.Count - 1; i >= 0; i-- )
				Output.WriteLine ( $"Cmd: {CmdResults[i].cmd} | Res: {CmdResults[i].res}" );
		}
		throw new TimeoutException ( $"Couldn't find command '{cmd}' within the time period" );
	}

	public void Dispose () {
		if ( !closing ) {
			StopProgram ();
			Task.Delay ( 40 * DelayMult ).Wait ();
		}
		if ( !MainTask.Wait ( 1000 ) ) {
			throw new Exception ( "Main task did not complete within the timeout period." );
		}
		StdStream.Dispose ();
	}

	[System.Flags]
	protected enum TestSensitivity {
		None = 0,
		Order = 1,
		Case = 2,
		Exact = 4,
		Exclusive = 8,
		Single = 16,
		Errorous = 32,
	};

	protected enum TestTimeout {
		Immediate = 0,
		Short = 1,
		Medium = 2,
		Long = 3,
	};

	protected string[] ClearOutput () {
		var ret = StdStream.ReadAllOutput ();
		StdStream.ClearOutput ();
		return ret;
	}

	protected string[] Test ( string[] cmds, string[] expectedOuts, TestSensitivity sensitivity, TestTimeout timeout, params string[] wrongOuts ) {
		foreach ( string cmd in cmds )
			StdStream.InputLine ( cmd );
		int reps = timeout switch {
			TestTimeout.Short  => 8,
			TestTimeout.Medium => 32,
			TestTimeout.Long   => 240,
			_                  => throw new ArgumentException ( $"Unsupported timeout variant '{timeout}'" )
		};
		int maxTimeout = System.Diagnostics.Debugger.IsAttached ? 800 * 1000 : 200 * reps;
		// Wait until all commands are processed before starting the timeout for callbacks
		WaitUntilCmd ( cmds[^1], maxTimeout
			, sensitivity.HasFlag ( TestSensitivity.Errorous )
			, sensitivity.HasFlag ( TestSensitivity.Exact ), wrongOuts
		);
		Task.Delay ( 20 ).Wait ();

		if (timeout == TestTimeout.Immediate ) {
			return AssertFinal ( expectedOuts, wrongOuts, sensitivity );
		}
		for ( ; reps >= 0; reps-- ) {
			Task.Delay ( DelayMult * (System.Diagnostics.Debugger.IsAttached ? 20 * 1000 : 20) ).Wait ();
			try {
				return Assert ( expectedOuts, wrongOuts, sensitivity );
			} catch ( SystemTest_CriticalError ce ) {
				throw;
			} catch {}
		}
		return AssertFinal ( expectedOuts, wrongOuts, sensitivity );
	}

	private void StopProgram () {
		if ( closing )
			return;
		closing = true;
		StdStream.InputLine ( "exit" );
	}

	private string[] AssertFinal ( string[] expectedResults, string[] wrongOuts, TestSensitivity sensitivity) {
		try {
			return Assert ( expectedResults, wrongOuts, sensitivity );
		} catch ( Exception e ) {
			Output.WriteLine ( "Last known output:" );
			foreach ( string s in StdStream.ReadAllOutput () )
				Output.WriteLine ( s );
			throw;
		}
	}

	private string[] Assert ( string[] expectedOuts, string[] wrongOuts, TestSensitivity sensitivity ) {
		wrongOuts ??= [];
		var output = StdStream.ReadAllOutput ();
		List<int>[] matches = new List<int>[expectedOuts.Length];
		for ( int i = 0; i < matches.Length; i++ )
			matches[i] = [];

		StringComparison strComp = sensitivity.HasFlag ( TestSensitivity.Case ) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
		HashSet<int> unusedLines = new ( Enumerable.Range ( 0, output.Length ) );

		for ( int i = 0; i < output.Length; i++ ) {
			if ( wrongOuts.Any ( wo => sensitivity.HasFlag ( TestSensitivity.Exact )
					? string.Equals ( output[i], wo, strComp )
					: output[i].Contains ( wo, strComp )
				) )
				throw new SystemTest_CriticalError ( $"Found unexpected output: '{output[i]}'" );

			for ( int j = 0; j < expectedOuts.Length; j++ ) {
				if ( sensitivity.HasFlag ( TestSensitivity.Exact ) ) {
					if ( string.Equals ( output[i], expectedOuts[j], strComp ) ) {
						matches[j].Add ( i );
						unusedLines.Remove ( i );
					}
				} else {
					if ( output[i].Contains ( expectedOuts[j], strComp ) ) {
						matches[j].Add ( i );
						unusedLines.Remove ( i );
					}
				}
			}
		}

		for (int i = 0; i < matches.Length; i++) {
			if ( matches[i].Count == 0 ) {
				var SIP = MainCliWrapper.CmdProc.Owner.Fetch<VScriptedInputProcessor> ();
				throw new Exception ( $"Expected output '{expectedOuts[i]}' not found in actual output." );
			}
		}

		if (sensitivity.HasFlag(TestSensitivity.Order)) {
			for ( int i = 1; i < matches.Length; i++ ) {
				for ( int x = 0; x < matches[i].Count; x++ ) {
					for ( int y = 1; y < matches[i-1].Count; y++ ) {
						if ( matches[i][x] < matches[i-1][y] )
							throw new Exception ( $"Expected output {i} found before expected output {i-1}." );
					}
				}
			}
		}

		if (sensitivity.HasFlag(TestSensitivity.Single)) {
			for ( int i = 0; i < matches.Length; i++ ) {
				if ( matches[i].Count > 1 )
					throw new Exception ( $"Expected output '{expectedOuts[i]}' found multiple times in actual output." );
			}
		}

		if (sensitivity.HasFlag(TestSensitivity.Exclusive) && unusedLines.Count > 0 )
			throw new Exception ( $"Found unexpected output: {string.Join ( "\n", unusedLines.Select ( i => output[i] ) )}" );

		Output.WriteLine ( "Last known output:" );
		foreach ( string s in StdStream.ReadAllOutput () )
			Output.WriteLine ( s );
		return output;
	}
}