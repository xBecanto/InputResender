using FluentAssertions;
using System.Collections.Generic;
using Xunit;
using MdxLibs.Services;

namespace MdxLibs.ServiceTests {
	public class BitListTest {
		const int TestItemCnt = 200;

		[Fact]
		public void TestList () {
			BitList testAr = new BitList { false, false, true, false };
			List<bool> bAr = new List<bool> ( TestItemCnt ) { false, false, true, false };

			for ( int i = 4; i < TestItemCnt; i++ ) {
				bool ret = (i & (1 << (i % 4))) > 0;
				bAr.Add ( ret );
				testAr.Add ( ret );
				testAr.IndexOf ( false ).Should ().Be ( bAr.IndexOf ( false ) );
				testAr.IndexOf ( true ).Should ().Be ( bAr.IndexOf ( true ) );
				testAr.Contains ( false ).Should ().Be ( bAr.Contains ( false ) );
				testAr.Contains ( true ).Should ().Be ( bAr.Contains ( true ) );
				testAr.Count.Should ().Be ( bAr.Count );
			}

			for ( int i = 0; i < bAr.Count; i++ )
				testAr[i].Should ().Be ( bAr[i] );

			for ( int i = 0; i < bAr.Count; i += 20 ) {
				bAr.RemoveAt ( i );
				testAr.RemoveAt ( i );
				testAr.Count.Should ().Be ( bAr.Count );
			}
			for ( int i = 0; i < bAr.Count; i++ )
				testAr[i].Should ().Be ( bAr[i] );

			for ( int i = bAr.Count - 1; i >= 0; i -= 11 ) {
				bAr.RemoveAt ( i );
				testAr.RemoveAt ( i );
				testAr.Count.Should ().Be ( bAr.Count );
			}
			for ( int i = 0; i < bAr.Count; i++ )
				testAr[i].Should ().Be ( bAr[i] );

			for ( int i = bAr.Count - 1; i >= 0; i -= 7 ) {
				bAr.Insert ( i, bAr[i % 16] );
				testAr.Insert ( i, testAr[i % 16] );
				testAr.Count.Should ().Be ( bAr.Count );
			}
			for ( int i = 0; i < bAr.Count; i++ )
				testAr[i].Should ().Be ( bAr[i] );

			var testEnum = testAr.GetEnumerator ();
			var origEnum = bAr.GetEnumerator ();
			int ID = 0;
			while(true) {
				bool origNext = origEnum.MoveNext ();
				bool testNext = testEnum.MoveNext ();
				testNext.Should ().Be ( origNext );
				if ( !testNext ) break;
				testEnum.Current.Should ().Be ( origEnum.Current );
				ID++;
			}
			bAr.Clear ();
			testAr.Clear ();
			testAr.Count.Should ().Be ( 0 );
		}
	}
}