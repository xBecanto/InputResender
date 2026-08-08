using System;

namespace MdxLibs.Services.TextMatching;
public class PatternCharSet (Func<char, bool> pred, string dsc, int min = 1, int max = 1 ) : APatternNode(min, max) {
	private readonly string Description = dsc;

	public static readonly PatternCharSet Alpha = new (char.IsLetter, "Alpha");
	public static readonly PatternCharSet Lower = new (char.IsLower, "Lower");
	public static readonly PatternCharSet Upper = new (char.IsUpper, "Upper");
	public static readonly PatternCharSet Digit = new (char.IsDigit, "Digit");
	public static readonly PatternCharSet AlphaNum = new (char.IsLetterOrDigit, "AlphaNum");
	public static readonly PatternCharSet Whitespace = new (char.IsWhiteSpace, "WS");
	public static readonly PatternCharSet NonWhitespace = new (c => !char.IsWhiteSpace ( c ), "!WS");
	public static readonly PatternCharSet Any = new (_ => true, ".");
	public static readonly PatternCharSet None = new (_ => false, "∅");
	public static PatternCharSet FromChar ( char c ) => new (ch => ch == c, $"[{c}]" );
	public static PatternCharSet FromString ( string chars ) => new (chars.Contains, $"[{chars}]" );
	public static PatternCharSet FromRange ( char start, char end ) => new (c => c >= start && c <= end, $"[{start}-{end}]" );

	internal override bool Match ( MatchProgress progress ) {
		if ( progress.CanAdvance ( 1 ) ) return false;

		return pred ( progress.NextChar () );
	}
}

public class PatternLiteral ( string text, int min = 1, int max = 1 ) : APatternNode ( min, max ) {
	internal override bool Match ( MatchProgress progress ) {
		if ( progress.CanAdvance ( text.Length ) ) return false;

		return text == progress.NextPart ( text.Length );
	}
}