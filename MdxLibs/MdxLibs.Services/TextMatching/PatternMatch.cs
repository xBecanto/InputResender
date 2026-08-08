using System.Collections.Generic;

namespace MdxLibs.Services.TextMatching;
public class PatternMatch {
	internal PatternMatch ( MatchProgress progress, bool success ) { }
}

internal class MatchProgress ( string text ) {
	public string Input = text;
	public int Position = 0;
	public readonly List<(string groupName, string value)> Captures = [];

	public char NextChar ()
		=> CanAdvance ( 1 )
			? throw new ("No more characters to read.")
			: Input[Position++];

	public string NextPart ( int size ) {
		if ( CanAdvance ( size ) ) throw new ("Not enough characters to read.");

		int start = Position;
		Position += size;
		return Input[start..Position];
	}

	public bool CanAdvance ( int step ) => Position + step < Input.Length;
}