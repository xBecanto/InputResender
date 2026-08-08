using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using MdxLibs.Core;

namespace InputResender.CLI;
public class StandardStream : IDisposable {
	public IReadOnlyList<string> InBuffer => inBuffer.AsReadOnly();
	public IReadOnlyList<string> OutBuffer => outBuffer.AsReadOnly();
	public IReadOnlyList<string> ErrBuffer => errBuffer.AsReadOnly();

	private List<string> inBuffer = new ();
	private List<string> outBuffer = new ();
	private List<string> errBuffer = new ();

	readonly BlockingCollection<string> StdIn = new ();
	readonly BlockingCollection<string> StdOut = new ();
	readonly BlockingCollection<string> StdErr = new ();

	public ConsoleManager ConsoleWrapper { get; init; }

	public void InputLine ( string line ) {
		lock ( inBuffer ) {
			inBuffer.Add ( line );
			StdIn.Add ( line );
		}
	}

	public void Output ( string ss ) {
		lock ( inBuffer ) {
			if ( !StdOut.TryTake ( out string oldLine ) ) oldLine = string.Empty;
			oldLine += ss;
			outBuffer.Add ( ss );
			StdOut.Add ( ss );
		}
	}

	public void OutputLine ( string line ) {
		lock ( outBuffer ) {
			outBuffer.Add ( line );
			StdOut.Add ( line );
		}
	}

	public StandardStream () {
		ConsoleWrapper = new (
			realWriteLine: OutputLine
			, realReadLine: StdIn.Take
			, realWrite: Output
			, realClear: null
			, realReadChar: null
			, realOverwriteLastLine: null
			);
	}

	public string[] ReadAllOutput () {
		lock ( outBuffer ) {
			return outBuffer.ToArray ();
		}
	}

	public void ClearOutput () {
		lock ( outBuffer ) {
			outBuffer.Clear ();
		}
	}

	public void Dispose () {
		StdIn.Dispose ();
		StdOut.Dispose ();
		StdErr.Dispose ();
	}
}