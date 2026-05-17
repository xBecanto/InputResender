using System.Net;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using SBld = System.Text.StringBuilder;

namespace Components.Library;
public static class MdxExtensions {
	public const int K = 1000;
	public const int M = K * K;
	public const int G = M * K;
	public static string ToShortString ( this int val ) {
		switch ( val ) {
		case 0: return "0";
		case 1: return "1";
		case int.MaxValue: return "2^32";
		case int.MinValue: return "-2^32";
		default:
			string sgn = val < 0 ? "-" : "";
			val = Math.Abs ( val );
			if ( val > G ) return $"{sgn}{val / G}G";
			if ( val > M ) return $"{sgn}{val / M}M";
			if ( val > K ) return $"{sgn}{val / K}K";
			return val.ToString ();
		}
	}
	public static int CalcSetHash<T> ( this ICollection<T> Set ) {
		int N = Set.Count;
		int hash = N.GetHashCode ();
		foreach ( T t in Set ) hash ^= t.GetHashCode ();
		return hash;
	}
	public static string AsString<T> ( this ICollection<T> Set, string sep = ", " ) {
		if ( Set == null ) return null;
		bool empty = true;
		SBld SB = new SBld ();
		foreach ( T t in Set ) {
			if ( !empty ) SB.Append ( sep );
			SB.Append ( t.ToString () );
		}
		return SB.ToString ();
	}
	public static string ToHex ( this byte[] data ) => data == null ? "NULL" : BitConverter.ToString ( data ).Replace ( "-", null );
	public static string ToHex ( this ReadOnlySpan<byte> data ) => data.IsEmpty ? "NULL" : BitConverter.ToString ( data.ToArray () ).Replace ( "-", null );
	public static IntPtr ToUnmanaged ( this IntPtr value ) {
		var ptr = Marshal.AllocHGlobal ( IntPtr.Size );
		var bAr = BitConverter.GetBytes ( value );
		Marshal.Copy ( bAr, 0, ptr, bAr.Length );
		return ptr;
	}

	public static void FreeUnmanaged ( this IntPtr value ) => Marshal.FreeHGlobal ( value );

	public static void ExecInUnmanaged ( this IntPtr value, System.Action<IntPtr> action ) {
		IntPtr ptr = value.ToUnmanaged ();
		action?.Invoke ( ptr );
		ptr.FreeUnmanaged ();
	}
	public static bool IsModifier ( this KeyCode key ) => (int)(key & KeyCode.Modifiers) > 1;
	public static bool TryGetValue<T, U> ( this Dictionary<T, U> dict, Func<T, bool> predicate, out U val ) {
		foreach ( var item in dict ) {
			if ( !predicate ( item.Key ) ) continue;
			val = item.Value;
			return true;
		}
		val = default;
		return false;
	}
	public static T[] Merge<T> ( this T[] origAr, params T[][] otherArs ) {
		if ( origAr == null ) return null;
		if ( otherArs == null ) return (T[])origAr.Clone ();
		int totSize = origAr.Length;
		int N = otherArs.Length;
		for ( int i = 0; i < N; i++ ) totSize += otherArs[i] == null ? 0 : otherArs[i].Length;
		T[] ret = new T[totSize];
		int pos = 0;
		for ( int i = 0; i < origAr.Length; i++ ) ret[pos++] = origAr[i];
		for ( int a = 0; a < N; a++ ) {
			if ( otherArs[a] == null ) continue;
			int S = otherArs[a].Length;
			for ( int i = 0; i < S; i++ ) ret[pos++] = otherArs[a][i];
		}
		return ret;
	}
	public static T[] SubArray<T> ( this T[] origAr, int start, int size ) {
		if ( origAr == null ) return null;
		int N = origAr.Length;
		if ( start < 0 ) start = N + start;
		if ( N < start + size ) return Array.Empty<T> ();

		T[] ret = new T[size];
		Array.Copy ( origAr, start, ret, 0, size );
		return ret;
	}

	public static bool Contains<T> ( this T[] ar, T val ) {
		if ( ar == null ) return false;
		foreach ( T t in ar ) {
			if ( t.Equals ( val ) ) return true;
		}
		return false;
	}

	public static long CalcHash ( this ReadOnlySpan<byte> data, int start = 0, int size = -1 ) {
		if ( data.IsEmpty ) return -1;
		if ( size < 0 ) size = data.Length - start;
		long ret = 0;
		for ( int i = start; i < start + size; i++ ) ret += data[i] ^ i.CalcHash ();
		return ret;
	}
	public static long CalcHash ( this byte[] data, int start = 0, int size = -1 ) => data == null ? -1 : CalcHash ( (ReadOnlySpan<byte>)data, start, size );

	public static int CalcHash ( this int num ) {
		uint seed = 0x57981A3D;
		long ret = seed;
		for ( int i = 0; i < 32; i++ ) {
			seed = NextRng ();
			ret += (num & 1) > 0 ? seed : -seed;
			num >>= 1;
		}
		return (int)ret;

		uint NextRng () { return (uint)(seed * 22695477ul + 1); }
	}

	public static bool ContainsPair<T, U> ( this IDictionary<T, U> dict, T key, U val ) {
		if ( dict.TryGetValue ( key, out U ret ) ) return ret.Equals ( val );
		return false;
	}
	public static IReadOnlyCollection<IReadOnlyCollection<T>> AsReadonly2D<T> ( this T[][] ar ) {
		int N = ar.Length;
		IReadOnlyCollection<T>[] ret = new IReadOnlyCollection<T>[N];
		for ( int i = 0; i < N; i++ ) ret[i] = Array.AsReadOnly ( ar[i] );
		return Array.AsReadOnly ( ret );
	}
	public static IReadOnlyList<IReadOnlyList<T>> AsReadonly2D<T> ( this T[] ar ) => new IReadOnlyList<T>[1] { Array.AsReadOnly ( ar ) };

	public static T[][] ToArray2D<T> ( this List<List<T>> data ) {
		int N = data.Count;
		T[][] ret = new T[N][];
		for ( int i = 0; i < N; i++ ) ret[i] = data[i].ToArray ();
		return ret;
	}
	public static string ToShortCode ( this int num ) => $"{(num < 0 ? "-" : "")}{((ulong)(num < 0 ? -num : num)).ToShortCode ()}";
	public static string ToShortCode ( this long num ) => $"{(num < 0 ? "-" : "")}{((ulong)(num < 0 ? -num : num)).ToShortCode ()}";

	private const string ShortCodeChAr ="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+*@#$%&";
	private static readonly ulong ShortCodeN = (ulong)ShortCodeChAr.Length;
	public static string ToShortCode ( this ulong num ) {
		const string chAr = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+*@#$%&";
		string ret = "";
		while ( num > 0 ) {
			ret += ShortCodeChAr[(int)(num % ShortCodeN)];
			num /= ShortCodeN;
		}

		return ret == "" ? new ([ShortCodeChAr[0]]) : ret;
	}

	public static ulong ParseShortCode ( this string shortCode ) {
		if ( string.IsNullOrEmpty ( shortCode ) )
			throw new ArgumentException ( "ShortCode cannot be null or empty", nameof ( shortCode ) );

		ulong result = 0;
		ulong multiplier = 1;

		foreach ( char c in shortCode ) {
			int index = ShortCodeChAr.IndexOf ( c );
			if ( index < 0 ) throw new ArgumentException ( $"Invalid character '{c}' in shortCode", nameof(shortCode) );

			result += (ulong)index * multiplier;
			multiplier *= ShortCodeN;
		}

		return result;
	}

	public static T ParseShortCode<T> ( this string shortCode ) where T : ISignedNumber<T>, IConvertible, IMinMaxValue<T> {
		ArgumentException.ThrowIfNullOrWhiteSpace ( nameof ( shortCode ) );

		shortCode = shortCode.Trim ();
		bool negative = shortCode.StartsWith ( '-' );
		if ( negative ) shortCode = shortCode[1..];

		ulong result = shortCode.ParseShortCode ().AssertInRange ( 0UL, T.MaxValue.ToUInt64 ( null ) );
		return negative ? T.CreateChecked ( -(long)result ) : T.CreateChecked ( result );
	}

	public static T AssertInRange<T> ( this T x, T min, T max ) where T : IComparable<T> {
		var cmpMin = x.CompareTo ( min );
		var cmpMax = x.CompareTo ( max );
		if ( cmpMin < 0 || cmpMax > 0 ) throw new ArgumentOutOfRangeException ( $"Value {x} is out of range [{min}, {max}]." );

		return x;
	}

	public static int Crop ( this int x, int min, int max ) => x < min ? min : x > max ? max : x;
	public static string AsString ( this MethodInfo MI ) {
		SBld ret = new SBld ();
		if ( MI.IsPublic ) ret.Append ( "public " );
		else if ( MI.IsPrivate ) ret.Append ( "private " );
		else if ( MI.IsAssembly ) ret.Append ( "internal " );
		else if ( MI.IsFamily ) ret.Append ( "protected " );
		else ret.Append ( "local " );
		if ( MI.IsStatic ) ret.Append ( "static " );
		if ( MI.IsAbstract ) ret.Append ( "abstract " );
		if ( MI.IsVirtual ) ret.Append ( "virtual " );

		ret.Append ( MI.DeclaringType.Name );
		ret.Append ( '.' );
		ret.Append ( MI.Name );
		ret.Append ( '(' );
		ret.Append ( string.Join ( ", ", MI.GetParameters ().Select ( ( p ) => $"{p.ParameterType.Name} {p.Name}" ) ) );
		ret.Append ( ')' );
		return ret.ToString ();
	}
	public static IPAddress GetNetworkAddr ( this IPAddress IP, int prefix = -1 ) {
		if ( IP == null ) return null;
		if ( prefix < 0 ) prefix = IP.AddressFamily switch {
			System.Net.Sockets.AddressFamily.InterNetwork => 24,
			System.Net.Sockets.AddressFamily.InterNetworkV6 => 48,
			_ => 0,
		};
		byte[] bAr = IP.GetAddressBytes ();
		for ( int i = 0; i < bAr.Length; i++ ) {
			bAr[i] &= prefix switch {
				0 => 0,
				1 => 0x80,
				2 => 0xC0,
				3 => 0xE0,
				4 => 0xF0,
				5 => 0xF8,
				6 => 0xFC,
				7 => 0xFE,
				_ => 0xff,
			};
			prefix -= 8;
		}
		return new IPAddress ( bAr );
	}

	public static string PrefixAllLines ( this string text, string prefix, string lineSep = null ) {
		if ( string.IsNullOrEmpty ( text ) ) return text;
		if ( string.IsNullOrEmpty ( prefix ) ) return text;
		if ( lineSep == null ) {
			lineSep = Environment.NewLine;
			text = text.ReplaceLineEndings ();
		}
		var ret = prefix + text.Replace ( lineSep, lineSep + prefix );
		// This should deal with situations like N*(text \n) so that last line is just empty
		// It will keep the last separator since it might be wanted, but the extra prefix would need to be added manually
		if ( ret.EndsWith ( prefix ) ) ret = ret.Substring ( 0, ret.Length - prefix.Length );
		return ret;
	}

	public static Type FindType ( string name ) {
		if ( string.IsNullOrEmpty ( name ) ) return null;
		var types = AppDomain.CurrentDomain.GetAssemblies ().SelectMany ( a => a.GetTypes () );
		if ( types == null ) return null;
		if ( types.Any ( t => t.Name == name ) ) return types.First ( t => t.Name == name );
		if ( types.Any ( t => t.FullName == name ) ) return types.First ( t => t.FullName == name );
		return null;
	}

	public static Dictionary<T, List<U>> FlipAndUnion<T, U> ( this Dictionary<U, T> dict ) {
		if ( dict == null ) return null;
		Dictionary<T, List<U>> ret = [];
		foreach ( var item in dict ) {
			if ( ret.TryGetValue ( item.Value, out List<U> lst ) ) {
				lst.Add ( item.Key );
			} else {
				lst = [item.Key];
				ret.Add ( item.Value, lst );
			}
		}
		return ret;
	}

	/// <summary>Normal string.StartsWith but returns the rest of the string on match, full string otherwise.</summary>
	public static bool StartsWith ( this string line, string prefix, out string rest, StringComparison compareOptions = StringComparison.OrdinalIgnoreCase ) {
		if ( string.IsNullOrEmpty ( line ) || string.IsNullOrEmpty ( prefix ) ) {
			rest = line;
			return false;
		}
		if ( line.StartsWith ( prefix, compareOptions ) ) {
			rest = line.Substring ( prefix.Length );
			return true;
		}
		rest = line;
		return false;
	}

	public static string SubstringBetween ( this string line, string prefix, string suffix, StringComparison compareOptions = StringComparison.OrdinalIgnoreCase ) {
		if ( string.IsNullOrEmpty ( line ) || string.IsNullOrEmpty ( prefix ) || string.IsNullOrEmpty ( suffix ) ) return line;
		if ( !line.StartsWith ( prefix, compareOptions ) ) return null;
		line = line[prefix.Length..];
		int suffixPos = line.IndexOf ( suffix, compareOptions );
		if ( suffixPos < 0 ) return null;
		return line[0..suffixPos];
	}

	public static int IndexOf<T> ( this IReadOnlyList<T> list, Func<T, bool> predicate ) {
		if ( list == null || predicate == null ) return -1;
		int N = list.Count;
		for ( int i = 0; i < N; i++ ) {
			if ( predicate ( list[i] ) ) return i;
		}
		return -1;
	}

	public static ICollection<(T, U)> Unwrap<T, U> ( this IDictionary<T, List<U>> dict ) {
		if ( dict == null ) return null;
		HashSet<(T, U)> ret = [];
		foreach ( var item in dict ) {
			if ( item.Value == null ) continue;
			foreach ( var v in item.Value ) {
				ret.Add ( (item.Key, v) );
			}
		}
		return ret;
	}


	private class LineGroup {
		public List<string> Lines;
		public int LineCount => Lines == null ? 0 : Lines.Count;
		public LineGroup ( List<string> lines ) { Lines = lines; }
		public List<string> Unpack () => Lines;
		public override string ToString () => $"LG({LineCount} : {(LineCount > 0 ? Lines[0] : "EMPTY")}";
	}
	public static List<List<string>>[] SplitToColumns ( this List<List<string>> lineGroups, int cols, int sepLines = 1 ) {
		if ( lineGroups == null ) return null;
		var ret = new List<List<string>>[cols];
		if ( cols <= 1 ) {
			ret[0] = lineGroups;
			return ret;
		}

		var groups = new List<LineGroup>[cols];
		for ( int i = 0; i < cols; i++ ) groups[i] = [];
		for ( int i = 0; i < lineGroups.Count; i++ ) {
			if ( lineGroups[i] == null ) throw new ArgumentException ( $"lineGroups[{i}] is null." );
			groups[0].Add ( new ( lineGroups[i] ) );
		}

		SplitColumn ( groups, 0, int.MaxValue, sepLines );
		foreach ( var group in groups ) {
			if ( group == null ) continue;
			ret[Array.IndexOf ( groups, group )] = group.Select ( g => g.Unpack () ).ToList ();
		}
		return ret;
	}
	private static void SplitColumn (List<LineGroup>[] cols, int startID, int maxLength, int sepLines = 1 ) {
		if ( cols == null || cols.Length < 2 || startID < 0 || startID + 1 >= cols.Length ) return;
		int N = cols[0].Count;
		for (int i = 0; i < N; i++ ) {
			// Pick out free element
			LineGroup g = cols[startID].PopLineGroup ();

			int firstSize = cols[startID].Sum ( sepLines );
			cols[startID + 1].Insert ( 0, g );
			SplitColumn ( cols, startID + 1, firstSize, sepLines );
			
			int nextSize = cols[startID + 1].Sum ( sepLines );
			if ( nextSize <= firstSize ) continue;
			// Revert
			g = cols[startID + 1].PickLineGroup ();
			cols[startID].Add ( g );
			break;
		}
	}
	private static LineGroup PopLineGroup ( this List<LineGroup> groups) {
		LineGroup g = groups[^1];
		groups.RemoveAt ( groups.Count - 1 );
		return g;
	}
	private static LineGroup PickLineGroup ( this List<LineGroup> groups ) {
		LineGroup g = groups[0];
		groups.RemoveAt ( 0 );
		return g;
	}
	private static int Sum (this List<LineGroup> groups, int sepLines = 1 ) {
		int ret = 0;
		foreach ( var g in groups ) ret += g.LineCount;
		if ( groups.Count > 0 ) ret += sepLines * (groups.Count - 1);
		return ret;
	}

	public static List<string> BuildColumBlocks ( this List<List<string>>[] lineGroups, string colSep = " | ", string blockSep = "", bool padLeft = false ) {
		// Don't include final linebreak in the block separator
		if ( lineGroups == null ) return null;
		int N = lineGroups.Length;
		for ( ; N > 0; N-- ) {
			if ( lineGroups[N - 1] != null && lineGroups[N - 1].Count > 0 ) break;
		}
		int[] colLengths = new int[N];
		List<string>[] Cols = new List<string>[N];

		// Calculate max width of each column and merge groups in single column 
		for ( int i = 0; i < N; i++ ) {
			Cols[i] = [];
			int maxLen = 0;
			foreach ( var lineGroup in lineGroups[i] ) {
				bool usseColSep = colSep != null && Cols[i].Count > 0;
				foreach ( var line in lineGroup ) {
					int len = line?.Length ?? 0;
					if ( len > maxLen ) maxLen = line.Length;
					Cols[i].Add ( line );
				}
				bool useSep = lineGroup != lineGroups[i][^1] && blockSep != null;
				if ( useSep ) Cols[i].AddRange ( blockSep.Split ( '\n' ) );
			}
			colLengths[i] = maxLen;
		}

		int maxLines = Enumerable.Max ( Cols.Select ( c => c.Count ) );
		for ( int i = 0; i < N; i++ ) {
			while ( Cols[i].Count < maxLines ) Cols[i].Add ( "" );
		}

		// Add right padding to each column except the last one, add separator and merge into single lines
		System.Text.StringBuilder SB = new ();
		List<string> ret = [];
		for ( int i = 0; i < maxLines; i++ ) {
			//ret.Add ( string.Join ( colSep, Cols.Select ( ( cols, i ) => cols[i].PadRight ( colLengths[i] ) ) ) );
			SB.Clear ();
			for ( int c = 0; c < N; c++ ) {
				if ( c > 0 ) SB.Append ( colSep );
				string line = Cols[c][i];
				if ( c < N - 1 ) {
					if ( padLeft ) line = line.PadLeft ( colLengths[c] );
					else line = line.PadRight ( colLengths[c] );
				}
				SB.Append ( line );
			}
			ret.Add ( SB.ToString () );
		}

		return ret;
	}

	public static (T, T) ToTuple<T> ( this T[] ar, int i0, int i1, bool canReturnDefault = false )
		=> (ar.GetElemnt ( i0, canReturnDefault ), ar.GetElemnt ( i1, canReturnDefault ) );

	private static T GetElemnt<T> ( this T[] ar, int id, bool canReturnDefault = false )
		=> (id < 0 || id >= ar.Length)
			? (canReturnDefault
				? default
				: throw new ArgumentOutOfRangeException ( $"Index {id} is out of range for array of length {ar.Length}." ) )
			: ar[id];
}