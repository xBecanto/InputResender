using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using MdxLibs.Services;

namespace MdxLibs.ServiceTests {
	public class ArDictTest {
		[Fact]
		public void TestArDict () {
			const int TestItemCnt = 10;
			const double NumOffset = 16.5;
			double[] Keys = new double[TestItemCnt];
			string[] Vals = new string[TestItemCnt];
			var testObj = new ArDict<double, string> ();
			testObj.Count.Should ().Be ( 0 );
			testObj.IsReadOnly.Should ().BeFalse ();

			for ( int i = 0; i < TestItemCnt; i++ ) {
				double key = i + NumOffset;
				string val = key.ToString ();
				testObj.Add ( key, val );
				Keys[i] = key;
				Vals[i] = val;
			}
			testObj.Count.Should ().Be ( TestItemCnt );
			testObj.Keys.Should ().Equal ( Keys );
			testObj.Values.Should ().Equal ( Vals );

			for ( int i = TestItemCnt - 1; i >= 0; i-- ) {
				double key = i + NumOffset;
				string val = key.ToString ();
				testObj[i].Should ().Be ( val );
				testObj[key].Should ().Be ( val );
				testObj.GetKey ( i ).Should ().Be ( key );
				testObj.FindIndex ( key ).Should ().Be ( i );
				testObj.FindIndex ( val ).Should ().Be ( i );
			}

			testObj.Remove ( Keys[0] ).Should ().BeTrue ();
			testObj.Count.Should ().Be ( TestItemCnt - 1 );
			string outVal;
			testObj.TryGetValue ( Keys[1], out outVal ).Should ().BeTrue ();
			outVal.Should ().Be ( Vals[1] );
			testObj.ContainsKey ( Keys[1] ).Should ().BeTrue ();
			testObj.Contains ( new KeyValuePair<double, string> ( Keys[1], Vals[1] ) ).Should ().BeTrue ();
			testObj.Contains ( new KeyValuePair<double, string> ( Keys[1], Vals[2] ) ).Should ().BeFalse ();
			testObj.Remove ( new KeyValuePair<double, string> ( Keys[1], Vals[1] ) ).Should ().BeTrue ();

			testObj.Count.Should ().Be ( TestItemCnt - 2 );
			testObj.TryGetValue ( Keys[1], out outVal ).Should ().BeFalse ();
			testObj.ContainsKey ( Keys[1] ).Should ().BeFalse ();
			testObj.Contains ( new KeyValuePair<double, string> ( Keys[1], Vals[1] ) ).Should ().BeFalse ();

			testObj.Remove ( new KeyValuePair<double, string> ( Keys[1], Vals[1] ) ).Should ().BeFalse ();
			testObj.ContainsKey ( Keys[1] ).Should ().BeFalse ();
			outVal.Should ().Be ( default );

			testObj.Clear ();
			testObj.Count.Should ().Be ( 0 );
			Action act = () => testObj.GetKey ( 0 );
			act.Should ().Throw<ArgumentOutOfRangeException> ();
		}
	}
}