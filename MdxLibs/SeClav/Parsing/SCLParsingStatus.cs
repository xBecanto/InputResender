using System;
using System.Collections.Generic;
using System.Linq;
using MdxLibs.Services;
using SeClav.Modules;
using TOpCode = SeClav.SId<SeClav.OpCodeTag>;
using TDst = SeClav.SId<SeClav.DstTag>;
using TArg = SeClav.SId<SeClav.ArgTag>;
using SCLContextInner = SeClav.Parsing.SCLParsingStatus.SCLParsingContextInner;

namespace SeClav.Parsing;
public class SCLParsingContext {
	private readonly SCLContextInner Inner;

	internal SCLParsingContext ( SCLContextInner inner ) => Inner = inner;

	public void RegisterVariable ( string Name, Func<IDataType> getter ) => Inner.RegisterVariable ( Name, getter );
}

internal class SCLParsingStatus {
	internal class SCLParsedScript : ISCLParsedScript {
		public IReadOnlyList<DataTypeDefinition> DataTypes { get; }
		public IReadOnlyList<ICommand> Commands { get; }
		public IReadOnlyList<CmdCall> CommandIndices { get; }
		public IReadOnlyList<IDataType> Constants { get; }
		public IReadOnlyList<IModuleInfo> Modules { get; }
		public IReadOnlyList<int> VariableTypes { get; }
		public IReadOnlyList<int> ResultTypes { get; }
		public IReadOnlyList<string> Disassembly { get; }
		public IReadOnlyDictionary<int, Func<IDataType>> Getters { get; }
		public IReadOnlyDictionary<string, SIdVal> InputVars { get; }
		public IReadOnlyDictionary<string, int> InputSamplers { get; }
		public IReadOnlyDictionary<string, SIdVal> OutputVars { get; }
		public IReadOnlyDictionary<string, int> ExternFunctions { get; }

		public SCLParsedScript ( SCLParsingStatus parsingStatus ) {
			// Also could have/extract from status some disassembly info (closer discussion in diary)

			foreach ( (string targetState, int cmdIndex, TOpCode opCode) in parsingStatus.StateJumps ) {
				if ( !parsingStatus.StateStarts.TryGetValue ( targetState, out int stateStart ) ) {
					if ( !parsingStatus.ReplaceNonexistingJumpsWithNOPs )
						throw new SCLParsingException (
							$"State jump target '{targetState}' does not have a registered state start."
						);

					parsingStatus.commandIndices[cmdIndex] = SCL_NOP.Create ( parsingStatus );
					continue;
				}

				CmdCall jmpCall = parsingStatus.commandIndices[cmdIndex];
				parsingStatus.commandIndices[cmdIndex] = new CmdCall
					( opCode
					, SCLInterpreter.CrDst ( stateStart )
					, jmpCall.flags
					, new TArg ( jmpCall.arg1 )
					, new TArg ( jmpCall.arg2 )
					, new TArg ( jmpCall.arg3 )
					, new TArg ( jmpCall.arg4 )
					);
			}

			DataTypes = parsingStatus.dataTypes.ToList ().AsReadOnly ();
			Commands = parsingStatus.commands.ToList ().AsReadOnly ();
			CommandIndices = parsingStatus.commandIndices.ToList ().AsReadOnly ();
			Modules = parsingStatus.modules.ToList ().AsReadOnly ();
			Constants = parsingStatus.constants.ToList ().AsReadOnly ();
			Disassembly = parsingStatus.disassembly.ToList ().AsReadOnly ();
			VariableTypes = parsingStatus.variables.Select ( v => v.dataType ).ToList ().AsReadOnly ();
			ResultTypes = parsingStatus.results.Select ( r => parsingStatus.dataTypes.IndexOf ( r.Definition ) ).ToList ().AsReadOnly ();
			Getters = new Dictionary<int, Func<IDataType>> ( parsingStatus.getters );
			InputVars = parsingStatus.inputVars.AsReadOnly ();
			InputSamplers = parsingStatus.inputSamplers.AsReadOnly ();
			OutputVars = parsingStatus.outputVars.AsReadOnly ();
			ExternFunctions = parsingStatus.externFunctions.AsReadOnly ();

			if (Disassembly.Count != CommandIndices.Count)
				throw new SCLParsingException ( $"Disassembly count ({Disassembly.Count}) does not match command indices count ({CommandIndices.Count})." );
		}
	}

	internal class SCLDebugInfo : ISCLDebugInfo {
		public ISCLParsedScript Script { get; }
		public IReadOnlyDictionary<SId<ArgTag>, string> VarNames { get; }
		public string CompileLog { get; }
		public string MemoryInfo { get; }

		public SCLDebugInfo ( SCLParsingStatus parsingStatus, List<string> Log = null ) {
			Script = new SCLParsedScript ( parsingStatus );
			VarNames = parsingStatus.variables
				.Select ( ( v, i ) => (SCLInterpreter.CrArgVar ( i ), v.name) )
				.ToDictionary ( kv => kv.Item1, kv => kv.Item2 );
			//CompileLog = string.Join ( Environment.NewLine, Log );
			CompileLog = Log is null ? "No log provided ..." : string.Join ( Environment.NewLine, Log );
			MemoryInfo = string.Join ( Environment.NewLine, parsingStatus.MemoryInfo.Select ( kv => $"{kv.Key}: {string.Join ( ", ", kv.Value )}" ) );
		}
	}



	/// <summary>A gateway for other methods (like Components) to access some internal data of SCLParsingStatus. Mostly to register meta methods.</summary>
	internal class SCLParsingContextInner {
		readonly SCLParsingStatus Status;

		public SCLParsingContextInner ( SCLParsingStatus status ) {
			Status = status;
		}

		public void RegisterVariable (string Name, Func<IDataType> getter) {
			ArgumentNullException.ThrowIfNull ( Name );
			ArgumentNullException.ThrowIfNull ( getter );
			if ( Status.variables.Any ( v => v.name == Name ) )
				throw new SCLDuplicateDefinitionException ( $"Variable '{Name}' is already defined." );
			int varID = Status.variables.Count;
			var defVal = getter ();
			int dataTypeID = Status.GetDataTypeID ( defVal.Definition );
			Status.variables.Add ( ( dataTypeID, Name ) );
			Status.getters[varID] = getter;
			Status.UpdateMemoryInfo ();
		}
	}




	readonly List<DataTypeDefinition> dataTypes = [];
	readonly List<ICommand> commands = [];
	readonly List<CmdCall> commandIndices = [];
	readonly List<(int dataType, string name)> variables = [];
	readonly List<IModuleInfo> modules = [];
	readonly List<IDataType> constants = [];
	readonly List<IDataType> results = []; // FIXME: This should only be <int> as we don't yet know the value, only the data type
	readonly Dictionary<int, Func<IDataType>> getters = [];
	private readonly Dictionary<string, HashSet<string>> alts = [];
	public Dictionary<string, SIdVal> inputVars = [];
	public Dictionary<string, int> inputSamplers = [];
	public Dictionary<string, SIdVal> outputVars = [];
	public Dictionary<string, int> externFunctions = [];
	public HashSet<string> PossibleCommands = [];
	public bool ReplaceNonexistingJumpsWithNOPs = false;
	//readonly List<Action<SCLParsedScript>> OnLoad = [];

	readonly Dictionary<string, DataTypeDefinition> dataTypeMap = [];
	readonly Dictionary<string, HashSet<ICommand>> commandMap = [];
	readonly Dictionary<string, IMacro> macros = [];
	readonly Dictionary<string, PraeDirective> praeDirectives = [];

	readonly Dictionary<string, int> StateStarts = [];
	readonly List<(string, int, TOpCode)> StateJumps = [];

	readonly Dictionary<string, string> MemoryInfo = [];
	public IReadOnlyDictionary<string, string> GetMemoryInfoRef () => MemoryInfo;

	public SCLParsedScript GetResult () => new SCLParsedScript ( this );
	public SCLParsingContext GetContext () => new SCLParsingContext ( new SCLContextInner ( this ) );

	readonly List<string> disassembly = [];
	public string LastLine { private get; set; } = "";

	public SCLParsingStatus () {
		dataTypeMap["void"] = new SCLT_Void ();
		dataTypes.Add ( dataTypeMap["void"] );
		results.Add ( new SCLT_Void.VoidData ( dataTypes[0] as SCLT_Void ) );
	}

	private void PushCmd ( string name, ICommand cmd ) {
		PossibleCommands.Add ( name );
		if ( commandMap.TryGetValue ( name, out var existing ) ) {
			if ( !existing.Add ( cmd ) )
				throw new SCLDuplicateDefinitionException ( $"Command '{cmd.CmdCode}' is already defined." );
		} else commandMap[name] = [cmd];
	}

	public void RegisterModule ( IModuleInfo module ) {
		ArgumentNullException.ThrowIfNull ( module );
		if ( modules.Any ( m => m.Name == module.Name ) )
			throw new SCLDuplicateDefinitionException ( $"Module '{module.Name}' is already registered." );
		modules.Add ( module );
		foreach ( var cmd in module.Commands ) {
			PushCmd ( cmd.CmdCode, cmd );
		}
		foreach ( var dt in module.DataTypes ) {
			if ( dataTypeMap.ContainsKey ( dt.Name ) )
				throw new SCLDuplicateDefinitionException ( $"Data type '{dt.Name}' is already defined." );
			dataTypeMap[dt.Name] = dt;
		}
		foreach ( var macro in module.Macros ) {
			if ( macros.ContainsKey ( macro.CmdCode ) )
				throw new SCLDuplicateDefinitionException ( $"Macro '{macro.CmdCode}' is already defined." );
			macros[macro.CmdCode] = macro;
		}
		foreach ( var prae in module.PraeDirectives ) {
			if ( praeDirectives.ContainsKey ( prae.Key ) )
				throw new SCLDuplicateDefinitionException ( $"Prae directive '{prae.Key}' is already defined." );
			praeDirectives[prae.Key] = prae.Value;
		}

		foreach ( var altNames in module.AltNames ) {
			PossibleCommands.Add ( altNames.Key );
			if ( alts.TryGetValue ( altNames.Key, out var existing ) ) {
				foreach ( var alt in altNames.Value ) existing.Add ( alt );
			} else alts[altNames.Key] = [..altNames.Value];
		}
	}

	public bool IsModuleRegistered ( string moduleName ) {
		ArgumentNullException.ThrowIfNull ( moduleName );
		return modules.Any ( m => m.Name == moduleName );
	}

	public PraeDirective TryGetPraeDirective ( string name ) {
		ArgumentNullException.ThrowIfNull ( name );
		return praeDirectives.TryGetValue ( name, out var action ) ? action : null;
	}
	public void RunPraeDirective ( PraeDirective prae, ArgParser args ) {
		ArgumentNullException.ThrowIfNull ( prae );
		prae ( GetContext (), args );
	}

	public (int dataType, string name) GetVarInfo ( TDst varID ) {
		if ( varID.ValueId >= variables.Count )
			throw new ArgumentOutOfRangeException ( nameof ( varID ), $"Variable ID {varID} is out of range (0..{variables.Count - 1})." );
		return variables[varID.ValueId];
	}
	public (int dataType, string name) GetVarInfo ( TArg varID ) {
		if ( varID.ValueId >= variables.Count )
			throw new ArgumentOutOfRangeException ( nameof ( varID ), $"Variable ID {varID} is out of range (0..{variables.Count - 1})." );
		return variables[varID.ValueId];
	}

	public TOpCode RegisterCustomCmd ( ICommand cmd ) {
		ArgumentNullException.ThrowIfNull ( cmd );
		if ( commands.Any ( c => c.CmdCode == cmd.CmdCode ) )
			throw new SCLDuplicateDefinitionException ( $"Command '{cmd.CmdCode}' is already registered." );
		commands.Add ( cmd );
		PushCmd ( cmd.CmdCode, cmd );
		return SCLInterpreter.CrOpCode ( commands.Count - 1 );
	}

	public void RegisterPraeDirective (string name, PraeDirective prae) {
		ArgumentNullException.ThrowIfNull ( prae );
				if ( praeDirectives.ContainsKey ( name ) )
			throw new SCLDuplicateDefinitionException ( $"Prae directive '{name}' is already defined." );
		praeDirectives[name] = prae;
	}

	public void RegisterMacro ( IMacro macro ) {
		ArgumentNullException.ThrowIfNull ( macro );
		if ( macros.ContainsKey ( macro.CmdCode ) )
			throw new SCLDuplicateDefinitionException ( $"Macro '{macro.CmdCode}' is already defined." );
		macros[macro.CmdCode] = macro;
	}

	public IMacro TryGetMacro (string cmd) {
		ArgumentNullException.ThrowIfNull ( cmd );
		if ( !macros.TryGetValue ( cmd, out IMacro macro ) ) return null;
		return macro;
	}

	[return: System.Diagnostics.CodeAnalysis.NotNull()]
	public HashSet<ICommand> TryGetCommands ( string cmd ) {
		ArgumentNullException.ThrowIfNull ( cmd );
		var ret = !commandMap.TryGetValue ( cmd, out HashSet<ICommand> command ) ? [] : command;
		if ( !alts.TryGetValue ( cmd, out HashSet<string> altNames ) ) return ret;

		foreach ( var alt in altNames ) {
			if ( commandMap.TryGetValue ( alt, out HashSet<ICommand> altCommands ) )
				ret.UnionWith ( altCommands );
		}
		return ret;
	}
	public void ConfirmCommand ( ICommand cmd ) {
		ArgumentNullException.ThrowIfNull ( cmd );
		if ( !commands.Contains ( cmd ) ) {
			if ( !commandMap.ContainsKey ( cmd.CmdCode ) )
				throw new SCLParsingException ( $"Command '{cmd.CmdCode}' ({cmd.CommonName}) is not registered nor present in any loaded module." );
			commands.Add ( cmd );
		}
	}

	public int GetCommandID ( ICommand cmd ) {
		ArgumentNullException.ThrowIfNull ( cmd );
		if ( !commands.Contains ( cmd ) ) {
			if ( !commandMap.ContainsKey ( cmd.CmdCode ) )
				throw new SCLParsingException ( $"Command '{cmd.CmdCode}' ({cmd.CommonName}) is not registered nor present in any loaded module." );
			commands.Add ( cmd );
		}
		return commands.IndexOf ( cmd );
	}

	private void TranslateDataType (ref DataTypeDefinition dataType) {
		if ( dataTypeMap.TryGetValue ( dataType.Name, out var existing ) )
			dataType = existing;
		else throw new SCLParsingException ( $"Data type '{dataType.Name}' is not registered." );
		if (!dataTypes.Contains ( dataType ) )
			dataTypes.Add ( dataType );
	}

	public DataTypeDefinition GetDataType ( string name ) {
		ArgumentNullException.ThrowIfNull ( name );
		return dataTypeMap.GetValueOrDefault ( name, null );
	}

	public DataTypeDefinition GetDataType ( ICommand cmd, int id) {
		ArgumentNullException.ThrowIfNull ( cmd );
		if ( id < 0 || id >= cmd.ArgC )
			throw new ArgumentOutOfRangeException ( nameof ( id ), $"Command '{cmd.CmdCode}' ({cmd.CommonName}) has only {cmd.ArgC} arguments, index {id} is out of range." );
		return GetDataType ( cmd.Args[id].type.Name );
	}

	public int GetDataTypeID ( DataTypeDefinition dataType ) {
		ArgumentNullException.ThrowIfNull ( dataType );
		TranslateDataType ( ref dataType );
		int typeDef = dataTypes.IndexOf ( dataType );
		if ( typeDef < 0 ) { typeDef = dataTypes.Count; dataTypes.Add ( dataType ); }
		return typeDef;
	}

	//public List<DataTypeDefinition> MapDataTypes ( ICollection<DataTypeDefinition> defs ) {
	//	ArgumentNullException.ThrowIfNull ( defs );
	//	List<DataTypeDefinition> mapped = [];
	//	foreach ( var def in defs ) {
	//		int id = dataTypes.IndexOf ( def );
	//		mapped.Add ( id >= 0 ? dataTypes[id] : null );
	//	}
	//	return mapped;
	//}

	public TArg RegisterVariable ( int dataType, string name ) {
		ArgumentNullException.ThrowIfNull ( name );
		if ( string.IsNullOrEmpty ( name ) ) throw new ArgumentException ( "Variable name cannot be empty.", nameof ( name ) );
		if ( variables.Any ( v => v.name == name ) )
			throw new SCLDuplicateDefinitionException ( $"Variable '{name}' is already defined." );
		var varID = SCLInterpreter.CrArgVar ( variables.Count );
		variables.Add ( (dataType, name) );
		UpdateMemoryInfo ();
		return varID;
	}

	public void RegisterExterInVariable (int dataType, string name ) {
		var varID = RegisterVariable ( dataType, name );
		inputVars[name] = varID.Generic;
	}
	public void RegisterExterOutVariable ( int dataType, string name ) {
		var varID = RegisterVariable ( dataType, name );
		outputVars[name] = varID.Generic;
	}
	public void RegisterExterSampler ( string name, DataTypeDefinition dataTypeIn, DataTypeDefinition dataTypeOut ) {
		ArgumentNullException.ThrowIfNullOrWhiteSpace ( name, nameof ( name ) );
		ArgumentNullException.ThrowIfNull ( dataTypeIn, nameof ( dataTypeIn ) );
		ArgumentNullException.ThrowIfNull ( dataTypeOut, nameof ( dataTypeOut ) );

		TranslateDataType ( ref dataTypeIn );
		TranslateDataType ( ref dataTypeOut );

		ExternMapper sampler = new ( name, dataTypeIn, dataTypeOut );
		PushCmd ( name, sampler );
		int cmdID = GetCommandID ( sampler );
		inputSamplers[name] = cmdID;
	}
	public void RegisterExterFunction ( string name, DataTypeDefinition returnType, DataTypeDefinition[] argTypes ) {
		ArgumentNullException.ThrowIfNullOrWhiteSpace ( name, nameof ( name ) );
		ArgumentNullException.ThrowIfNull ( returnType, nameof ( returnType ) );
		ArgumentNullException.ThrowIfNull ( argTypes, nameof ( argTypes ) );
		TranslateDataType ( ref returnType );
		for (int i = 0; i < argTypes.Length; i++ ) {
			ArgumentNullException.ThrowIfNull ( argTypes[i], nameof ( argTypes ) );
			TranslateDataType ( ref argTypes[i] );
		}
		ExternFunction function = new ( name, argTypes, returnType );
		PushCmd ( name, function );
		int cmdID = GetCommandID ( function );
		externFunctions[name] = cmdID;
	}

	public int RegisterStateStart ( string stateName ) {
		ArgumentNullException.ThrowIfNullOrWhiteSpace ( stateName, nameof ( stateName ) );
		if ( StateStarts.ContainsKey ( stateName ) )
			throw new SCLDuplicateDefinitionException ( $"State '{stateName}' is already defined." );
		return StateStarts[stateName] = commandIndices.Count;
	}
	public void RegisterStateJump ( string stateName, ushort opCode, ushort flagRequired ) {
		ArgumentNullException.ThrowIfNullOrWhiteSpace ( stateName, nameof ( stateName ) );
		TOpCode opCodeT = SCLInterpreter.CrOpCode ( opCode );
		StateJumps.Add ( ( stateName, commandIndices.Count, opCodeT ) );
		CmdCall jmp = new ( opCodeT, new TDst ( 0, 0 ), flagRequired );
		PushCommand ( jmp );

	}

	public bool TryGetVarID ( string name, out TArg varID ) {
		ArgumentNullException.ThrowIfNull ( name );
		int index = variables.FindIndex ( v => v.name == name );
		if ( index < 0 ) {
			varID = default;
			return false;
		}
		varID = SCLInterpreter.CrArgVar ( index );
		return true;
	}
	public TArg GetVarID ( string name ) {
		//return SCLInterpreter.CrArgVar ( index );
		if ( TryGetVarID ( name, out var varID ) ) return varID;
		throw new KeyNotFoundException ( $"Variable '{name}' not found." );
	}

	public TArg AddConstant ( IDataType value ) {
		ArgumentNullException.ThrowIfNull ( value );
		constants.Add ( value );
		UpdateMemoryInfo ();
		return SCLInterpreter.CrArgCon ( constants.Count - 1 );
	}

	public TDst RegisterResult ( DataTypeDefinition dataType ) {
		int typeDef = GetDataTypeID ( dataType );
		results.Add ( dataTypes[typeDef].Default );
		UpdateMemoryInfo ();
		return SCLInterpreter.CrDst ( results.Count - 1 );
	}

	public DataTypeDefinition GetTypeOfVar ( TDst varID ) {
		switch (varID.ValueType) {
			case SCLInterpreter.ResultTypeID:
				if ( varID.ValueId >= results.Count )
					throw new ArgumentOutOfRangeException ( nameof ( varID ), $"Result ID {varID} is out of range (0..{results.Count - 1})." );
				return results[varID.ValueId].Definition;
			case SCLInterpreter.VarTypeID:
				if ( varID.ValueId >= variables.Count )
					throw new ArgumentOutOfRangeException ( nameof ( varID ), $"Variable ID {varID} is out of range (0..{variables.Count - 1})." );
				return dataTypes[variables[varID.ValueId].dataType];
			case SCLInterpreter.ConstTypeID:
				if ( varID.ValueId >= constants.Count )
					throw new ArgumentOutOfRangeException ( nameof ( varID ), $"Constant ID {varID} is out of range (0..{constants.Count - 1})." );
				return constants[varID.ValueId].Definition;
		default:
				throw new ArgumentOutOfRangeException ( nameof ( varID ), $"Variable ID {varID} is not of variable type ({SCLInterpreter.VarTypeID})." );
		}
			throw new ArgumentOutOfRangeException ( nameof ( varID ), $"Variable ID {varID} is out of range (0..{variables.Count - 1})." );
	}
	public DataTypeDefinition GetTypeOfVar ( TArg varID ) => GetTypeOfVar ( new TDst ( varID.Generic ) );

	public void PushCommand ( CmdCall cmd ) {
		commandIndices.Add ( cmd );
		disassembly.Add ( LastLine );
		UpdateMemoryInfo ();
	}


	void UpdateMemoryInfo () {
		MemoryInfo[$"Variables ({SCLInterpreter.VarTypeID})"] = string.Join ( ", ", variables.Select ( v => $"{v.name}:{dataTypes[v.dataType].Name}" ) );
		MemoryInfo[$"Constants ({SCLInterpreter.ConstTypeID})"] = string.Join ( ", ", constants.Select ( ( c, i ) => $"{i}:{c.Definition.Name}={c}" ) );
		MemoryInfo[$"Results ({SCLInterpreter.ResultTypeID})"] = string.Join ( ", ", results.Select ( ( r, i ) => $"{i}:{r.Definition.Name}" ) );
		System.Text.StringBuilder sb = new ();
		for ( int i = 0; i < commandIndices.Count; i++ ) {
			if ( i > 0 ) sb.Append ( ", " );
			var c = commandIndices[i];
			string cmdCode = c.opCode.ValueId switch {
				ISCLParsedScript.JMP_OPCODE_ID => "JMP",
				ISCLParsedScript.FORK_OPCODE_ID => "FORK",
				ISCLParsedScript.TERMINATE_OPCODE_ID => "STOP",
				ISCLParsedScript.SUSPEND_OPCODE_ID => "SUSPEND",
				_ => commands[c.opCode.ValueId].CmdCode
			};
			sb.Append ( $"{i}:{cmdCode}(" );
			for ( int a = 0; a < CmdCall.MaxDirectArgs; a++ ) {
				var arg = c.ArgAt ( a );
				if ( arg.RawValue == 0 ) break;
				if ( a > 0 ) sb.Append ( ", " );
				switch ( arg.ValueType ) {
					case SCLInterpreter.ResultTypeID:
						sb.Append ( $"R{arg.ValueId}" );
						break;
					case SCLInterpreter.VarTypeID:
						sb.Append ( $"V{arg.ValueId}" );
						break;
					case SCLInterpreter.ConstTypeID:
						sb.Append ( $"C{arg.ValueId}" );
						break;
					default:
						sb.Append ( $"?{arg.RawValue}" );
						break;
				}
				sb.Append ( '<' );
				sb.Append ( GetTypeOfVar ( new TDst ( arg ) ).Name );
				sb.Append ( '>' );
			}
			sb.Append ( ')' );
		}
	}
}