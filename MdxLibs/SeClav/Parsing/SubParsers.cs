using System;
using System.Collections.Generic;
using System.Linq;
using MdxLibs.Services;
using SeClav.Modules;
using TArg = SeClav.SId<SeClav.ArgTag>;
using TDst = SeClav.SId<SeClav.DstTag>;
using TOpCode = SeClav.SId<SeClav.OpCodeTag>;

// It might be a good idea to:
//  1) Move all the parsing related stuff into this namespace
//  2) Remove the SCL prefix from files and classes as it already is in SeClav namespace
//       This might cause the need to adjust some names to not conflict with common names in other major namespaces

namespace SeClav.Parsing;
internal class ParsingContext {
	public readonly System.Func<string, IModuleInfo> ModuleLoader;
	public readonly SCLParsingStatus Status;
	public TOpCode AssignOpCode;
	public readonly List<string> Log = [];
	public Action<string> Logger;
	public bool EnableLogging;
	public readonly IReadOnlyDictionary<string, string> MemoryInfo;
	public Action<string> ProcessLine;

	public ParsingContext ( System.Func<string, IModuleInfo> moduleLoader, SCLParsingStatus status, IReadOnlyDictionary<string, string> memoryInfo, TOpCode assignOpCode ) {
		ModuleLoader = moduleLoader;
		Status = status;
		MemoryInfo = memoryInfo ?? new Dictionary<string, string> ();
		AssignOpCode = assignOpCode;
		EnableLogging = false;
	}

	public string GetIdentifier ( ref string line ) {
		string originalLine = line;
		// Somewhen it would be better to specify what is a valid identifier
		// We'd like to allow also non-ASCII characters, but not all (e.g. excluding apostrophes, diacritics, etc. at start or end)
		if ( string.IsNullOrEmpty ( line ) ) return null;
		//for ( int i = 0; i < line.Length; i++ ) {
		//	if ( char.IsLetter ( line[i] ) || line[i] == '_' ) continue;
		//	if ( i > 0 && (char.IsDigit ( line[i] )) ) continue;
		//	if ( i == 0 ) return null;
		//	string ret = line[..i];
		//	line = line[i..].TrimStart ();
		//	return ret;
		//}
		//string ret = line;
		//line = string.Empty;
		//return ret;

		int pos = 0;
		bool isOk = true;
		for ( ; isOk && pos < line.Length; pos++ ) {
			isOk = false;
			if ( char.IsLetter ( line[pos] ) ) isOk = true;
			else if ( line[pos] == '_' ) isOk = true;
			else if ( pos > 0 && char.IsDigit ( line[pos] ) ) isOk = true;
			else pos--; // Compensate for the pos++ in the for loop to not include the invalid character
		}
		if ( pos == 0 ) return null;
		string ret = line[..pos];
		line = line[pos..].TrimStart ();
		return ret;
	}

	public void LogAdd ( string message ) {
		Log.Add ( message );
		Logger?.Invoke ( message );
	}

	public static ushort GetFlag (ref string line) {
		int flagReq = 0;
		if (line.StartsWith('?')) {
			line = line[1..];
			for (int i = 0; i < 3 && line.Length > 0; i++) {
				if ( line[0] == ' ' ) break;
				int linePos = line[0] switch {
					// Numerical
					'0' => throw new SCLSyntaxException ( $"Testing for '0' flag is not allowed!", line ),
															  '1' => 1, '2' => 2, '3' => 3,
					'4' => 4, '5' => 5, '6' => 6, '7' => 7,
					'8' => 8, '9' => 9, 'A' => 10, 'B' => 11,
					'C' => 12, 'D' => 13, 'E' => 14, 'F' => 15,
					// Aliases
					'N' => 0, // NONE
					'!' => 1, // IF
					'~' => 2, // ELSE
					'=' => 3, // EQUAL
					'>' => 4, // LARGER
					'<' => 5, // SMALLER
					_ => throw new SCLSyntaxException ( $"Invalid flag character '{line[0]}' in command flags.", line )
				};
				flagReq |= linePos << (i * 5); // 4bit + sign bit per flag
				line = line[1..];
			}
			line = line.Trim ();
		}
		return (ushort)flagReq;
	}

	public (int guiderID, string arg)[] ProcessMacro ( IMacro macro, string line ) {
		if ( macro.guiders.Count == 0 )
			return [(-1, line)]; // No guiders, return the whole line as a single part

		List<(int guiderID, string arg)> parts = [];
		int lastPos = 0;
		if ( macro.UnorderedGuiders ) {
			string[] splitters = macro.guiders.Select ( g => g.split ).ToArray ();
			while ( true ) {
				int nearestPos = -1;
				int nearestGuiderID = -1;
				string nearestSplitter = null;
				for ( int i = 0; i < splitters.Length; i++ ) {
					int pos = line.IndexOf ( splitters[i], lastPos );
					if ( pos < 0 ) continue;
					if ( nearestPos < 0 || pos < nearestPos ) {
						nearestPos = pos;
						nearestGuiderID = i;
						nearestSplitter = splitters[i];
					}
				}
				if ( nearestPos < 0 ) break;
				string part = line[lastPos..nearestPos];
				parts.Add ( (nearestGuiderID, part) );
				lastPos = nearestPos + nearestSplitter.Length;
			}
			if ( lastPos < line.Length ) {
				string part = line[lastPos..];
				parts.Add ( (-1, part) );
			}
		} else {
			List<string> args = [.. line.Split ( [' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries )];
			for ( int i = 0; i < macro.guiders.Count; i++ ) {
				var guider = macro.guiders[i];
				int pos = Math.Abs ( guider.after ) - 1;
				bool canRepeat = guider.after < 0;

				if ( pos >= args.Count ) {
					if ( canRepeat ) continue;
					throw new SCLMacroException ( $"Macro '{macro.CmdCode}' expects at least {guider.after + 1} arguments, but only {args.Count} were provided.", line );
				}
				int sepPos = (args[pos]?.IndexOf ( guider.split )).GetValueOrDefault ( -1 );
				bool nextStarts = sepPos < 0 && (args.Count > pos + 1) && args[pos + 1].StartsWith ( guider.split );
				if ( sepPos < 0 && !nextStarts ) {
					if ( canRepeat ) continue;
					else throw new SCLMacroException ( $"Macro '{macro.CmdCode}' expects argument {pos + 1} to start with '{guider.split}', but got '{args[pos]}'.", line );
				}

				string part = string.Join ( ' ', args[lastPos..pos] );
				if ( nextStarts ) {
					if ( part.Length == 0 || part.EndsWith ( ' ' ) ) part += args[pos];
					else part += ' ' + args[pos];
					pos++;
					sepPos = 0;
				} else {
					if ( sepPos > 0 ) part += args[lastPos][..sepPos];
				}
				args[pos] = args[pos][(sepPos + guider.split.Length)..]; // Remove the guider split from the argument
				if ( string.IsNullOrWhiteSpace ( args[pos] ) )
					args.RemoveAt ( pos ); // Remove the current argument, as it is fully consumed

				for ( int j = 0; j < macro.guiders.Count; j++ ) {
					if ( guider.split.Contains ( macro.guiders[j].split ) ) continue;
					if ( part.Contains ( macro.guiders[j].split ) )
						throw new SCLMacroException ( $"Macro '{macro.CmdCode}' guider split '{macro.guiders[j].split}' found inside argument for guider split '{guider.split}'.", line );
				}

				parts.Add ( (i, part) );

				if ( canRepeat ) {
					for ( int j = pos - 1; j >= lastPos; j-- )
						args.RemoveAt ( j );
					i--;
				} else
					lastPos = pos;
			}
			if ( lastPos < args.Count ) {
				string part = string.Join ( ' ', args[lastPos..] );
				parts.Add ( (-1, part) );
			}
		}

		if ( macro.SelectRight ) {
			var copy = parts.ToArray ();
			parts.Clear ();
			if ( !string.IsNullOrWhiteSpace ( copy[0].arg ) )
				parts.Add ( (-1, copy[0].arg) );
			// Hopefully this simple trick will work: Simply combine previous guider ID with next part.
			// This should convert left-selecting guiders to right-selecting ones.
			for ( int i = 1; i < copy.Length; i++ )
				parts.Add ( (copy[i - 1].guiderID, copy[i].arg) );
		}
		return parts.ToArray ();
	}

	public CmdArgInfo ParseArg ( ref string line, ParsingContext context, DataTypeDefinition expectedType, string errBase = $"Command '<name>' expects argument <id>" ) {
		expectedType = context.Status.GetDataType ( expectedType.Name );
		// Constant argument
		if ( expectedType.TryParse ( ref line, out IDataType arg ) ) {
			return new CmdArgInfo ( arg );
		}

		// Inter-result argument
		if ( SubCommandParser.Parse ( line, context, out SubCommandParser subCommand ) ) {
			line = subCommand.RemainLine;
			var interType = subCommand.Command.ReturnType;
			interType = context.Status.GetDataType ( interType.Name );
			if ( !interType.Equals ( expectedType ) )
				throw new SCLCommandArgumentException ( $"{errBase} of type '{expectedType.Name}', but the command '{subCommand.Command.CmdCode}' ({subCommand.Command.CommonName}) returns type '{interType.Name}'.", line );
			return new CmdArgInfo ( subCommand );
		}

		string token = context.GetIdentifier ( ref line );
		if ( string.IsNullOrEmpty ( token ) )
			throw new SCLCommandArgumentException ( $"{errBase} of type '{expectedType.Name}', but none was provided.", line );

		// Variable argument
		if ( context.Status.TryGetVarID ( token, out TArg varID ) ) {
			var varType = context.Status.GetTypeOfVar ( varID );
			if ( expectedType != varType )
				throw new SCLCommandArgumentException ( $"{errBase} of type '{expectedType.Name}', but variable '{token}' is of type '{varType.Name}'.", line );
			return new CmdArgInfo ( varID );
		}

		throw new SCLCommandArgumentException ( $"{errBase} of type '{expectedType.Name}', but got '{token}'.", line );
	}
}

internal abstract class SubParserBase {
	protected readonly ParsingContext Context;
	public readonly string OriginalLine;
	public string RemainLine { get; protected set; }

	public SubParserBase ( ParsingContext context, string originalLine ) {
		Context = context;
		OriginalLine = originalLine;
	}

	public abstract void Apply ();
	public void MustEndLine () {
		if ( !string.IsNullOrWhiteSpace ( RemainLine ) )
			throw new SCLSyntaxException ( $"Unexpected trailing characters '{RemainLine}' after processing line '{OriginalLine}'.", OriginalLine );
	}
}

internal class SubStateParser : SubParserBase {
	public readonly string StateName;
	public readonly bool IsAccepting;
	public readonly Dictionary<string, (string nextState, bool canParallel)> Transitions;
	public readonly List<(string nextState, bool canParallel)> eTransitions;
	public readonly IReadOnlyList<string> AwaitingTokens;
	public int PCindex;

	public SubStateParser ( ParsingContext context, bool isAccepting, string originalLine, string stateName, string[] awaitingTokens ) : base ( context, originalLine ) {
		StateName = stateName;
		IsAccepting = isAccepting;
		Transitions = new ();
		eTransitions = new ();
		AwaitingTokens = awaitingTokens;
	}
	
	public static bool Parse ( string line, ParsingContext context, out SubStateParser result ) {
		result = null;
		string originalLine = line;
		if ( !line.StartsWith ( '-' ) ) return false;

		string[] awaitingTokens = AwaitingTransition ( ref line );
		string stateName = NextToken ( ref line, "-" );
		if ( string.IsNullOrEmpty ( stateName ) )
			throw new SCLStateTransitionException ( $"Invalid state transition format in line '{originalLine}'. Missing state name.", originalLine );
		bool isAccepting = IsEnclosed ( ref stateName, "[", "]" );
		result = new ( context, isAccepting, originalLine, stateName, awaitingTokens );

		while ( true ) {
			line = line.TrimStart ();
			if ( line.Length == 0 ) break;
			if ( !line.StartsWith ( '-' ) )
				throw new SCLStateTransitionException ( $"Invalid state transition format in line '{originalLine}'. Expected '-' at the start of transition definition.", originalLine );
			line = line[1..];
			(string token, bool canParallel) = OutTransition ( ref line );
			string nextState = NextToken ( ref line, "-" );
			if (nextState != null) {
				if ( token == null ) result.eTransitions.Add ( (nextState, canParallel) );
				else {
					AssertTokenName ( token, 32 );
					if ( result.Transitions.ContainsKey ( token ) )
						throw new SCLStateTransitionException ( $"Duplicate transition token '{token}' in line '{originalLine}'.", originalLine );
					result.Transitions[token] = (nextState, canParallel);
				}
			} else throw new SCLStateTransitionException ( $"Invalid state transition format in line '{originalLine}'. Missing next state after token '{token}'.", originalLine );

			if ( line.Length == 0 ) break;
		}
		return true;
	}

	private static string NextToken ( ref string line, string separator ) {
		int tokenEnd = line.IndexOf ( separator );
		if ( tokenEnd < 0 )
			tokenEnd = line.Length;
		string token = line[..tokenEnd].Trim ();
		line = line[tokenEnd..].TrimStart ();
		return string.IsNullOrEmpty ( token ) ? null : token;
	}
	private static (string token, int used) NextToken ( ref string line, string[] separator ) {
		int[] tokenEnds = new int[separator.Length];
		bool foundAny = false;
		for ( int i = 0; i < separator.Length; i++ ) {
			int pos = line.IndexOf ( separator[i] );
			tokenEnds[i] = pos >= 0 ? pos : int.MaxValue;
			if ( pos >= 0 ) foundAny = true;
		}
		if ( !foundAny ) return (null, -1);

		int nearestEnd = tokenEnds.Min ();
		int nearestSepIndex = Array.IndexOf ( tokenEnds, nearestEnd );
		string token = line[..nearestEnd].Trim ();
		line = line[nearestEnd..].TrimStart ();
		return (string.IsNullOrEmpty ( token ) ? null : token, nearestSepIndex);
	}

	private static string[] AwaitingTransition ( ref string line ) {
		line.Trim ();
		if (line.StartsWith("-->")) {
			line = line[3..].TrimStart ();
			return Array.Empty<string> ();
		}

		string awaitingGroup = line.SubstringBetween ( "-(", ")->" );
		if ( !string.IsNullOrEmpty ( awaitingGroup ) ) {
			string[] tokens = awaitingGroup.Split ( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
			foreach ( string token in tokens ) AssertTokenName ( token, 16 );
			// Don't await specific states, but emited 'tokens'. This allows more flexibility. Like multiple states can emit the same token, or a state can emit different tokens in different transitions.
			line = line[(5 + awaitingGroup.Length)..].TrimStart ();
			return tokens;
		}
		// No group, but maybe a single token?
		awaitingGroup = line.SubstringBetween ( "-", "->" );
		if ( !string.IsNullOrEmpty ( awaitingGroup ) ) {
			AssertTokenName ( awaitingGroup, 16 );
			line = line[(3 + awaitingGroup.Length)..].TrimStart ();
			return [awaitingGroup];
		}
		throw new SCLStateTransitionException ( $"Invalid state transition format in line '{line}'. Either '-->' or '-(state1, state2, ...)->' or '-state->' is expected at the start of the line.", line );
	}

	private static (string token, bool canParallel) OutTransition ( ref string line ) {
		line.Trim ();
		(string token, int usedSep) = NextToken ( ref line, ["-|>>", "->"] );
		if ( usedSep < 0 )
			throw new SCLStateTransitionException ( $"Invalid state transition format in line '{line}'. Missing transition arrow '->' or '-|>>'.", line );
		int startOffset = usedSep == 0 ? 4 : 2; // Length of the separator
		line = line[startOffset..].TrimStart ();
		bool canParallel = usedSep == 0;
		return (token, canParallel);
	}

	private static void AssertTokenName (string token, int maxSize) {
		if (string.IsNullOrEmpty ( token ) || token.Length > maxSize )
			throw new SCLStateTransitionException ( $"Invalid token name '{token}'. Token name must be 1 to {maxSize} characters long.", token );
		if ( !token.All ( c => char.IsLetterOrDigit ( c ) || c == '_' ) || token.StartsWith ( '_' ) || token.EndsWith ( '_' ) )
			throw new SCLStateTransitionException ( $"Invalid token name '{token}'. Token name must consist of letters, digits or underscores, and cannot start or end with an underscore.", token );
	}

	private static bool IsEnclosed ( ref string token, string prefix, string suffix ) {
		if ( token.StartsWith ( prefix ) && token.EndsWith ( suffix ) ) {
			token = token[(prefix.Length..^suffix.Length)];
			return true;
		}
		return false;
	}

	public override void Apply () {
		if (AwaitingTokens.Count > 0) {
			throw new NotImplementedException ( $"Awaiting tokens in state transitions are not yet implemented. Line: '{OriginalLine}'." );
		}
		PCindex = Context.Status.RegisterStateStart ( StateName );
		foreach ( var eTrans in eTransitions )
			Context.Status.RegisterStateJump ( eTrans.nextState, eTrans.canParallel ? ISCLParsedScript.FORK_OPCODE_ID : ISCLParsedScript.JMP_OPCODE_ID, 0 );
	}
}

internal class SubMacroParser : SubParserBase {
	public readonly string Callname;
	public readonly IMacro Macro;
	public IReadOnlyList<string> RewrittenLines { get; private set; }

	private SubMacroParser ( ParsingContext context, string originalLine, string callname, IMacro macro ) : base ( context, originalLine ) {
		Callname = callname;
		Macro = macro;
	}

	public static bool Parse (string line, ParsingContext context, out SubMacroParser result) {
		result = null;
		string originalLine = line;
		ushort flags = line.StartsWith ( '?' ) ? ParsingContext.GetFlag ( ref line ) : (ushort)0;
		string token = context.GetIdentifier ( ref line );
		if ( string.IsNullOrEmpty ( token ) ) return false;
		IMacro macro = context.Status.TryGetMacro ( token );
		if ( macro == null ) return false;

		result = new ( context, originalLine, token, macro );
		var parts = context.ProcessMacro ( macro, line );
		result.RewrittenLines = macro.RewriteByGuiders ( flags, parts );
		if ( result.RewrittenLines == null ) // Macro rejected the input
			throw new SCLMacroException ( $"Macro '{macro.CmdCode}' could not process the input line ({originalLine}).", originalLine );
		result.RemainLine = string.Empty; // All is expected to be consumed by the macro
		return true;
	}

	public override void Apply () {
		foreach ( string rewrittenLine in RewrittenLines )
			Context.ProcessLine ( rewrittenLine );
	}
}

internal class SubUsingParser : SubParserBase {
	public readonly string ModuleName;
	public readonly IModuleInfo Module;

	private SubUsingParser ( ParsingContext context, string originalLine, string moduleName, IModuleInfo module ) : base ( context, originalLine ) {
		ModuleName = moduleName;
		Module = module;
	}

	public static bool Parse ( string line, ParsingContext context, out SubUsingParser result ) {
		result = null;
		string originalLine = line;
		if ( !line.StartsWith ( "@using ", out string moduleName ) ) return false;
		if ( context.Status.IsModuleRegistered ( moduleName ) )
			throw new SCLDuplicateUsingException ( moduleName, originalLine );
		var module = context.ModuleLoader ( moduleName );
		if ( module == null ) throw new SCLDirectiveException ( $"Module '{moduleName}' not found.", originalLine );
		if ( context.EnableLogging ) context.LogAdd ( $"Module '{moduleName}' loaded: {module.Description}, {module.Commands.Count} commands, {module.DataTypes.Count} data types." );
		result = new ( context, originalLine, moduleName, module );
		result.RemainLine = string.Empty;
		return true;
	}

	public override void Apply () {
		Context.Status.RegisterModule ( Module );
	}
}

internal class SubPraeParser : SubParserBase {
	public readonly string Name;
	public ArgParser Args { get; private set; }
	public PraeDirective Directive { get; private set; }

	public SubPraeParser ( ParsingContext context, string originalLine, string name, string line, PraeDirective prae ) : base ( context, originalLine ) {
		Name = name;
		Args = new ( line, null );
		Directive = prae;
	}

	public static bool Parse ( string line, ParsingContext context, out SubPraeParser result ) {
		result = null;
		string originalLine = line;
		if ( !line.StartsWith ( "@", out string metaCommand ) ) return false;
		line = line[1..]; // '@' shouldn't be part of the command but shouldn't be followed by whitespace either
		string cmdToken = context.GetIdentifier ( ref line );
		if ( cmdToken == null ) return false;

		var prae = context.Status.TryGetPraeDirective ( cmdToken );
		if ( prae == null ) return false;

		result = new ( context, originalLine, cmdToken, line, prae );
		result.RemainLine = string.Empty; // All is expected to be consumed by the directive. Even if not, entire rest of the line is passed as arguments.
		return true;
	}

	public override void Apply () => Context.Status.RunPraeDirective ( Directive, Args );
}

internal class SubAssignmentParser : SubParserBase {
	public readonly string Name;
	public readonly CmdArgInfo Src;
	public TDst? Dst { get; private set; } = null;
	public readonly string DstName;
	public ushort RequiredFlags = 0;

	private SubAssignmentParser ( ParsingContext context, string originalLine, string name, CmdArgInfo src, TDst? dst, string dstName, ushort requiredFlags ) : base ( context, originalLine ) {
		Name = name;
		Src = src;
		Dst = dst;
		DstName = dstName;
		RequiredFlags = requiredFlags;
	}

	public static bool Parse (string line, ParsingContext context, out SubAssignmentParser result ) {
		result = null;
		string originalLine = line;
		ushort flags = ParsingContext.GetFlag ( ref line );
		string token = context.GetIdentifier ( ref line );
		if ( string.IsNullOrEmpty ( token ) ) return false;
		if ( !context.Status.TryGetVarID ( token, out TArg sId ) )
			return false;

		if ( !line.StartsWith ( '=' ) ) return false;
		line = line[1..].TrimStart ();

		var srcType = context.Status.GetTypeOfVar ( sId );
		var src = context.ParseArg ( ref line, context, srcType, $"Assignment to variable '{token}' expects value" );

		result = new ( context, originalLine, token, src, SCLInterpreter.CrDst ( sId ), token, flags );
		result.RemainLine = line;
		return true;
	}

	public static bool Parse (string line, string varName, DataTypeDefinition varType, ParsingContext context, out SubAssignmentParser result ) {
		result = null;
		if ( string.IsNullOrEmpty ( varName ) ) return false;
		string originalLine = line;

		if ( !line.StartsWith ( '=' ) ) return false;
		line = line[1..].TrimStart ();

		var src = context.ParseArg ( ref line, context, varType, $"Assignment to variable '{varName}' expects value" );

		result = new ( context, originalLine, varName, src, null, varName, 0 );
		result.RemainLine = line;
		return true;
	}

	public override void Apply () {
		if (!Dst.HasValue) {
			if ( Context.Status.TryGetVarID ( DstName, out TArg varID ) )
				Dst = SCLInterpreter.CrDst ( varID );
			else throw new SCLCommandArgumentException ( $"Variable '{DstName}' not found for assignment.", OriginalLine );
		}
		TArg dstArg = new ( Dst.Value.Generic );
		TArg srcArg;

		if (Src.Constant != null) {
			srcArg = Context.Status.AddConstant ( Src.Constant );
		} else if ( Src.VariableID != null ) {
			srcArg = Src.VariableID.Value;
		} else if ( Src.InterCommand != null ) {
			srcArg = SCLInterpreter.CrArgRes ( Src.InterCommand.Destination.HasValue
				? Src.InterCommand.Destination.Value.ValueId
				: Src.InterCommand.TryRegisterResult ().ValueId );
			Src.InterCommand.Apply ();
		} else throw new SCLCommandArgumentException ( $"Source for assignment to '{Name}' is not properly set.", OriginalLine );

		Context.Status.PushCommand ( new ( Context.AssignOpCode, Dst.Value, RequiredFlags, dstArg, srcArg ) );
	}
}

internal class SubDataTypeParser : SubParserBase {
	public readonly string Name;
	public readonly DataTypeDefinition DataType;
	public readonly int TypeDefID;
	public SubAssignmentParser Assignment { get; private set; } = null;

	private SubDataTypeParser ( ParsingContext context, string originalLine, string name, DataTypeDefinition dataType, int typeDefID ) : base ( context, originalLine ) {
		Name = name;
		DataType = dataType;
		TypeDefID = typeDefID;
	}

	public static bool Parse ( string line, ParsingContext context, out SubDataTypeParser result ) {
		result = null;
		string originalLine = line;
		string nextToken = context.GetIdentifier ( ref line );
		if ( string.IsNullOrEmpty ( nextToken ) ) return false;
		DataTypeDefinition dataType = context.Status.GetDataType ( nextToken );
		if ( dataType == null ) return false;
		// Migth be valid token, just not a data type (invalid token would throw an exception)

		int typeDefID = context.Status.GetDataTypeID ( dataType );
		string varName = context.GetIdentifier ( ref line );

		result = new ( context, originalLine, varName, dataType, typeDefID );
		SubAssignmentParser assignment;
		SubAssignmentParser.Parse ( line, varName, dataType, context, out assignment );
		result.Assignment = assignment;
		result.RemainLine = assignment != null ? assignment.RemainLine : line;
		return true;
	}

	public override void Apply () {
		TArg varID = Context.Status.RegisterVariable ( TypeDefID, Name );

		if ( Context.EnableLogging ) Context.LogAdd ( $"Defining variable '{Context.Status.GetVarInfo ( SCLInterpreter.CrDst ( varID ) ).name}' of type '{DataType.Name}'." );

		if ( Assignment != null )
			Assignment.Apply ();
		else {
			// While the default value would be assigned when allocating the variable,
			//     explicit assignment allows more consistent behaviour with the new Finite State Machine system.
			Context.Status.PushCommand(new (Context.AssignOpCode, new(varID.Generic), 0, varID, Context.Status.AddConstant ( DataType.Default ) ) );
		}
	}
}

internal struct CmdArgInfo {
	public readonly IDataType? Constant;
	public readonly TArg? VariableID;
	public readonly SubCommandParser? InterCommand;

	public CmdArgInfo ( IDataType constant ) { Constant = constant; }
	public CmdArgInfo ( TArg varID ) { VariableID = varID; }
	public CmdArgInfo ( SubCommandParser interCommand ) { InterCommand = interCommand; }
}

internal class SubCommandParser : SubParserBase {
	public readonly string Name;
	public readonly ushort FlagRequired;
	public readonly ICommand Command;
	private CmdArgInfo[] args;
	public IReadOnlyList<CmdArgInfo> Args => args;
	public TDst? Destination { get; private set; } = null;

	private SubCommandParser ( ParsingContext context, string originalLine, string name, ICommand command, ushort flagRequired ) : base ( context, originalLine ) {
		Name = name;
		FlagRequired = flagRequired;
		Command = command;

		args = new CmdArgInfo[command.ArgC];
	}

	public static bool Parse ( string line, ParsingContext context, out SubCommandParser result ) {
		result = null;
		string originalLine = line;
		ushort flagReq = ParsingContext.GetFlag ( ref line );
		string token = context.GetIdentifier ( ref line );
		if ( string.IsNullOrEmpty ( token ) ) return false;
		ICommand cmd = context.Status.TryGetCommand ( token );
		if ( cmd == null ) return false;
		result = new ( context, originalLine, token, cmd, flagReq );

		if (cmd.Args.Count != cmd.ArgC)
			throw new SCLParsingException ( $"Internal error: Command '{cmd.CmdCode}' argument count mismatch between ICommand definition and Args list.", originalLine );
		for ( int i = 0; i < cmd.ArgC; i++ ) {
			if ( cmd.Args[i].type is SCLT_Any ) {
				result.args[i] = new CmdArgInfo ( SCLInterpreter.CrArgVar ( 1 ) ); // 'ANY' is requested. Corrently no better way to provide some 'placeholder' value.
				line = string.Empty; // 'ANY' should probably consume the rest of the line.
			} else {
				var type = context.Status.GetDataType ( cmd, i );

				result.args[i] = context.ParseArg ( ref line, context, type, $"Command '{result.Name}' expects argument {i + 1}" );
			}
		}

		result.RemainLine = line;
		return true;
	}

	public TDst TryRegisterResult () {
		if (Command.ReturnType == null) throw new SCLCommandArgumentException ( $"Command '{Name}' does not have a return type.", OriginalLine );
		if ( Destination != null ) throw new SCLCommandArgumentException ( $"Command '{Name}' result destination already registered.", OriginalLine );
		return (Destination = Context.Status.RegisterResult ( Command.ReturnType )).Value;
	}

	public void SetDestination ( TDst dst ) {
		if ( Destination != null ) throw new SCLCommandArgumentException ( $"Command '{Name}' destination already set.", OriginalLine );
		Destination = dst;
	}

	public override void Apply () {
		int N = args.Length;
		TArg[] fArg = new TArg[N];
		for ( int i = 0; i < N; i++ ) {
			if ( args[i].Constant != null ) {
				fArg[i] = Context.Status.AddConstant ( args[i].Constant );
				AssertArg ( "constant" );
			} else if ( args[i].VariableID != null ) {
				fArg[i] = args[i].VariableID.Value;
				AssertArg ( "variable" );
			} else if ( args[i].InterCommand != null ) {
				fArg[i] = SCLInterpreter.CrArgRes ( args[i].InterCommand.TryRegisterResult ().ValueId );
				AssertArg ( "inter-command result" );
				args[i].InterCommand.Apply ();
			} else throw new SCLCommandArgumentException ( $"Argument {i + 1} for command '{Name}' is not properly set.", OriginalLine );

			void AssertArg ( string argType ) {
				if ( Context.Status.GetTypeOfVar ( fArg[i] ) == null )
					throw new SCLCommandArgumentException ( $"Internal error: Argument {i + 1} for command '{Command.CmdCode}' could not resolve type of {argType} '{args[i]}'.", OriginalLine );
			}
		}

		CmdCall call = new (
			SCLInterpreter.CrOpCode ( Context.Status.GetCommandID ( Command ) ),
			Destination.HasValue ? Destination.Value : SCLInterpreter.CrDst ( 0 ),
			FlagRequired,
			fArg
			);
		if ( Context.EnableLogging ) Context.LogAdd ( $"Pushing command '{Name}' with {N} argument(s)." );
		Context.Status.PushCommand ( call );
	}
}





