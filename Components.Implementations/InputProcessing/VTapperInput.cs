using Components.Interfaces;
using Components.Library;
using DataHolder = Components.Interfaces.HInputEventDataHolder;
using ModE = Components.Interfaces.InputData.Modifier;

namespace Components.Implementations {
	public class VTapperInput : DInputProcessor {
		public enum StateType { Normal, Shift, Switch }
		public static readonly string[] MappingNames = ["Single", "Double", "Triple", "Shift", "Switch"];
		public const int MappingCount = 5;
		public const int ComboCount = 32; // 2^5 trigger keys

		private readonly (KeyCode key, ModE mod)[][] mapping = new (KeyCode, ModE)[5][] { [
				(KeyCode.Scroll, ModE.None), (KeyCode.O, ModE.None), (KeyCode.I, ModE.None), (KeyCode.F, ModE.None),
				(KeyCode.E, ModE.None), (KeyCode.Space, ModE.None), (KeyCode.S, ModE.None), (KeyCode.Scroll, ModE.None),
				(KeyCode.A, ModE.None), (KeyCode.B, ModE.None), (KeyCode.R, ModE.None), (KeyCode.Y, ModE.None),
				(KeyCode.N, ModE.None), (KeyCode.Down, ModE.None), (KeyCode.K, ModE.None), (KeyCode.L, ModE.None),
				(KeyCode.T, ModE.None), (KeyCode.M, ModE.None), (KeyCode.P, ModE.None), (KeyCode.V, ModE.None),
				(KeyCode.U, ModE.None), (KeyCode.Q, ModE.None), (KeyCode.H, ModE.None), (KeyCode.G, ModE.None),
				(KeyCode.D, ModE.None), (KeyCode.X, ModE.None), (KeyCode.W, ModE.None), (KeyCode.Z, ModE.None),
				(KeyCode.CapsLock, ModE.None), (KeyCode.LineFeed, ModE.None), (KeyCode.J, ModE.None), (KeyCode.C, ModE.None),
			], [
				(KeyCode.Scroll, ModE.None), (KeyCode.LineFeed, ModE.None), (KeyCode.Tab, ModE.None), (KeyCode.OemCloseBrackets, ModE.None),
				(KeyCode.Space, ModE.None), (KeyCode.Scroll, ModE.None), (KeyCode.OemOpenBrackets, ModE.None), (KeyCode.Scroll, ModE.None),
				(KeyCode.Back, ModE.None), (KeyCode.OemSemicolon, ModE.Shift), (KeyCode.D0, ModE.Shift), (KeyCode.OemBackslash, ModE.None),
				(KeyCode.D9, ModE.Shift), (KeyCode.Scroll, ModE.None), (KeyCode.OemMinus, ModE.None), (KeyCode.OemMinus, ModE.Shift),
				(KeyCode.Oemcomma, ModE.None), (KeyCode.OemPeriod, ModE.None), (KeyCode.OemCloseBrackets, ModE.Shift), (KeyCode.OemQuotes, ModE.Shift),
				(KeyCode.OemOpenBrackets, ModE.Shift), (KeyCode.OemQuestion, ModE.Shift), (KeyCode.OemQuotes, ModE.None), (KeyCode.Oemtilde, ModE.Shift),
				(KeyCode.PrintScreen, ModE.None), (KeyCode.OemPeriod, ModE.Shift), (KeyCode.VolumeMute, ModE.None), (KeyCode.Oemcomma, ModE.Shift),
				(KeyCode.CapsLock, ModE.None), (KeyCode.Scroll, ModE.None), (KeyCode.OemMinus, ModE.Shift), (KeyCode.OemQuestion, ModE.None),
			], [
				(KeyCode.Scroll, ModE.None), (KeyCode.Scroll, ModE.None), (KeyCode.Sleep, ModE.None), (KeyCode.End, ModE.None),
				(KeyCode.Help, ModE.None), (KeyCode.Scroll, ModE.None), (KeyCode.Home, ModE.None), (KeyCode.Scroll, ModE.None),
				(KeyCode.Back, ModE.Ctrl), (KeyCode.A, ModE.Ctrl), (KeyCode.PageDown, ModE.None), (KeyCode.Right, ModE.None),
				(KeyCode.PageUp, ModE.None), (KeyCode.Scroll, ModE.None), (KeyCode.Down, ModE.None), (KeyCode.Help, ModE.None),
				(KeyCode.Delete, ModE.None), (KeyCode.V, ModE.Ctrl), (KeyCode.C, ModE.Ctrl), (KeyCode.LWin, ModE.None),
				(KeyCode.X, ModE.Ctrl), (KeyCode.Scroll, ModE.None), (KeyCode.Scroll, ModE.None), (KeyCode.Escape, ModE.None),
				(KeyCode.Apps, ModE.None), (KeyCode.Left, ModE.None), (KeyCode.Scroll, ModE.None), (KeyCode.Y, ModE.Ctrl),
				(KeyCode.Scroll, ModE.None), (KeyCode.Scroll, ModE.None), (KeyCode.Z, ModE.Ctrl), (KeyCode.Up, ModE.None),
			], [
				(KeyCode.Scroll, ModE.None), (KeyCode.O, ModE.Shift), (KeyCode.I, ModE.Shift), (KeyCode.F, ModE.Shift),
				(KeyCode.E, ModE.Shift), (KeyCode.Space, ModE.None), (KeyCode.S, ModE.Shift), (KeyCode.Scroll, ModE.None),
				(KeyCode.A, ModE.Shift), (KeyCode.B, ModE.Shift), (KeyCode.R, ModE.Shift), (KeyCode.Y, ModE.Shift),
				(KeyCode.N, ModE.Shift), (KeyCode.Down, ModE.None), (KeyCode.K, ModE.Shift), (KeyCode.L, ModE.Shift),
				(KeyCode.T, ModE.Shift), (KeyCode.M, ModE.Shift), (KeyCode.P, ModE.Shift), (KeyCode.V, ModE.Shift),
				(KeyCode.U, ModE.Shift), (KeyCode.Q, ModE.Shift), (KeyCode.H, ModE.Shift), (KeyCode.G, ModE.Shift),
				(KeyCode.D, ModE.Shift), (KeyCode.X, ModE.Shift), (KeyCode.W, ModE.Shift), (KeyCode.Z, ModE.Shift),
				(KeyCode.CapsLock, ModE.None), (KeyCode.LineFeed, ModE.None), (KeyCode.J, ModE.Shift), (KeyCode.C, ModE.Shift),
			], [
				(KeyCode.Scroll, ModE.None), (KeyCode.Oemplus, ModE.Shift), (KeyCode.D1, ModE.None), (KeyCode.Down, ModE.None),
				(KeyCode.D2, ModE.None), (KeyCode.Right, ModE.None), (KeyCode.D3, ModE.None), (KeyCode.D5, ModE.Shift),
				(KeyCode.D4, ModE.None), (KeyCode.Left, ModE.None), (KeyCode.D5, ModE.None), (KeyCode.D7, ModE.Shift),
				(KeyCode.D6, ModE.None), (KeyCode.D4, ModE.Shift), (KeyCode.D7, ModE.None), (KeyCode.D8, ModE.Shift),
				(KeyCode.D8, ModE.None), (KeyCode.Up, ModE.None), (KeyCode.D9, ModE.None), (KeyCode.D3, ModE.Shift),
				(KeyCode.D0, ModE.None), (KeyCode.Space, ModE.None), (KeyCode.D2, ModE.Shift), (KeyCode.NoName, ModE.None),
				(KeyCode.D6, ModE.Shift), (KeyCode.OemSemicolon, ModE.Shift), (KeyCode.OemPipe, ModE.Shift), (KeyCode.D5, ModE.Shift),
				(KeyCode.Space, ModE.None), (KeyCode.D8, ModE.Shift), (KeyCode.D4, ModE.Shift), (KeyCode.D1, ModE.Shift),
			]
		};
		private readonly KeyCode[] TriggerKeys;
		private KeyCode ShiftKey = KeyCode.CapsLock;
		private KeyCode SwitchKey = KeyCode.Scroll;
		private ModE TriggerMod;
		int Presses = 0;
		BitField LastTapCombo = new BitField ();
		public StateType State { get; private set; } = StateType.Normal;
		InputData LastRet = null;
		DateTime LastPress;
		public int WaitTime = 350;
		public Action<string> Log = null;
		public bool Verbose = true;

		BitField fingers;
		bool WaitForRelease = false;

		// Should allow only single modifier for all keys. Dsc is also useless. So constructor should rather be (CoreBase, KeyCode[], Modifier = None)
		public VTapperInput ( CoreBase owner, KeyCode[] keys, ModE mod ) : base ( owner ) {
			if ( keys.Length < 5 ) throw new DataMisalignedException ( $"TapStrap simulator processor needs 5 keys defined!" );
			TriggerMod = mod;
			TriggerKeys = new KeyCode[5];
			for ( int i = 0; i < 5; i++ ) {
				if ( TriggerKeys.Contains ( keys[i] ) ) throw new DataMisalignedException ( $"Key {keys[i]} cannot be used to represent two different fingers!" );
				TriggerKeys[i] = keys[i];
			}
		}

		public override int ComponentVersion => 1;

		public void SetMapping ( int mapID, int comboIndex, KeyCode key, ModE mod = ModE.None ) {
			if ( mapID < 0 || mapID >= MappingCount ) throw new ArgumentOutOfRangeException ( nameof ( mapID ) );
			if ( comboIndex < 0 || comboIndex >= ComboCount ) throw new ArgumentOutOfRangeException ( nameof ( comboIndex ) );
			mapping[mapID][comboIndex] = (key, mod);
		}

		public (KeyCode key, ModE mod) GetMapping ( int mapID, int comboIndex ) {
			if ( mapID < 0 || mapID >= MappingCount ) throw new ArgumentOutOfRangeException ( nameof ( mapID ) );
			if ( comboIndex < 0 || comboIndex >= ComboCount ) throw new ArgumentOutOfRangeException ( nameof ( comboIndex ) );
			return mapping[mapID][comboIndex];
		}

		public void SetKeys ( KeyCode[] keys ) {
			if ( keys.Length != 7 ) throw new ArgumentException ( nameof(keys) );

			for ( int i = 0; i < 5; i++ ) TriggerKeys[i] = keys[i];
			ShiftKey = keys[5];
			SwitchKey = keys[6];
		}

		public void SetTriggers ( ModE mod ) {
			TriggerMod = mod;
		}

		public (KeyCode[], KeyCode, KeyCode, ModE) GetTriggerKeys () => (TriggerKeys, ShiftKey, SwitchKey, TriggerMod);

		public string PrintState () => $"{Convert.ToString ( fingers.Value, 2 )}x{Presses}:{State}:{(WaitForRelease ? "Pressed" : "Released")}";

		public struct KeyState {
			public Dictionary<KeyCode, bool> Keys;
			public ModE ActMods;
			public bool KeyReleased;
		}

		int GetID (KeyCode key) {
			for (int i = 0; i < 5; i++)
				if (key == TriggerKeys[i]) return i;
			return -1;
		}

		struct InputParseResult {
			public BitField OrigVal, NewVal, Pressed, Released;
			public bool IsAnyReleased;
			public bool AllSkipped;
			public InputParseResult ( bool allSkipped = true ) {
				OrigVal = NewVal = Pressed = Released = new BitField ();
				IsAnyReleased = false;
				AllSkipped = allSkipped;
			}
		}

		InputParseResult ParseInput ( DataHolder[] inputCombo ) {
			InputParseResult res = new (true);

			for ( int i = inputCombo.Length - 1; i >= 0; i-- ) {
				KeyCode key = (KeyCode)inputCombo[i].InputCode;
				int id = GetID ( key );
				res.AllSkipped &= id < 0;
				if ( id < 0 ) continue;
				if ( Verbose )
					Owner.LogFcn?.Invoke ( $"  @i{i}: [{key}] (#{id}) = ( {inputCombo[i].ValueX} | Δ{inputCombo[i].DeltaX} )" );
				res.OrigVal[id] = inputCombo[i].ValueX - inputCombo[i].DeltaX >= DataHolder.PressThreshold;
				res.NewVal[id] = inputCombo[i].ValueX >= DataHolder.PressThreshold;

				if ( inputCombo[i].DeltaX < 0 ) { // finger is being released
					res.Pressed[id] = res.OrigVal[id];
					res.Released[id] = res.NewVal[id];
					res.IsAnyReleased = true;
				} else {
					res.Released[id] = res.OrigVal[id];
					res.Pressed[id] = res.NewVal[id];
				}
			}
			return res;
		}

		/// <inheritdoc/>
		public override bool ProcessInput ( DataHolder[] input ) {
			int Cnt = input == null ? -1 : input.Length;
			if ( Cnt < 1 ) return true;
			var mods = ReadModifiers ( input );
			if ( (mods & TriggerMod) < TriggerMod ) return true;

			var parsed = ParseInput ( input );

			if ( parsed.AllSkipped ) {
				if ( Verbose ) Owner.LogFcn?.Invoke ( $"No relevant keys in input, skipping." );
				return true;
			}

			string LF = Environment.NewLine;
			if ( Verbose )
				Owner.LogFcn?.Invoke ( $"Current state: {State}{LF}Presses: {Presses}{LF}Waiting for release: {WaitForRelease}{LF}{(parsed.IsAnyReleased ? "Some key is released" : "No key was released")}{LF}Finger state:{LF}\t\tOrigVal : {parsed.OrigVal.ToString ().PadLeft ( 5, '0' )}{LF}\tNewValue: {parsed.NewVal.ToString ().PadLeft ( 5, '0' )}{LF}\tPressed : {parsed.Pressed.ToString ().PadLeft ( 5, '0' )}{LF}\tReleased: {parsed.Released.ToString ().PadLeft ( 5, '0' )}" );

			if ( WaitForRelease ) {
				if ( parsed.Released.Value == 0 ) WaitForRelease = false;
				if ( Verbose ) Owner.LogFcn?.Invoke ( $"Waiting for release ({(parsed.Released.Value == 0 ? "Is Released" : "Still waiting")})" );
				return false;
			} else if ( parsed.IsAnyReleased ) {
				if ( parsed.Released.Value != 0 ) WaitForRelease = true;
				else if ( Verbose ) Owner.LogFcn?.Invoke ( $"Key pressed AND released, not waiting." );
				DateTime time = DateTime.Now;
				double msDiff = (time - LastPress).TotalMilliseconds;
				if ( State == StateType.Normal && Presses < 2 && msDiff < WaitTime && LastTapCombo == parsed.Pressed ) {
					Presses++;
					var ret = LastRet.Clone<InputData> ();
					ret.Cmnd = InputData.Command.Cancel;
					Callback ( ret );
					if ( Verbose ) Owner.LogFcn?.Invoke ( $"Pressed {parsed.Pressed.ToString ().PadLeft ( 5, '0' )}  {Presses}x times in {msDiff}ms after last press" );
				} else {
					LastTapCombo = parsed.Pressed;
					Presses = 0;
				}
				LastPress = time;

				if (State == StateType.Switch && parsed.Pressed.Value == 0x1FUL) {
					msDiff = 0;
				}
				LastRet = new InputData ( this );
				int mapID = 0;
				switch ( State ) {
				case StateType.Normal:
					switch ( mapping[0][LastTapCombo].key ) {
					case KeyCode.CapsLock: State = StateType.Shift; LastTapCombo.Reset (); return false;
					case KeyCode.NoName: State = StateType.Switch; LastTapCombo.Reset (); return false;
					}
					mapID = Presses;
					break;
				case StateType.Shift:
					switch ( mapping[0][LastTapCombo].key ) {
					case KeyCode.CapsLock: State = StateType.Normal; LastTapCombo.Reset (); return false;
					case KeyCode.NoName: State = StateType.Switch; LastTapCombo.Reset (); return false;
					default: State = StateType.Normal ; break;
					}
					mapID = 3;
					break;
				case StateType.Switch:
					switch ( mapping[0][LastTapCombo].key ) {
					case KeyCode.CapsLock: State = StateType.Switch; LastTapCombo.Reset (); return false;
					case KeyCode.NoName: State = StateType.Normal; LastTapCombo.Reset (); return false;
					}
					mapID = 4;
					break;
				}
				var key = mapping[mapID][LastTapCombo];
				LastRet.Key = key.key;
				LastRet.Modifiers = key.mod;
				LastRet.X = 1;
				LastRet.Y = LastRet.Z = 0;
				LastRet.Cmnd = InputData.Command.Type;
				LastRet.DeviceID = input[0].HookInfo.DeviceID;
				if ( Verbose )
					Owner.LogFcn?.Invoke ( $"Key released, firing callback with mapped key {key.key} and modifier {key.mod}." );
				FireCallback ( LastRet.Clone<InputData> () );
				if ( key.key == KeyCode.Back & key.mod == ModE.Ctrl ) {
					FireCallback ( LastRet.Clone<InputData> () );
					FireCallback ( LastRet.Clone<InputData> () );
					FireCallback ( LastRet.Clone<InputData> () );
				}
				return false;
			} else {
				if ( Verbose ) Owner.LogFcn?.Invoke ( $"No key released, skipping." );
				return false;
			}
		}

		public override StateInfo Info => new VStateInfo ( this );
		public class VStateInfo : DStateInfo {
			public readonly string[] TriggerKeys;
			public readonly string TriggerModifier;
			public readonly string ActState;
			public readonly string LastReturn;

			public VStateInfo ( VTapperInput owner ) : base ( owner ) {
				int N = TriggerKeys.Length;
				TriggerKeys = new string[N];
				for (int i = 0; i < N; i++ )
					TriggerKeys[i] = owner.TriggerKeys[i].ToString ();
				TriggerModifier = owner.TriggerMod.ToString ();
				ActState = $"{owner.Presses}x{owner.LastTapCombo}({owner.State})";
				LastReturn = $"{owner.LastRet} ({owner.LastPress.ToLongTimeString ()})";
			}
			public override string AllInfo () => $"{base.AllInfo ()}{BR}Trigger Modifiers: {TriggerModifier}{BR}Trigger keys:{BR}\t{string.Join ( BR + '\t', TriggerKeys )}{BR}Act State: {ActState}{BR}Last Returned: {LastReturn}";
		}
	}
}