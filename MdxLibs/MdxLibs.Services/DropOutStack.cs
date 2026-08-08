using System;
using System.Collections;
using System.Collections.Generic;

namespace MdxLibs.Services {
	public class DropOutStack<T> : IEnumerable<T> {
		readonly int N;
		T[] Data;
		int Size, Cnt;

		public DropOutStack(int maxSize) { N = maxSize; Data = new T[N]; Size = Cnt = 0; }

		public int Count => Cnt;

		public void Push ( T item ) { Data[Size] = item; Size = (Size + 1) % N; Cnt = int.Min ( Cnt + 1, N ); }
		public T Pop () { if ( Cnt < 1 ) throw new IndexOutOfRangeException (); Cnt--; Size = (Size - 1) % N; return Data[Size]; }
		public void Clear () { Size = 0; }
		public T SafePop () { if ( Cnt < 1 ) return default; Size = (Size - 1) % N; return Data[Size]; }

		public IEnumerator<T> GetEnumerator () {
			for ( int i = 0; i < Cnt; i++ )
				yield return Data[(Size - i)%N];
		}
		IEnumerator IEnumerable.GetEnumerator () => GetEnumerator ();
	}
}
