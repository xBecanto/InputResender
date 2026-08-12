using System;
using System.Collections.Generic;
using InputResender.Services;
using MdxLibs.Core;
using MdxLibs.Definitions;
using MdxLibs.Services;

namespace InputResender.Definitions.InputProcessing {
	public abstract class DInputProcessor : ComponentBase_CoreBase {
		public DInputProcessor ( CoreBase owner ) : base ( owner ) {
			ModifiersDict = new Dictionary<KeyCode, (InputData.Modifier, bool)> () {
				{KeyCode.ControlKey, (InputData.Modifier.Ctrl, true)},
				{KeyCode.ShiftKey, (InputData.Modifier.Shift, true)},
				{KeyCode.LControlKey, (InputData.Modifier.Ctrl, true)},
				{KeyCode.RControlKey, (InputData.Modifier.Ctrl, true)},
				{KeyCode.LShiftKey, (InputData.Modifier.Shift, true)},
				{KeyCode.RShiftKey, (InputData.Modifier.Shift, true)},
				{KeyCode.Alt, (InputData.Modifier.Alt, true)},
				{KeyCode.RMenu, (InputData.Modifier.AltGr, true)},
				{KeyCode.LWin, (InputData.Modifier.WinKey, true)},
				{KeyCode.RWin, (InputData.Modifier.WinKey, true)},
			};
		}

		protected sealed override IReadOnlyList<(string opCode, Type opType)> AddCommands () => new List<(string opCode, Type opType)> () {
				(nameof(ProcessInput), typeof(bool)),
				(nameof(SetCustomModifier), typeof(void)),
				(nameof(SequentialProcess), typeof(InputData[])),
				(nameof(ReadModifiers), typeof(InputData.Modifier)),
				(nameof(Modifiers), typeof(IReadOnlyDictionary<KeyCode, (InputData.Modifier mod, bool readOnly)>)),
				(nameof(Callback), typeof(Action<InputData>)),
			};

		/// <inheritdoc cref="DHookManager.HookCallback"/>
		public abstract DHookManager.ConsumingStatus ProcessInput ( HInputEventDataHolder[] inputCombination );
		private Action<InputData> callback;
		public Action<InputData> Callback { set { callback = value; } protected get => callback ?? Owner.Fetch<DCommandWorker> ().Push; }

		protected void FireCallback ( InputData data, ComponentSelector target = null ) {
			Callback?.Invoke ( data );
			DComponentJoiner.TrySend ( this, target, data );
		}

		private Dictionary<KeyCode, (InputData.Modifier mod, bool system)> ModifiersDict;
		public IReadOnlyDictionary<KeyCode, (InputData.Modifier mod, bool readOnly)> Modifiers {
			get => ModifiersDict.AsReadOnly ();
		}
		public void SetCustomModifier ( KeyCode key, InputData.Modifier customMod ) {
			if ( ModifiersDict.ContainsKey ( key ) ) {
				if ( ModifiersDict[key].system ) throw new InvalidOperationException ( $"{key} cannot be assigned as custom modifier because it is used by system to trigger {ModifiersDict[key].mod} modifier" );
				if ( customMod == InputData.Modifier.None ) ModifiersDict.Remove ( key );
				else ModifiersDict[key] = (customMod, false);
			} else if ( customMod != InputData.Modifier.None ) ModifiersDict.Add ( key, (customMod, false) );
		}
		/// <summary>Can simplify calling of ProcessInput. When any time-dependent results are generated, those can only be captured by original callback (if was assigned beforehand).</summary>
		public InputData[] SequentialProcess ( HInputEventDataHolder[] inputCombination ) {
			List<InputData> ret = new List<InputData> ();
			Action<InputData> origCallback = Callback;
			Callback = ret.Add;
			ProcessInput ( inputCombination );
			Callback = origCallback;
			return ret.ToArray ();
		}

		public InputData.Modifier ReadModifiers ( HInputEventDataHolder[] inputCombination ) {
			int Cnt = inputCombination == null ? -1 : inputCombination.Length;
			if ( Cnt < 1 ) return InputData.Modifier.None;

			InputData.Modifier mods = InputData.Modifier.None;
			for ( int i = Cnt - 1; i >= 0; i-- ) {
				InputData.Modifier nMod;
				KeyCode key = (KeyCode)inputCombination[i].InputCode;
				if ( ModifiersDict.TryGetValue ( key, out var mod ) ) nMod = mod.mod;
				else nMod = InputData.Modifier.None;

				if ( inputCombination[i].Pressed >= 1 ) mods |= nMod;
				else mods &= ~nMod;
			}
			return mods;
		}

		public abstract class DStateInfo : StateInfo {
			public readonly string Callback;
			public readonly string[] Modifiers;

			public DStateInfo (DInputProcessor owner) : base (owner) {
				Callback = owner.Callback.Method.AsString ();
				Modifiers = new string[owner.Modifiers.Count];
				int ID = 0;
				foreach ( var mod in owner.Modifiers )
					Modifiers[ID++] = $"{mod.Key} => {mod.Value.mod}({(mod.Value.readOnly ? "system" : "user")})";
			}
			public override string AllInfo () => $"{base.AllInfo ()}{BR}Modifiers:{BR}{string.Join ( BR, Modifiers )}{BR}Callback:{BR}{Callback}";
		}
	}

	public class MInputProcessor : DInputProcessor {
		public MInputProcessor ( CoreBase owner ) : base ( owner ) { }

		public override int ComponentVersion => 1;

		public override DHookManager.ConsumingStatus ProcessInput ( HInputEventDataHolder[] inputCombination ) {
			if ( inputCombination == null || inputCombination.Length == 0 ) return DHookManager.ConsumingStatus.Error;
			var firstEvent = inputCombination[0];
			Callback?.Invoke ( new InputData ( this ) {
				Cmnd = firstEvent.Pressed >= 1 ? InputData.Command.KeyPress : InputData.Command.KeyRelease,
				X = firstEvent.Pressed,
				Key = (KeyCode)firstEvent.InputCode,
				DeviceID = firstEvent.HookInfo.DeviceID
			} );
			return DHookManager.ConsumingStatus.Consume;
		}

		public override StateInfo Info => new VStateInfo ( this );
		public class VStateInfo : DStateInfo {
			public VStateInfo ( MInputProcessor owner ) : base ( owner ) {
			}
		}
	}

	public class InputData : SerializableDataHolderBase<ComponentBase> {
		public enum Command { None, KeyPress, KeyRelease, MouseMove, Cancel, Type }
		[Flags]
		public enum Modifier {
			None = 0, Shift = 1, Ctrl = 2, Alt = 4, AltGr = 8, WinKey = 16,
			CustMod1 = 32, CustMod2 = 64, CustMod3 = 128, CustMod4 = 256,
			CustMod5 = 512, CustMod6 = 1024, CustMod7 = 2048, CustMod8 = 4096,
			CustMod9 = 1 << 13, CustMod10 = 1 << 14, CustMod11 = 1 << 15, CustMod12 = 1 << 16,
			CustMod13 = 1 << 17, CustMod14 = 1 << 18, CustMod15 = 1 << 19, CustMod16 = 1 << 20,
			CustMod17 = 1 << 21, CustMod18 = 1 << 22, CustMod19 = 1 << 23, CustMod20 = 1 << 24,
			CustMod21 = 1 << 25, CustMod22 = 1 << 26, CustMod23 = 1 << 27, CustMod24 = 1 << 28,
			CustMod25 = 1 << 29, CustMod26 = 1 << 30, CustMod27 = 1 << 31
		}

		public Command Cmnd = Command.None;
		public KeyCode Key = KeyCode.None;
		public int DeviceID = 0;
		public Modifier Modifiers = Modifier.None;
		public float X = 0, Y = 0, Z = 0;
		public bool Pressed { get { return (X >= 1) | (Y >= 1) | (Z >= 1); } }

		public static InputData Empty (ComponentBase owner) {
			return new InputData ( owner ) {
				Cmnd = Command.None,
				Key = KeyCode.None,
				DeviceID = 0, Modifiers = 0,
				X = 0, Y = 0, Z = 0
			};
		}
		public InputData ( ComponentBase owner ) : base ( owner ) { }
		public InputData ( ComponentBase owner, KeyCode key, bool Pressed, Modifier mod = Modifier.None ) : base ( owner ) {
			Cmnd = Pressed ? Command.KeyPress : Command.KeyRelease;
			Key = key;
			Modifiers = mod;
			X = Pressed ? 1 : 0;
			Y = Z = 0;
		}

		public override DataHolderBase<ComponentBase> Clone () => new InputData (Owner) { Cmnd = Cmnd, Key = Key, DeviceID = DeviceID, Modifiers = Modifiers, X = X, Y = Y, Z = Z };
		public override bool Equals ( object obj ) {
			if ( obj == null ) return false;
			if ( obj.GetType () != GetType () ) return false;
			var item = (InputData)obj;
			bool ret = (Cmnd.Equals ( item.Cmnd )) &
				(Key.Equals ( item.Key )) &
				(DeviceID.Equals ( item.DeviceID )) &
				(Modifiers.Equals ( item.Modifiers )) &
				(X.Equals ( item.X )) &
				(Y.Equals ( item.Y )) &
				(Z.Equals ( item.Z ));
			return ret;
		}
		public override int GetHashCode () => (Cmnd, Key, DeviceID, Modifiers, X, Y, Z).GetHashCode ();
		public override string ToString () => $"({Cmnd}@{Key}({DeviceID})[{Modifiers}]:{{{X}, {Y}, {Z}}})";
		/// <inheritdoc/>
		public override SerializableDataHolderBase<ComponentBase> Deserialize ( byte[] Data, bool overwrite ) {
			if (overwrite) {
				Cmnd = (Command)BitConverter.ToUInt32 ( Data, 0 );
				Key = (KeyCode)BitConverter.ToUInt32 ( Data, 4 );
				DeviceID = BitConverter.ToInt32 ( Data, 8 );
				Modifiers = (Modifier)BitConverter.ToUInt32 ( Data, 12 );
				X = BitConverter.ToSingle ( Data, 16 );
				Y = BitConverter.ToSingle ( Data, 20 );
				Z = BitConverter.ToSingle ( Data, 24 );
				return this;
			} else return new InputData ( Owner ) {
				Cmnd = (Command)BitConverter.ToUInt32 ( Data, 0 ),
				Key = (KeyCode)BitConverter.ToUInt32 ( Data, 4 ),
				DeviceID = BitConverter.ToInt32 ( Data, 8 ),
				Modifiers = (Modifier)BitConverter.ToUInt32 ( Data, 12 ),
				X = BitConverter.ToSingle ( Data, 16 ),
				Y = BitConverter.ToSingle ( Data, 20 ),
				Z = BitConverter.ToSingle ( Data, 24 )
			};
		}
		public override byte[] Serialize () {
			List<byte> ret = new List<byte> ();
			ret.AddRange ( BitConverter.GetBytes ( (uint)Cmnd ) );
			ret.AddRange ( BitConverter.GetBytes ( (uint)Key ) );
			ret.AddRange ( BitConverter.GetBytes ( DeviceID ) );
			ret.AddRange ( BitConverter.GetBytes ( (uint)Modifiers ) );
			ret.AddRange ( BitConverter.GetBytes ( X ) );
			ret.AddRange ( BitConverter.GetBytes ( Y ) );
			ret.AddRange ( BitConverter.GetBytes ( Z ) );
			return ret.ToArray ();
		}
	}
}