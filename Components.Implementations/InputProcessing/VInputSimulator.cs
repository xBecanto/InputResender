using Components.Interfaces;
using Components.Library;

namespace Components.Implementations {
	public class VInputSimulator : DInputSimulator {
		public int SimulateDelay = 50;

		public VInputSimulator ( CoreBase newOwner ) : base ( newOwner ) {
		}

		public override int ComponentVersion => 1;

		public override HInputEventDataHolder[] ParseCommand ( InputData data ) => GetParser ( data.Cmnd ).Parse ( data );
		public override int Simulate ( params HInputEventDataHolder[] data ) {
			int ret = 0;
			var simulator = Owner.Fetch<DInputReader> ();
			if (simulator == null)
				Owner.PushDelayedError ( "Failed to fetch DInputReader component! Cannot simulate input without it!"
					, new NullReferenceException() );
			if ( Verbose )
				Owner.PushDelayedMsg ( "Requested simulating input of " + string.Join ( " | ", data.ToList () ) );
			foreach ( HInputEventDataHolder h in data ) {
				ret += (short)simulator.SimulateInput ( h, AllowRecapture );
				if ( SimulateDelay >= 0 ) System.Threading.Tasks.Task.Delay ( SimulateDelay ).Wait ();
			}
			return ret;
		}

		private SDInputCommandParser GetParser (InputData.Command cmnd) => (SDInputCommandParser)SubComponentFactory<DInputSimulator, InputData.Command>.Fetch ( this, cmnd );
	}

	public class SInputCommandParser_KeyDown : SDInputCommandParser {
		public SInputCommandParser_KeyDown ( DInputSimulator newOwner ) : base ( newOwner ) { }
		public readonly static SubComponentInfo<DInputSimulator, InputData.Command> SCInfo = new (
			( cmd ) => cmd == InputData.Command.KeyPress ? 1 : 0,
			( cmp, cmd ) => new SInputCommandParser_KeyDown ( cmp )
			);
		/*public override HInputEventDataHolder[] Parse ( InputData data ) =>
			new[] {
			new HKeyboardEventDataHolder ( data.Owner.Owner.Fetch<DInputReader> (), data.DeviceID, (int)data.Key, data.X, data.X )
			};*/
		public override HInputEventDataHolder[] Parse ( InputData data ) {
			var inputReader = data.Owner.Owner.Fetch<DInputReader> ();
			var events = new List<HInputEventDataHolder> ();

			// Add modifier key down events
			events.AddRange ( GenerateModifierEvents ( data.Modifiers, data.DeviceID, true, inputReader ) );

			// Add main key down event
			events.Add ( new HKeyboardEventDataHolder ( inputReader, data.DeviceID, (int)data.Key, data.X, data.X ) );

			// Add modifier key up events (in reverse order)
			var modUpEvents = GenerateModifierEvents ( data.Modifiers, data.DeviceID, false, inputReader );
			for ( int i = modUpEvents.Length - 1; i >= 0; i-- ) {
				events.Add ( modUpEvents[i] );
			}

			return events.ToArray ();
		}
	}
	public class SInputCommandParser_KeyUp : SDInputCommandParser {
		public SInputCommandParser_KeyUp ( DInputSimulator newOwner ) : base ( newOwner ) { }
		public readonly static SubComponentInfo<DInputSimulator, InputData.Command> SCInfo = new (
			( cmd ) => cmd == InputData.Command.KeyRelease ? 1 : 0,
			( cmp, cmd ) => new SInputCommandParser_KeyUp ( cmp )
			);
		public override HInputEventDataHolder[] Parse ( InputData data ) {
			var inputReader = data.Owner.Owner.Fetch<DInputReader> ();
			var events = new List<HInputEventDataHolder> ();

			// Add modifier key down events
			events.AddRange ( GenerateModifierEvents ( data.Modifiers, data.DeviceID, true, inputReader ) );

			// Add main key up event
			events.Add ( new HKeyboardEventDataHolder ( inputReader, data.DeviceID, (int)data.Key, 0, 0 - data.X ) );

			// Add modifier key up events (in reverse order)
			var modUpEvents = GenerateModifierEvents ( data.Modifiers, data.DeviceID, false, inputReader );
			for ( int i = modUpEvents.Length - 1; i >= 0; i-- ) {
				events.Add ( modUpEvents[i] );
			}

			return events.ToArray ();
		}
	}
	public class SInputCommandParser_Type : SDInputCommandParser {
		public SInputCommandParser_Type ( DInputSimulator newOwner ) : base ( newOwner ) { }
		public readonly static SubComponentInfo<DInputSimulator, InputData.Command> SCInfo = new (
			( cmd ) => cmd == InputData.Command.Type ? 1 : 0,
			( cmp, cmd ) => new SInputCommandParser_Type ( cmp )
			);
		public override HInputEventDataHolder[] Parse ( InputData data ) {
			var inputReader = data.Owner.Owner.Fetch<DInputReader> ();
			var events = new List<HInputEventDataHolder> ();

			// Add modifier key down events
			events.AddRange ( GenerateModifierEvents ( data.Modifiers, data.DeviceID, true, inputReader ) );

			// Add main key down event
			events.Add ( new HKeyboardEventDataHolder ( inputReader, data.DeviceID, (int)data.Key, data.X, data.X ) );

			// Add main key up event
			events.Add ( new HKeyboardEventDataHolder ( inputReader, data.DeviceID, (int)data.Key, 0, 0 - data.X ) );

			// Add modifier key up events (in reverse order)
			var modUpEvents = GenerateModifierEvents ( data.Modifiers, data.DeviceID, false, inputReader );
			for ( int i = modUpEvents.Length - 1; i >= 0; i-- ) {
				events.Add ( modUpEvents[i] );
			}

			return events.ToArray ();
		}
	}
}
