using System.Collections.Generic;

namespace MdxLibs.Services.TextMatching;
public abstract class APatternNode (int min, int max) {
	public int Min { get; protected set; } = min;
	public int Max { get; protected set; } = max;

	public virtual APatternSequence Seq ( APatternNode next ) => new (next);
	public virtual APatternSelection Sel ( APatternNode next ) => new (next);

	public APatternNode Once () { Min = 1; Max = 1; return this; }
	public APatternNode Opt () { Min = 0; Max = 1; return this; }
	public APatternNode Many () { Min = 0; Max = int.MaxValue; return this; }
	public APatternNode AtLeast ( int min ) { Min = min; return this; }
	public APatternNode AtMost ( int max ) { Max = max; return this; }
	public APatternNode Between ( int min, int max ) { Min = min; Max = max; return this; }

	public PatternMatch Match ( string text ) {
		MatchProgress progress = new ( text );
		bool success = Match ( progress );
		return new ( progress, success );
	}
	internal abstract bool Match ( MatchProgress progress );
}

public abstract class APatternNodeAggregator (APatternNode first, int min = 1, int max = 1) : APatternNode (min, max) {
	protected readonly List<APatternNode> Patterns = [first];
}