using System.Collections.Generic;

namespace MdxLibs.Services {
	public struct DictionaryKey {
		int Key;

		public DictionaryKey(int key) { Key = key; }
		public static DictionaryKey Empty { get => new DictionaryKey ( 0 ); }
		public bool Valid => Key != 0;

		public override bool Equals ( object obj ) {
			if ( obj is DictionaryKey )
				return Key == ((DictionaryKey)obj).Key;
			return false;
		}
		public override int GetHashCode () => Key;
		public override string ToString () => $"({Key.ToShortCode ()})";
		public static DictionaryKey Parse ( string shortCode ) => new (shortCode.ParseShortCode<int> ());
		public static bool operator == ( DictionaryKey A, DictionaryKey B ) => A.Key == B.Key;
		public static bool operator != ( DictionaryKey A, DictionaryKey B ) => A.Key != B.Key;
		public static explicit operator DictionaryKey ( int key ) { return new DictionaryKey ( key ); }
		public static explicit operator DictionaryKey ( nint key ) { return new DictionaryKey ( (int)key ); }

		// Maybe add method (for 'security') to bind the key with a collection
	}
	public class DictionaryKeyFactory {
		int LastKey = -1234;
		HashSet<int> UsedKeys;

		public DictionaryKeyFactory () {
			UsedKeys = new HashSet<int> () { 0 };
		}

		public DictionaryKey NewKey () {
			while ( UsedKeys.Contains ( LastKey ) ) LastKey = LastKey * 75 + 74;
			UsedKeys.Add ( LastKey );
			return new DictionaryKey ( LastKey );
		}
	}
}