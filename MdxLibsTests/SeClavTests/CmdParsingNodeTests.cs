using System;
using System.Collections.Generic;
using FluentAssertions;
using SeClav;
using SeClav.Modules;
using SeClav.Parsing;
using Xunit;

namespace SeClavTests;

public class CmdParsingNodeTests {
	private class SimpleNode : ACmdParsingNode {
		public SimpleNode ( int startPos, int endPos ) : base ( startPos, endPos ) { }
		public override string ToString () => $"Simple({StartPos}-{EndPos})";
	}

	private static SimpleNode           Node      ( int s, int e )   => new ( s, e );
	private static CmdParsingNode_String Str    ( string v, int s )  => new ( v, s, s + v.Length );
	private static CmdParsingNode_Arg    IntArg ( int v, int s = 0 ) { var t = new TestValueIntDef (); return new ( new ( new TestValueInt ( t, v ) ), t, s, s + 1 ); }

	private static CmdParsingNode_Cmd CreateCmdNode ( ICommand command, string callName ) {
		var status     = new SCLParsingStatus ();
		var assignCode = status.RegisterCustomCmd ( new CmdAssignment () );
		var ctx        = new ParsingContext ( _ => null, status, null, assignCode ) { ProcessLine = _ => { } };
		var holder     = new CommandHolder ( new CommandParserHelper ( ctx, string.Empty ), command, callName, callName + " dummy_arg" );
		return new CmdParsingNode_Cmd ( holder, 0 );
	}

	// ==========================================
	// 1. Construction
	// ==========================================

	[Fact]
	public void StringNode_Construction () {
		var node = Str ( "hello world", 5 );
		node.Value.Should ().Be ( "hello world" );
		node.StartPos.Should ().Be ( 5 );
		node.EndPos.Should ().Be ( 16 );
		node.HasNextNodes.Should ().BeFalse ();

		// Value is trimmed on construction
		new CmdParsingNode_String ( "  hello  ", 0, 9 ).Value.Should ().Be ( "hello" );
	}

	[Fact]
	public void ArgNode_Construction () {
		var typeDef = new TestValueIntDef ();
		var node    = new CmdParsingNode_Arg ( new CmdArgInfo ( new TestValueInt ( typeDef, 42 ) ), typeDef, 0, 2 );
		node.ParsedType.Should ().BeSameAs ( typeDef );
		node.ArgInfo.Constant.Should ().BeOfType<TestValueInt> ().Which.Value.Should ().Be ( 42 );
		node.StartPos.Should ().Be ( 0 );
		node.EndPos.Should ().Be ( 2 );
	}

	// ==========================================
	// 2. Linking
	// ==========================================

	[Fact]
	public void Linking_SuffixAndPrefix_BothEstablishLink () {
		var (a, b) = (Node ( 0, 1 ), Node ( 2, 3 ));
		a.Suffix ( b );
		a.HasNextNodes.Should ().BeTrue ();
		a.GetUniqueNextNode<SimpleNode> ().Should ().BeSameAs ( b );

		// Prefix produces the same forward-link result
		var (c, d) = (Node ( 4, 5 ), Node ( 6, 7 ));
		d.Prefix ( c );
		c.GetUniqueNextNode<SimpleNode> ().Should ().BeSameAs ( d );
	}

	[Fact]
	public void Linking_MultipleNextNodes_AllReachable () {
		var (a, b, c) = (Node ( 0, 1 ), Node ( 2, 3 ), Node ( 4, 5 ));
		a.Suffix ( b );
		a.Suffix ( c );   // c has no previous yet – second branch is allowed
		var collected = new List<ACmdParsingNode> ();
		a.PushPotentialsIntoCollection ( collected );
		collected.Should ().HaveCount ( 2 ).And.Contain ( b ).And.Contain ( c );
	}

	[Fact]
	public void Linking_NullArgument_Throws () {
		var node = Node ( 0, 1 );
		((Action)(() => node.Suffix    ( null! ))).Should ().Throw<ArgumentNullException> ();
		((Action)(() => node.Prefix    ( null! ))).Should ().Throw<ArgumentNullException> ();
		((Action)(() => node.ReplaceWith ( null! ))).Should ().Throw<ArgumentNullException> ();
	}

	[Fact]
	public void IllegalLinks_Throw () {
		var (a, b, c) = (Node ( 0, 1 ), Node ( 2, 3 ), Node ( 4, 5 ));
		a.Suffix ( b );   // b.PreviousNode = a

		((Action)(() => b.Prefix ( c )))   // b already has a previous
			.Should ().Throw<InvalidOperationException> ().WithMessage ( "*already has a previous node*" );
		((Action)(() => a.Suffix ( b )))   // b already in a's next-list
			.Should ().Throw<InvalidOperationException> ().WithMessage ( "*already has this next node*" );
		((Action)(() => c.Suffix ( b )))   // b's previous is taken without overwrite permission
			.Should ().Throw<InvalidOperationException> ().WithMessage ( "*already has a previous node*" );
	}

	[Fact]
	public void Suffix_WithOverwritePrevious_AllowedWhenCorrectPreviousGiven () {
		var (a, b, c) = (Node ( 0, 1 ), Node ( 2, 3 ), Node ( 4, 5 ));
		a.Suffix ( b );
		((Action)(() => c.Suffix ( b, overwritePrevious: a ))).Should ().NotThrow ();
		c.GetUniqueNextNode<SimpleNode> ().Should ().BeSameAs ( b );
	}

	// ==========================================
	// 3. GetUniqueNextNode
	// ==========================================

	[Fact]
	public void GetUniqueNextNode_SuccessAndErrorCases () {
		// Success
		var (a, b) = (Node ( 0, 1 ), Node ( 2, 3 ));
		a.Suffix ( b );
		a.GetUniqueNextNode<SimpleNode> ().Should ().BeSameAs ( b );

		// No nexts
		((Action)(() => Node ( 0, 1 ).GetUniqueNextNode<SimpleNode> ()))
			.Should ().Throw<InvalidOperationException> ();

		// Multiple nexts
		var multi = Node ( 0, 1 );
		multi.Suffix ( Node ( 2, 3 ) );
		multi.Suffix ( Node ( 4, 5 ) );
		((Action)(() => multi.GetUniqueNextNode<SimpleNode> ()))
			.Should ().Throw<InvalidOperationException> ().WithMessage ( "*unique next node*" );

		// Wrong type
		var wrongType = Node ( 0, 1 );
		wrongType.Suffix ( Str ( "x", 2 ) );
		((Action)(() => wrongType.GetUniqueNextNode<SimpleNode> ()))
			.Should ().Throw<InvalidOperationException> ().WithMessage ( "*not of the expected type*" );
	}

	// ==========================================
	// 4. BacktrackToRoot
	// ==========================================

	[Fact]
	public void BacktrackToRoot_TracksFullPath () {
		var a = Node ( 0, 1 );
		a.BacktrackToRoot ().Should ().Equal ( a );   // single node

		var (b, c) = (Node ( 2, 3 ), Node ( 4, 5 ));
		a.Suffix ( b );
		b.Suffix ( c );
		c.BacktrackToRoot ().Should ().Equal ( a, b, c );
	}

	[Fact]
	public void BacktrackToRoot_BranchingChain_TracesCurrentBranch () {
		var (a, b, c) = (Node ( 0, 1 ), Node ( 2, 3 ), Node ( 4, 5 ));
		a.Suffix ( b );
		a.Suffix ( c );
		b.BacktrackToRoot ().Should ().Equal ( a, b );
		c.BacktrackToRoot ().Should ().Equal ( a, c );
	}

	// ==========================================
	// 5. ReplaceWith / ReplaceSuffix
	// ==========================================

	[Fact]
	public void ReplaceWith_MiddleOfChain_RewiresLinks () {
		var (a, b, c, bNew) = (Node ( 0, 1 ), Node ( 2, 3 ), Node ( 4, 5 ), Node ( 2, 3 ));
		a.Suffix ( b );
		b.Suffix ( c );
		b.ReplaceWith ( bNew );
		a.GetUniqueNextNode<SimpleNode> ().Should ().BeSameAs ( bNew );
		bNew.GetUniqueNextNode<SimpleNode> ().Should ().BeSameAs ( c );
	}

	[Fact]
	public void ReplaceSuffix_Success () {
		var (a, b, bNew) = (Node ( 0, 1 ), Node ( 2, 3 ), Node ( 4, 5 ));
		a.Suffix ( b );
		a.ReplaceSuffix ( b, bNew );
		a.GetUniqueNextNode<SimpleNode> ().Should ().BeSameAs ( bNew );
	}

	[Fact]
	public void ReplaceSuffix_ErrorCases () {
		var (a, b, c) = (Node ( 0, 1 ), Node ( 2, 3 ), Node ( 4, 5 ));
		a.Suffix ( b );

		// Wrong original node
		((Action)(() => a.ReplaceSuffix ( c, Node ( 6, 7 ) )))
			.Should ().Throw<InvalidOperationException> ();

		// Multiple next nodes (not unique)
		a.Suffix ( c );
		((Action)(() => a.ReplaceSuffix ( b, Node ( 6, 7 ) )))
			.Should ().Throw<InvalidOperationException> ().WithMessage ( "*unique next node*" );

		// New node already belongs to another chain
		var (x, y) = (Node ( 8, 9 ), Node ( 10, 11 ));
		var (a2, b2) = (Node ( 0, 1 ), Node ( 2, 3 ));
		x.Suffix ( y );   // y.PreviousNode = x
		a2.Suffix ( b2 );
		((Action)(() => a2.ReplaceSuffix ( b2, y )))
			.Should ().Throw<InvalidOperationException> ().WithMessage ( "*already assigned to existing chain*" );
	}

	// ==========================================
	// 6. Split – injectBack=false  (Reusability)
	// ==========================================

	[Fact]
	public void Split_NoInject_DoesNotModifyOriginalNode () {
		var original = Str ( "a asdf f d fdsa", 8 );  // [8, 23]
		original.Split ( 9, injectBack: false );
		original.Value.Should ().Be ( "a asdf f d fdsa" );
		original.StartPos.Should ().Be ( 8 );
		original.EndPos.Should ().Be ( 23 );
		original.HasNextNodes.Should ().BeFalse ();
	}

	/// <summary>
	/// "Equals: a asdf f d fdsa" reuse scenario: one remainder-string node is split at
	/// every whitespace to produce all candidate (left, right) pairs independently.
	/// </summary>
	[Fact]
	public void Split_NoInject_Reusable_AllPairsFromSameNode () {
		// ws at Value-index: 1, 6, 8, 10  →  absolute: 9, 14, 16, 18
		var original = Str ( "a asdf f d fdsa", 8 );

		var (f0, s0) = original.Split ( 9,  injectBack: false );
		var (f1, s1) = original.Split ( 14, injectBack: false );
		var (f2, s2) = original.Split ( 16, injectBack: false );
		var (f3, s3) = original.Split ( 18, injectBack: false );

		f0.Value.Should ().Be ( "a" );           s0.Value.Should ().Be ( "asdf f d fdsa" );
		f1.Value.Should ().Be ( "a asdf" );      s1.Value.Should ().Be ( "f d fdsa" );
		f2.Value.Should ().Be ( "a asdf f" );    s2.Value.Should ().Be ( "d fdsa" );
		f3.Value.Should ().Be ( "a asdf f d" );  s3.Value.Should ().Be ( "fdsa" );

		original.Value.Should ().Be ( "a asdf f d fdsa" );   // untouched after all splits
		original.HasNextNodes.Should ().BeFalse ();

		f0.Suffix ( s0 );
		f1.HasNextNodes.Should ().BeFalse ();   // chaining pair 0 does not affect pair 1
		s2.HasNextNodes.Should ().BeFalse ();
	}

	[Fact]
	public void Split_NoInject_BoundariesAndOutOfRange () {
		// Correct part values and positions
		var (first, second) = Str ( "hello world", 0 ).Split ( 5, injectBack: false );
		first.Value.Should ().Be ( "hello" );
		first.StartPos.Should ().Be ( 0 );
		first.EndPos.Should ().Be ( 5 );
		second.Value.Should ().Be ( "world" );
		second.EndPos.Should ().Be ( 11 );

		// Split at start → first is null
		var (f2, s2) = Str ( "hello", 5 ).Split ( 5, injectBack: false );
		f2.Should ().BeNull ();
		s2.Value.Should ().Be ( "hello" );

		// Split at end → second is null
		var (f3, s3) = Str ( "hello", 5 ).Split ( 10, injectBack: false );
		f3.Value.Should ().Be ( "hello" );
		s3.Should ().BeNull ();

		// Out-of-range throws
		var node = Str ( "hello", 5 );
		((Action)(() => node.Split ( 4,  false ))).Should ().Throw<ArgumentOutOfRangeException> ();
		((Action)(() => node.Split ( 11, false ))).Should ().Throw<ArgumentOutOfRangeException> ();
	}

	// ==========================================
	// 7. Split – injectBack=true
	// ==========================================

	[Fact]
	public void Split_InjectBack_ReplacesOriginalInChain () {
		var pre      = Node ( 0, 8 );
		var original = Str ( "hello world", 9 );    // [9, 20]
		var post     = Node ( 21, 30 );
		pre.Suffix ( original );
		original.Suffix ( post );

		// Split at 14 → "hello"[9,14] and "world"[15,20]
		var (first, second) = original.Split ( 14, injectBack: true );

		pre.GetUniqueNextNode<CmdParsingNode_String> ().Should ().BeSameAs ( first );
		first!.GetUniqueNextNode<CmdParsingNode_String> ().Should ().BeSameAs ( second );
		second!.GetUniqueNextNode<SimpleNode> ().Should ().BeSameAs ( post );
	}

	// ==========================================
	// 8. Three-Way Split
	// ==========================================

	[Fact]
	public void Split3_NoInject_Behavior () {
		var original = Str ( "a b c", 0 );
		var (first, second, third) = original.Split ( 1, 3, injectBack: false );

		// Correct values (whitespace trimmed between split points)
		first!.Value.Should ().Be ( "a" );
		second!.Value.Should ().Be ( "b" );
		third!.Value.Should ().Be ( "c" );

		// Unlike the 2-way split, the 3-way split always chains the produced parts
		first.GetUniqueNextNode<CmdParsingNode_String> ().Should ().BeSameAs ( second );
		second.GetUniqueNextNode<CmdParsingNode_String> ().Should ().BeSameAs ( third );
		third.HasNextNodes.Should ().BeFalse ();

		// Original is completely untouched
		original.Value.Should ().Be ( "a b c" );
		original.HasNextNodes.Should ().BeFalse ();

		// Boundary case: split at 0 and 5 → null first and third, full middle
		var (f, mid, t) = Str ( "hello", 0 ).Split ( 0, 5, injectBack: false );
		f.Should ().BeNull ();
		mid!.Value.Should ().Be ( "hello" );
		t.Should ().BeNull ();
	}

	// ==========================================
	// 9. CmdParsingNode_Cmd – PushArg
	// ==========================================

	[Fact]
	public void CmdNode_PushArg_AcceptsArgNode () {
		var cmdNode = CreateCmdNode ( new AddInts (), "ADD_INT" );  // ArgC=2
		cmdNode.PushArg ( IntArg ( 1, 8 ) );
		cmdNode.ProcessedArgs.Should ().Be ( 1 );
		cmdNode.MissingArgs.Should ().Be ( 1 );
	}

	[Fact]
	public void CmdNode_PushArg_ErrorCases () {
		// Overflow: too many arguments
		var full = CreateCmdNode ( new AddInts (), "ADD_INT" );  // ArgC=2
		full.PushArg ( IntArg ( 1, 8 ) );
		full.PushArg ( IntArg ( 2, 10 ) );
		((Action)(() => full.PushArg ( IntArg ( 3, 12 ) )))
			.Should ().Throw<InvalidOperationException> ().WithMessage ( "*already been processed*" );

		// Wrong type: CmdParsingNode_String is neither Arg nor Cmd
		var inv = CreateCmdNode ( new AddInts (), "ADD_INT" );
		((Action)(() => inv.PushArg ( Str ( "bad", 0 ) )))
			.Should ().Throw<InvalidOperationException> ().WithMessage ( "*Argument must be*" );
	}
}
