using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SeClav.Parsing;
internal abstract class ACmdParsingNode {
	protected ACmdParsingNode PreviousNode;
	protected List<ACmdParsingNode> PotentialNextNodes;
	public readonly int StartPos;
	public readonly int EndPos;

	protected ACmdParsingNode ( int startPos, int endPos ) {
		PotentialNextNodes = [];
		StartPos = startPos;
		EndPos = endPos;
	}

	public void PushPotentialsIntoCollection ( ICollection<ACmdParsingNode> collection ) {
		foreach ( var node in PotentialNextNodes ) collection.Add ( node );
	}

	public ACmdParsingNode[] BacktrackToRoot () {
		List<ACmdParsingNode> ret = [];
		ACmdParsingNode current = this;
		while ( current != null ) {
			ret.Add ( current );
			current = current.PreviousNode;
		}
		ret.Reverse ();
		return ret.ToArray ();
	}

	[return: NotNull]
	public T GetUniqueNextNode<T> () where T : ACmdParsingNode {
		if ( PotentialNextNodes.Count == 0 ) throw new InvalidOperationException ( "Node has no next node." );
		if ( PotentialNextNodes.Count != 1 ) throw new InvalidOperationException ( "Node does not have a unique next node." );
		if ( PotentialNextNodes[0] == null ) throw new InvalidOperationException ( "Unexpected null as a next node" );
		if ( PotentialNextNodes[0] is not T nextNode ) throw new InvalidOperationException ( "Next node is not of the expected type." );
		return nextNode;
	}

	public void ReplaceSuffix ( ACmdParsingNode orig, ACmdParsingNode newNode ) {
		ArgumentNullException.ThrowIfNull ( orig );
		ArgumentNullException.ThrowIfNull ( newNode );
		if ( PotentialNextNodes.Count != 1 ) throw new InvalidOperationException ( "Node does not have a unique next node." );
		if ( PotentialNextNodes[0] != orig ) throw new InvalidOperationException ( "Next node is not one which is expected" );
		if ( newNode.PreviousNode != null ) throw new InvalidOperationException ( "The new node is already assigned to existing chain" );

		PotentialNextNodes[0] = newNode;
		newNode.PreviousNode = this;
	}

	public bool HasNextNodes => PotentialNextNodes.Count > 0;

	public void Prefix ( ACmdParsingNode node ) {
		ArgumentNullException.ThrowIfNull ( node );
		if ( PreviousNode != null ) throw new InvalidOperationException ( "Node already has a previous node." );

		PreviousNode = node;
		node.PotentialNextNodes.Add ( this );
	}

	public void Suffix ( ACmdParsingNode node, ACmdParsingNode overwritePrevious = null ) {
		ArgumentNullException.ThrowIfNull ( node );
		if ( PotentialNextNodes.Contains ( node ) )
			throw new InvalidOperationException ( "Node already has this next node." );
		if ( node.PreviousNode != null && node.PreviousNode != overwritePrevious )
			throw new InvalidOperationException ( "Node already has a previous node." );

		PotentialNextNodes.Add ( node );
		node.PreviousNode = this;
	}

	public void ReplaceWith ( ACmdParsingNode newNode ) {
		ArgumentNullException.ThrowIfNull ( newNode );

		if ( PreviousNode != null ) {
			PreviousNode.PotentialNextNodes.Remove ( this );
			PreviousNode.PotentialNextNodes.Add ( newNode );
			newNode.PreviousNode = PreviousNode;
		}

		foreach ( var nextNode in PotentialNextNodes ) {
			nextNode.PreviousNode = newNode;
			newNode.PotentialNextNodes.Add ( nextNode );
		}
		PotentialNextNodes.Clear ();
	}

	// public void InsertAfter ( ACmdParsingNode node ) {
	// 	ArgumentNullException.ThrowIfNull ( node );
	// 	if ( node.PreviousNode != null ) throw new InvalidOperationException ( "Node already has a previous node." );
	//
	// 	foreach ( var nextNode in PotentialNextNodes ) nextNode.PreviousNode = node;
	// 	node.PotentialNextNodes.AddRange ( PotentialNextNodes );
	// 	PotentialNextNodes.Clear ();
	// 	PotentialNextNodes.Add ( node );
	// 	node.PreviousNode = this;
	// }
}

internal class CmdParsingNode_Cmd : ACmdParsingNode {
	public readonly CommandHolder Command;
	private readonly ACmdParsingNode[] Args;
	public int ProcessedArgs { get; private set; } = 0;
	public int MissingArgs => Args.Length - ProcessedArgs;

	public CmdParsingNode_Cmd ( CommandHolder command, int startPos ) : base ( startPos
		, startPos + command.CallName.Length
	) {
		Command = command;
		Args = new ACmdParsingNode[command.Command.ArgC];
	}

	public void PushArg ( ACmdParsingNode arg ) {
		if ( ProcessedArgs >= Args.Length ) throw new InvalidOperationException ( "All arguments have already been processed." );
		if ( arg is not CmdParsingNode_Arg or CmdParsingNode_Cmd )
			throw new InvalidOperationException ( "Argument must be a CmdParsingNode_Arg or CmdParsingNode_Cmd." );
		Args[ProcessedArgs++] = arg;
	}

	public override string ToString () => $"Node_Cmd({Command.Command.CmdCode}, {StartPos}-{EndPos}, {Command.Command.GetType ().Name}, ProcessedArgs={ProcessedArgs})";
}

internal class CmdParsingNode_Arg ( CmdArgInfo argInfo, DataTypeDefinition parsedType, int startPos, int endPos ) : ACmdParsingNode ( startPos, endPos
) {
	public readonly CmdArgInfo ArgInfo = argInfo;
	public readonly DataTypeDefinition ParsedType = parsedType;

	public override string ToString () => $"Node_Arg({ParsedType}({ArgInfo}), {StartPos}-{EndPos})";
}

internal class CmdParsingNode_String ( string value, int startPos, int endPos ) : ACmdParsingNode ( startPos, endPos ) {
	public readonly string Value = value.Trim ();

	public override string ToString () => $"Node_String({Value}, {StartPos}-{EndPos})";

	public (CmdParsingNode_String firstNode, CmdParsingNode_String secondNode) Split ( int splitPos, bool injectBack ) {
		if ( splitPos < StartPos || splitPos > EndPos )
			throw new ArgumentOutOfRangeException ( nameof ( splitPos ), "Split position must be within the bounds of the string node." );

		string firstPart = Value[..(splitPos - StartPos)].TrimEnd ();
		string secondPart = Value[(splitPos - StartPos)..].TrimStart ();

		var firstNode = StartPos == splitPos ? null : new CmdParsingNode_String ( firstPart, StartPos, StartPos + firstPart.Length );
		var secondNode = EndPos == splitPos ? null : new CmdParsingNode_String (secondPart, EndPos - secondPart.Length, EndPos );

		if ( injectBack ) {
			PreviousNode?.ReplaceSuffix ( this, firstNode ?? secondNode );
			firstNode?.Suffix ( secondNode );
			// Should be one or the other. Both nulls would be a result of a bug, but keep the ?. to silence the warning
			(secondNode ?? firstNode)?.PotentialNextNodes.AddRange ( PotentialNextNodes );
		}

		return (firstNode, secondNode);
	}

	public (CmdParsingNode_String firstNode, CmdParsingNode_String secondNode, CmdParsingNode_String thirdNode) Split ( int firstSplitPos, int secondSplitPos, bool injectBack ) {
		if ( firstSplitPos < StartPos || firstSplitPos > EndPos )
			throw new ArgumentOutOfRangeException ( nameof ( firstSplitPos ), "First split position must be within the bounds of the string node." );
		if ( secondSplitPos < StartPos || secondSplitPos > EndPos )
			throw new ArgumentOutOfRangeException ( nameof ( secondSplitPos ), "Second split position must be within the bounds of the string node." );
		if ( firstSplitPos > secondSplitPos )
			throw new ArgumentException ( "First split position must be less than or equal to second split position." );

		string firstPart = Value[..(firstSplitPos - StartPos)].TrimEnd ();
		string secondPart = Value[(firstSplitPos - StartPos)..(secondSplitPos - StartPos)].Trim ();
		string thirdPart = Value[(secondSplitPos - StartPos)..].TrimStart ();

		var firstNode = StartPos == firstSplitPos ? null : new CmdParsingNode_String ( firstPart, StartPos, StartPos + firstPart.Length );
		var secondNode = firstSplitPos == secondSplitPos ? null : new CmdParsingNode_String ( secondPart, firstSplitPos, secondSplitPos );
		var thirdNode = EndPos == secondSplitPos ? null : new CmdParsingNode_String ( thirdPart, EndPos - thirdPart.Length, EndPos );

		if ( secondNode != null ) firstNode?.Suffix ( secondNode );
		if ( thirdNode != null ) (secondNode ?? firstNode)?.Suffix ( thirdNode );

		if ( injectBack ) {
			PreviousNode?.ReplaceSuffix ( this, firstNode ?? secondNode ?? thirdNode );
			// Should be one or the other. All nulls would be a result of a bug, but keep the ?. to silence the warning
			(thirdNode ?? secondNode ?? firstNode)?.PotentialNextNodes.AddRange ( PotentialNextNodes );
		}

		return (firstNode, secondNode, thirdNode);
	}
}


internal struct CommandHolder {
	public readonly CommandParserHelper Parent;
	public readonly ICommand Command;
	public readonly string OriginalLine;
	public readonly string CallName;
	private int StartPos, EndPos;

	public CommandHolder ( CommandParserHelper parent, ICommand command, string callName, string originalLine ) {
		Parent = parent;
		Command = command;
		CallName = callName;
		OriginalLine = originalLine;

		StartPos = originalLine.IndexOf ( callName );
		EndPos = StartPos + CallName.Length;
	}

	/// <summary>Find any next instance of the command, such that it is after given position.
	/// Keep current spot if already past the given mark, unless <paramref name="force"/> is set to true.</summary>
	public bool Advance (int minStart = -1, bool force = false) {
		if ( !force && StartPos > minStart ) return true; // Is already 'advanced' enough
		while ( true ) {
			if ( minStart >= OriginalLine.Length ) return false;
			int nextPos = OriginalLine.IndexOf ( CallName, minStart );
			if ( nextPos < 0 ) return false;

			StartPos = nextPos;
			EndPos = StartPos + CallName.Length;
			if ( !IsValid ) continue;

			return true;
		}
	}

	// public CmdParsingNode_Arg LoadPreArg () {
	// 	if ( StartPos <= 0 ) return null; // No pre-arg for starters, don't remove from set
	// 	// Load the argument before the callname, which is not a command.
	// 	string preArgLine = OriginalLine[..StartPos].TrimEnd ();
	// 	var type = Parent.Context.Status.GetDataType ( Command, 0 );
	// 	try {
	// 		var firstArg = Parent.Context.ParseArg_NoCmd ( ref preArgLine, Parent.Context, type, null );
	// 		if ( preArgLine.Length > 0 ) return null; // Not fully consumed, invalid pre-arg
	// 		return new ( firstArg, 0, StartPos );
	// 	}
	// 	catch ( Exception e ) { return null; }
	// }

	public readonly CmdParsingNode_Arg LoadArg ( int argIndex, CmdParsingNode_String remLine ) {
		string line = remLine.Value.Trim ();
		var type = Parent.Context.Status.GetDataType ( Command, argIndex );
		if ( type == null ) {
			type = Parent.Context.Status.GetDataType ( Command, argIndex ); // Try again for debugging
			throw new InvalidOperationException (
				$"Could not find data type for argument index {argIndex} of command {Command.CmdCode}"
			);
		}

		try {
			var firstArg = Parent.Context.ParseArg_NoCmd ( ref line, Parent.Context, type, null );
			int endPos = remLine.StartPos + (remLine.Value.Length - line.Length);
			CmdParsingNode_Arg ret = new (firstArg, type, remLine.StartPos, endPos);
			if ( line.Length > 0 )
				ret.Suffix ( new CmdParsingNode_String ( line.Trim (), endPos, endPos + line.Length ) );
			return ret;
		}
		catch ( Exception e ) { return null; }
	}

	public (CmdParsingNode_String, CmdParsingNode_Cmd, CmdParsingNode_String) ToNode ( CmdParsingNode_String remLine ) {
		if ( remLine != null && (remLine.StartPos > StartPos || remLine.EndPos < EndPos) )
			throw new InvalidOperationException (
				"The provided remaining line does not encompass the command's position."
			);

		CmdParsingNode_Cmd ret = new (this, StartPos);
		if ( remLine == null ) {
			// Creating a command 'out of thing air', use the whole OriginalLine
			CmdParsingNode_String postNode = null, preNode = null;
			int preEnd = StartPos - 1;
			while ( preEnd >= 0 ) {
				if ( char.IsWhiteSpace ( OriginalLine[preEnd] ) ) { preEnd--; continue; }
				preNode = new (OriginalLine[..(preEnd + 1)], 0, preEnd + 1);
				ret.Prefix ( preNode );
				break;
			}
			int postStart = EndPos + 1;
			while ( OriginalLine.Length > postStart ) {
				if ( char.IsWhiteSpace ( OriginalLine[postStart] ) ) { postStart++; continue; }
				postNode = new (OriginalLine[postStart..], postStart, OriginalLine.Length);
				ret.Suffix ( postNode );
				break;
			}

			return (preNode, ret, postNode);
		} else {
			var (preNode, cmdNode, postNode) = remLine.Split ( StartPos, EndPos, false );
			cmdNode.ReplaceWith ( ret );
			// Divide the remLine and update the pre and post nodes accordingly
			/*if ( remLine.StartPos < StartPos ) {
				(preNode, postNode) = remLine.Split ( StartPos, true );
				(var cmdTextNode, postNode) = postNode.Split ( EndPos, true );
				preNode.ReplaceSuffix ( postNode, ret );
			}

			if ( remLine.EndPos > EndPos ) {
				(var origPre, postNode) = remLine.Split ( EndPos, true );
				ret.Suffix ( postNode, overwritePrevious: origPre );
			}*/
			return (preNode, ret, postNode);
		}
	}

	public bool IsValid => StartPos >= 0 && EndPos <= OriginalLine.Length
		&& ( StartPos == 0 || char.IsWhiteSpace ( OriginalLine[StartPos - 1] ) )
		&& ( EndPos == OriginalLine.Length || char.IsWhiteSpace ( OriginalLine[EndPos] ) );

	public override string ToString () => $"CommandHolder({Command.CmdCode}, {CallName}, {StartPos}-{EndPos})";
}

internal class CommandParserHelper {
	public readonly ParsingContext Context;
	public readonly string OriginalLine;
	private readonly List<string> PossibleCallnames;
	private readonly List<CommandHolder> Commands = [];
	private readonly HashSet<CmdParsingNode_Cmd> Open = [];
	public readonly HashSet<ACmdParsingNode[]> CandidateSequences = [];

	public CommandParserHelper ( ParsingContext context, string line, DataTypeDefinition reqRetType = null ) {
		Context = context;
		OriginalLine = line;

		PossibleCallnames = context.Status.PossibleCommands.Where ( line.Contains ).ToList ();
		foreach ( var callname in PossibleCallnames ) {
			Commands.AddRange ( context.Status.TryGetCommands ( callname )
				.Select ( cmd => new CommandHolder ( this, cmd, callname, line ) )
			);
		}
		Commands.RemoveAll ( c => !c.IsValid );
		if (reqRetType != null) reqRetType = context.Status.GetDataType ( reqRetType.Name );

		List<CmdParsingNode_Cmd> roots = [];
		var starters = Commands.Where ( c => line.StartsWith ( c.CallName ) ).ToList ();
		if ( starters.Count > 0 ) {
			foreach ( var starter in starters ) {
				if ( reqRetType != null && reqRetType != context.Status.GetDataType ( starter.Command.ReturnType.Name ) ) continue;
				var (_, initNode, _) = starter.ToNode ( null );
				roots.Add ( initNode );
			}
		} else {
			foreach ( var cmd in Commands ) {
				if ( reqRetType != null && reqRetType != context.Status.GetDataType ( cmd.Command.ReturnType.Name ) ) continue;
				var (preString, initNode, _) = cmd.ToNode ( null );
				var preArg = cmd.LoadArg ( 0, preString );
				if ( preArg == null ) continue;
				if ( preArg.HasNextNodes ) continue; // Pre-arg must be fully consumed, otherwise invalid sequence

				preString.ReplaceWith ( preArg );
				initNode.PushArg ( preArg );
				roots.Add ( initNode );
			}
		}

		Open.UnionWith ( roots );
		List<Exception> exceptions = [];
		while ( Open.Count > 0 ) {
			var node = Open.First ();
			try {
				var candidateRes = TryCandidate ( node );
				if ( candidateRes == null || candidateRes.Count == 0 ) continue;
				Open.UnionWith ( candidateRes );
			}
			catch ( Exception ex ) {
				// Invalid candidate, let's continue
				exceptions.Add ( ex );
			}
		}

		foreach ( var root in roots ) {
			List<ACmdParsingNode> thisEnds = [];
			List<ACmdParsingNode> locOpen = [root];
			while ( locOpen.Count > 0 ) {
				var node = locOpen.First ();
				locOpen.Remove ( node );
				if ( node is CmdParsingNode_Cmd cmdNode ) {
					if ( cmdNode.MissingArgs == 0 ) { // Should be always true, but just in case
						ACmdParsingNode lastNode = cmdNode;
						while ( lastNode.HasNextNodes )
							lastNode = lastNode.GetUniqueNextNode<ACmdParsingNode> ();
						thisEnds.Add ( lastNode );
					}
				}
				node.PushPotentialsIntoCollection ( locOpen );
			}

			foreach ( var end in thisEnds )
				CandidateSequences.Add ( end.BacktrackToRoot () );
		}

		if ( CandidateSequences.Count == 0 && exceptions.Count > 0 ) {
			throw new AggregateException ( "No valid command sequences could be parsed.", exceptions );
		}
	}

	private List<CmdParsingNode_Cmd> TryCandidate ( CmdParsingNode_Cmd argNode ) {
		Open.Remove ( argNode );
		List<CmdParsingNode_Cmd> localOpens = [];
		if ( !argNode.HasNextNodes && argNode.Command.Command.ArgC == 0 ) return localOpens;

		// Copy the list to avoid modifying the original while iterating
		(CommandHolder holder, bool active)[] myHolders = Commands.Select ( c => (c, true) ).ToArray ();

		var remLine = argNode.GetUniqueNextNode<CmdParsingNode_String> ();

		void AdvanceHolders ( ACmdParsingNode preNode ) {
			int minStart;
			try {
				minStart = preNode.GetUniqueNextNode<CmdParsingNode_String> ().StartPos;
			}
			catch ( Exception _ ) { minStart = preNode.EndPos; }
			for ( int i = 0; i < myHolders.Length; i++ ) {
				if ( !myHolders[i].active ) continue;
				myHolders[i].active = myHolders[i].holder.Advance ( minStart );
			}
		}
		AdvanceHolders ( argNode );

		if ( !myHolders.Any ( h => h.active ) ) {
			ACmdParsingNode actNode = argNode;
			while ( argNode.MissingArgs > 0 ) {
				if ( remLine.Value.Length == 0 )
					return null; // Missing argument, yet no more string to parse, invalid sequence

				var nextArg = argNode.Command.LoadArg ( argNode.ProcessedArgs, remLine );
				if ( nextArg == null )
					return null; // Missing argument, yet could not parse another one, invalid sequence

				argNode.PushArg ( nextArg );
				actNode.ReplaceSuffix ( remLine, nextArg );
				if ( argNode.MissingArgs > 0 ) remLine = nextArg.GetUniqueNextNode<CmdParsingNode_String> ();
				actNode = nextArg;
			}

			return localOpens;
		}

		List<(CommandHolder holder, bool active)> starters = myHolders.Where ( c => c.active && remLine.Value.StartsWith ( c.holder.CallName ) ).ToList ();
		foreach ( var (starter, _) in starters ) {
			var (_, nextCmd, _) = starter.ToNode ( remLine );
			argNode.Suffix ( nextCmd );
			localOpens.Add ( nextCmd );
		}

		var nextDirectArg = argNode.Command.LoadArg ( argNode.ProcessedArgs, remLine );
		if ( nextDirectArg != null ) {
			argNode.PushArg ( nextDirectArg );
			argNode.ReplaceSuffix ( remLine, nextDirectArg );
			remLine = nextDirectArg.GetUniqueNextNode<CmdParsingNode_String> ();
			AdvanceHolders ( nextDirectArg );
		}

		if ( nextDirectArg != null && !myHolders.Any ( h => h.active ) ) {
			while ( argNode.MissingArgs > 0 ) {
				if ( remLine.Value.Length == 0 )
					return null; // Missing argument, yet no more string to parse, invalid sequence

				var nextArg = argNode.Command.LoadArg ( argNode.ProcessedArgs, remLine );
				if ( nextArg == null )
					return null; // Missing argument, yet could not parse another one, invalid sequence

				argNode.PushArg ( nextArg );
				nextDirectArg.ReplaceSuffix ( remLine, nextArg );
				nextDirectArg = nextArg;
				if ( argNode.MissingArgs > 0 ) remLine = nextArg.GetUniqueNextNode<CmdParsingNode_String> ();

			}
		}

		foreach ( var (cmd, active) in myHolders ) {
			if ( !active ) continue;
			var (preArgNode, nextCmdNode, _) = cmd.ToNode ( remLine );
			if (nextCmdNode == null) continue;

			var preArg = preArgNode != null ? cmd.LoadArg ( 0, preArgNode ) : null;
			if (preArg != null ) {
				if ( preArg.HasNextNodes ) continue; // Pre-arg must be fully consumed, otherwise invalid sequence
				preArgNode.ReplaceWith ( preArg );
			}

			ACmdParsingNode nextNode = preArgNode == null ? nextCmdNode : preArg;
			if ( nextNode == null ) continue;

			if ( nextNode.HasNextNodes ) localOpens.Add ( nextCmdNode );

			if ( nextDirectArg != null )
				nextDirectArg.Suffix ( nextNode );
			else
				argNode.Suffix ( nextNode );
		}

		if ( argNode.HasNextNodes ) {

		} else return localOpens;
		try {
			argNode.GetUniqueNextNode<CmdParsingNode_String> ();
			return localOpens; // No valid sequence from here, yet still has unconsumed string
		}
		catch ( Exception e ) { return localOpens; }
	}
}