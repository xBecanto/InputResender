namespace Components.Library;
public struct Part {
	public readonly int Start, End;
	public int Length => End - Start;
	public bool IsEmpty => Start == End;
	public bool Valid => Start >= 0 && End >= 0 && Start <= End;

	public Part ( int start, int end ) {
		Start = start;
		End = end;
	}

	public Range Before => new ( 0, Start );
	public Range Inside => new ( Start, End );
	public Range After => new ( End, int.MaxValue );

	public Range Between ( Part other ) {
		if ( End < other.Start ) return new (End, other.Start);
		if ( other.End < Start ) return new (other.End, Start);

		throw new ArgumentException ( "Parts overlap" );
	}

	public override string ToString () => $"Part({Start}|{End})";


	public static implicit operator Part ( (int Start, int End) tuple ) => new ( tuple.Start, tuple.End );
	public static implicit operator Range ( Part part ) => new ( part.Start, part.End );
	public static implicit operator Part ( Range range ) => new ( range.Start.Value, range.End.Value );
	public static Part Empty => new ( -1, -1 );
	///<summary>Returns the range in between two disjoint parts.</summary>
	public static Range operator >> ( Part a, Part b ) => a.Between(b);
	public static bool operator == ( Part a, Part b ) => a.Start == b.Start && a.End == b.End;
	public static bool operator != ( Part a, Part b ) => !(a == b);
	///<summary>∃ x ∈ a, y ∈ b : x ≥ y</summary>
	public static bool operator >= ( Part a, Part b ) => a.End > b.Start;
	///<summary>∃ x ∈ a, y ∈ b : x ≤ y</summary>
	public static bool operator <= ( Part a, Part b ) => a.Start < b.End;
	///<summary>∀ x ∈ a, y ∈ b : x &gt; y</summary>
	public static bool operator > ( Part a, Part b ) => a.Start > b.End;
	///<summary>∀ x ∈ a, y ∈ b : x &lt; y</summary>
	public static bool operator < ( Part a, Part b ) => a.End < b.Start;
}