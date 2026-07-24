using Components.Library;
using SeClav.Commands;
using TOpCode = SeClav.SId<SeClav.OpCodeTag>;
using TDst = SeClav.SId<SeClav.DstTag>;
using TArg = SeClav.SId<SeClav.ArgTag>;
using TExtraArgs = SeClav.SId<SeClav.ExtraArgsTag>;
using Components.Interfaces.SeClav.Parsing;

namespace SeClav;
internal class SCLParsing {
	//private readonly DModuleLoader ModuleLoader;
	private readonly System.Func<string, IModuleInfo> ModuleLoader;
	private readonly SCLParsingStatus Status;
	private readonly TOpCode AssignOpCode, ThrowOpCode, NopOpCode;
	private readonly List<string> Log = [];
	public Action<string> Logger;
	public bool EnableLogging;
	public readonly IReadOnlyDictionary<string, string> MemoryInfo;

	private readonly ParsingContext parsingContext;

	public SCLParsing ( System.Func<string, IModuleInfo> moduleLoader, Action<string> logger = null, bool enableLogging = true ) {
		ArgumentNullException.ThrowIfNull ( moduleLoader );
		Logger = logger;
		EnableLogging = enableLogging;
		ModuleLoader = moduleLoader;
		Status = new ();
		NopOpCode = Status.RegisterCustomCmd ( new SCL_NOP () );
		AssignOpCode = Status.RegisterCustomCmd ( new CmdAssignment () );
		ThrowOpCode = Status.RegisterCustomCmd ( new ThrowCmd () );
		Status.RegisterPraeDirective ( "in", RegisterExternalInput );
		Status.RegisterPraeDirective ( "mapper", RegisterExternalMapper );
		Status.RegisterPraeDirective ( "extFcn", RegisterExternalFunction );
		Status.RegisterPraeDirective ( "out", RegisterExternalOutput );
		Status.RegisterMacro ( new EmitMacro ( ProcessEmitMacro ) );

		MemoryInfo = Status.GetMemoryInfoRef ();

		parsingContext = new ParsingContext ( ModuleLoader, Status, MemoryInfo, AssignOpCode ) {
			EnableLogging = EnableLogging,
			Logger = Logger,
			ProcessLine = ProcessLine
		};
		if ( EnableLogging ) parsingContext.LogAdd ( "SCL parser started" );
	}

	/// <summary>Mostly intented only for tests. Prefer assigning prae directives into module and loading the module instead.</summary>
	internal void RegisterPraeDirective ( string directive, PraeDirective prae ) {
		Status.RegisterPraeDirective ( directive, prae );
	}

	private void RegisterExternalInput ( SCLParsingContext context, ArgParser args ) {
		string typeName = args.String ( 0, "Type of the external input to register", 1, true );
		string varName = args.String ( 1, "Name of the variable to register the external input into", 1, true );
		var typeDef = Status.GetDataType ( typeName );
		int typeID = Status.GetDataTypeID ( typeDef );
		Status.RegisterExterInVariable ( typeID, varName );
	}

	private void RegisterExternalMapper ( SCLParsingContext context, ArgParser args ) {
		if (args.ArgC != 4)
			throw new SCLDirectiveException ( $"@mapper directive expects exactly 3 arguments: output type, mapper name, input type, separated by colon ':'. Got {args.ArgC - 1}.", args.Line ( 0 ) );
		if ( args.String ( 2, null, 1, true ) != ":" )
			throw new SCLDirectiveException ( $"@mapper directive expects exactly 3 arguments: output type, mapper name, input type, separated by colon ':'. Got '{args.String ( 2, null, 1, true )}' instead of ':'.", args.Line ( 0 ) );

		string outName = args.String ( 0, "Name of the output variable to register the external mapper into", 1, true );
		string mapperName = args.String ( 1, "Name of the mapper to register", 1, true );
		string inName = args.String ( 3, "Name of the input variable to register the external mapper from", 1, true );

		var outTypeDef = Status.GetDataType ( outName );
		var inTypeDef = Status.GetDataType ( inName );
		Status.RegisterExterSampler ( mapperName, inTypeDef, outTypeDef );
	}

	private void RegisterExternalFunction ( SCLParsingContext context, ArgParser args ) {
		const int ARGS_START = 3;
		if ( args.ArgC < ARGS_START + 1 ) throw new SCLDirectiveException ( $"@mapper directive expects at least 3 arguments: output type, function name, input type, separated by colon ':'. Got {args.ArgC - 1}.", args.Line ( 0 ) );
		if ( args.String ( 2, null, 1, true ) != ":" ) throw new SCLDirectiveException ( $"@mapper directive expects exactly 3 arguments: output type, function name, input type, separated by colon ':'. Got '{args.String ( 2, null, 1, true )}' instead of ':'.", args.Line ( 0 ) );

		string returnTypeName = args.String ( 0, "Name of the return type", 1, true );
		string funcName = args.String ( 1, "Name of the function to register", 1, true );
		var returnType = Status.GetDataType ( returnTypeName );

		var argTypes = new DataTypeDefinition[args.ArgC - ARGS_START];
		for ( int i = ARGS_START; i < args.ArgC; i++ ) {
			string argTypeName = args.String ( i, $"Name of argument type {i - ARGS_START + 1}", 1, true );
			var argType = Status.GetDataType ( argTypeName );
			argTypes[i - ARGS_START] = argType;
		}
		Status.RegisterExterFunction ( funcName, returnType, argTypes );
	}

	private void RegisterExternalOutput ( SCLParsingContext context, ArgParser args ) {
		string typeName = args.String ( 0, "Type of the external output to register", 1, true );
		string varName = args.String ( 1, "Name of the variable to register the external output from", 1, true );
		var typeDef = Status.GetDataType ( typeName );
		int typeID = Status.GetDataTypeID ( typeDef );
		Status.RegisterExterOutVariable ( typeID, varName );
	}

	private void ProcessEmitMacro (ushort flags, string eventName, bool suspending ) {
		if (!CurrentActiveState.Transitions.TryGetValue (eventName, out var existing) )
			throw new SCLStateTransitionException ( $"Current active state '{CurrentActiveState.StateName}' does not have a transition for event '{eventName}', which is required to use 'emit' macro with that event name.", CurrentActiveState.OriginalLine );
		ushort opCode = 0;
		if (suspending) {
			if ( existing.canParallel )
				throw new SCLStateTransitionException ( $"Suspending emit macro with parallel transition is not supported yet.", CurrentActiveState.OriginalLine );
			opCode = ISCLParsedScript.SUSPEND_OPCODE_ID;
		} else {
			opCode = existing.canParallel ? ISCLParsedScript.FORK_OPCODE_ID : ISCLParsedScript.JMP_OPCODE_ID;
		}
			Status.RegisterStateJump ( existing.nextState, opCode, flags );
	}

	public ISCLParsedScript GetResult () {
		FinishParsing ();
		return new SCLParsingStatus.SCLParsedScript ( Status );
	}

	public ISCLDebugInfo GetResultWithDebugInfo (bool finalize = true, bool allowNonexistingJumps = false) {
		if ( finalize ) FinishParsing ();
		if ( allowNonexistingJumps ) Status.ReplaceNonexistingJumpsWithNOPs = true;
		return new SCLParsingStatus.SCLDebugInfo ( Status, Log );
	}

	private void FinishParsing () {
		// It is a question if this should actually close up the script or should just 'prepare' it for generating the result, without modifying the underlying Status. For now, this is destructive action, but it might be a useful feature to allow calling Finalize multiple times, for producing different scripts from the same source. (Create one script, add some more lines, create another script with the same Status but different content, etc.)

		InsertStateClosing ();
	}
	private void InsertStateClosing () {
		if ( CurrentActiveState == null ) return;
		if ( CurrentActiveState.IsAccepting ) {
			TOpCode endCmd = SCLInterpreter.CrOpCode ( ISCLParsedScript.TERMINATE_OPCODE_ID );
			CmdCall cmdCall = new ( endCmd, SCLInterpreter.CrDst ( FirstEntryStatus.PCindex ), 0 );
			parsingContext.Status.PushCommand ( cmdCall );
		} else {
			TOpCode endCmd = SCLInterpreter.CrOpCode ( ISCLParsedScript.SUSPEND_OPCODE_ID );
			CmdCall cmdCall = new ( endCmd, SCLInterpreter.CrDst ( CurrentActiveState.PCindex ), 0 );
			parsingContext.Status.PushCommand ( cmdCall );
		}
	}

	/*
	.......┌──────┐....┌───────┐.....<DataType>┌──────┐.<VarName>.┌───────┐...............
	.......│Empty.│....│Module.│.....┌────────►│DefVar├──────────►│VarName│...............
	.......└------┘....└-------┘.....│.........└──────┘...........└-------┘...............
	.............▲......▲..........┌───►.........┌──────┐.............│...................
	............∅│......│@using....│...└────────►│SetVar├─────────┬───┘...................
	.............│......│..........│...<VarName>.└──────┘.......=.│.......................
	.............├──────┴┐.........│..............................▼.......................
	....────────►│.Start.├─────────┘........................┌──────┐............┌────────┐
	.............└-------┘......................┌───────────┤Assign├───────────►│Constant│
	........<Command>│..........┌──────────┐....│<Command>..└──────┘.<Constant>.└--------┘
	.................└─────────►│Expression│◄───┘.........................................
	............................└─────┬────┘..........┌───────┐...........................
	..................................│...............│.<Arg>.│...........................
	..................................│.ϵ.....┌───────┴─┐.....│...........................
	..................................└──────►│Arguments│◄────┘...........................
	..........................................└---------┘.................................
	*/

	SubStateParser CurrentActiveState = null;
	SubStateParser FirstEntryStatus = null;

	public void ProcessLine ( string line ) {
		if ( EnableLogging ) parsingContext.LogAdd ( $"Processing line: {line}" );
		parsingContext.Status.LastLine = line;
		string originalLine = line;
		line = line.Trim ();
		int commentIndex = line.IndexOf ( '#' );
		if ( commentIndex == 0 || (commentIndex > 0 && line[commentIndex - 1] != '\\' ) )
			line = line[..commentIndex].TrimEnd ();

		if ( string.IsNullOrEmpty ( line ) ) return;
		if ( SubMacroParser.Parse ( line, parsingContext, out var macroResult ) ) {
			macroResult.MustEndLine ();
			macroResult.Apply ();
			return;
		}
		if ( SubPraeParser.Parse ( line, parsingContext, out var praeResult ) ) {
			praeResult.MustEndLine ();
			praeResult.Apply ();
			return;
		}
		if ( SubUsingParser.Parse ( line, parsingContext, out var usingResult ) ) {
			usingResult.MustEndLine ();
			usingResult.Apply ();
			return;
		}
		if ( SubStateParser.Parse ( line, parsingContext, out var stateResult ) ) {
			FirstEntryStatus ??= stateResult;
			stateResult.MustEndLine ();
			CmdCall cmdCall;
			if (CurrentActiveState == null) {
				parsingContext.Status.RegisterStateJump ( stateResult.StateName, ISCLParsedScript.JMP_OPCODE_ID, 0 );
			} else if ( CurrentActiveState.IsAccepting ) {
				TOpCode endCmd = SCLInterpreter.CrOpCode ( ISCLParsedScript.TERMINATE_OPCODE_ID );
				cmdCall = new ( endCmd, SCLInterpreter.CrDst ( FirstEntryStatus.PCindex ), 0 );
				parsingContext.Status.PushCommand ( cmdCall );
			} else {
				TOpCode endCmd = SCLInterpreter.CrOpCode ( ISCLParsedScript.SUSPEND_OPCODE_ID );
				cmdCall = new ( endCmd, SCLInterpreter.CrDst ( CurrentActiveState.PCindex ), 0 );
				parsingContext.Status.PushCommand ( cmdCall );
			}

			stateResult.Apply ();
			CurrentActiveState = stateResult;
			return;
		}
		if ( SubDataTypeParser.Parse ( line, parsingContext, out var dataTypeResult ) ) {
			dataTypeResult.MustEndLine ();
			dataTypeResult.Apply ();
			return;
		}
		if ( SubAssignmentParser.Parse ( line, parsingContext, out var assignmentResult ) ) {
			assignmentResult.MustEndLine ();
			assignmentResult.Apply ();
			return;
		}
		if ( SubCommandParser.Parse ( line, parsingContext, out var commandResult ) ) {
			commandResult.MustEndLine ();
			commandResult.Apply ();
			return;
		}
		throw new SCLSyntaxException ( $"Could not parse line: '{originalLine}'.", originalLine );
	}
}