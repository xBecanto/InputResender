using System;
using System.Collections.Generic;
using System.Linq;

namespace MdxLibs.Services;
public class TextPattern {
	private readonly PatternNode _root;
	private readonly bool _fullMatch;

	public TextPattern ( PatternNode root, bool fullMatch = true ) {
		_root = root;
		_fullMatch = fullMatch;
	}

	public MatchResult Match ( string input ) {
		if ( input == null ) return MatchResult.Failure;

		foreach ( var state in _root.AllMatches ( input, 0, MatchState.Empty ) ) {
			if ( _fullMatch && state.EndPos != input.Length ) continue;

			return new MatchResult ( true, state.EndPos, state.Captures );
		}

		return MatchResult.Failure;
	}

	public bool IsMatch ( string input ) => Match ( input ).Success;

	// ===== Static factory methods =====

	public static PatternNode Char ( CharSet set ) => new CharNode ( set );
	public static PatternNode Ch ( char c ) => Char ( CharSet.From ( c ) );
	public static PatternNode Literal ( string text ) => new LiteralNode ( text );
	public static PatternNode Seq ( params PatternNode[] nodes ) => new SequenceNode ( nodes );
	public static PatternNode OneOf ( params PatternNode[] alternatives ) => new AlternationNode ( alternatives );
	public static PatternNode AtLeast (PatternNode inner, int num) => new QuantifierNode (inner, num, -1);
	public static PatternNode Opt ( PatternNode inner ) => new QuantifierNode ( inner, 0, 1 );
	public static PatternNode Rep ( PatternNode inner, int min, int max ) => new QuantifierNode ( inner, min, max );
	public static PatternNode Capture ( string name, PatternNode inner ) => new CaptureNode ( name, inner );
	public static PatternNode AnyChar () => Char ( CharSet.Any );
	public static PatternNode Rest () => AtLeast ( AnyChar (), 0 );

	public sealed class CharSet {
		private readonly Func<char, bool> _predicate;
		private readonly string _description;

		private CharSet ( Func<char, bool> predicate, string description ) {
			_predicate = predicate;
			_description = description;
		}

		// --- Predefined sets ---
		public static readonly CharSet Alpha = new (char.IsLetter, "Alpha");
		public static readonly CharSet Lower = new (char.IsLower, "Lower");
		public static readonly CharSet Upper = new (char.IsUpper, "Upper");
		public static readonly CharSet Digit = new (char.IsDigit, "Digit");
		public static readonly CharSet AlphaNum = new (char.IsLetterOrDigit, "AlphaNum");
		public static readonly CharSet Whitespace = new (char.IsWhiteSpace, "WS");
		public static readonly CharSet NonWhitespace = new (c => !char.IsWhiteSpace ( c ), "!WS");
		public static readonly CharSet Any = new (_ => true, ".");
		public static readonly CharSet None = new (_ => false, "∅");

		public static CharSet From ( string chars ) => new (chars.Contains, $"[{chars}]");
		public static CharSet From ( params char[] chars ) => From ( new string ( chars ) );
		public static CharSet From ( Func<char, bool> predicate, string description = "custom" )
			=> new (predicate, description);

		public bool Contains ( char c ) => _predicate ( c );

		public CharSet Union ( CharSet other )
			=> new (c => _predicate ( c ) || other._predicate ( c ), $"({_description}|{other._description})");

		public CharSet Union ( char c ) => Union ( From ( c ) );
		public CharSet Union ( string chars ) => Union ( From ( chars ) );

		public CharSet Except ( CharSet other )
			=> new (c => _predicate ( c ) && !other._predicate ( c ), $"({_description}-{other._description})");

		public CharSet Except ( char c ) => Except ( From ( c ) );
		public CharSet Not () => new (c => !_predicate ( c ), $"!{_description}");

		public override string ToString () => _description;
	}

	public readonly record struct CapturedItem ( string Name, string Value );

	internal readonly record struct MatchState ( int EndPos, IReadOnlyList<CapturedItem> Captures ) {
		internal MatchState WithCapture ( string name, string value ) {
			var list = new List<CapturedItem> ( Captures ) { new CapturedItem ( name, value ) };
			return new ( EndPos, list );
		}

		internal static readonly IReadOnlyList<CapturedItem> Empty = [];
	}

	public class MatchResult {
		public static readonly MatchResult Failure = new (false, 0, null);

		public bool Success { get; }

		public int Length { get; }

		private readonly IReadOnlyList<CapturedItem> _ordered;
		private readonly Dictionary<string, List<string>> _byName;

		internal MatchResult ( bool success, int length, IReadOnlyList<CapturedItem> captures ) {
			Success = success;
			Length = length;
			_ordered = captures ?? [];
			_byName = [];
			foreach ( var cap in _ordered ) {
				if ( !_byName.TryGetValue ( cap.Name, out var lst ) ) _byName[cap.Name] = lst = [];
				lst.Add ( cap.Value );
			}
		}

		public IReadOnlyList<CapturedItem> AllCaptures => _ordered;

		public string Get ( string path )
			=> _byName.TryGetValue ( path, out var lst ) && lst.Count > 0 ? lst[^1] : null;

		public IReadOnlyList<string> GetAll ( string path ) => _byName.TryGetValue ( path, out var lst ) ? lst : [];

		/// <summary>
		/// Returns direct child captures under <paramref name="parentPath"/>
		/// (use null/empty for root).  Each entry is the child's simple name and all its values.
		/// For example, given captures "line/cmd" and "line/desc", <c>GetChildren("line")</c>
		/// yields ("cmd", …) and ("desc", …).
		/// </summary>
		public IEnumerable<(string ChildName, IReadOnlyList<string> Values)> GetChildren ( string parentPath = null ) {
			string prefix = string.IsNullOrEmpty ( parentPath ) ? "" : parentPath + "/";
			var seen = new HashSet<string> ();
			foreach ( var key in _byName.Keys ) {
				if ( !key.StartsWith ( prefix ) ) continue;

				string rest = key[prefix.Length..];
				int slash = rest.IndexOf ( '/' );
				string childName = slash >= 0 ? rest[..slash] : rest;
				if ( string.IsNullOrEmpty ( childName ) ) continue;

				string fullChild = prefix + childName;
				if ( seen.Add ( fullChild ) ) yield return (childName, GetAll ( fullChild ));
			}
		}

		public IEnumerable<(string RelativeName, string Value)> GetDescendants ( string parentPath ) {
			string prefix = parentPath + "/";
			foreach ( var cap in _ordered )
				if ( cap.Name.StartsWith ( prefix ) )
					yield return (cap.Name[prefix.Length..], cap.Value);
		}

		public override string ToString ()
			=> Success
				? $"Match(len={Length}, captures=[{string.Join ( ", ", _ordered.Select ( c => $"{c.Name}={c.Value}" ) )}])"
				: "NoMatch";
	}

	public abstract class PatternNode {
		internal abstract IEnumerable<MatchState> AllMatches (
			string input, int pos, IReadOnlyList<CapturedItem> captures
		);

		/// <summary>Returns the first (greedy) match, or null on failure.</summary>
		internal MatchState? FirstMatch ( string input, int pos = 0 ) {
			foreach ( var s in AllMatches ( input, pos, MatchState.Empty ) ) return s;

			return null;
		}

		// --- Fluent post-fix helpers ---
		public PatternNode Optional () => TextPattern.Opt ( this );
		public PatternNode OneOrMore () => TextPattern.AtLeast ( this, 1 );
		public PatternNode ZeroOrMore () => TextPattern.AtLeast ( this, 0 );
		public PatternNode Times ( int exact ) => TextPattern.Rep ( this, exact, exact );
		public PatternNode AtLeast ( int min ) => TextPattern.Rep ( this, min, -1 );
		public PatternNode Between ( int min, int max ) => TextPattern.Rep ( this, min, max );
		public PatternNode Capture ( string name ) => TextPattern.Capture ( name, this );
	}

// ===== Internal node implementations =====

	internal sealed class CharNode : PatternNode {
		private readonly CharSet _set;
		internal CharNode ( CharSet set ) => _set = set;

		internal override IEnumerable<MatchState> AllMatches (
			string input, int pos, IReadOnlyList<CapturedItem> captures
		) {
			if ( pos < input.Length && _set.Contains ( input[pos] ) ) yield return new MatchState ( pos + 1, captures );
		}

		public override string ToString () => $"Char({_set})";
	}

	internal sealed class LiteralNode : PatternNode {
		private readonly string _text;
		internal LiteralNode ( string text ) => _text = text;

		internal override IEnumerable<MatchState> AllMatches (
			string input, int pos, IReadOnlyList<CapturedItem> captures
		) {
			if ( pos + _text.Length > input.Length ) yield break;

			for ( int i = 0; i < _text.Length; i++ )
				if ( input[pos + i] != _text[i] )
					yield break;

			yield return new MatchState ( pos + _text.Length, captures );
		}

		public override string ToString () => $"Literal(\"{_text}\")";
	}

	internal sealed class SequenceNode : PatternNode {
		private readonly PatternNode[] _nodes;
		internal SequenceNode ( PatternNode[] nodes ) => _nodes = nodes;

		internal override IEnumerable<MatchState> AllMatches (
			string input, int pos, IReadOnlyList<CapturedItem> captures
		)
			=> MatchFrom ( input, 0, pos, captures );

		private IEnumerable<MatchState> MatchFrom (
			string input, int idx, int pos, IReadOnlyList<CapturedItem> captures
		) {
			if ( idx >= _nodes.Length ) {
				yield return new MatchState ( pos, captures );

				yield break;
			}

			foreach ( var s in _nodes[idx].AllMatches ( input, pos, captures ) )
			foreach ( var final in MatchFrom ( input, idx + 1, s.EndPos, s.Captures ) )
				yield return final;
		}

		public override string ToString () => $"Seq({string.Join ( ", ", _nodes.AsEnumerable () )})";
	}

	internal sealed class AlternationNode : PatternNode {
		private readonly PatternNode[] _alternatives;
		internal AlternationNode ( PatternNode[] alternatives ) => _alternatives = alternatives;

		internal override IEnumerable<MatchState> AllMatches (
			string input, int pos, IReadOnlyList<CapturedItem> captures
		) {
			foreach ( var alt in _alternatives )
			foreach ( var s in alt.AllMatches ( input, pos, captures ) )
				yield return s;
		}

		public override string ToString () => $"OneOf({string.Join ( " | ", _alternatives.AsEnumerable () )})";
	}

	internal sealed class QuantifierNode : PatternNode {
		private readonly PatternNode _inner;
		private readonly int _min, _max; // _max == -1 means unlimited

		internal QuantifierNode ( PatternNode inner, int min, int max ) {
			_inner = inner;
			_min = min;
			_max = max;
		}

		internal override IEnumerable<MatchState> AllMatches (
			string input, int pos, IReadOnlyList<CapturedItem> captures
		)
			=> MatchGreedy ( input, pos, captures, 0 );

		private IEnumerable<MatchState> MatchGreedy (
			string input, int pos, IReadOnlyList<CapturedItem> captures, int count
		) {
			bool canContinue = _max < 0 || count < _max;

			if ( canContinue ) {
				foreach ( var s in _inner.AllMatches ( input, pos, captures ) ) {
					if ( s.EndPos == pos ) continue; // skip zero-length inner matches to avoid infinite loops

					foreach ( var deeper in MatchGreedy ( input, s.EndPos, s.Captures, count + 1 ) )
						yield return deeper;
				}
			}

			// Yield the "stop here" option after longer matches (greedy ordering)
			if ( count >= _min ) yield return new MatchState ( pos, captures );
		}

		public override string ToString () {
			string q = (_min, _max) switch {
				(0, 1)    => "?"
				, (1, -1) => "+"
				, (0, -1) => "*"
				, _       => $"{{{_min},{(_max < 0 ? "∞" : _max.ToString ())}}}"
			};
			return $"({_inner}){q}";
		}
	}

	internal sealed class CaptureNode : PatternNode {
		private readonly string _name;
		private readonly PatternNode _inner;

		internal CaptureNode ( string name, PatternNode inner ) {
			_name = name;
			_inner = inner;
		}

		internal override IEnumerable<MatchState> AllMatches (
			string input, int pos, IReadOnlyList<CapturedItem> captures
		) {
			foreach ( var s in _inner.AllMatches ( input, pos, captures ) ) {
				// Append this capture after any captures the inner node recorded
				string value = input[pos..s.EndPos];
				yield return s.WithCapture ( _name, value );
			}
		}

		public override string ToString () => $"Capture(\"{_name}\", {_inner})";
	}
}