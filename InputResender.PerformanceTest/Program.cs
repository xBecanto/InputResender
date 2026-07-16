/*using System;
using InputResender.UnitTests;

namespace InputResender.PerformanceTest; 
public class Program {
	public static void Main ( string[] args ) {
		var iter = GlobalCommandTest.CommandIterator ().GetEnumerator ();
		Console.WriteLine ( "Started performance testing ..." );
		DateTime start = DateTime.Now;
		DateTime stop = start + TimeSpan.FromSeconds ( 120 );
		int processed = 0;
		while ( DateTime.Now < stop ) {
			if ( !iter.MoveNext () ) {
				Console.WriteLine ( $"Finished performance testing in {(DateTime.Now - start).TotalSeconds:f0} s, processed {processed} commands." );
				return;
			}

			try {
				(string cmd, Type t) = ((string, Type))iter.Current[0];
				GlobalCommandsHelpAvailableTest testObj = new ( null );
				testObj.HelpAvailable_P ( cmd, t );
				processed++;
				Console.WriteLine ( $"Processed {processed} command" );
			} catch { }
		}
		Console.WriteLine ( $"Timeout stopped performance testing after {(DateTime.Now - start).TotalMinutes:f2} m, processed {processed} commands." );
	}
}*/