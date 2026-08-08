using System;
using System.Collections;
using System.Collections.Generic;

namespace MdxLibs.Services {
	public class SetRelation<Tkey, Tval, Ukey, Uval> : ICollection<(Tkey A, Ukey B)> {
		private readonly Dictionary<Tkey, Tval> SetA;
		private readonly Dictionary<Ukey, Uval> SetB;
		private readonly HashSet<(Tkey A, Ukey B)> Pairs;
		private Func<Tkey, Tval> ConstructorA;
		private Func<Ukey, Uval> ConstructorB;
		private Action<Tkey, Tval> DestructorA;
		private Action<Ukey, Uval> DestructorB;

		public SetRelation ( Func<Tkey, Tval> constructorA, Func<Ukey, Uval> constructorB, Action<Tkey, Tval> destructorA, Action<Ukey, Uval> destructorB ) {
			SetA = new Dictionary<Tkey, Tval> ();
			SetB = new Dictionary<Ukey, Uval> ();
			Pairs = new HashSet<(Tkey A, Ukey B)> ();
			if ( constructorA == null ) throw new ArgumentNullException ( $"{nameof ( constructorA )} cannot be null!" );
			ConstructorA = constructorA;
			if ( constructorB == null ) throw new ArgumentNullException ( $"{nameof ( constructorB )} cannot be null!" );
			ConstructorB = constructorB;
			if ( destructorA == null ) throw new ArgumentNullException ( $"{nameof ( destructorA )} cannot be null!" );
			DestructorA = destructorA;
			if ( destructorB == null ) throw new ArgumentNullException ( $"{nameof ( destructorB )} cannot be null!" );
			DestructorB = destructorB;
		}

		public int Count => Pairs.Count;
		public bool IsReadOnly => false;
		public IReadOnlyCollection<Tkey> SetAKeys { get => SetA.Keys; }
		public IReadOnlyCollection<Tval> SetAValues { get => SetA.Values; }
		public IReadOnlyCollection<Ukey> SetBKeys { get => SetB.Keys; }
		public IReadOnlyCollection<Uval> SetBValues { get => SetB.Values; }

		public void Add ( (Tkey A, Ukey B) item ) {
			if ( !SetA.ContainsKey ( item.A ) ) SetA.Add ( item.A, ConstructorA ( item.A ) );
			if ( !SetB.ContainsKey ( item.B ) ) SetB.Add ( item.B, ConstructorB ( item.B ) );
			Pairs.Add ( item );
		}
		public void Clear () {
			foreach ( var item in SetA ) DestructorA ( item.Key, item.Value );
			foreach ( var item in SetB ) DestructorB ( item.Key, item.Value );
			SetA.Clear ();
			SetB.Clear ();
			Pairs.Clear ();
		}
		public bool Contains ( (Tkey A, Ukey B) item ) => Pairs.Contains ( item );
		public void CopyTo ( (Tkey A, Ukey B)[] array, int arrayIndex ) => Pairs.CopyTo ( array, arrayIndex );
		public IEnumerator<(Tkey A, Ukey B)> GetEnumerator () => Pairs.GetEnumerator ();
		public void ForEach (Action<Tkey, Tval, Ukey, Uval> act) {
			foreach (var pair in Pairs) act ( pair.A, SetA[pair.A], pair.B, SetB[pair.B] );
		}
		public bool Remove ( (Tkey A, Ukey B) item ) {
			bool ret = Pairs.Remove ( item );
			if ( ret ) {
				bool hasA = false;
				bool hasB = false;
				foreach ( var pair in Pairs ) {
					if ( pair.A.Equals ( item.A ) ) hasA = true;
					if ( pair.B.Equals ( item.B ) ) hasB = true;
					if ( hasA & hasB ) break;
				}
				if ( !hasA ) { DestructorA ( item.A, SetA[item.A] ); SetA.Remove ( item.A ); }
				if ( !hasB ) { DestructorB ( item.B, SetB[item.B] ); SetB.Remove ( item.B ); }
			}
			return ret;
		}
		IEnumerator IEnumerable.GetEnumerator () => GetEnumerator ();
		public override string ToString () => ToString ( " | " );
		public string ToString ( string sep ) => string.Join ( sep, PairsToString () );
		public string[] PairsToString () {
			string[] ret = new string[Pairs.Count];
			int ID = 0;
			foreach ( var pair in Pairs )
				ret[ID++] = $"[{pair.A}] ({SetA[pair.A]}) <=> [{pair.B}] ({SetB[pair.B]})";
			return ret;
		}
	}
}