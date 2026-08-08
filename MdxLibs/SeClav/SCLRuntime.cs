using System;
using System.Collections.Generic;
using System.Linq;
using MdxLibs.Core;
using MdxLibs.Services;

namespace SeClav;
internal class SCLRuntimeLocked ( ISCLParsedScript script ) : SCLRuntimeWorker ( script );

internal class SCLRuntimeExpandable : SCLRuntimeWorker {
	private readonly List<IDataType> variables;
	private readonly List<IDataType> results;

	public SCLRuntimeExpandable ( ISCLParsedScript script ) : base ( script, out var vars, out var res ) {
		variables = vars;
		results = res;
	}

	public new void Expand ( ISCLParsedScript script ) {
		base.Expand ( script );
		var tmpVariables = script.VariableTypes.Select ( t => {
			if ( t < 0 || t >= script.DataTypes.Count )
				throw new IndexOutOfRangeException ( $"Variable type index {t} is out of range <0|{script.DataTypes.Count}>." );
			return script.DataTypes[t].Default;
		} ).ToList ().AsReadOnly ();
		var tmpResults = script.ResultTypes.Select ( t => {
			if ( t < 0 || t >= script.DataTypes.Count )
				throw new IndexOutOfRangeException ( $"Result type index {t} is out of range <0|{script.DataTypes.Count}>." );
			return script.DataTypes[t].Default;
		} ).ToList ().AsReadOnly ();

		if (tmpVariables.Count < Variables.Count || tmpResults.Count < Results.Count)
			throw new InvalidOperationException ( $"Cannot shrink the runtime memory. Current variables: {Variables.Count}, new variables: {tmpVariables.Count}. Current results: {Results.Count}, new results: {tmpResults.Count}." );

		for ( int i = 0; i < Variables.Count; i++ ) {
			if ( Variables[i].Definition == tmpVariables[i].Definition ) continue;
			throw new InvalidOperationException ( $"Cannot change existing variable definition. Variable ID {i} has definition {Variables[i].Definition.Name}({Variables[i].Definition.Owner}), attempted to change to {tmpVariables[i].Definition.Name}({tmpVariables[i].Definition.Owner})." );
		}
		for ( int i = 0; i < Results.Count; i++ ) {
			if ( Results[i] == null && tmpResults[i] == null ) continue;
			if ( Results[i].Definition == tmpResults[i].Definition ) continue;
			throw new InvalidOperationException ( $"Cannot change existing result definition. Result ID {i} has definition {Results[i].Definition.Name}({Results[i].Definition.Owner}), attempted to change to {tmpResults[i].Definition.Name}({tmpResults[i].Definition.Owner})." );
		}

		for (int i = Variables.Count; i < tmpVariables.Count; i++)
			variables.Add ( tmpVariables[i] );
		for (int i = Results.Count; i < tmpResults.Count; i++)
			results.Add ( tmpResults[i] );
	}
}

internal abstract class SCLRuntimeWorker : ISCLRuntime {
	public ISCLParsedScript Script;
	public readonly IReadOnlyList<IDataType> Variables;
	public readonly IReadOnlyList<IDataType> Results;
	private ISCLRuntime.SCLFlags flags;

	private const int VarIdMask = SCLInterpreter.VarIdMask;
	private const int VarIdShift = SCLInterpreter.VarIdShift;


	protected SCLRuntimeWorker ( ISCLParsedScript script ) : this ( script, out var _, out var _ ) { }

	protected SCLRuntimeWorker ( ISCLParsedScript script, out List<IDataType> variables, out List<IDataType> results ) {
		Script = script ?? throw new ArgumentNullException ( nameof ( script ) );

		variables = script.VariableTypes.Select ( t => {
			if ( t < 0 || t >= script.DataTypes.Count )
				throw new IndexOutOfRangeException ( $"Variable type index {t} is out of range <0|{script.DataTypes.Count}>." );
			return script.DataTypes[t].Default;
		} ).ToList ();
		results = script.ResultTypes.Select ( t => {
			if ( t < 0 || t >= script.DataTypes.Count )
				throw new IndexOutOfRangeException ( $"Result type index {t} is out of range <0|{script.DataTypes.Count}>." );
			return script.DataTypes[t].Default;
		} ).ToList ();
		Variables = variables.AsReadOnly ();
		Results = results.AsReadOnly ();

		foreach ( var (id, getter) in script.Getters) {
			if ( id < 0 || id >= Variables.Count )
				throw new IndexOutOfRangeException ( $"Getter ID {id} is out of range <0|{Variables.Count}>." );
			var val = getter ();
			if (val.Definition != Variables[id].Definition )
				throw new InvalidOperationException ( $"Getter for variable ID {id} returned type {val.Definition.Name}({val.Definition.Owner}), expected {Variables[id].Definition.Name}({Variables[id].Definition.Owner})." );
			Variables[id].Assign ( val );
		}
	}

	protected void Expand ( ISCLParsedScript script ) {
		Script = script ?? throw new ArgumentNullException ( nameof ( script ) );
	}

	public IDataType SafeGetVar ( SIdVal varID ) {
		ushort varType = varID.ValueType;
		ushort ID = varID.ValueId;
		// FIXME: Never use 0 as a valid ID! Debugging this is a nightmare. Treat 0 as invalid ID.
		switch ( varType ) {
		case SCLInterpreter.VarTypeID:
			if ( ID >= Variables.Count )
				throw new IndexOutOfRangeException ( $"Variable ID {ID} is out of range <0|{Variables.Count}>." );
			return Variables[ID];
		case SCLInterpreter.ConstTypeID:
			if ( ID >= Script.Constants.Count )
				throw new IndexOutOfRangeException ( $"Constant ID {ID} is out of range <0|{Script.Constants.Count}>." );
			return Script.Constants[ID];
		case SCLInterpreter.ResultTypeID:
			if ( ID >= Results.Count )
				throw new IndexOutOfRangeException ( $"Result ID {ID} is out of range <0|{Results.Count}>." );
			return Results[ID];
		case SCLInterpreter.InvalidTypeID:
			throw new InvalidOperationException ( $"Index type '{SCLInterpreter.InvalidTypeID}' is reserved for future use and cannot be used yet." );
		default:
			throw new ArgumentOutOfRangeException ( nameof ( varID ), $"Invalid variable ID {varID}. The type {varType} is outside of supported range!" );
		}
	}

	public void SafeSetVar ( SIdVal varID, IDataType value ) {
		switch ( varID.ValueType ) {
		case SCLInterpreter.VarTypeID:
			if ( varID.ValueId < 0 )
				throw new AccessViolationException ( $"Detected attemt to write into protected memory at {varID.ValueId}" );
			if ( varID.ValueId >= Variables.Count )
				throw new IndexOutOfRangeException ( $"Variable ID {varID} is out of range." );
			if ( !value.Definition.Equals ( Variables[varID.ValueId].Definition ) )
				throw new InvalidOperationException ( $"Cannot set variable {varID} to type {value.Definition.Name}, expected {Variables[varID.ValueId].Definition.Name}." );
			Variables[varID.ValueId].Assign ( value );
			break;
		case SCLInterpreter.ConstTypeID:
			throw new AccessViolationException ( $"Detected attemt to write into protected memory at constant ID {varID.ValueId}" );
		case SCLInterpreter.ResultTypeID:
			if ( varID.ValueId >= Results.Count )
				throw new IndexOutOfRangeException ( $"Result ID {varID} is out of range." );
			if ( value.Definition != Results[varID.ValueId].Definition )
				throw new InvalidOperationException ( $"Cannot set result {varID} to type {value.Definition.Name}, expected {Results[varID.ValueId].Definition.Name}." );
			Results[varID.ValueId].Assign ( value );
			break;
		case SCLInterpreter.InvalidTypeID:
			throw new InvalidOperationException ( "Index type '0' is reserved for future use and cannot be used yet." );
		default: throw new ArgumentOutOfRangeException ( nameof ( varID ), $"Invalid variable ID {varID}." );
		}
	}
	//public IDataType GetVar ( int varID ) => varID < 0 ? Script.Constants[IDtoConstPtr ( varID )] : Variables[varID];
	public IDataType GetVar ( SIdVal varID ) => (varID.ValueType) switch {
		SCLInterpreter.VarTypeID => Variables[varID.ValueId],
		SCLInterpreter.ConstTypeID => Script.Constants[varID.ValueId],
		SCLInterpreter.ResultTypeID => Results[varID.ValueId],
		SCLInterpreter.InvalidTypeID => throw new InvalidOperationException ( "Index type '0' is reserved for future use and cannot be used yet." ),
		_ => throw new ArgumentOutOfRangeException ( nameof ( varID ), $"Invalid variable ID {varID}." )
	};
	//public void SetVar ( int varID, IDataType value ) => Variables[varID].Assign ( value );
	public void SetVar ( SIdVal varID, IDataType value ) {
		switch ( varID.ValueType ) {
		case SCLInterpreter.VarTypeID: Variables[varID.ValueId].Assign ( value ); break;
		case SCLInterpreter.ResultTypeID: Results[varID.ValueId].Assign ( value ); break;
		default: throw new ArgumentOutOfRangeException ( nameof ( varID ), $"Invalid variable ID {varID}." );
		}
	}

	public void UpdateMemoryInfo ( ref Dictionary<string, string> memInfo ) {
		memInfo["Variables"] = $"{Variables.Count} variables: {string.Join ( " | ", Variables.Select ( ( v, i ) => $"V{i}={v}" ) )}";
		memInfo["Results"] = $"{Results.Count} results : {string.Join ( " | ", Results.Select ( ( r, i ) => $"R{i}={r}" ) )}";
		memInfo["Constants"] = $"{Script.Constants.Count} constants : {string.Join ( " | ", Script.Constants.Select ( ( c, i ) => $"C{i}={c}" ) )}";
	}

	public ISCLRuntime.SCLFlags GetFlags () => flags;
	public void SetFlag ( ISCLRuntime.SCLFlags value ) => flags |= value;
	public void ResetFlag ( ISCLRuntime.SCLFlags value ) => flags &= ~value;
}




public class SCLDebugger {
	public struct Entry {
		private static string FlagsMarks = "N!~=><6789ABCDEFzyxwvutsrqponmlkjihgfedcba";
		public int PC;
		public string Line;
		public string CmdCall;
		public ISCLRuntime.SCLFlags Flags;
		public List<(SIdVal ptr, string name, string val)> MemoryInfo;
		internal Entry ( int pc, CmdCall cmd, string dissassembly, ISCLRuntime runtime ) {
			PC = pc;
			Line = dissassembly;
			CmdCall = cmd.ToString ()
				.Replace ( "0$16382", "0$J" )
				.Replace ( "0$16381", "0$F" )
				.Replace ( "0$16380", "0$T" )
				.Replace ( "0$16379", "0$S" );
			MemoryInfo = [];
			Flags = runtime.GetFlags ();
		}
		public override readonly string ToString () {
			string memInfoStr = string.Join ( "  |  ", MemoryInfo.Select ( m => $"{m.ptr} '{m.name}' = {m.val}" ) );
			BitField flagsField = new ();
			flagsField.Value = (uint)Flags;
			char[] flagChars = new char[sizeof(ushort)*8];
			for ( int i = 0; i < sizeof(ushort)*8; i++ )
				flagChars[i] = flagsField[i] ? FlagsMarks[i] : '_';
			string flagsStr = new ( flagChars.Reverse ().ToArray () );
			return $"PC={PC,-2}: {Line,-56} \t|  {CmdCall, -40} {flagsStr}  |   {memInfoStr}";
		}
	}
	public List<(SIdVal id, string name)> CapturedVars = [];
	public List<Entry> ExecutionLog = [];
	public void ClearLog () => ExecutionLog.Clear ();
	public int LoggingRate = 1; // Log every N commands, set to 0 to disable logging

	internal int LogExecution ( int PC, CmdCall cmd, string dissassembly, ISCLRuntime runtime) {
		Entry e = new ( PC, cmd, dissassembly, runtime );
		foreach ( var varID in CapturedVars )
			e.MemoryInfo.Add ( (varID.id, varID.name, runtime.SafeGetVar ( varID.id ).ToString ()) );
		ExecutionLog.Add ( e );
		return LoggingRate > 0 ? LoggingRate : int.MaxValue;
	}
}

public class SCLRuntimeHolder {
	internal SCLRuntimeLocked BaseRuntime;
	private readonly HashSet<string> UnassignedExternals = new ();
	private List<string> Errors = [], Log = [];
	private readonly int WatchdogMax;
	private SCLDebugger Debugger;
	private object PersistentStatus;

	public IReadOnlyList<string> RuntimeErrors => Errors.AsReadOnly ();
	public IReadOnlyList<string> RuntimeLog => Log.AsReadOnly ();

	public SCLDebugger SetupDebugger ( int loggingRate = 1 ) {
		Debugger = new SCLDebugger { LoggingRate = loggingRate };
		return Debugger;
	}

	public void ResetStatus () => PersistentStatus = null;

	internal SCLRuntimeHolder ( SCLRuntimeLocked runtime, int watchdogMax = int.MaxValue ) {
		if ( runtime == null )
			throw new ArgumentNullException ( nameof ( runtime ) );
		BaseRuntime = runtime;
		WatchdogMax = watchdogMax;

		List<string> allExternals = runtime.Script.InputVars.Keys
			.Union ( runtime.Script.InputSamplers.Keys )
			.Union ( runtime.Script.ExternFunctions.Keys )
			.ToList ();
		foreach ( var varName in allExternals )
			if ( !UnassignedExternals.Add ( varName ) )
				throw new InvalidOperationException ( $"Duplicate external identifier '{varName}' found in the script." );
	}

	public SCLRuntimeHolder ( SCLScriptHolder scriptHolder, int watchdogMax = int.MaxValue )
		: this ( new SCLRuntimeLocked ( scriptHolder?.ParsedScript ), watchdogMax ) { }

	public IDataType SafeGetVar ( SIdVal varID ) => BaseRuntime.SafeGetVar ( varID );
	public void SafeSetVar ( SIdVal varID, IDataType value ) => BaseRuntime.SafeSetVar ( varID, value );
	public T GetDefinition<T> () where T : DataTypeDefinition {
		Type type = typeof ( T );
		var ret = BaseRuntime.Script.DataTypes.FirstOrDefault ( x => x.GetType () == type ) as T;
		if ( ret == null )
			throw new InvalidOperationException ( $"Data type definition of type '{typeof ( T ).Name}' not found in the script." );
		return ret;
	}

	public IDataType Execute ( bool safe ) {
		if ( UnassignedExternals.Count > 0 )
			throw new InvalidOperationException ( $"Cannot execute script: Unassigned external variables: {string.Join ( ", ", UnassignedExternals )}." );
		SCLRunner runner = new ( BaseRuntime.Script, WatchdogMax );
		if (PersistentStatus != null )
			runner.PersistantStatus = PersistentStatus;

		if ( Debugger != null && Debugger.LoggingRate > 0 ) {
			runner.DebuggerCallback = Debugger.LogExecution;
			runner.nextBreakpoint = 0;
		}
		var ret = safe ? runner.ExecuteSafe ( BaseRuntime, [], ref Log ) :
			runner.Execute ( BaseRuntime, [] );
		PersistentStatus = runner.PersistantStatus;
		return ret;
	}

	public void ClearErrors () => Errors.Clear ();
	public void ClearLog () => Log.Clear ();

	public void SetExternVar ( string name, IDataType val, CoreBase owner ) {
		if ( !BaseRuntime.Script.InputVars.TryGetValue ( name, out SIdVal varID ) ) {
			// Don't throw here, just log the error and continue
			// Caller can thus always try to set all externals and let the script to choose if it's used or not
			Errors.Add ( $"Input variable '{name}' not found in the script." );
			return;
		}
		try {
			BaseRuntime.SafeSetVar ( varID, val );
			UnassignedExternals.Remove ( name );
		} catch ( Exception ex ) {
			owner?.PushDelayedError ( $"Failed to set external variable '{name}': {ex.Message}", ex );
		}
	}
	public void SetExternVar<DataTypeDef> ( string name, Func<DataTypeDef, IDataType> valCtor, CoreBase owner ) where DataTypeDef : DataTypeDefinition {
		if ( !BaseRuntime.Script.InputVars.TryGetValue ( name, out SIdVal varID ) ) {
			// Don't throw here, just log the error and continue
			// Caller can thus always try to set all externals and let the script to choose if it's used or not
			Errors.Add ( $"Input variable '{name}' not found in the script." );
			return;
		}
		try {
			var dataTypeDef = GetDefinition<DataTypeDef> ();
			BaseRuntime.SafeSetVar ( varID, valCtor ( dataTypeDef ) );
			UnassignedExternals.Remove ( name );
		} catch ( Exception ex ) {
			owner?.PushDelayedError ( $"Failed to set external variable '{name}': {ex.Message}", ex );
		}
	}

	public bool TryGetOutputVar (string name, CoreBase owner, out IDataType value) {
		value = null;
		if ( !BaseRuntime.Script.OutputVars.TryGetValue ( name, out SIdVal varID ) ) {
			Errors.Add ( $"Output variable '{name}' not found in the script." );
			return false;
		}
		try {
			value = BaseRuntime.SafeGetVar ( varID );
			return true;
		} catch ( Exception ex ) {
			owner?.PushDelayedError ( $"Failed to get output variable '{name}': {ex.Message}", ex );
			return false;
		}
	}

	public bool TryStoreOutputVar ( string name, CoreBase owner, ref IDataType value ) {
		if ( !BaseRuntime.Script.OutputVars.TryGetValue ( name, out SIdVal varID ) ) {
			Errors.Add ( $"Output variable '{name}' not found in the script." );
			return false;
		}
		try {
			value = BaseRuntime.SafeGetVar ( varID );
			return true;
		} catch ( Exception ex ) {
			owner?.PushDelayedError ( $"Failed to get output variable '{name}': {ex.Message}", ex );
			return false;
		}
	}

	public void SetExternSampler<InT, OutT> ( string name, Func<ISCLRuntime, IDataType, IDataType> sampler, CoreBase owner ) where InT : DataTypeDefinition where OutT : DataTypeDefinition {
		if ( !BaseRuntime.Script.InputSamplers.TryGetValue ( name, out int cmdIndex ) )
			throw new KeyNotFoundException ( $"Input sampler '{name}' not found in the script." );
		try {
			var cmd = BaseRuntime.Script.Commands[cmdIndex];
			if ( cmd is not ExternMapper externCmd )
				throw new InvalidOperationException ( $"Command at index {cmdIndex} for sampler '{name}' is not an extern sampler command." );
			externCmd.Set ( name, GetDefinition<OutT> (), GetDefinition<InT> (), sampler );
			UnassignedExternals.Remove ( name );
		} catch ( Exception ex ) {
			owner?.PushDelayedError ( $"Failed to set external sampler '{name}': {ex.Message}", ex );
		}
	}

	public void SetExternFunction<In1T, OutT> ( string name, Func<ISCLRuntime, IDataType[], IDataType> function, CoreBase owner )
		where In1T : DataTypeDefinition where OutT : DataTypeDefinition
		=> SetExternFcn ( name, [GetDefinition<In1T> ()], GetDefinition<OutT> (), function, owner );

	public void SetExternFunction<In1T, In2T, OutT> ( string name, Func<ISCLRuntime, IDataType[], IDataType> function, CoreBase owner )
		where In1T : DataTypeDefinition where In2T : DataTypeDefinition where OutT : DataTypeDefinition
		=> SetExternFcn ( name, [GetDefinition<In1T> (), GetDefinition<In2T> ()], GetDefinition<OutT> (), function, owner );

	public void SetExternFunction<In1T, In2T, In3T, OutT> ( string name, Func<ISCLRuntime, IDataType[], IDataType> function, CoreBase owner )
		where In1T : DataTypeDefinition where In2T : DataTypeDefinition where In3T : DataTypeDefinition where OutT : DataTypeDefinition
		=> SetExternFcn ( name, [GetDefinition<In1T> (), GetDefinition<In2T> (), GetDefinition<In3T> ()], GetDefinition<OutT> (), function, owner );

	public void SetExternFunction<In1T, In2T, In3T, In4T, OutT> ( string name, Func<ISCLRuntime, IDataType[], IDataType> function, CoreBase owner )
		where In1T : DataTypeDefinition where In2T : DataTypeDefinition where In3T : DataTypeDefinition where In4T : DataTypeDefinition where OutT : DataTypeDefinition
		=> SetExternFcn ( name, [GetDefinition<In1T> (), GetDefinition<In2T> (), GetDefinition<In3T> (), GetDefinition<In4T> ()], GetDefinition<OutT> (), function, owner );

	// More arguments isn't currently supported! That would require either expanding the CmdCall structure or supporting ExtraArguments in SCLRunner.

	private void SetExternFcn ( string name, DataTypeDefinition[] inArgs, DataTypeDefinition outArg, Func<ISCLRuntime, IDataType[], IDataType> function, CoreBase owner ) {
		foreach ( var def in inArgs )
			if ( def == null )
				throw new InvalidOperationException ( $"Input argument type for extern function '{name}' is not found in the script's data types." );

		if ( !BaseRuntime.Script.ExternFunctions.TryGetValue ( name, out int cmdIndex ) )
			throw new KeyNotFoundException ( $"Extern function '{name}' not found in the script." );
		try {
			var cmd = BaseRuntime.Script.Commands[cmdIndex];
			if ( cmd is not ExternFunction externCmd )
				throw new InvalidOperationException ( $"Command at index {cmdIndex} for function '{name}' is not an extern function command." );
			externCmd.Set ( name, outArg, inArgs, function );
			UnassignedExternals.Remove ( name );
		} catch ( Exception ex ) {
			owner?.PushDelayedError ( $"Failed to set external function '{name}': {ex.Message}", ex );
		}
	}
}