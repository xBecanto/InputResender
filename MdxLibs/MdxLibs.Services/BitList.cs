using System;
using System.Collections;
using System.Collections.Generic;
using Num = System.UInt64;

namespace MdxLibs.Services {
	public class BitList : IList<bool> {
		const int Width = sizeof ( Num ) * 8;
		const Num LastMask = 1UL << (Width - 1);
		Num[] Data = new Num[0];
		int Size = 0, N = 0;

		public bool this[int ID] {
			get {
				if ( (ID >= Size) | (ID < 0) ) throw new IndexOutOfRangeException ();
				var pos = Math.DivRem ( ID, Width );
				return (Data[pos.Quotient] & (1UL << pos.Remainder)) > 0;
			}
			set {
				if ( (ID >= Size) | (ID < 0) ) throw new IndexOutOfRangeException ();
				var pos = Math.DivRem ( ID, Width );
				if ( value ) Data[pos.Quotient] |= 1UL << pos.Remainder;
				else Data[pos.Quotient] &= ~(1UL << pos.Remainder);
			}
		}

		public int Count => Size;
		public bool IsReadOnly => false;

		public void Add ( bool item ) {
			Size++;
			int pos;
			if ( Size % Width == 1 ) {
				Resize ( N + 1 );
				pos = 0;
			} else pos = Size % Width - 1;
			if ( item ) Data[N - 1] |= 1UL << pos;
			else Data[N - 1] &= ~(1UL << pos);
		}
		public void Clear () { Data = new Num[0]; Size = N = 0; }
		public bool Contains ( bool item ) {
			for (int i = 0; i < N; i++ ) {
				if ( item & (Data[i] > 0) ) return true;
				else if ( !item & (Data[i] < Num.MaxValue) ) return true;
			}
			return false;
		}
		public void CopyTo ( bool[] array, int arrayIndex ) {
			int ID = arrayIndex;
			for (int i = 0; i < N; i++ ) {
				Num data = Data[i];
				for (int j = 0; j < Width; j++ ) {
					array[ID++] = (data & 1) > 0;
					data >>= 1;
				}
			}
		}
		public IEnumerator<bool> GetEnumerator () {
			for ( int i = 0; i < Size; i++ ) yield return this[i];
		}
		public int IndexOf ( bool item ) {
			for ( int i = 0; i < N; i++ ) {
				Num data = Data[i];
				for ( int j = 0; j < Width; j++ ) {
					Num v = data & 1UL;
					if ( item & (v > 0) ) return i * Width + j;
					else if ( !item & (v == 0) ) return i * Width + j;
					data >>= 1;
				}
			}
			return -1;
		}
		public void Insert ( int ID, bool item ) {
			if ( (ID >= Size) | (ID < 0) ) throw new IndexOutOfRangeException ();
			Size++;
			for (int i = Size - 1; i >= ID + 1; i-- )
				this[i] = this[i - 1];
			this[ID] = item;
		}
		public bool Remove ( bool item ) {
			int ID = IndexOf ( item );
			if (ID < 0 ) return false;
			RemoveAt ( ID );
			return true;
		}
		public void RemoveAt ( int ID ) {
			if ( (ID >= Size) | (ID < 0) ) throw new IndexOutOfRangeException ();
			for ( int i = ID + 1; i < Size; i++ )
				this[i - 1] = this[i];
			this[Size - 1] = false;
			Size--;
			if ( N % Width == 0 ) Resize ( N - 1 );
		}
		IEnumerator IEnumerable.GetEnumerator () => GetEnumerator ();

		private void Resize (int newSize) {
			N = newSize;
			var oldAr = Data;
			Data = new Num[N];
			Array.Copy ( oldAr, Data, N - 1 );
		}
	}
}