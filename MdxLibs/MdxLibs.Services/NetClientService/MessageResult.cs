using System.Collections.Generic;
using System.Diagnostics;

namespace MdxLibs.Services.NetClientService {
	public class MessageResult {
		public enum SignalBackSelector { Immediately, AfterInnerProcess, AfterCallback, Manual }
		static int MsgCnt = 0;
		public readonly int MsgID = MsgCnt++;
		public enum Type { Received, Interrupted, Closed, Direct, Error }
		public readonly Type ResultType;
		public readonly List<(string, StackTrace)> Origin = new List<(string, StackTrace)> ();
		public readonly byte[] Data;
		public readonly object SourceEP, TargetEP;

		public MessageResult ( byte[] data, object src, object dest, bool isDirect = false ) {
			Data = data;
			SourceEP = src;
			TargetEP = dest;
			ResultType = isDirect ? Type.Direct : Type.Received;
			PushTrace ( "Creating valid result" );
		}
		public MessageResult ( Type errType, object src ) {
			Data = null;
			ResultType = errType;
			SourceEP = src;
			TargetEP = null;
			PushTrace ( $"Creating error ({errType}) result" );
		}

		public void PushTrace ( string context ) => Origin?.Add ( (context, new StackTrace ()) );

		public override string ToString () => $"${MsgID}<{ResultType}>({SourceEP}=>{TargetEP})" + (Data != null ? $"[{Data.Length}]" : string.Empty);
	}
}