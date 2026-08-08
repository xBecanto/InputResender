namespace MdxLibs.Services.TextMatching;
public class APatternSelection ( APatternNode first, int min = 1, int max = 1 )
	: APatternNodeAggregator ( first, min, max ) {
	internal override bool Match ( MatchProgress progress ) {
		foreach ( var pattern in Patterns ) {
			if ( pattern.Match ( progress ) ) return true;
		}

		return false;
	}

	public override APatternSelection Sel ( APatternNode next ) {
		Patterns.Add ( next );
		return this;
	}
}