using System.Collections.Generic;
using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using MdxLibs.Core;
using DataHolder = InputResender.Definitions.InputProcessing.HInputEventDataHolder;

namespace InputResender.Variants.InputProcessing {
	public class VInputMerger : DInputMerger {
		protected List<DataHolder> eventList;

		public VInputMerger ( CoreBase owner ) : base ( owner ) {
			eventList = [];
		}

		public override int ComponentVersion => 1;
		public override int MemoryCount => eventList.Count;

		public override void ClearMemory () => eventList.Clear ();

		public override DataHolder[] ProcessInput ( DataHolder inputData ) {
			// The current system is very primitive, working only on time-insensitive binary input.
			if ( inputData == null ) return [];
			KeyCode keyCode = (KeyCode)inputData.InputCode;
			DataHolder lastDuplicate = RemoveDuplicates ( inputData );
			List<DataHolder> ret = null;
			if (lastDuplicate == null)
				ret = [(DataHolder)inputData.Clone ()];
			else {
				lastDuplicate.SetNewValue ( inputData.ValueX, inputData.ValueY, inputData.ValueZ );
				ret = [(DataHolder)lastDuplicate.Clone ()];
			}
			for (int i = 0; i < eventList.Count; i++) {
				var data = eventList[i].Clone<DataHolder> ();
				data.SetNewValue ( data.ValueX, data.ValueY, data.ValueZ );
				ret.Add ( data );
			}

			if ( ret[0].Pressed < 1 ) {
				// If key is released
				if (keyCode.IsModifier()) {
					RemoveEvents ( keyCode );
				} else {
					RemoveEvents ( keyCode );
				}
			} else {
				// If key is pressed
				if ( keyCode.IsModifier () ) {
					eventList.Add ( ret[0] );
				} else {
					eventList.Insert ( 0, ret[0] );
				}
			}

			if (Verbose && Owner.LogFcn != null) {
				var SB = new System.Text.StringBuilder ();
				SB.Append ( $" -- {nameof ( ProcessInput )}:" );
				foreach ( var data in ret ) {
					char mark = '.';
					if (data.DeltaX == 0 && data.ValueX < DataHolder.PressThreshold) mark = '_';
					else if (data.DeltaX == 0 && data.ValueX >= DataHolder.PressThreshold) mark = '*';
					else if (data.DeltaX > 0) mark = '^';
					else if (data.DeltaX < 0) mark = 'v';
					SB.Append ( $"  {mark}{(KeyCode)data.InputCode}" );
				}
				Owner.LogFcn ( SB.ToString () );
			}
			return ret.ToArray ();
		}

		private void RemoveEvents (KeyCode keyCode) {
			for ( int i = 0; i < eventList.Count; i++ ) {
				if ( eventList[i].InputCode != (int)keyCode ) continue;
				eventList.RemoveAt ( i );
				i--;
			}
		}

		private DataHolder RemoveDuplicates (DataHolder inputData) {
			DataHolder lastDuplicate = null;
			KeyCode keyCode = (KeyCode)inputData.InputCode;
			for ( int i = eventList.Count - 1; i >= 0; i-- ) {
				// Removing events this way also removes the information about how long is something being pressed.
				if ( eventList[i].InputCode != (int)keyCode ) continue;
				if ( eventList[i].ValueX != inputData.ValueX ) continue;
				if ( eventList[i].ValueY != inputData.ValueY ) continue;
				if ( eventList[i].ValueZ != inputData.ValueZ ) continue;
				lastDuplicate = eventList[i];
				eventList.RemoveAt ( i );
			}

			return lastDuplicate;
		}

		public override StateInfo Info => new VStateInfo ( this );
		public class VStateInfo : DStateInfo {
			public new VInputMerger Owner => (VInputMerger)base.Owner;
			public VStateInfo ( VInputMerger owner ) : base ( owner ) { }

			protected override string[] GetBufferedEvents () {
				int N = Owner.eventList.Count;
				string[] ret = new string[N];
				for ( int i = 0; i < N; i++ )
					ret[i] = Owner.eventList[i].ToString ();
				return ret;
			}
		}
	}
}