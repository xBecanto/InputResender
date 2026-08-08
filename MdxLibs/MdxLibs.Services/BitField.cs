using System;
using Num = System.UInt32;

namespace MdxLibs.Services {
	public struct BitField {
		public const Num One = 1U;
		public const int Width = sizeof ( Num ) * 8;
		Num Data;

		public bool this[int ID] {
			get {
				if ( (ID >= Width) | (ID < 0) ) throw new IndexOutOfRangeException ();
				return (Data & (One << ID)) > 0;
			}
			set {
				if ( (ID >= Width) | (ID < 0) ) throw new IndexOutOfRangeException ();
				if ( value ) Data |= One << ID;
				else Data &= ~(One << ID);
			}
		}

		public void Reset () => Data = 0;
		public static implicit operator Num ( BitField val ) => val.Data;
		public void Not ( int ID ) => Data ^= One << ID;
		public int Count (bool val ) {
			int ret = 0;
			for (int i = 0; i < Width;i++) {
				ret += ((Data >> i) & One) == (val ? One : 0) ? 1 : 0;
			}
			return ret;
		}
		public override string ToString () => Convert.ToString ( Data, 2 );
		public Num Value { get => Data; set => Data = value; }
	}
}