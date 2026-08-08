using System;
using System.Runtime.InteropServices;
using System.Text;
using MdxLibs.Core;

namespace InputResender.CLI;
public static class NativeEntryPoint {
	[UnmanagedCallersOnly ( EntryPoint = nameof ( Init ) )]
	public static IntPtr Init () {
		return NativeSession.Create ();
	}

	[UnmanagedCallersOnly ( EntryPoint = nameof ( Cleanup ) )]
	public static void Cleanup ( IntPtr handle ) {
		NativeSession.Cleanup ( handle );
	}

	[UnmanagedCallersOnly ( EntryPoint = nameof ( Process ) )]
	public static IntPtr Process ( IntPtr sessionPtr, IntPtr linePtr ) {
		var ssn = NativeSession.Fetch ( sessionPtr );
		string line = Marshal.PtrToStringUni ( linePtr );
		var ret = ssn.cmdProcessor.ProcessLine ( line );

		string header;
		if ( ret == null ) header = "NULL";
		else {
			header = ret switch {
				ErrorCommandResult => "Error",
				_                  => ""
			};
		}

		string body;
		if ( ret == null ) body = "NULL";
		else if ( string.IsNullOrEmpty ( ret.Message ) ) body = "Empty";
		else body = ret.Message;

		string msg = $"\u0001{header}\u0002{body}\u0003";
		return Marshal.StringToHGlobalUni ( msg );
	}

	[UnmanagedCallersOnly(EntryPoint = nameof ( FetchDelayed ) )]
	public static IntPtr FetchDelayed (IntPtr sessionPtr) {
		var ssn = NativeSession.Fetch ( sessionPtr );
		StringBuilder SB = new ();
		ssn.cmdProcessor.Owner?.FlushDelayedMsgs ( ( s ) => SB.AppendLine ( s ) );
		return Marshal.StringToHGlobalUni( SB.ToString () );
	}
}

internal struct NativeSession {
	public CommandProcessor cmdProcessor;
	GCHandle gcHandle;
	IntPtr pointer;
	
	/// <summary>Create new session and return unmanaged pointer to it</summary>
	public static IntPtr Create () {
		var session = new NativeSession();
		session.gcHandle = GCHandle.Alloc ( session, GCHandleType.Pinned );
		session.pointer = session.gcHandle.AddrOfPinnedObject ();
		return session.pointer;
	}
	public static NativeSession Fetch (IntPtr ptr) {
		var handle = GCHandle.FromIntPtr ( ptr );
		return (NativeSession)handle.Target;
	}
	public static void Cleanup (IntPtr ptr) {
		var obj = Fetch ( ptr );
		obj.gcHandle.Free ();
		obj.pointer = IntPtr.Zero;
	}
}