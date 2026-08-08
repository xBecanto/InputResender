using System;
using InputResender.Services;
using MdxLibs.Core;
using MdxLibs.Definitions;
using MdxLibs.Services;

namespace InputResender.Definitions.InputProcessing {
	public class MTextWriter : DTextWriter {
		string text = "";
		DropOutStack<string> UndoList, RedoList;

		public MTextWriter ( CoreBase newOwner ) : base ( newOwner ) {
			UndoList = new DropOutStack<string> ( 64 );
			RedoList = new DropOutStack<string> ( 64 );
		}

		public override string Text => text;
		public override int ComponentVersion => 1;

		public override void Undo () { if ( UndoList.Count < 1 ) return; RedoList.Push ( text ); text = UndoList.Pop (); }
		public override void Redo () { if ( RedoList.Count < 1 ) return; UndoList.Push ( text ); text = RedoList.Pop (); }

		public override void Clear () { text = ""; UndoList.Clear (); RedoList.Clear (); }

		public override bool Type ( InputData input ) {
			if ( input == null || input.Cmnd == InputData.Command.None ) return false;
			if ( input.Cmnd == InputData.Command.Cancel ) {
				Undo ();
				return true;
			}
			if ( input.Cmnd != InputData.Command.Type && input.Cmnd != InputData.Command.KeyPress ) return false;

			InputData.Modifier mod = input.Modifiers;

			if (mod == InputData.Modifier.Ctrl) {
				switch (input.Key) {
				case KeyCode.Z: Undo (); return true;
				case KeyCode.Y: Undo (); return true;
				}
			}
			if (mod != InputData.Modifier.None & mod != InputData.Modifier.Shift ) return false;

			bool shift = mod == InputData.Modifier.Shift;
			char C = (char)input.Key;
			UndoList.Push ( text );
			RedoList.Clear ();

			if (C >= '0' & C <= '9' ) {
				if ( mod == InputData.Modifier.None ) { text += C; return true; }
				if ( shift ) {
					switch (C) {
					case '0': text += ")"; return true;
					case '1': text += "!"; return true;
					case '2': text += "@"; return true;
					case '3': text += "#"; return true;
					case '4': text += "$"; return true;
					case '5': text += "%"; return true;
					case '6': text += "^"; return true;
					case '7': text += "&"; return true;
					case '8': text += "*"; return true;
					case '9': text += "("; return true;
					}
				}
			}
			if ( C >= 'A' & C <= 'Z' ) {
				if ( mod == InputData.Modifier.Shift ) { text += C; return true; }
				if ( mod == InputData.Modifier.None ) { text += char.ToLower ( C ); return true; }
			}

			int N = text.Length;
			switch (input.Key) {
			case KeyCode.Back: if (N > 0) text = text.Substring (0, N - 1); return true;
			case KeyCode.Decimal: text += '.'; return true;
			case KeyCode.OemBackslash: text += shift ? '|' : '\\'; return true;
			case KeyCode.OemCloseBrackets: text += shift ? '}' : ']'; return true;
			case KeyCode.Oemcomma: text += shift ? '<' : ','; return true;
			case KeyCode.OemMinus: text += shift ? '_' : '-'; return true;
			case KeyCode.OemOpenBrackets: text += shift ? '{' : '['; return true;
			case KeyCode.OemPeriod: text += shift ? '>' : '.'; return true;
			case KeyCode.OemPipe: text += shift ? '|' : '\\'; return true;
			case KeyCode.Oemplus: text += shift ? '+' : '='; return true;
			case KeyCode.OemQuestion: text += shift ? '?' : '/'; return true;
			case KeyCode.OemQuotes: text += shift ? '"' : '\''; return true;
			case KeyCode.OemSemicolon: text += shift ? ':' : ';'; return true;
			case KeyCode.Oemtilde: text += shift ? '~' : '`'; return true;
			case KeyCode.Tab: text += '\t'; return true;
			case KeyCode.Space: text += ' '; return true;
			case KeyCode.LineFeed: text += Environment.NewLine; return true;
			}

			return false;
		}

		public override ComponentBase.StateInfo Info => new VStateInfo ( this );
		public class VStateInfo : DStateInfo {
			public new MTextWriter Owner => (MTextWriter)base.Owner;
			public VStateInfo ( MTextWriter owner ) : base ( owner ) { }
			protected override string[] GetRedoList () {
				string[] ret = new string[Owner.RedoList.Count];
				int ID = 0;
				foreach ( var act in Owner.RedoList )
					ret[ID++] = act;
				return ret;

			}
			protected override string[] GetUndoList () {
				string[] ret = new string[Owner.UndoList.Count];
				int ID = 0;
				foreach ( var act in Owner.UndoList )
					ret[ID++] = act;
				return ret;
			}
		}
	}
}