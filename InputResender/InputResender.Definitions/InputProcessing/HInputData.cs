using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using InputResender.Services;
using MdxLibs.Core;
using MdxLibs.Definitions;
using MdxLibs.Services;

namespace InputResender.Definitions.InputProcessing {
	/// <summary>High-Level version of HInputData</summary>
	public abstract class HInputEventDataHolder : DataHolderBase<ComponentBase> {
		public const int PressThreshold = ushort.MaxValue;
		public HHookInfo HookInfo { get; protected set; }
		public int InputCode { get; protected set; }
		public int ValueX { get; protected set; }
		public int ValueY { get; protected set; }
		public int ValueZ { get; protected set; }
		public int DeltaX { get; protected set; }
		public int DeltaY { get; protected set; }
		public int DeltaZ { get; protected set; }

		public float Pressed {
			get {
				const double D = PressThreshold;
				double X = ValueX / D, Y = ValueY / D, Z = ValueZ / D;
				return (float)Math.Sqrt ( X * X + Y * Y + Z * Z );
			}
		}
		public bool BeingPressedX => ValueX >= PressThreshold && (ValueX - DeltaX) < PressThreshold;
		public bool BeingPressedY => ValueY >= PressThreshold && (ValueY - DeltaY) < PressThreshold;
		public bool BeingPressedZ => ValueZ >= PressThreshold && (ValueZ - DeltaZ) < PressThreshold;
		public bool BeingReleasedX => ValueX < PressThreshold && (ValueX - DeltaX) >= PressThreshold;
		public bool BeingReleasedY => ValueY < PressThreshold && (ValueY - DeltaY) >= PressThreshold;
		public bool BeingReleasedZ => ValueZ < PressThreshold && (ValueZ - DeltaZ) >= PressThreshold;

		public HInputEventDataHolder ( ComponentBase owner, HHookInfo hookInfo ) : base ( owner ) { HookInfo = hookInfo; }

		public static HInputEventDataHolder KeyDown ( ComponentBase owner, HHookInfo hookInfo, KeyCode key ) => new HKeyboardEventDataHolder ( owner, hookInfo, (int)key, VKChange.KeyDown );

		public static HInputEventDataHolder KeyUp ( ComponentBase owner, HHookInfo hookInfo, KeyCode key ) => new HKeyboardEventDataHolder ( owner, hookInfo, (int)key, VKChange.KeyUp );

		public void AddHookIDs (IDictionary<VKChange, DictionaryKey> hooks) {
			foreach ( var hook in hooks ) HookInfo.AddHookID ( hook.Value, hook.Key );
		}
		public void SetNewValue ( int X, int Y, int Z ) {
			DeltaX = X - ValueX; DeltaY = Y - ValueY; DeltaZ = Z - ValueZ;
			ValueX = X; ValueY = Y; ValueZ = Z;
		}

		public int Convert ( float f ) => (int)(f * PressThreshold);

		public override bool Equals ( object obj ) {
			if ( obj == null ) return false;
			if ( obj.GetType () != GetType () ) return false;
			var item = (HInputEventDataHolder)obj;
			bool fullEqCheck = FullEqCheck & item.FullEqCheck;
			bool ret = (InputCode.Equals ( item.InputCode )) &
				(ValueX.Equals ( item.ValueX )) &
				(ValueY.Equals ( item.ValueY )) &
				(ValueZ.Equals ( item.ValueZ )) &
				(DeltaX.Equals ( item.DeltaX )) &
				(DeltaY.Equals ( item.DeltaY )) &
				(DeltaZ.Equals ( item.DeltaZ ));
			return ret & (HookInfo.Equals ( item.HookInfo ));
		}
		public override int GetHashCode () => (HookInfo, InputCode, ValueX, ValueY, ValueZ, DeltaX, DeltaY, DeltaZ).GetHashCode ();
		public override string ToString () => $"{(HookInfo == null ? "NoID" : HookInfo.DeviceID)}.{InputCode} ({(KeyCode)InputCode})[{ValueX.ToShortString ()};{ValueY.ToShortString ()};{ValueZ.ToShortString ()}] Δ[{DeltaX.ToShortString ()};{DeltaY.ToShortString ()};{DeltaZ.ToShortString ()}]";
	}

	public class LLInputStatusExtra {
		[StructLayout ( LayoutKind.Sequential )]
		private class StatusData {
			public int StructMark;
			public nint OrigExtraInfo;
			public uint TimeOfRegistration;
			public int UID;
			public byte Holders;
		}

		static Dictionary<nint, LLInputStatusExtra> Extras = [];
		static int uidProvider = 42;

		readonly GCHandle handle;
		readonly StatusData data;
		public nint Ptr => handle.AddrOfPinnedObject ();

		private LLInputStatusExtra (nint origExtra) {
			data = new () {
				StructMark = 0x5ad40fb7,
				OrigExtraInfo = origExtra,
				TimeOfRegistration = (uint)(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond),
				UID = uidProvider++,
				Holders = 1
			};
			handle = GCHandle.Alloc ( data, GCHandleType.Pinned );
		}

		public static LLInputStatusExtra Create (nint origExtra) {
			lock( Extras ) {
				if ( Extras.TryGetValue ( origExtra, out LLInputStatusExtra extra ) )
					return extra;
				return new ( origExtra );
			}
		}
		public void Load ( nint ptr ) => Marshal.PtrToStructure ( ptr, this );
		public void Save ( nint ptr ) => Marshal.StructureToPtr ( this, ptr, false );
	}

	/// <summary>Low-Level version of HInputEventDataHolder</summary>
	public abstract class HInputData : DataHolderBase<ComponentBase> {
		protected HInputData ( ComponentBase owner ) : base ( owner ) {
		}

		public abstract int DeviceID { get; protected set; }
		public bool IsPressed { get => Pressed == VKChange.KeyDown; }
		public abstract VKChange Pressed { get; protected set; }
		public abstract IInputLLValues Data { get; protected set; }
		public abstract nint ExtraInfo { get; protected set; }
		public abstract int SizeOf { get; }
		public abstract uint TimeStamp { get; }
		public abstract void UpdateByHook (DLowLevelInput hookObj, DictionaryKey hookID );
		public abstract void SetExtraInfo ( nint dataLocation, nint extraInfo );
		public KeyCode Key { get => Data.Key; }

		protected bool MarkedInvalid = false;

		public virtual bool IsInvalid {
			get => MarkedInvalid;
			set {
				if ( MarkedInvalid )
					throw new InvalidOperationException (
						"This input data is already marked as invalid and cannot be reused!"
					);

				MarkedInvalid = value;
			}
		}

		public override bool Equals ( object obj ) {
			if ( obj == null ) return false;
			if ( obj.GetType () != GetType () ) return false;
			var item = (HInputData)obj;
			return (Pressed == item.Pressed) & (SizeOf == item.SizeOf) & (DeviceID == item.DeviceID) & (DeviceID == item.DeviceID);
		}
		public abstract nint GetFullHash ( nint wParam, nint lParam );
	}

	public interface IInputLLValues {
		IInputLLValues Clone ();
		int SizeOf { get; }
		KeyCode Key { get; }
	}


	public class HInputData_Mock : HInputData {
		IInputStruct_Mock data;
		int deviceID = 1;

		public HInputData_Mock ( ComponentBase owner, IInputStruct_Mock values ) : base ( owner ) => data = values;
		public HInputData_Mock ( ComponentBase owner, DictionaryKey hookID, VKChange keyChange, IntPtr vkCode ) : base ( owner ) => data = new IInputStruct_Mock ( hookID, keyChange, vkCode );

		public override IInputLLValues Data {
			get => data;
			protected set => data = (IInputStruct_Mock)value;
		}

		public override int SizeOf => data.SizeOf;
		public override int DeviceID { get => deviceID; protected set => deviceID = value; }
		public override uint TimeStamp => (uint)CreationTime.Ticks;
		public override nint ExtraInfo { get; protected set; }
		public override VKChange Pressed { get => data.KeyChange; protected set => data.KeyChange = value; }

		public override DataHolderBase<ComponentBase> Clone () => new HInputData_Mock ( Owner, (IInputStruct_Mock)data.Clone () );
		public override bool Equals ( object obj ) {
			if ( obj == null ) return false;
			if ( obj.GetType () != GetType () ) return false;
			var item = (HInputData_Mock)obj;
			return data.Equals ( item.data );
		}
		public override int GetHashCode () => data.HookID.GetHashCode ();
		public override string ToString () => $"#{data.HookID}:{data.KeyChange}@{data.VKCode}";
		public override void UpdateByHook ( DLowLevelInput hookObj, DictionaryKey hookID ) {
			data.HookID = hookID;
		}
		public override void SetExtraInfo ( nint dataLocation, nint extraInfo ) => ExtraInfo = extraInfo;
		public override nint GetFullHash ( nint wParam, nint lParam ) => InputManagementService.CalcHashKeyboard ( data.KeyChange, data.Key, (uint)CreationTime.Ticks, wParam );

		public struct IInputStruct_Mock : IInputLLValues {
			public DictionaryKey HookID;
			public VKChange KeyChange;
			public IntPtr VKCode;

			public IInputStruct_Mock ( DictionaryKey hookID, VKChange keyChange, IntPtr vkCode ) { HookID = hookID; KeyChange = keyChange; VKCode = vkCode; }

			public int SizeOf => Marshal.SizeOf ( this );

			public KeyCode Key => (KeyCode)VKCode;

			public IInputLLValues Clone () => new IInputStruct_Mock ( HookID, KeyChange, VKCode );
		}
	}
}