using System;
using System.Collections.Generic;
using System.Linq;

namespace SeClav.Parsing;
internal class SubCommandParser : SubParserBase {
	/// Please note that SeClav does not aim to support full "traditional" syntax tree parsing.
	/// Instead, it offers only a limited complexity to offer a little bit more intuitive syntax.
	/// The grammar is following: ( C = Command, A = Argument after command, P = Argument before infix command,
	///		* = repeated as many times as command expects | x = same as * but without the first argument)
	/// C | CA* | PCAx - initial command call
	/// A = constant | variable | C | PCAx | CA* - subsuquent arguments can be almost anything
	///		- Arguments are checked for type compatibility, when ambigous between infix and prefix command notation, the infix notation can be preferred, but can still be rejected
	/// A 'command' line must therefore start with a command (prefix or infix). A 'tree' of infix commands for example is not supported.
	/// Example (parantheses are added for clarity, not natively supported):
	///     Invalid: [(a + b) + (c + d)] + [(e + f) + (g + h)] - First command is the (a+b), which expects only two arguments, rest of the line is not parsed;
	///     Valid:   +[ +(a+b) (c+d)] [+(e+f) (g+h)] - Manual conversion of the entire line but the most inner commands to a prefix notation
	///					- The main sum is now the first command, parses left half as first argument, right half as second argument, line is now fully parsed
	/// Doing the traditional syntax tree is complicated by the SeClav idea of no inbuilt data types, operation precedence, etc.
	public readonly string Name;
	public readonly ushort FlagRequired;
	public readonly ICommand Command;
	private CmdArgInfo[] args;
	public IReadOnlyList<CmdArgInfo> Args => args;
	public SId<DstTag>? Destination { get; private set; } = null;

	private SubCommandParser (
		ParsingContext context, string originalLine, string name, ICommand command, ushort flagRequired
	) : base ( context, originalLine ) {
		Name = name;
		FlagRequired = flagRequired;
		Command = command;

		args = new CmdArgInfo[command.ArgC];
	}

	public static bool Parse ( string line, ParsingContext context, out SubCommandParser result, DataTypeDefinition expectedType = null ) {
		result = null;
		string originalLine = line;
		ushort flagReq = ParsingContext.GetFlag ( ref line );
		var candidate = FilterCandidates ( context, line, expectedType );
		if ( candidate == null ) return false;
		result = FromArgCmd ( candidate, context, originalLine, flagReq );
		result.RemainLine = string.Empty;
		return true;
	}

	private static SubCommandParser FromArgCmd ( ArgCmd root, ParsingContext context, string originalLine, ushort flagRequired ) {
		var cmdNode = (CmdParsingNode_Cmd)root.Node;
		var sub = new SubCommandParser ( context, originalLine, cmdNode.Command.CallName, root.Command, flagRequired );
		var argsList = root.ArgsList;
		for ( int i = 0; i < argsList.Count; i++ ) {
			var argNode = argsList[i];
			if ( argNode is ArgCmd subCmd )
				sub.args[i] = new ( FromArgCmd ( subCmd, context, originalLine, 0 ) );
			else if ( argNode.Node is CmdParsingNode_Arg argParsed )
				sub.args[i] = argParsed.ArgInfo;
		}
		return sub;
	}

	public SId<DstTag> TryRegisterResult () {
		if ( Command.ReturnType == null )
			throw new SCLCommandArgumentException ( $"Command '{Name}' does not have a return type.", OriginalLine );
		if ( Destination != null )
			throw new SCLCommandArgumentException ( $"Command '{Name}' result destination already registered."
				, OriginalLine
			);

		return (Destination = Context.Status.RegisterResult ( Command.ReturnType )).Value;
	}

	public void SetDestination ( SId<DstTag> dst ) {
		if ( Destination != null )
			throw new SCLCommandArgumentException ( $"Command '{Name}' destination already set.", OriginalLine );

		Destination = dst;
	}

	public override void Apply () {
		int N = args.Length;
		SId<ArgTag>[] fArg = new SId<ArgTag>[N];
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
			} else
				throw new SCLCommandArgumentException ( $"Argument {i + 1} for command '{Name}' is not properly set."
					, OriginalLine
				);

			void AssertArg ( string argType ) {
				if ( Context.Status.GetTypeOfVar ( fArg[i] ) == null )
					throw new SCLCommandArgumentException (
						$"Internal error: Argument {i + 1} for command '{Command.CmdCode}' could not resolve type of {argType} '{args[i]}'."
						, OriginalLine
					);
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

	public static ArgCmd FilterCandidates ( ParsingContext context, string line, DataTypeDefinition expectedType = null ) {
		 CommandParserHelper cmdParserHelper = new (context, line, expectedType);
		// var candidates = cmdParserHelper.CandidateSequences.Select ( TryCandidate )
		// 	.Where ( c => c != null )
		// 	.Where ( c => TypeCheck ( c, context ) )
		// 	.ToList ();
		List<ArgCmd> candidates = new ();
		foreach ( var seq in cmdParserHelper.CandidateSequences ) {
			var candidate = TryCandidate ( seq );
			if ( candidate == null ) continue;
			bool typeCheck = TypeCheck ( candidate, context );
			if ( typeCheck )
				candidates.Add ( candidate );
		}
		return candidates.Count == 1 ? candidates[0] : null;
	}

	private static bool TypeCheck ( ArgCmd root, ParsingContext context ) {
		var argsList = root.ArgsList;
		for ( int i = 0; i < argsList.Count; i++ ) {
			var expectedType = context.Status.GetDataType ( root.Command, i );
			if ( expectedType == null ) return false;
			var argNode = argsList[i];
			if ( argNode is ArgCmd subCmd ) {
				// Sub-command used as argument: its return type must match the expected type
				var returnType = subCmd.Command.ReturnType;
				if ( returnType == null ) return false;
				var resolvedReturn = context.Status.GetDataType ( returnType.Name );
				if ( resolvedReturn == null || resolvedReturn != expectedType ) return false;
				if ( !TypeCheck ( subCmd, context ) ) return false;
			} else if ( argNode.Node is CmdParsingNode_Arg argParsed ) {
				var argType = argParsed.ParsedType;
				context.Status.TranslateDataType ( ref argType );
				if ( argParsed.ParsedType == null || argType != expectedType ) return false;
			} else {
				return false;
			}
		}
		return true;
	}

	private static ArgCmd TryCandidate ( ACmdParsingNode[] sequence ) {
		if ( sequence.Length == 0 ) return null;
		List<ArgRoot> seq = new ();
		foreach ( var node in sequence ) {
			switch ( node ) {
			case CmdParsingNode_Cmd cmdNode: seq.Add ( new ArgCmd ( cmdNode ) ); break;
			case CmdParsingNode_Arg argNode: seq.Add ( new ArgRoot ( argNode ) ); break;
			default:                         return null; // Some unprocessed text, invalid candidate
			}
		}
		if ( seq.Count == 1 && seq[0] is ArgCmd cmd ) return cmd;
		if ( seq.Count <= 1 ) return null;

		ArgCmd rootCmd = null;
		if ( seq[0] is ArgCmd rootNode1 ) {
			rootCmd = rootNode1; // Prefix notation
		}
		else if ( seq[1] is ArgCmd rootNode2 ) {
				rootCmd = rootNode2; // Infix notation
				if ( rootCmd.IsComplete ) return null; // Infix, yet command does not expect any arguments, invalid candidate
				rootCmd.PreArg ( seq[0] ); // seq[0] is the pre-arg that precedes the command in infix notation
				seq.RemoveAt ( 0 ); // Remove only the pre-arg; rootCmd stays in seq[0] for TrySimplify
		} else return null;

		while ( true ) {
			try { if ( !TrySimplify ( seq ) ) break; }
			catch ( Exception _ ) { return null; }
		}

		//return seq.Count == 1 && seq[0] is ArgCmd finalCmd && finalCmd == rootCmd && finalCmd.IsComplete ? rootCmd : null;
		if ( seq.Count != 1 ) return null;
		if ( seq[0] is not ArgCmd finalCmd ) return null;
		if ( finalCmd != rootCmd ) return null;
		if ( !finalCmd.IsComplete ) return null;
		return finalCmd;
	}

	private static bool TrySimplify ( List<ArgRoot> seq ) {
		if ( seq.Count < 2 ) return false;

		bool hasChanged = false;
		{ // Try to simplify the first infix notation
			if ( seq[1] is ArgCmd cmdNode && !cmdNode.IsComplete ) {
				cmdNode.PreArg ( seq[0] );
				seq.RemoveAt ( 0 );
				hasChanged = true;
			}
		}

		{ // Try to merge ending args into last command
			for ( int i = seq.Count - 1; i >= 0; i-- ) {
				if ( seq[i] is not ArgCmd cmdNode ) continue;
				while ( i + 1 < seq.Count && !cmdNode.IsComplete ) {
					cmdNode.PostArg ( seq[i + 1] );
					seq.RemoveAt ( i + 1 );
					hasChanged = true;
				}
			}
		}
		{ // Try to merge args in between commands
			for (int second = seq.Count - 1; second >= 0; second--) {
				if ( seq[second] is not ArgCmd secondCmdNode ) continue;
				for ( int first = second - 1; first >= 0; first-- ) {
					if ( seq[first] is not ArgCmd firstCmdNode ) continue;

					if ( second - first == 1 ) ; // Two commands are adjecent, nothing to do
					else if ( second - first == 2 && firstCmdNode.IsComplete ) {
						// First is complete, only valid case is for the arg between to be prefix of second
						// If not, let it throw, as any other option is also invalid
						secondCmdNode.PreArg ( seq[first + 1] );
						seq.RemoveAt ( first + 1 );
						hasChanged = true;
					} else if ( secondCmdNode.IsComplete ) {
						// All must be postArgs of first, let it throw if not
						for ( int i = first + 1; i < second; i++ ) firstCmdNode.PostArg ( seq[i] );
						seq.RemoveRange ( first + 1, second - first - 1 );
						hasChanged = true;
					} else if ( second - first > 3 ) {
						// Push all in between but the last one to first, left last one undecided
						for ( int i = first + 1; i < second - 1; i++ ) firstCmdNode.PostArg ( seq[i] );
						seq.RemoveRange ( first + 1, second - first - 2 );
						hasChanged = true;
						if (secondCmdNode.MissingArgs == 1) {
							// If second command expects only one more argument, it must be the last one, let it throw if not
							secondCmdNode.PreArg ( seq[first + 1] );
							seq.RemoveAt ( first + 1 );
						}
					}

					second = first + 1;
				}
			}
		}
		{ // Merge command as argument into previous adjacent command
			for ( int i = seq.Count - 2; i >= 0; i-- ) {
				if ( seq[i] is not ArgCmd firstCmdNode || seq[i + 1] is not ArgCmd secondCmdNode ) continue;

				if ( secondCmdNode.IsComplete && firstCmdNode.MissingArgs > 0 && firstCmdNode.AcceptedPrefix ) {
					firstCmdNode.PostArg ( secondCmdNode );
					seq.RemoveAt ( i + 1 );
					hasChanged = true;
				}
			}

		}
		{ // Try to merge completed command as argument into first command
			if (seq[0] is ArgCmd firstCmdNode) {
				while (seq.Count > 1 && seq[1] is ArgCmd secondCmdNode && secondCmdNode.IsComplete) {
					// If first command is already full, throw anyway, as there is already no way of parsing this sequence, and the candidate is invalid
					firstCmdNode.PostArg ( secondCmdNode );
					seq.RemoveAt ( 1 );
					hasChanged = true;
				}
			}
		}
		return hasChanged;
	}

	internal class ArgRoot ( ACmdParsingNode node ) {
		public ACmdParsingNode Node = node;
	}

	internal class ArgCmd : ArgRoot {
		public readonly int ArgC;
		public readonly ICommand Command;
		private readonly List<ArgRoot> Args = [];
		public IReadOnlyList<ArgRoot> ArgsList => Args;
		public bool IsComplete => Args.Count == ArgC;
		public int MissingArgs => ArgC - Args.Count;
		public bool AcceptedPrefix { get; private set; } = false;

		public ArgCmd ( CmdParsingNode_Cmd node ) : base ( node ) {
			Command = node.Command.Command;
			ArgC = Command.ArgC;
			Args.EnsureCapacity ( ArgC );
		}

		public void PostArg ( ArgRoot arg ) {
			if ( Args.Count >= ArgC ) throw new ArgumentException ( "Too many arguments for command.", nameof(arg) );
			Args.Add ( arg );
		}
		public void PreArg ( ArgRoot arg ) {
			if ( AcceptedPrefix ) throw new ArgumentException ( "Prefix argument already accepted for command.", nameof(arg) );
			if ( Args.Count >= ArgC ) throw new ArgumentException ( "Too many arguments for command.", nameof(arg) );
			Args.Insert ( 0, arg );
			AcceptedPrefix = true;
		}
	}
}