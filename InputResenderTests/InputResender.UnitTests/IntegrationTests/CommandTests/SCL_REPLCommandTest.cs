using Xunit;
using Xunit.Abstractions;

namespace InputResender.UnitTests.IntegrationTests.CommandTests;
public class SCL_REPLCommandTest : BaseIntegrationTest {
	public SCL_REPLCommandTest ( ITestOutputHelper output )
		: base ( null, output, InitCmdsList ( "load sclModules" ) ) { }

	[Fact]
	public void SupportsBasicVariableWorkflow () {
		LoadREPL ();
		AssertExecByRegex ( "scli safemode off", @"Safe mode disabled\." );

		WriteLine ( "@using BasicModule" );
		WriteLine ( "String msg" );
		WriteLine ( "msg = \"Hello World!\"" );
		WriteLine ( "Int num = 42" );
		WriteLine ( "num = ADD_INT num 25" );
		WriteLine ( "msg = APPEND_INT_TO_STR msg num" );

		AssertVar ( "num", "67" );
		AssertVar ( "msg", "Hello World!67" );
	}

	[Fact]
	public void ReportsErrorsForMissingArgumentsAndUninitializedState () {
		AssertExecErrorByRegex ( "scli read num", @"REPL is not initialized" );

		LoadREPL ();
		WriteLine ( "@using BasicModule" );
		AssertExecErrorByRegex ( "scli write", @"No command provided to write\." );

		WriteLine ( "String msg" );
		AssertExecErrorByRegex ( "scli read missingVar", @"Variable 'missingVar' not found\." );
		AssertExecErrorByRegex ( "scli read", @"No variable name provided to read\." );
	}

	[Fact]
	public void ReportsErrorsForMalformedSeClavSyntax () {
		LoadREPL ();
		AssertExecErrorByRegex ( "scli write ((String msg = ))", @"Could not parse line: .*String msg =.*" );
	}

	[Fact]
	public void MultipleSameUsingsDoesNotThrow () {
		LoadREPL ();
		AssertExecByRegex ( "scli write ((@using BasicModule))", @"Processed .*@using BasicModule.*" );
		AssertExecByRegex ( "scli write ((@using BasicModule))", @".*Module 'BasicModule' is already imported\..*" );
		AssertExecByRegex ( "scli write ((@using BasicModule))", @".*Module 'BasicModule' is already imported\..*" );
	}

	[Fact]
	public void SimpleStateDefinitionIsSupported () {
		LoadREPL ();
		WriteLine ( "@using BasicModule" );
		WriteLine ( "Int num = 0" );
		AssertVar ( "num", "0" );
		WriteLine ( "--> [State] -a-> State" );
		WriteLine ( "num = ADD_INT num 1" );
		AssertVar ( "num", "1" );
		WriteLine ( "COMPARE_INT num 10" );
		WriteLine ( "?< emit a" );
		AssertVar ( "num", "10" );
		WriteLine ( "num = 42" );
		AssertVar ( "num", "42" );
	}

	[Fact]
	public void ComplexStateDefinitionIsSupported () {
		LoadREPL ();
		WriteLine ( "@using BasicModule" );
		WriteLine ( "Int num = 0" );
		AssertVar ( "num", "0" );
		WriteLine ( "String msg" );
		WriteLine ( "--> Init -a-> S1" );
		WriteLine ( "num = 42" );
		WriteLine ( "msg = \"Init.\"" );
		AssertVar ( "num", "42" );
		AssertVar ( "msg", "Init." );
		WriteFinLine ( "emit a" ); // Should be NOP
		AssertVar ( "num", "42" );
		AssertVar ( "msg", "Init." );
		WriteLine ( "--> S1 -a-> S1 -b-> Exit" );
		WriteLine ( "msg = \"S1.\"" );
		WriteLine ( "num = ADD_INT num 1" );
		AssertVar ( "num", "43" );
		AssertVar ( "msg", "S1." );
		WriteLine ( "COMPARE_INT num 67" );
		WriteLine ( "?< emit a" ); // Cycle
		WriteFinLine ( "emit b" ); // Again NOP
		AssertVar ( "num", "67" );
		AssertVar ( "msg", "S1." );
		WriteLine ( "--> [Exit]" ); // Only now will "emit b" work, after one extra iteration
		WriteFinLine ( "msg = \"Finished.\"" );
		AssertVar ( "msg", "Finished." );
		AssertVar ( "num", "68" );
	}

	[Fact]
	public void FlagsRemainActive () {
		LoadREPL ();
		WriteLine ( "@using BasicModule" );
		WriteLine ( "String msg = \"Iniť\"" );
		WriteLine ( "Int num = 42" );
		WriteLine ( "COMPARE_INT num 10" );
		WriteLine ( "?< msg = \"Smaller\"" );
		WriteLine ( "?> msg = \"Larger\"" );
		AssertVar ( "msg", "Larger" );
	}

	private void LoadREPL () {
		AssertExec ( "scli reload", "SeClav REPL reloaded" );
		AssertExec ( "scli safemode off", "Safe mode disabled." );
	}

	private void WriteLine ( string line ) => AssertExec ( $"scli write -r (({line}))", $"Processed '{line}'." );
	private void WriteFinLine (string line) => AssertExec ( $"scli write -r -f (({line}))", $"Processed '{line}'." );

	private void AssertVar ( string name, string val )
		=> AssertExec ( $"scli read {name}", $"Variable '{name}' has value: {val}" );
}
