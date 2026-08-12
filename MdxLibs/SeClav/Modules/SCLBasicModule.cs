using System;
using System.Collections.Generic;

namespace SeClav.Modules;
public class BasicValueIntDef : DataTypeDefinition {
	public override string Name => "Int";
	public override string Description => "Int data type.";
	public override IReadOnlySet<ICommand> Commands => null; // 'dot' operator not implemented yet

	public override bool TryParse ( ref string line, out IDataType result ) {
		for ( int i = line.Length; i > 0; i-- ) {
			if ( int.TryParse ( line[..i], out int v ) ) {
				result = new BasicValueInt ( this, v );
				line = line[i..].TrimStart ();
				return true;
			}
		}
		result = null!;
		return false;
	}
	public override IDataType Default => new BasicValueInt ( this, 0 );
}

public class BasicValueStringDef : DataTypeDefinition {
	public override string Name => "String";
	public override string Description => "String data type.";
	public override IReadOnlySet<ICommand> Commands => null; // 'dot' operator not implemented yet

	public override bool TryParse ( ref string line, out IDataType result ) {
		result = null!;
		if ( line.StartsWith ( '"' ) ) {
			while ( true ) {
				int endQuote = line.IndexOf ( '"', 1 );
				if ( endQuote < 0 ) break;
				if ( line[endQuote - 1] != '\\' ) {
					result = new BasicValueString ( this, line[1..endQuote].Replace ( "\\\"", "\"" ) );
					line = line[(endQuote + 1)..].TrimStart ();
					return true;
				}
				line = line[(endQuote + 1)..];
			}
		}
		return false;
	}
	public override IDataType Default => new BasicValueString ( this, "" );
}

public class BasicValueInt : IDataType {
	public DataTypeDefinition Definition { get; }
	public int Value;
	public BasicValueInt ( DataTypeDefinition definition, int value ) {
		Definition = definition;
		Value = value;
	}
	public void Assign ( IDataType value ) {
		if ( value is not BasicValueInt v )
			throw new InvalidOperationException ( $"Cannot assign value of type '{value.Definition.Name}' to '{Definition.Name}'." );
		Value = v.Value;
	}
	public override string ToString () => Value.ToString ();
	public override bool Equals ( object? obj ) {
		return obj is BasicValueInt v && Value == v.Value;
	}
	public override int GetHashCode () => Value.GetHashCode ();
}

public class BasicValueString : IDataType {
	public DataTypeDefinition Definition { get; }
	public string Value;
	public BasicValueString ( DataTypeDefinition definition, string value ) {
		Definition = definition;
		Value = value;
	}
	public void Assign ( IDataType value ) {
		if ( value is not BasicValueString v )
			throw new InvalidOperationException ( $"Cannot assign value of type '{value.Definition.Name}' to '{Definition.Name}'." );
		Value = v.Value;
	}
	public override string ToString () => Value.ToString ();
	public override bool Equals ( object? obj ) {
		return obj is BasicValueString v && Value == v.Value;
	}
	public override int GetHashCode () => Value.GetHashCode ();
}



public class CompareInt : ICommand {
	public string CmdCode => "COMPARE_INT";
	public string CommonName => "Compare Integers";
	public string Description => "Compares two integer values, sets relevant flags and returns -1, 0, or 1.";
	public int ArgC => 2;
	public IReadOnlyList<(string name, DataTypeDefinition type, string description)> Args => [
		("a", new BasicValueIntDef (), "First integer to compare"),
		("b", new BasicValueIntDef (), "Second integer to compare")
	];
	public DataTypeDefinition ReturnType => new BasicValueIntDef ();
	public IDataType Execute ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args ) {
		var va = (BasicValueInt)runtime.GetVar ( args[0] );
		var vb = (BasicValueInt)runtime.GetVar ( args[1] );
		int result = va.Value.CompareTo ( vb.Value );
		ISCLRuntime.SetOrReset ( runtime, ISCLRuntime.SCLFlags.Equal, result == 0 );
		ISCLRuntime.SetOrReset ( runtime, ISCLRuntime.SCLFlags.Larger, result > 0 );
		ISCLRuntime.SetOrReset ( runtime, ISCLRuntime.SCLFlags.Smaller, result < 0 );
		return new BasicValueInt ( va.Definition, result );
	}
	public IDataType ExecuteSafe ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args, ref List<string> progress ) {
		SIdVal aID = args[0];
		SIdVal bID = args[1];
		IDataType a = runtime.SafeGetVar ( aID );
		IDataType b = runtime.SafeGetVar ( bID );
		if ( a is not BasicValueInt va )
			throw new InvalidOperationException ( $"Expected integer for argument 'a', got '{a.Definition.Name}'." );
		if ( b is not BasicValueInt vb )
			throw new InvalidOperationException ( $"Expected integer for argument 'b', got '{b.Definition.Name}'." );
		int result = va.Value.CompareTo ( vb.Value );
		progress.Add ( $" . Compare {va} to {vb} -> {result}" );
		ISCLRuntime.SetOrReset ( runtime, ISCLRuntime.SCLFlags.Equal, result == 0 );
		ISCLRuntime.SetOrReset ( runtime, ISCLRuntime.SCLFlags.Larger, result > 0 );
		ISCLRuntime.SetOrReset ( runtime, ISCLRuntime.SCLFlags.Smaller, result < 0 );
		return new BasicValueInt ( va.Definition, result );
	}
}

public class AppendIntToString : ICommand {
	public string CmdCode => "APPEND_INT_TO_STR";
	public string CommonName => "Append Int to String";
	public string Description => "Appends an integer value to a string value.";
	public int ArgC => 2;
	public IReadOnlyList<(string name, DataTypeDefinition type, string description)> Args => [
		("str", new BasicValueStringDef (), "String to append to"),
		("i", new BasicValueIntDef (), "Integer to append")
	];
	public DataTypeDefinition ReturnType => new BasicValueStringDef ();
	public IDataType Execute ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args ) {
		var str = runtime.GetVar ( args[0] ) as BasicValueString;
		var i = runtime.GetVar ( args[1] ) as BasicValueInt;
		return new BasicValueString ( str.Definition, str.Value + i.Value.ToString () );
	}
	public IDataType ExecuteSafe ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args, ref List<string> progress ) {
		SIdVal strID = args[0];
		SIdVal iID = args[1];
		IDataType str = runtime.SafeGetVar ( strID );
		IDataType i = runtime.SafeGetVar ( iID );
		if ( str is not BasicValueString vs )
			throw new InvalidOperationException ( $"Expected string for argument 'str', got '{str.Definition.Name}'." );
		if ( i is not BasicValueInt vi )
			throw new InvalidOperationException ( $"Expected integer for argument 'i', got '{i.Definition.Name}'." );
		progress.Add ( $" . \"{str}\" + {i} -> {vs.Value + vi.Value.ToString ()}" );
		return new BasicValueString ( str.Definition, vs.Value + vi.Value.ToString () );
	}
}

public class AddInts : ICommand {
	public string CmdCode => "ADD_INT";
	public string CommonName => "Add Integers";
	public string Description => "Adds two integer values.";
	public int ArgC => 2;
	public IReadOnlyList<(string name, DataTypeDefinition type, string description)> Args => [
		("a", new BasicValueIntDef (), "First integer to add"),
		("b", new BasicValueIntDef (), "Second integer to add")
	];
	public DataTypeDefinition ReturnType => new BasicValueIntDef ();
	public IDataType Execute ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args ) {
		var a = runtime.GetVar ( args[0] ) as BasicValueInt;
		var b = runtime.GetVar ( args[1] ) as BasicValueInt;
		return new BasicValueInt ( a.Definition, a.Value + b.Value );
	}
	public IDataType ExecuteSafe ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args, ref List<string> progress ) {
		SIdVal aID = args[0];
		SIdVal bID = args[1];
		IDataType a = runtime.SafeGetVar ( aID );
		IDataType b = runtime.SafeGetVar ( bID );
		if ( a is not BasicValueInt va )
			throw new InvalidOperationException ( $"Expected integer for argument 'a', got '{a.Definition.Name}'." );
		if ( b is not BasicValueInt vb )
			throw new InvalidOperationException ( $"Expected integer for argument 'b', got '{b.Definition.Name}'." );
		progress.Add ( $" . {va} + {vb} -> {va.Value + vb.Value}" );
		return new BasicValueInt ( va.Definition, va.Value + vb.Value );
	}
}

public class ConcatStrs : ICommand {
	public string CmdCode => "CONCAT_STR";
	public string CommonName => "Concatenate Strings";
	public string Description => "Concatenates two string values.";
	public int ArgC => 2;
	public IReadOnlyList<(string name, DataTypeDefinition type, string description)> Args => [
		("str1", new BasicValueStringDef (), "First string to concatenate"),
		("str2", new BasicValueStringDef (), "Second string to concatenate")
	];
	public DataTypeDefinition ReturnType => new BasicValueStringDef ();
	public IDataType Execute ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args ) {
		var str1 = runtime.GetVar ( args[0] ) as BasicValueString;
		var str2 = runtime.GetVar ( args[1] ) as BasicValueString;
		return new BasicValueString ( str1.Definition, str1.Value + str2.Value );
	}
	public IDataType ExecuteSafe ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args, ref List<string> progress ) {
		SIdVal str1ID = args[0];
		SIdVal str2ID = args[1];
		IDataType str1 = runtime.SafeGetVar ( str1ID );
		IDataType str2 = runtime.SafeGetVar ( str2ID );
		if ( str1 is not BasicValueString v1 )
			throw new InvalidOperationException ( $"Expected string for argument 'str1', got '{str1.Definition.Name}'." );
		if ( str2 is not BasicValueString v2 )
			throw new InvalidOperationException ( $"Expected string for argument 'str2', got '{str2.Definition.Name}'." );
		progress.Add ( $" . \"{str1}\" + \"{str2}\" -> \"{v1.Value + v2.Value}\"" );
		return new BasicValueString ( str1.Definition, v1.Value + v2.Value );
	}
}

public class ReadFlags : ICommand {
	public string CmdCode => "READ_FLAGS";
	public string CommonName => "Read Flags";
	public string Description => "Reads the current runtime flags.";
	public int ArgC => 0;
	public IReadOnlyList<(string name, DataTypeDefinition type, string description)> Args => [];
	public DataTypeDefinition ReturnType => new BasicValueIntDef ();
	public IDataType Execute ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args ) {
		int flags = (int)runtime.GetFlags ();
		return new BasicValueInt ( ReturnType, flags );
	}
	public IDataType ExecuteSafe ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args, ref List<string> progress ) {
		int flags = (int)runtime.GetFlags ();
		progress.Add ( $" . Current flags: {flags}" );
		return new BasicValueInt ( ReturnType, flags );
	}
}

public class SCL_BasicModule : IModuleInfo {
	public const string ModuleName = "BasicModule";
	public string Name => ModuleName;
	public string Description => "Implementation of basic functionalities.";

	public DataTypeDefinition IntTypeDef => IntDef;
	public DataTypeDefinition StrTypeDef => StrDef;

	private readonly DataTypeDefinition IntDef = new BasicValueIntDef ();
	private readonly DataTypeDefinition StrDef = new BasicValueStringDef ();

	public IReadOnlySet<ICommand> Commands => new HashSet<ICommand> () {
		new AddInts (), new ConcatStrs (), new AppendIntToString (),
		new ReadFlags (), new CompareInt (),
	};

	public IReadOnlySet<IMacro> Macros => new HashSet<IMacro> () {
	};

	public IReadOnlySet<DataTypeDefinition> DataTypes => new HashSet<DataTypeDefinition> () {
		IntDef, StrDef,
	};

	// Prae-directives are provided by tests themselves as needed
	public IReadOnlyDictionary<string, PraeDirective> PraeDirectives => new Dictionary<string, PraeDirective> () {
	};

	public IReadOnlyDictionary<string, IReadOnlySet<string>> AltNames => new Dictionary<string, IReadOnlySet<string>> { };
}