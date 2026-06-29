using Components.Library;

namespace Components.Interfaces {
	public abstract class DInputSimulator : ComponentBase<CoreBase> {
		public bool AllowRecapture { get; set; } = false;
		public virtual bool Verbose { get; set; } = false;

		/// <summary>Maps modifier flags to their corresponding keycodes. Used when parsing InputData to generate modifier key events.</summary>
		protected Dictionary<InputData.Modifier, KeyCode> ModifierMapping = new Dictionary<InputData.Modifier, KeyCode> {
			{ InputData.Modifier.Shift, KeyCode.LShiftKey },
			{ InputData.Modifier.Ctrl, KeyCode.LControlKey },
			{ InputData.Modifier.Alt, KeyCode.Alt },
			{ InputData.Modifier.AltGr, KeyCode.RMenu },
			{ InputData.Modifier.WinKey, KeyCode.LWin },
		};

		protected DInputSimulator ( CoreBase newOwner ) : base ( newOwner ) { }

		protected sealed override IReadOnlyList<(string opCode, Type opType)> AddCommands () => new List<(string opCode, Type opType)> () {
				(nameof(ParseCommand), typeof(HInputEventDataHolder[])),
				(nameof(Simulate), typeof(int)),
				("get_"+nameof(AllowRecapture), typeof(bool)),
				("set_"+nameof(AllowRecapture), typeof(void)),
				("get_"+nameof(Verbose), typeof(bool)),
				("set_"+nameof(Verbose), typeof(void)),
				(nameof(SetModifierMapping), typeof(void)),
				(nameof(GetModifierMapping), typeof(KeyCode?)),
				(nameof(GetAllModifierMappings), typeof(IReadOnlyDictionary<InputData.Modifier, KeyCode>)),
			};

		public abstract HInputEventDataHolder[] ParseCommand ( InputData data );
		public abstract int Simulate ( params HInputEventDataHolder[] data );

		/// <summary>Set the keycode that should be used for a specific modifier when parsing commands.</summary>
		public void SetModifierMapping ( InputData.Modifier modifier, KeyCode keyCode ) {
			// Don't allow setting None modifier
			if ( modifier == InputData.Modifier.None )
				throw new ArgumentException ( "Cannot set mapping for Modifier.None", nameof(modifier) );

			// Allow clearing a mapping by setting to None
			if ( keyCode == KeyCode.None ) {
				ModifierMapping.Remove ( modifier );
			} else {
				ModifierMapping[modifier] = keyCode;
			}
		}

		/// <summary>Get the keycode mapped to a specific modifier, or null if not mapped.</summary>
		public KeyCode? GetModifierMapping ( InputData.Modifier modifier ) {
			return ModifierMapping.TryGetValue ( modifier, out var keyCode ) ? keyCode : null;
		}

		/// <summary>Get all currently configured modifier mappings.</summary>
		public IReadOnlyDictionary<InputData.Modifier, KeyCode> GetAllModifierMappings () {
			return ModifierMapping;
		}

		public override StateInfo Info => new DStateInfo ( this );
		public class DStateInfo : StateInfo {
			public readonly bool AllowingRecapture;
			public DStateInfo ( DInputSimulator owner ) : base ( owner ) {
				AllowingRecapture = owner.AllowRecapture;
			}
			public override string AllInfo () => $"{base.AllInfo ()}{BR}Recapture={AllowingRecapture}";
		}
	}

	public abstract class SDInputCommandParser : SubComponentBase<DInputSimulator, InputData.Command> {
		protected SDInputCommandParser ( DInputSimulator newOwner ) : base ( newOwner ) { }
		public abstract HInputEventDataHolder[] Parse ( InputData data );

		/// <summary>
		/// Generate key events for modifiers. Returns an array of key events.
		/// </summary>
		/// <param name="modifiers">The modifiers to generate events for</param>
		/// <param name="deviceID">Device ID for the events</param>
		/// <param name="keyDown">True to generate key down events, false for key up</param>
		/// <param name="inputReader">The input reader to use as the owner of events</param>
		protected HInputEventDataHolder[] GenerateModifierEvents ( InputData.Modifier modifiers, int deviceID, bool keyDown, ComponentBase inputReader ) {
			if ( modifiers == InputData.Modifier.None )
				return Array.Empty<HInputEventDataHolder> ();

			var events = new List<HInputEventDataHolder> ();

			// Process each modifier flag
			foreach ( InputData.Modifier mod in Enum.GetValues ( typeof ( InputData.Modifier ) ) ) {
				if ( mod == InputData.Modifier.None ) continue;
				if ( !modifiers.HasFlag ( mod ) ) continue;

				var keyCode = Owner.GetModifierMapping ( mod );
				if ( keyCode == null ) continue; // Skip unmapped modifiers

				if ( keyDown ) {
					events.Add ( new HKeyboardEventDataHolder ( inputReader, deviceID, (int)keyCode.Value, 1, 1 ) );
				} else {
					events.Add ( new HKeyboardEventDataHolder ( inputReader, deviceID, (int)keyCode.Value, 0, -1 ) );
				}
			}

			return events.ToArray ();
		}
	}
}