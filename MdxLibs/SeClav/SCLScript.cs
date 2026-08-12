using System;
using System.Collections.Generic;
using MdxLibs.Services;
using SeClav.Parsing;

namespace SeClav;
public class SCLScriptHolder {
	// This class is simply a public wrapper around ISCLDebugInfo to allow passing it around without exposing the interface itself.
	// As a public it behaves as empty object. Internal code can access the actual script and debug info.

	private ISCLDebugInfo debugInfo;

	internal ISCLParsedScript ParsedScript => debugInfo.Script;
	internal ISCLDebugInfo DebugInfo => debugInfo;
	public IReadOnlyList<(string, Exception)> Errors;
	private List<(string, Exception)> ErrorList;

	public string ScriptName { get; private set; }

	internal SCLScriptHolder ( string code, string name, System.Func<string, IModuleInfo> moduleLoader, bool allowThrow = false ) {
		ScriptName = name;
		ErrorList = [];
		Errors = ErrorList.AsReadOnly ();
		if ( string.IsNullOrEmpty ( code ) ) throw new ArgumentNullException ( nameof ( code ) );
		SCLParsing parser = new ( moduleLoader );
		var lines = code.Split ( new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None );

		foreach ( var line in lines ) {
			//var trimmed = line.Trim ();
			//if ( string.IsNullOrEmpty ( trimmed ) || trimmed.StartsWith ( "#" ) ) continue; // Skip empty lines and comments

			if ( allowThrow ) {
				parser.ProcessLine ( line );
			} else {
				try {
					parser.ProcessLine ( line );
				} catch ( Exception ex ) {
					ErrorList.Add ( ($"Error processing line '{line}': {ex.Message}", ex) );
				}
			}
		}

		debugInfo = parser.GetResultWithDebugInfo ();
	}

	public SIdVal GetVariableID (string name) {
		foreach ( var kvp in debugInfo.VarNames ) {
			if ( kvp.Value == name ) {
				return kvp.Key.Generic;
			}
		}
		throw new KeyNotFoundException ( $"Variable '{name}' not found in script." );
	}
	public bool IsUsingModule (string moduleName) {
		foreach ( var mod in ParsedScript.Modules ) {
			if ( mod.Name == moduleName ) return true;
		}
		return false;
	}
}

internal interface ISCLParsedScript {
	const ushort JMP_OPCODE_ID = SCLInterpreter.VarIdMask - 1;
	const ushort FORK_OPCODE_ID = SCLInterpreter.VarIdMask - 2;
	const ushort TERMINATE_OPCODE_ID = SCLInterpreter.VarIdMask - 3;
	const ushort SUSPEND_OPCODE_ID = SCLInterpreter.VarIdMask - 4;

	IReadOnlyList<DataTypeDefinition> DataTypes { get; }
	IReadOnlyList<ICommand> Commands { get; }
	IReadOnlyList<CmdCall> CommandIndices { get; }
	IReadOnlyList<IDataType> Constants { get; }
	IReadOnlyList<IModuleInfo> Modules { get; }
	IReadOnlyList<int> VariableTypes { get; }
	IReadOnlyList<int> ResultTypes { get; }
	IReadOnlyDictionary<int, Func<IDataType>> Getters { get; }
	IReadOnlyList<string> Disassembly { get; }
	IReadOnlyDictionary<string, SIdVal> InputVars { get; }
	// The runtime owner (RuntimeHolder) is responsible to insert proper function into the command list at index at InputSamplers[<name>]
	IReadOnlyDictionary<string, int> InputSamplers { get; }
	IReadOnlyDictionary<string, SIdVal> OutputVars { get; }
	IReadOnlyDictionary<string, int> ExternFunctions { get; }

	public struct ExternFunction {
		public readonly string Name;
		public readonly DataTypeDefinition ReturnType;
		public readonly IReadOnlyList<(string name, DataTypeDefinition type)> Args;
		public readonly Func<IReadOnlyList<IDataType>, IDataType> FunctionBody;
		public ExternFunction ( string name, DataTypeDefinition returnType, IReadOnlyList<(string name, DataTypeDefinition type)> args, Func<IReadOnlyList<IDataType>, IDataType> functionBody ) {
			Name = name;
			ReturnType = returnType;
			Args = args;
			FunctionBody = functionBody;
		}
	}
}

internal interface ISCLDebugInfo {
	ISCLParsedScript Script { get; }
	IReadOnlyDictionary<SId<ArgTag>, string> VarNames { get; }
	string CompileLog { get; }
}

public abstract class DataTypeDefinition {
	public abstract string Name { get; }
	public abstract string Description { get; }
	public abstract IReadOnlySet<ICommand> Commands { get; }
	public abstract bool TryParse ( ref string line, out IDataType result );
	public abstract IDataType Default { get; }

	public readonly int GlobalID;
	public readonly string Owner;
	private static int nextGlobalID = 1;
	private static object globalIDLock = new ();
	protected DataTypeDefinition () {
		lock ( globalIDLock )
			GlobalID = nextGlobalID++;
		//Owner = string.Join ( "\n", DefineOwner () );
		Owner = string.Empty;
	}

	/*private IReadOnlyList<string> DefineOwner () {
		var stack = new System.Diagnostics.StackTrace ( true );
		List<string> lines = stack.ToString ().Split ( new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries ).ToList ();
		lines.RemoveAll ( ( line ) => line.Contains ( "at System." ) || line.Contains ( "at Xunit." ) || line.Contains ( "at Reflection" ) );
		lines.Reverse ();
		return lines.AsReadOnly ();
	}*/
}

public interface IDataType {
	DataTypeDefinition Definition { get; }
	void Assign ( IDataType value );
}

public delegate void PraeDirective ( SCLParsingContext context, ArgParser args );

public interface IModuleInfo {
	string Name { get; }
	string Description { get; }
	IReadOnlySet<ICommand> Commands { get; }
	IReadOnlySet<IMacro> Macros { get; }
	IReadOnlySet<DataTypeDefinition> DataTypes { get; }
	IReadOnlyDictionary<string, PraeDirective> PraeDirectives { get; }
	IReadOnlyDictionary<string, IReadOnlySet<string>> AltNames { get; }
}

public interface ISCLRuntime {
	[Flags]
	public enum SCLFlags : ushort {
		Empty = 0,
		None = 1,
		If = 2,
		Else = 4,
		Equal = 8,
		Larger = 16,
		Smaller = 32,

		_1 = 0b0000_0000_0000_0010,
		_2 = 0b0000_0000_0000_0100,
		_3 = 0b0000_0000_0000_1000,
		_4 = 0b0000_0000_0001_0000,
		_5 = 0b0000_0000_0010_0000,
		_6 = 0b0000_0000_0100_0000,
		_7 = 0b0000_0000_1000_0000,
		_8 = 0b0000_0001_0000_0000,
		_9 = 0b0000_0010_0000_0000,
		_A = 0b0000_0100_0000_0000,
		_B = 0b0000_1000_0000_0000,
		_C = 0b0001_0000_0000_0000,
		_D = 0b0010_0000_0000_0000,
		_E = 0b0100_0000_0000_0000,
		_F = 0b1000_0000_0000_0000
	}
	IDataType GetVar ( SIdVal varID );
	void SetVar ( SIdVal varID, IDataType value );
	IDataType SafeGetVar ( SIdVal varID );
	void SafeSetVar ( SIdVal varID, IDataType value );
	SCLFlags GetFlags ();
	void SetFlag ( SCLFlags value );
	void ResetFlag ( SCLFlags value );

	void UpdateMemoryInfo ( ref Dictionary<string, string> memInfo );

	T SafeGetVar<T>( SIdVal varID ) where T : SeClav.IDataType {
		var arg0 = SafeGetVar ( varID );
		if ( arg0 is not T status )
			throw new InvalidOperationException ( $"Argument 0 is not of type {typeof(T).Name}." );
		return status;
	}
	(T, U) SafeGetVar<T, U> ( SIdVal var0, SIdVal var1 )
		where T : SeClav.IDataType where U : SeClav.IDataType
		=> (SafeGetVar<T> ( var0 ), SafeGetVar<U> ( var1 ));
	(T, U, V) SafeGetVar<T, U, V> ( SIdVal var0, SIdVal var1, SIdVal var2 )
		where T : SeClav.IDataType where U : SeClav.IDataType where V : SeClav.IDataType
		=> (SafeGetVar<T> ( var0 ), SafeGetVar<U> ( var1 ), SafeGetVar<V> ( var2 ));
	(T, U, V, W) SafeGetVar<T, U, V, W> ( SIdVal var0, SIdVal var1, SIdVal var2, SIdVal var3 )
		where T : SeClav.IDataType where U : SeClav.IDataType where V : SeClav.IDataType where W : SeClav.IDataType
		=> (SafeGetVar<T> ( var0 ), SafeGetVar<U> ( var1 ), SafeGetVar<V> ( var2 ), SafeGetVar<W> ( var3 ));

	T GetVar<T>( SIdVal varID ) where T : SeClav.IDataType => (T)GetVar ( varID );
	(T, U) GetVar<T, U> ( SIdVal var0, SIdVal var1 )
		where T : SeClav.IDataType where U : SeClav.IDataType
		=> (GetVar<T> ( var0 ), GetVar<U> ( var1 ));
	(T, U, V) GetVar<T, U, V> ( SIdVal var0, SIdVal var1, SIdVal var2 )
		where T : SeClav.IDataType where U : SeClav.IDataType where V : SeClav.IDataType
		=> (GetVar<T> ( var0 ), GetVar<U> ( var1 ), GetVar<V> ( var2 ));
	(T, U, V, W) GetVar<T, U, V, W> ( SIdVal var0, SIdVal var1, SIdVal var2, SIdVal var3 )
		where T : SeClav.IDataType where U : SeClav.IDataType where V : SeClav.IDataType where W : SeClav.IDataType
		=> (GetVar<T> ( var0 ), GetVar<U> ( var1 ), GetVar<V> ( var2 ), GetVar<W> ( var3 ));

	static void SetOrReset (ISCLRuntime runtime, SCLFlags flag, bool set ) {
		if ( set ) runtime.SetFlag ( flag );
		else runtime.ResetFlag ( flag );
	}
}

public interface ICommandGen {
	string CmdCode { get; }
	string CommonName { get; }
	string Description { get; }
}
public interface ICommand : ICommandGen {
	int ArgC { get; }
	// The AssemblyX86 style of dst, src1, src2, ... is recommended
	IReadOnlyList<(string name, DataTypeDefinition type, string description)> Args { get; }
	DataTypeDefinition ReturnType { get; }
	IDataType Execute ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args );
	/// <summary>A 'debug' version. Should perform extra checks to catch and specify errors more easily.</summary>
	IDataType ExecuteSafe ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args, ref List<string> progress );
}
public interface IMacro : ICommandGen {
	bool SelectRight { get; } // If true, a guider starts a 'part' instead of ending it
	bool UnorderedGuiders { get; } // If true, guider parts can be in any order
	IReadOnlyList<(int after, string split)> guiders { get; }
	/// <summary>Allow to rewrite textual command based on (guiderID, argument) into separete multiline commands</summary>
	string[] RewriteByGuiders ( ushort flags, (int guiderID, string arg)[] parts );
}

public struct SIdVal {
	private readonly ushort Val;
	public SIdVal ( int type, int id ) => Val = (ushort)((type << SCLInterpreter.VarIdShift) | (id & SCLInterpreter.VarIdMask));
	public override string ToString () => $"{Val >> SCLInterpreter.VarIdShift}${Val & SCLInterpreter.VarIdMask}";
	public readonly ushort ValueType => (ushort)(Val >> SCLInterpreter.VarIdShift);
	public readonly ushort ValueId => (ushort)(Val & SCLInterpreter.VarIdMask);
	public readonly ushort RawValue => Val;
}
public interface IIdTag { }
public struct OpCodeTag : IIdTag { }
public struct DstTag : IIdTag { }
public struct ArgTag : IIdTag { }
public struct ExtraArgsTag : IIdTag { }
public struct SId<TTag> : IEquatable<SId<TTag>> where TTag : struct, IIdTag {
	private readonly SIdVal Val;
	public SId ( int type, int id ) => Val = new SIdVal ( type, id );
	public SId ( SIdVal val ) => Val = val;
	//public static implicit operator ushort ( SId<TTag> op ) => (ushort)(op.Val & SCLInterpreter.VarIdMask);
	public bool Equals ( SId<TTag> other ) => Val.RawValue == other.Val.RawValue;
	public override bool Equals ( object? obj ) => obj is SId<TTag> other && Equals ( other );
	public override int GetHashCode () => Val.RawValue.GetHashCode ();
	public override string ToString () => $"{Val.ValueType}${Val.ValueId}";
	public readonly ushort ValueType => Val.ValueType;
	public readonly ushort ValueId => Val.ValueId;
	public readonly ushort RawValue => Val.RawValue;
	public readonly SIdVal Generic => Val;
}

internal struct CmdCall {
	public enum PtrType { Variable, Constant, ExtraArgument }

	public readonly SIdVal opCode;
	public readonly SIdVal dst;
	public readonly SIdVal arg1;
	public readonly SIdVal arg2;
	public readonly SIdVal arg3;
	public readonly SIdVal arg4;
	public readonly ushort flags;
	public readonly SIdVal extraArgs; // Currently unused, reserved for future use

	public const int MaxDirectArgs = 4;
	public SIdVal ArgAt (int id) {
		return id switch {
			0 => arg1,
			1 => arg2,
			2 => arg3,
			3 => arg4,
			_ => throw new IndexOutOfRangeException ( "CmdCall only supports up to 5 direct arguments." )
		};
	}

	public CmdCall ( SId<OpCodeTag> opCode, SId<DstTag> target, ushort flagReq, params SId<ArgTag>[] args ) {
		if ( args.Length > 4 ) throw new NotSupportedException ( "Cannot directly assign more than 4 arguments per call. Please use index into shared pool of extra arguments." );
		if ( opCode.ValueId < 0 || opCode.ValueId > ushort.MaxValue ) throw new NotSupportedException ( "OpCode must be a positive number between 0 and 65535." );
		this.opCode = opCode.Generic;
		dst = target.Generic;
		flags = flagReq;
		int N = args.Length;
		arg1 = N > 0 ? args[0].Generic : new SIdVal ( 0, 0 );
		arg2 = N > 1 ? args[1].Generic : new SIdVal ( 0, 0 );
		arg3 = N > 2 ? args[2].Generic : new SIdVal ( 0, 0 );
		arg4 = N > 3 ? args[3].Generic : new SIdVal ( 0, 0 );
		extraArgs = new SIdVal ( 0, 0 );
	}

	public CmdCall ( int opCode, List<CmdCall> extras, params int[] args ) {
		throw new NotImplementedException ();
	}

	public override string ToString () {
		string ret = opCode.ToString ()
				.Replace ( "$16382", "$J" )
				.Replace ( "$16381", "$F" )
				.Replace ( "$16380", "$T" )
				.Replace ( "$16379", "$S" );
		return $"!{ret,-4}->{dst,4} [{arg1}, {arg2}, {arg3}, {arg4}], ~({flags})";
	}
}

internal struct CmdExtraArgs {

}
internal abstract class AExternFcn : ICommand {
	protected List<(string name, DataTypeDefinition type, string description)> IndexerArg = [
		("input", null, "Indexer placeholder")
	];
	protected string CmnName = "External Command - Empty";
	protected DataTypeDefinition ResultType = null;
	public bool IsInitialized => ResultType != null && argBuffer != null;
	protected IDataType[] argBuffer = null;

	public string CmdCode { get; init; }
	public string CommonName => CmnName;
	public string Description => "A placeholder for an external command.";
	public int ArgC => argBuffer.Length;
	public IReadOnlyList<(string name, DataTypeDefinition type, string description)> Args => IndexerArg;
	public DataTypeDefinition ReturnType => ResultType;

	protected AExternFcn (string cmdName) {
		CmdCode = cmdName;
		CmnName = "External Command - " + cmdName;
	}

	public IDataType Execute ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args ) {
		for ( int i = 0; i < argBuffer.Length; i++ )
			argBuffer[i] = runtime.GetVar ( args[i] );
		return ExecuteInternal ( runtime );
	}

	public IDataType ExecuteSafe ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args, ref List<string> progress ) {
		if ( args.Count != argBuffer.Length )
			throw new InvalidOperationException ( $"Invalid argument count for external command '{CmnName}': expected {argBuffer.Length}, got {args.Count}." );
		for ( int i = 0; i < argBuffer.Length; i++ ) {
			argBuffer[i] = runtime.SafeGetVar ( args[i] );
			if ( argBuffer[i].Definition.Equals ( IndexerArg[i].type ) == false )
				throw new InvalidOperationException ( $"Invalid argument type for external command '{CmnName}' at argument {i}: expected {IndexerArg[i].type.Name}, got {argBuffer[i].Definition.Name}." );
		}
		return ExecuteInternal ( runtime );
	}

	protected void Set ( string commonName, DataTypeDefinition resultType, DataTypeDefinition[] argType ) {
		CmnName = "External Command - " + commonName;
		ResultType = resultType;
		while ( argType.Length != IndexerArg.Count )
			IndexerArg.Add ( ($"input{IndexerArg.Count}", null, "Indexer placeholder") );
		for ( int i = 0; i < argType.Length; i++ )
			IndexerArg[i] = ($"input{i}", argType[i], $"Input argument of type {argType[i].Name}");
		argBuffer = new IDataType[argType.Length];
	}

	protected abstract IDataType ExecuteInternal ( ISCLRuntime runtime );
}

internal class ExternFunction : AExternFcn {
	private Func<ISCLRuntime, IDataType[], IDataType> MapperFunction;

	public ExternFunction ( string cmdName, DataTypeDefinition[] inputTypes, DataTypeDefinition outputType ) : base ( cmdName ) {
		Set ( "External Function - Empty", outputType, inputTypes, null );
	}

	protected override IDataType ExecuteInternal ( ISCLRuntime runtime ) => MapperFunction ( runtime, argBuffer );

	public void Set ( string commonName, DataTypeDefinition resultType, DataTypeDefinition[] argTypes, Func<ISCLRuntime, IDataType[], IDataType> function ) {
		Set ( commonName, resultType, argTypes );
		MapperFunction = function;
	}
}

internal class ExternMapper : AExternFcn {
	private Func<ISCLRuntime, IDataType, IDataType> MapperFunction;

	public ExternMapper ( string cmdName, DataTypeDefinition inputType, DataTypeDefinition outputType ) : base ( cmdName ) {
		Set ( "External Mapper - Empty", outputType, inputType, null );
	}

	protected override IDataType ExecuteInternal ( ISCLRuntime runtime ) => MapperFunction ( runtime, argBuffer[0] );

	public void Set ( string commonName, DataTypeDefinition resultType, DataTypeDefinition argType, Func<ISCLRuntime, IDataType, IDataType> function ) {
		Set ( commonName, resultType, [argType] );
		MapperFunction = function;
	}
}