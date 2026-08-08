using FluentAssertions;
using System.Collections;
using Xunit;
using MdxLibs.Services;

namespace MdxLibs.ServiceTests {
	public class BitFieldTest {
		[Fact]
		public void Count () {
			var cnt = GenerateData ( out BitField testObj, out BitArray chckObj );
			testObj.Count ( false ).Should ().Be ( cnt.zeros );
			testObj.Count ( true ).Should ().Be ( cnt.ones );
			testObj.Reset ();
			testObj.Count ( false ).Should ().Be ( BitField.Width );
			testObj.Count ( true ).Should ().Be ( 0 );
		}

		[Fact]
		public void GetSet () {
			var cnt = GenerateData ( out BitField testObj, out BitArray chckObj );
			for (int i = BitField.Width - 1; i > 0; i--) {
				testObj[i].Should ().Be ( chckObj[i] );
				testObj[i] = testObj[i - 1];
				testObj[i].Should ().Be ( chckObj[i - 1] );
				testObj.Not ( i );
				testObj[i].Should ().Be ( !chckObj[i - 1] );
			}
			testObj[0].Should ().Be ( chckObj[0] );
			testObj[0] = false;
			testObj[0].Should ().Be ( false );
			testObj.Not ( 0 );
			testObj[0].Should ().Be ( true );
		}

		private (int ones, int zeros) GenerateData (out BitField testObj, out BitArray chckObj) {
			testObj = new BitField ();
			chckObj = new BitArray ( BitField.Width );
			int ones = 0, zeros = 0;

			int ID = 0;
			for (int i = 0; true; i++ ) {
				testObj[ID] = true;
				chckObj[ID] = true;
				ones++;
				ID++;
				for (int j = 0; j < i; j++) {
					if ( ID >= BitField.Width ) return (ones, zeros);
					testObj[ID] = false;
					chckObj[ID] = false;
					zeros++;
					ID++;
				}
			}
		}
	}
}