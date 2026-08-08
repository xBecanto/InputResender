namespace MdxLibs.Services.TextMatching;
public class APatternSequence (APatternNode first, int min = 1, int max = 1) : APatternNodeAggregator (first, min, max) {
	public APatternSequence ( APatternNode first, APatternNode second, int min = 1, int max = 1 )
		: this ( first, min, max )
		=> Patterns.Add ( second );

	public APatternSequence ( APatternNode[] nodes, int min = 1, int max = 1 )
		: this ( nodes[0], min, max )
		=> Patterns.AddRange ( nodes[1..] );

	public override APatternSequence Seq ( APatternNode next ) {
		Patterns.Add ( next );
		return this;
	}

	internal override bool Match ( MatchProgress progress ) {
		int startPos = progress.Position;

		foreach ( var pattern in Patterns ) {
			if ( !pattern.Match ( progress ) ) {
				progress.Position = startPos;
				return false;
			}
		}

		return true;
	}
}