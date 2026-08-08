using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using InputResender.Definitions.InputProcessing;

namespace InputResender.Windows.Input;
public class WinLLInputStatusExtra : CInputLLParser.InputEventInfo {
	public const int MARK = StatusData.MARK;
	[StructLayout ( LayoutKind.Sequential )]
	private class StatusData {
		public const int MARK = 0x5ad40fb7;
		public readonly int StructMark;
		public readonly nint OrigExtraInfo;
		public readonly uint TimeOfRegistration;
		public readonly uint UID;
		public readonly nint structPtr;
		private int Holders;
		private int locked;

		public StatusData (uint uid, nint origData) {
			StructMark = MARK;
			OrigExtraInfo = origData;
			TimeOfRegistration = (uint)(System.DateTime.Now.Ticks / System.TimeSpan.TicksPerMillisecond);
			UID = uid; // This should be set by the caller
			Holders = 1; // Initial holder count

			//GCHandle handle = GCHandle.Alloc ( this, GCHandleType.Pinned );
			//structPtr = handle.AddrOfPinnedObject ();
			structPtr = Marshal.AllocHGlobal ( Marshal.SizeOf<StatusData> () );
			Marshal.StructureToPtr ( this, structPtr, false ); // Save the structure to unmanaged memory
		}
		private StatusData () { }

		public static StatusData TryLoad ( nint ptr ) => TryLoad ( ptr, out var _ );
		public static StatusData TryLoad ( nint ptr, out System.Exception ex ) {
			ex = null;
			if ( ptr == nint.Zero ) { ex = new System.ArgumentNullException ( nameof ( ptr ) ); return null; }
			try {
				int mark = Marshal.ReadInt32 ( ptr );
				if ( mark != MARK ) {
					ex = new System.InvalidOperationException ( $"Invalid struct mark {mark:X} at {ptr:X}, expected {MARK:X}" );
					return null;
				}
			} catch ( System.Exception e ) { ex = e; return null; }
			try {
				var ret = Marshal.PtrToStructure<StatusData> ( ptr );
				ret.Holders++;
				Marshal.StructureToPtr ( ret, ptr, false ); // Update the structure in memory
				return ret;
			}
			catch ( System.Exception e ) { ex = e; return null; }
		}

		private void Lock() {
			while ( Interlocked.CompareExchange ( ref locked, 1, 0 ) != 0 )
				continue;
			Interlocked.MemoryBarrierProcessWide ();
		}
		public void Unlock() {
			Interlocked.MemoryBarrierProcessWide ();
			locked = 0;
		}

		public void Free () {
			Lock ();
			Holders--;
			if ( Holders <= 0 ) {
				Unlock ();
				Marshal.FreeHGlobal ( structPtr );
			} else {
				Unlock ();
			}
		}
	}




	public readonly HWInput inputData;
	readonly StatusData statusData;
	//readonly GCHandle handle;

	public uint TimeOfRegistration => statusData.TimeOfRegistration;
	public uint UID => statusData.UID;
	public nint StatusPtr => statusData.structPtr;
	public nint StatusOrigExtraInfo => statusData.OrigExtraInfo;

	private WinLLInputStatusExtra(HWInput data, StatusData status, bool consume, bool process) {
		inputData = data;
		statusData = status;
		//handle = statusData.Handle;
		ShouldProcess = process;
		CanConsume = consume;
	}

	public override void Dispose () {
		statusData.Free ();
	}

	protected override bool Equals ( CInputLLParser.InputEventInfo other ) {
		if ( other == null ) return false;
		if ( other.GetType () != GetType () ) return false;
		var item = (WinLLInputStatusExtra)other;
		bool ret = inputData.Equals ( item.inputData );
		ret &= (statusData.UID == item.statusData.UID);
		ret &= (statusData.OrigExtraInfo == item.statusData.OrigExtraInfo);
		ret &= (statusData.structPtr == item.statusData.structPtr);
		ret &= (statusData.TimeOfRegistration == item.statusData.TimeOfRegistration);
		return ret;
	}

	public class WinLLInputStatusParser : CInputLLParser.InputEventParser {
		Dictionary<nint, WinLLInputStatusExtra> Extras = [];
		uint uidProvider = 42;

		public override WinLLInputStatusExtra Parse ( int nCode, nint wParam, nint lParam ) {
			HWInput data;
			try { data = new ( wParam, lParam ); } catch ( System.Exception _ ) { return null; }
			lock ( Extras ) {
				if ( Extras.TryGetValue ( data.ExtraInfo, out var extra ) )
					return extra;
			}
			var status = StatusData.TryLoad ( data.ExtraInfo );
			if ( status == null ) {
				lock ( Extras ) {
					status = new ( uidProvider++, data.ExtraInfo );
				}
				data.UpdateExtraInfo ( lParam, status.structPtr );
			}
			WinLLInputStatusExtra ret = new ( data, status, true, nCode >= 0 );
			lock ( Extras ) { Extras.Add ( data.ExtraInfo, ret ); }
			return ret;
		}

		public override void Dispose () {}
	}
}