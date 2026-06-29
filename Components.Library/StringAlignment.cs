using System;
using System.Linq;
using System.Collections.Generic;
namespace Components.Library;

public enum AlignmentType {
	Match,      // Token is the same in both strings
	Insertion,  // Token appears in second string but not in first
	Deletion,   // Token appears in first string but not in second
	Mutation    // Token appears in both but with different content
}

public class AlignmentToken {
	public readonly AlignmentType Type;
	public readonly char Mark;
	public readonly string FirstValue;
	public readonly string SecondValue;
	public readonly int FirstPos;
	public readonly int SecondPos;
	public int TotScore { get; private set; }
	public double Mutativity { get; private set; }
	public AlignmentToken[] SubAlignments { get; private set; }

	public override string ToString() => $"{Type}: '{FirstValue}' → '{SecondValue}'";

	public string GetPos (int offset = 0) => Type switch {
			AlignmentType.Deletion    => $"[{FirstPos + offset}]"
			, AlignmentType.Insertion => $"[{SecondPos + offset}]"
			, AlignmentType.Match or AlignmentType.Mutation => FirstPos == SecondPos
				? $"[{FirstPos + offset}]"
				: $"[{FirstPos + offset}/{SecondPos + offset}]"
			, _ => $"[{FirstPos + offset}/{SecondPos + offset}]"
		};

	public AlignmentToken ( AlignmentType type, string firstValue, int firstPos, string secondValue, int secondPos ) {
		Type = type;
		FirstValue = firstValue;
		SecondValue = secondValue;
		FirstPos = firstPos;
		SecondPos = secondPos;
		Mark = type switch {
			AlignmentType.Match => ' ',
			AlignmentType.Insertion => '+',
			AlignmentType.Deletion => '-',
			AlignmentType.Mutation => '~',
			_ => ' ',
		};
	}

	public void AssignSubResult ( (AlignmentToken[] alignemnts, int totScore) sub ) {
		SubAlignments = sub.alignemnts;
		TotScore = sub.totScore;
		Mutativity = (double)sub.totScore / Math.Max ( FirstPos, SecondPos );
		int maxSize = Math.Max ( FirstPos, SecondPos );
		int minScore = maxSize * Math.Min ( StringAlignment.MismatchScore, StringAlignment.GapPenalty );
		int maxScore = maxSize * StringAlignment.MatchScore;
		Mutativity = 1 - (double)(sub.totScore - minScore) / (maxScore - minScore);
	}

	public static AlignmentToken Match (string a, int aPos, int bPos) => new (AlignmentType.Match, a, aPos, a, bPos);
	public static AlignmentToken Insertion (string[] bAr, int bPos) => new (AlignmentType.Insertion, null, -1, bAr[bPos], bPos);
	public static AlignmentToken Deletion (string[] aAr, int aPos) => new (AlignmentType.Deletion, aAr[aPos], aPos, null, -1);
	public static AlignmentToken Mutation ( string[] aAr, int aPos, string[] bAr, int bPos )
		=> new (AlignmentType.Mutation, aAr[aPos], aPos, bAr[bPos], bPos);

	public static AlignmentToken MatchOrMutation ( string[] aAr, int aPos, string[] bAr, int bPos )
		=> new (aAr[aPos] == bAr[bPos] ? AlignmentType.Match : AlignmentType.Mutation
			, aAr[aPos], aPos, bAr[bPos], bPos
		);

	public static (AlignmentToken[], int) MatchOrMutation ( string a, string b ) {
		bool match = a == b;
		return ([new (match ? AlignmentType.Match : AlignmentType.Mutation, a, 0, b, 0)]
			, match ? StringAlignment.MatchScore : StringAlignment.MismatchScore);
	}
}

public class StringAlignment {
	public const int MatchScore = 2;
	public const int MismatchScore = -1;
	public const int GapPenalty = -1;

	public static (AlignmentToken[], int) Align ( string first, string second, string[][] separators ) {
		if ( separators == null || separators.Length == 0 ) {
			return AlignCharacters ( first, second );
		}

		return AlignRecursive ( first, second, separators, 0 );
	}

	private static (AlignmentToken[], int) AlignRecursive ( string first, string second, string[][] separators, int depth ) {
		if ( depth >= separators.Length ) {
			return AlignmentToken.MatchOrMutation ( first, second );
		}

		string[] firstTokens = first.TokenizeBySeparators ( separators[depth] );
		string[] secondTokens = second.TokenizeBySeparators ( separators[depth] );

		(AlignmentToken[] tokenAlignment, int finalScore) = NeedlemanWunsch ( firstTokens, secondTokens );

		// If there are more separator levels, recursively align sub-tokens
		if ( depth + 1 < separators.Length ) {
			foreach ( var token in tokenAlignment ) {
				if ( token.Type == AlignmentType.Match || token.Type == AlignmentType.Mutation ) {
					token.AssignSubResult ( AlignRecursive (
							token.FirstValue ?? "",
							token.SecondValue ?? "",
							separators,
							depth + 1
						)
					);
				}
			}
		}

		return (tokenAlignment, finalScore);
	}

	[Flags]
	enum Backtrack : byte {
		None = 0,
		Diagonal = 1,
		Up = 2,
		Left = 4,
	}

	private static (AlignmentToken[], int) NeedlemanWunsch ( string[] first, string[] second ) {
		/*
		 So you don't have to search what's what:
		 'Left' would be first string, going row by row downard.
		 'Top' would be the second string, going columns by column to the right.
		 An insertion, i.e. like GT->GCCT should result in move to the right.
		 Deletion moves down. As intuition for this can be:
		 if you pass same strings, just first or second has bunch of characters added/removed,
		 there kinda will not be enough space to move in one of the directions. So it must go where the table is larger.
		 */
		int n = first.Length;
		int m = second.Length;

		if ( n == 0 || m == 0 ) {
			if ( n == m ) return ([], 0); // No text in both

			int N = int.Max ( n, m );
			string[] act = n == 0 ? second : first;
			var ret = new AlignmentToken[N];
			int score = 0;
			for (int i = 0; i < N; i++) {
				ret[i] = AlignmentToken.Insertion ( act, i );
				score += GapPenalty * act[i].Length;
			}

			return (ret, score);
		}

		var scoreMatrix = new (int Val, Backtrack Dir)[n + 1, m + 1];
		InitializeMatrix ( scoreMatrix, n, m, first, second );

		// Fill the scoring matrix
		for ( int i = 1; i <= n; i++ ) {
			for ( int j = 1; j <= m; j++ ) {
				int matchScore = scoreMatrix[i - 1, j - 1].Val + GetScore ( first[i - 1], second[j - 1] )
					* (1 + (first[i - 1].Length + second[j - 1].Length) / 2);
				int deleteScore = scoreMatrix[i - 1, j].Val + GapPenalty * first[i - 1].Length;
				int insertScore = scoreMatrix[i, j - 1].Val + GapPenalty * second[j - 1].Length;

				int max = Math.Max ( matchScore, Math.Max ( deleteScore, insertScore ) );
				scoreMatrix[i, j] = (max, Backtrack.None);
				if ( max == matchScore ) scoreMatrix[i, j].Dir |= Backtrack.Diagonal;
				if ( max == deleteScore ) scoreMatrix[i, j].Dir |= Backtrack.Up;
				if ( max == insertScore ) scoreMatrix[i, j].Dir |= Backtrack.Left;
			}
		}

		// Traceback to get the alignment
		return (Traceback ( first, second, scoreMatrix ), scoreMatrix[n, m].Val);
	}

	private static void InitializeMatrix ( (int Val, Backtrack Dir)[,] matrix, int rows, int cols, string[] first, string[] second ) {
		matrix[0, 0] = (0, Backtrack.None);
		for ( int i = 1; i <= rows; i++ ) matrix[i, 0] = (matrix[i - 1, 0].Val + GapPenalty * first[i - 1].Length, Backtrack.Up);
		for ( int j = 1; j <= cols; j++ ) matrix[0, j] = (matrix[0, j - 1].Val + GapPenalty * second[j - 1].Length, Backtrack.Left);
	}

	private static int GetScore ( string a, string b ) {
		return a == b ? MatchScore : MismatchScore;
	}

	private static AlignmentToken[] Traceback ( string[] first, string[] second, (int Val, Backtrack Dir)[,] scoreMatrix ) {
		List<AlignmentToken> result = new ();
		int i = first.Length;
		int j = second.Length;

		while ( i > 0 || j > 0 ) {
			if (scoreMatrix[i, j].Dir.HasFlag (Backtrack.Diagonal)) {
				result.Add ( AlignmentToken.MatchOrMutation ( first, i - 1, second, j - 1 ) );
				i--;
				j--;
			} else if (scoreMatrix[i, j].Dir.HasFlag (Backtrack.Up)) {
				result.Add ( AlignmentToken.Deletion ( first, i - 1 ) );
				i--;
			} else if (scoreMatrix[i, j].Dir.HasFlag (Backtrack.Left)) {
				result.Add ( AlignmentToken.Insertion ( second, j - 1 ) );
				j--;
			} else {
				break; // Should not happen
			}
			/*if ( i > 0 && j > 0 ) {
				int matchScore = scoreMatrix[i - 1, j - 1] + GetScore ( first[i - 1], second[j - 1] );
				if ( scoreMatrix[i, j] == matchScore ) {
					// Match or mutation
					result.Add ( AlignmentToken.MatchOrMutation ( first, i - 1, second, j - 1 ) );
					i--;
					j--;
					continue;
				}
			}

			if ( i > 0 && scoreMatrix[i, j] == scoreMatrix[i - 1, j] + GapPenalty ) {
				result.Add ( AlignmentToken.Deletion ( first, i - 1 ) );
				i--;
			} else if ( j > 0 ) {
				result.Add ( AlignmentToken.Insertion ( second, j - 1 ) );
				j--;
			} else {
				break;
			}*/
		}

		result.Reverse ();
		return result.ToArray ();
	}

	private static (AlignmentToken[], int) AlignCharacters ( string first, string second ) {
		string[] firstChars = first.Select ( c => c.ToString () ).ToArray ();
		string[] secondChars = second.Select ( c => c.ToString () ).ToArray ();

		return NeedlemanWunsch ( firstChars, secondChars );
	}
}