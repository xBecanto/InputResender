using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace MdxLibs.Services {
	/// <summary>Offers combination of List and Dictionary functionalities.</summary>
	public class ArDict<Key, Val> : IDictionary<Key, Val>, IEnumerable<Val> {
		private List<Key> keys;
		private List<Val> vals;

		public ArDict () {
			keys = new List<Key> ();
			vals = new List<Val> ();
		}

		public Val this[Key key] { get => vals[FindIndex ( key )]; set => vals[FindIndex ( key )] = value; }
		public Val this[int index] { get => vals[index]; set => vals[index] = value; }

		public Key GetKey ( int index ) => keys[index];
		public int FindIndex ( Val val ) => vals.IndexOf ( val );
		public int FindIndex ( Key key ) => keys.IndexOf ( key );

		public ICollection<Key> Keys => keys;
		public ICollection<Val> Values => vals;

		public int Count => keys.Count;
		public bool IsReadOnly => false;

		public void Add ( Key key, Val value ) { keys.Add ( key ); vals.Add ( value ); }
		public void Add ( KeyValuePair<Key, Val> item ) => Add ( item.Key, item.Value );
		public void Clear () { keys.Clear (); vals.Clear (); }
		public bool Contains ( KeyValuePair<Key, Val> item ) { int keyID = keys.IndexOf ( item.Key ); return keyID < 0 ? false : vals[keyID].Equals ( item.Value ); }
		public bool ContainsKey ( Key key ) => keys.Contains ( key );
		public void CopyTo ( KeyValuePair<Key, Val>[] array, int arrayIndex ) {
			int N = keys.Count;
			for ( int i = 0; i < N; i++ ) array[i + arrayIndex] = new KeyValuePair<Key, Val> ( keys[i], vals[i] );
		}
		public IEnumerator<KeyValuePair<Key, Val>> GetEnumerator () {
			for ( int i = 0; i < keys.Count; i++ ) yield return new KeyValuePair<Key, Val> ( keys[i], vals[i] );
		}
		public bool Remove ( Key key ) {
			int keyID = keys.IndexOf ( key );
			if ( keyID < 0 ) return false;
			vals.RemoveAt ( keyID );
			keys.RemoveAt ( keyID );
			return true;
		}
		public bool Remove ( KeyValuePair<Key, Val> item ) {
			int keyID = keys.IndexOf ( item.Key );
			int valID = vals.IndexOf ( item.Value );
			if ( keyID < 0 ) return false;
			if ( keyID != valID ) return false;
			vals.RemoveAt ( keyID );
			keys.RemoveAt ( keyID );
			return true;
		}
		public bool TryGetValue ( Key key, [MaybeNullWhen ( false )] out Val value ) {
			int keyID = keys.IndexOf ( key );
			value = keyID < 0 ? default : vals[keyID];
			return keyID >= 0;
		}
		IEnumerator IEnumerable.GetEnumerator () => GetEnumerator ();
		IEnumerator<Val> IEnumerable<Val>.GetEnumerator () => vals.GetEnumerator ();
	}
}