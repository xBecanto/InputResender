using System;
using System.Collections.Generic;
using SeClav;
using SeClav.Modules;

namespace SeClavTests;
internal class TestValueIntDef : DataTypeDefinition {
	public override string Name => "TestInt";
	public override string Description => "Test integer data type.";
	public override IReadOnlySet<ICommand> Commands => null; // 'dot' operator not implemented yet			e
	public override bool TryParse ( ref string line, out IDataType result ) {
		for ( int i = line.Length; i > 0; i-- ) {
			if ( int.TryParse ( line[..i], out int v ) ) {
				result = new TestValueInt ( this, v );
				line = line[i..].TrimStart ();
				return true;
			}
		}
		result = null!;
		return false;
	}
	public override IDataType Default => new TestValueInt ( this, 0 );
}

internal class TestValueStringDef : DataTypeDefinition {
	public override string Name => "TestString";
	public override string Description => "Test string data type.";
	public override IReadOnlySet<ICommand> Commands => null; // 'dot' operator not implemented yet
	public override bool TryParse ( ref string line, out IDataType result ) {
		result = null!;
		if ( line.StartsWith ( '"' ) ) {
			while (true) {
				int endQuote = line.IndexOf ( '"', 1 );
				if ( endQuote < 0 ) break;
				if ( line[endQuote - 1] != '\\' ) {
					result = new TestValueString ( this, line[1..endQuote].Replace ( "\\\"", "\"" ) );
					line = line[(endQuote + 1)..].TrimStart ();
					return true;
				}
				line = line[(endQuote + 1)..];
			}
		}
		return false;
	}
	public override IDataType Default => new TestValueString ( this, "" );
}

internal class TestValueInt : IDataType {
	public DataTypeDefinition Definition { get; }
	public int Value;
	public TestValueInt ( DataTypeDefinition definition, int value ) {
		Definition = definition;
		Value = value;
	}
	public void Assign ( IDataType value ) {
		if ( value is not TestValueInt v )
			throw new InvalidOperationException ( $"Cannot assign value of type '{value.Definition.Name}' to '{Definition.Name}'." );
		Value = v.Value;
	}
	public override string ToString () => Value.ToString ();
	public override bool Equals ( object? obj ) {
		return obj is TestValueInt v && Value == v.Value;
	}
	public override int GetHashCode () => Value.GetHashCode ();
}

internal class TestValueString : IDataType {
	public DataTypeDefinition Definition { get; }
	public string Value;
	public TestValueString ( DataTypeDefinition definition, string value ) {
		Definition = definition;
		Value = value;
	}
	public void Assign ( IDataType value ) {
		if ( value is not TestValueString v )
			throw new InvalidOperationException ( $"Cannot assign value of type '{value.Definition.Name}' to '{Definition.Name}'." );
		Value = v.Value;
	}
	public override string ToString () => Value.ToString ();
	public override bool Equals ( object? obj ) {
		return obj is TestValueString v && Value == v.Value;
	}
	public override int GetHashCode () => Value.GetHashCode ();
}



internal class AssertEqual : ICommand {
	public string CmdCode => "ASSERT_EQ";
	public string CommonName => "Assert Equal";
	public string Description => "Asserts that two values have same type and value. Throws an exception if they are not equal.";
	public int ArgC => 2;
	public IReadOnlyList<(string name, DataTypeDefinition type, string description)> Args => [
		("var1", new SCLT_Any (), "First integer variable to compare"),
		("var2", new SCLT_Same (), "Second integer variable to compare")
	];
	public DataTypeDefinition ReturnType => new SCLT_Same ();
	public IDataType Execute ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args ) {
		var var1 = runtime.GetVar ( args[0] );
		var var2 = runtime.GetVar ( args[1] );
		return var1.Equals ( var2 ) ? var1 : throw new InvalidOperationException ( "Assert Equal failed: Variable values are not the same." );
	}
	public IDataType ExecuteSafe ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args, ref List<string> progress ) {
		SIdVal var1ID = args[0];
		SIdVal var2ID = args[1];
		IDataType var1 = runtime.SafeGetVar ( args[1] );
		IDataType var2 = runtime.SafeGetVar ( args[2] );
		progress.Add ( $" . {var1} == {var2} ? ({var1.Equals ( var2 )})" );
		if ( var1.Definition.Name != var2.Definition.Name )
			throw new InvalidOperationException ( $"Assert Equal failed: Variable types are not the same: '{var1.Definition.Name}' != '{var2.Definition.Name}'." );
		if ( !var1.Equals ( var2 ) )
			throw new InvalidOperationException ( $"Assert Equal failed: Variable values are not the same: '{var1}' != '{var2}'." );
		return var1;
	}
}

internal class CompareInt : ICommand {
	public string CmdCode => "COMPARE_INT";
	public string CommonName => "Compare Integers";
	public string Description => "Compares two integer values, sets relevant flags and returns -1, 0, or 1.";
	public int ArgC => 2;
	public IReadOnlyList<(string name, DataTypeDefinition type, string description)> Args => [
		("a", new TestValueIntDef (), "First integer to compare"),
		("b", new TestValueIntDef (), "Second integer to compare")
	];
	public DataTypeDefinition ReturnType => new TestValueIntDef ();
	public IDataType Execute ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args ) {
		var va = (TestValueInt)runtime.GetVar ( args[0] );
		var vb = (TestValueInt)runtime.GetVar ( args[1] );
		int result = va.Value.CompareTo ( vb.Value );
		ISCLRuntime.SetOrReset ( runtime, ISCLRuntime.SCLFlags.Equal, result == 0 );
		ISCLRuntime.SetOrReset ( runtime, ISCLRuntime.SCLFlags.Larger, result > 0 );
		ISCLRuntime.SetOrReset ( runtime, ISCLRuntime.SCLFlags.Smaller, result < 0 );
		return new TestValueInt ( va.Definition, result );
	}
	public IDataType ExecuteSafe ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args, ref List<string> progress ) {
		SIdVal aID = args[0];
		SIdVal bID = args[1];
		IDataType a = runtime.SafeGetVar ( aID );
		IDataType b = runtime.SafeGetVar ( bID );
		if ( a is not TestValueInt va )
			throw new InvalidOperationException ( $"Expected integer for argument 'a', got '{a.Definition.Name}'." );
		if ( b is not TestValueInt vb )
			throw new InvalidOperationException ( $"Expected integer for argument 'b', got '{b.Definition.Name}'." );
		int result = va.Value.CompareTo ( vb.Value );
		progress.Add ( $" . Compare {va} to {vb} -> {result}" );
		ISCLRuntime.SetOrReset ( runtime, ISCLRuntime.SCLFlags.Equal, result == 0 );
		ISCLRuntime.SetOrReset ( runtime, ISCLRuntime.SCLFlags.Larger, result > 0 );
		ISCLRuntime.SetOrReset ( runtime, ISCLRuntime.SCLFlags.Smaller, result < 0 );
		return new TestValueInt ( va.Definition, result );
	}
}

internal class AppendIntToString : ICommand {
	public string CmdCode => "APPEND_INT_TO_STR";
	public string CommonName => "Append Int to String";
	public string Description => "Appends an integer value to a string value.";
	public int ArgC => 2;
	public IReadOnlyList<(string name, DataTypeDefinition type, string description)> Args => [
		("str", new TestValueStringDef (), "String to append to"),
		("i", new TestValueIntDef (), "Integer to append")
	];
	public DataTypeDefinition ReturnType => new TestValueStringDef ();
	public IDataType Execute ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args ) {
		var str = runtime.GetVar ( args[0] ) as TestValueString;
		var i = runtime.GetVar ( args[1] ) as TestValueInt;
		return new TestValueString ( str.Definition, str.Value + i.Value.ToString () );
	}
	public IDataType ExecuteSafe ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args, ref List<string> progress ) {
		SIdVal strID = args[0];
		SIdVal iID = args[1];
		IDataType str = runtime.SafeGetVar ( strID );
		IDataType i = runtime.SafeGetVar ( iID );
		if ( str is not TestValueString vs )
			throw new InvalidOperationException ( $"Expected string for argument 'str', got '{str.Definition.Name}'." );
		if ( i is not TestValueInt vi )
			throw new InvalidOperationException ( $"Expected integer for argument 'i', got '{i.Definition.Name}'." );
		progress.Add ( $" . \"{str}\" + {i} -> {vs.Value + vi.Value.ToString ()}" );
		return new TestValueString ( str.Definition, vs.Value + vi.Value.ToString () );
	}
}

internal class AddInts : ICommand {
	public string CmdCode => "ADD_INT";
	public string CommonName => "Add Integers";
	public string Description => "Adds two integer values.";
	public int ArgC => 2;
	public IReadOnlyList<(string name, DataTypeDefinition type, string description)> Args => [
		("a", new TestValueIntDef (), "First integer to add"),
		("b", new TestValueIntDef (), "Second integer to add")
	];
	public DataTypeDefinition ReturnType => new TestValueIntDef ();
	public IDataType Execute ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args ) {
		var a = runtime.GetVar ( args[0] ) as TestValueInt;
		var b = runtime.GetVar ( args[1] ) as TestValueInt;
		return new TestValueInt ( a.Definition, a.Value + b.Value );
	}
	public IDataType ExecuteSafe ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args, ref List<string> progress ) {
		SIdVal aID = args[0];
		SIdVal bID = args[1];
		IDataType a = runtime.SafeGetVar ( aID );
		IDataType b = runtime.SafeGetVar ( bID );
		if ( a is not TestValueInt va )
			throw new InvalidOperationException ( $"Expected integer for argument 'a', got '{a.Definition.Name}'." );
		if ( b is not TestValueInt vb )
			throw new InvalidOperationException ( $"Expected integer for argument 'b', got '{b.Definition.Name}'." );
		progress.Add ( $" . {va} + {vb} -> {va.Value + vb.Value}" );
		return new TestValueInt ( va.Definition, va.Value + vb.Value );
	}
}

internal class ConcatStrs : ICommand {
	public string CmdCode => "CONCAT_STR";
	public string CommonName => "Concatenate Strings";
	public string Description => "Concatenates two string values.";
	public int ArgC => 2;
	public IReadOnlyList<(string name, DataTypeDefinition type, string description)> Args => [
		("str1", new TestValueStringDef (), "First string to concatenate"),
		("str2", new TestValueStringDef (), "Second string to concatenate")
	];
	public DataTypeDefinition ReturnType => new TestValueStringDef ();
	public IDataType Execute ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args ) {
		var str1 = runtime.GetVar ( args[0] ) as TestValueString;
		var str2 = runtime.GetVar ( args[1] ) as TestValueString;
		return new TestValueString ( str1.Definition, str1.Value + str2.Value );
	}
	public IDataType ExecuteSafe ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args, ref List<string> progress ) {
		SIdVal str1ID = args[0];
		SIdVal str2ID = args[1];
		IDataType str1 = runtime.SafeGetVar ( str1ID );
		IDataType str2 = runtime.SafeGetVar ( str2ID );
		if ( str1 is not TestValueString v1 )
			throw new InvalidOperationException ( $"Expected string for argument 'str1', got '{str1.Definition.Name}'." );
		if ( str2 is not TestValueString v2 )
			throw new InvalidOperationException ( $"Expected string for argument 'str2', got '{str2.Definition.Name}'." );
		progress.Add ( $" . \"{str1}\" + \"{str2}\" -> \"{v1.Value + v2.Value}\"" );
		return new TestValueString ( str1.Definition, v1.Value + v2.Value );
	}
}

internal class SetFlag : ICommand {
	public string CmdCode => "SET_FLAG";
	public string CommonName => "Set Flag";
	public string Description => "Sets a runtime flag.";
	public int ArgC => 1;
	public IReadOnlyList<(string name, DataTypeDefinition type, string description)> Args => [
		("flag", new TestValueIntDef (), "Flag value to set")
	];
	public DataTypeDefinition ReturnType => new SCLT_Void ();
	public IDataType Execute ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args ) {
		var flag = runtime.GetVar ( args[0] ) as TestValueInt;
		runtime.SetFlag ( ( ISCLRuntime.SCLFlags )(1 << flag.Value) );
		return new SCLT_Void ().Default;
	}
	public IDataType ExecuteSafe ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args, ref List<string> progress ) {
		SIdVal flagID = args[0];
		IDataType flagOffset = runtime.SafeGetVar ( flagID );
		if ( flagOffset is not TestValueInt vf )
			throw new InvalidOperationException ( $"Expected integer for argument 'flag', got '{flagOffset.Definition.Name}'." );
		progress.Add ( $" . Set flag to {vf.Value}" );
		runtime.SetFlag ( (ISCLRuntime.SCLFlags)(1 << vf.Value) );
		return new SCLT_Void ().Default;
	}
}

internal class ResetFlag : ICommand {
	public string CmdCode => "RESET_FLAG";
	public string CommonName => "Reset Flag";
	public string Description => "Resets a runtime flag.";
	public int ArgC => 1;
	public IReadOnlyList<(string name, DataTypeDefinition type, string description)> Args => [
		("flag", new TestValueIntDef (), "Flag value to reset")
	];
	public DataTypeDefinition ReturnType => new SCLT_Void ();
	public IDataType Execute ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args ) {
		var flag = runtime.GetVar ( args[0] ) as TestValueInt;
		runtime.ResetFlag ( ( ISCLRuntime.SCLFlags )( 1 << flag.Value ) );
		return new SCLT_Void ().Default;
	}
	public IDataType ExecuteSafe ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args, ref List<string> progress ) {
		SIdVal flagID = args[0];
		IDataType flagOffset = runtime.SafeGetVar ( flagID );
		if ( flagOffset is not TestValueInt vf )
			throw new InvalidOperationException ( $"Expected integer for argument 'flag', got '{flagOffset.Definition.Name}'." );
		progress.Add ( $" . Reset flag to {vf.Value}" );
		runtime.ResetFlag ( ( ISCLRuntime.SCLFlags )( 1 << vf.Value ) );
		return new SCLT_Void ().Default;
	}
}

internal class ReadFlags : ICommand {
	public string CmdCode => "READ_FLAGS";
	public string CommonName => "Read Flags";
	public string Description => "Reads the current runtime flags.";
	public int ArgC => 0;
	public IReadOnlyList<(string name, DataTypeDefinition type, string description)> Args => [];
	public DataTypeDefinition ReturnType => new TestValueIntDef ();
	public IDataType Execute ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args ) {
		int flags = ( int )runtime.GetFlags ();
		return new TestValueInt ( ReturnType, flags );
	}
	public IDataType ExecuteSafe ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args, ref List<string> progress ) {
		int flags = ( int )runtime.GetFlags ();
		progress.Add ( $" . Current flags: {flags}" );
		return new TestValueInt ( ReturnType, flags );
	}
}

internal class SetResetMultipleFlags : IMacro {
	public string CmdCode => "SET_RESET_FLAGS";
	public string CommonName => "Set/Reset Multiple Flags";
	public string Description => "Sets and resets multiple runtime flags based on input.";
	public bool SelectRight => true;
	public bool UnorderedGuiders => true;
	public IReadOnlyList<(int after, string split)> guiders => [(-1, "-"), (-1, "+")];

	public string[] RewriteByGuiders ( ushort flags, (int guiderID, string arg)[] parts ) {
		SetFlag setter = new ();
		ResetFlag resetter = new ();
		List<string> rewritten = [];
		foreach ( var part in parts ) {
			switch(part.guiderID) {
			case 0: // 1st guider, i.e. RESET_FLAG
				rewritten.Add ( resetter.CmdCode + " " + part.arg );
				break;
			case 1: // 2nd guider, i.e. SET_FLAG
				rewritten.Add ( setter.CmdCode + " " + part.arg );
				break;
			default: throw new IndexOutOfRangeException ($"Guider (#{part.guiderID}, '{part.arg}') is not supported.");
			}
		}
		return [.. rewritten];
	}
}

internal class JoinStrings : IMacro {
	public string CmdCode => "JOIN_STRINGS";
	public string CommonName => "Join Strings";
	public string Description => "Joins multiple strings into one.";
	public bool SelectRight => false;
	public bool UnorderedGuiders => false;
	public IReadOnlyList<(int after, string split)> guiders => [(1, "="), (-2, "|")];
	// ⮤ Allow limitless number of strings separated by commas
	public string[] RewriteByGuiders ( ushort flags, (int guiderID, string arg)[] parts ) {
		// Just a reminder, this is only a test method, doesn't need to be user-friendly command
		// This specific macro would provide bad result for strings containing the separator inside i.e. '|'.
		ConcatStrs concater = new ();
		List<string> rewritten = [];
		// Define target variable:
		if ( parts.Length < 3 ) throw new InvalidOperationException ( "JOIN_STRINGS macro requires at least two string arguments to join." ); // Variable name + at least two strings

		if ( parts[0].guiderID != 0 )
			throw new InvalidOperationException ( "JOIN_STRINGS macro requires variable name before '='." );
		if ( parts[1].guiderID != 1 || parts[2].guiderID != 1 )
			throw new InvalidOperationException ( "JOIN_STRINGS macro requires remaining arguments to be strings separated by ','." );
		rewritten.Add($"{concater.ReturnType.Name} {parts[0].arg} = {concater.CmdCode} \"{parts[1].arg}\" \"{parts[2].arg}\"");

		for ( int i = 3; i < parts.Length; i++ ) {
			if ( parts[i].guiderID != 1 ) throw new InvalidOperationException ( "JOIN_STRINGS macro requires remaining arguments to be strings separated by ','." );
			rewritten.Add ( $"{parts[0].arg} = {concater.CmdCode} {parts[0].arg} \"{parts[i].arg}\"" );
		}
		return [.. rewritten];
	}
}

internal class AddOrAppend : IMacro {
	public string CmdCode => "ADD_OR_APPEND";
	public string CommonName => "Add or Append";
	public string Description => "Adds two integers or appends two strings based on input types.";
	public bool SelectRight => true;
	public bool UnorderedGuiders => false;
	public IReadOnlyList<(int after, string split)> guiders => [(1, "->"), (-2, "."), (2, "."), (-3, "+")];
	public string[] RewriteByGuiders ( ushort flags, (int guiderID, string arg)[] parts ) {
		if ( parts.Length < 3 )
			throw new InvalidOperationException ( "ADD_OR_APPEND macro requires at least two arguments." );
		ConcatStrs concater = new ();
		AddInts adder = new ();
		AppendIntToString appI2S = new ();
		List<string> rewritten = [];

		if ( parts.Length < 4 ) throw new InvalidOperationException ( "JOIN_STRINGS macro requires at least 4 arguments: target variable, initial stringA, initial stringB, integer to append." );
		if ( parts[0].guiderID != 0 )
			throw new InvalidOperationException ( "JOIN_STRINGS macro requires variable name after '->'." );
		if ( parts[1].guiderID != 1 || parts[2].guiderID != 1 )
			throw new InvalidOperationException ( "JOIN_STRINGS macro requires first two arguments to be strings separated by '.'." );

		rewritten.Add($"{concater.ReturnType.Name} {parts[0].arg} = {concater.CmdCode} \"{parts[1].arg}\" \"{parts[2].arg}\"");
		int argID = 3;
		while ( argID < parts.Length && parts[argID].guiderID == 1 ) {
			rewritten.Add ( $"{parts[0].arg} = {concater.CmdCode} {parts[0].arg} \"{parts[argID++].arg}\"" );
			argID++;
		}

		if ( argID >= parts.Length )
			throw new InvalidOperationException ( "ADD_OR_APPEND macro requires at least one integer to append after strings." );
		if ( parts[argID].guiderID != 2 )
			throw new InvalidOperationException ( "ADD_OR_APPEND macro requires integer arguments to be appended after '+'." );


		rewritten.Add ( $"{adder.ReturnType.Name} {parts[0].arg}_I_tmp = {parts[argID].arg}" );
		argID++;
		while ( argID < parts.Length ) {
			if ( parts[argID].guiderID != 2 )
				throw new InvalidOperationException ( "ADD_OR_APPEND macro requires integer arguments to be appended after '+'." );
			rewritten.Add ( $"{parts[0].arg}_I_tmp = {adder.CmdCode} {parts[0].arg}_I_tmp {parts[argID++].arg}" );
		}
		rewritten.Add ( $"{parts[0].arg} = {appI2S.CmdCode} {parts[0].arg} {parts[0].arg}_I_tmp" );
		return [.. rewritten];
	}
}

internal class SCL_TestModule : IModuleInfo {
	public string Name => "TestModule";
	public string Description => "Module for testing purposes";

	public DataTypeDefinition IntTypeDef => IntDef;
	public DataTypeDefinition StrTypeDef => StrDef;

	private readonly DataTypeDefinition IntDef = new TestValueIntDef ();
	private readonly DataTypeDefinition StrDef = new TestValueStringDef ();

	public IReadOnlySet<ICommand> Commands => new HashSet<ICommand> () {
		new AssertEqual (), new AddInts (), new ConcatStrs (), new AppendIntToString (),
		new SetFlag (), new ResetFlag (), new ReadFlags (), new CompareInt (),
	};

	public IReadOnlySet<IMacro> Macros => new HashSet<IMacro> () {
		new SetResetMultipleFlags (), new JoinStrings (),
	};

	public IReadOnlySet<DataTypeDefinition> DataTypes => new HashSet<DataTypeDefinition> () {
		IntDef, StrDef,
	};

	// Prae-directives are provided by tests themselves as needed
	public IReadOnlyDictionary<string, PraeDirective> PraeDirectives => new Dictionary<string, PraeDirective> ();
}