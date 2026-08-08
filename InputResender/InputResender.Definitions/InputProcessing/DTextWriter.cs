using System;
using System.Collections.Generic;
using MdxLibs.Core;
using MdxLibs.Definitions;

namespace InputResender.Definitions.InputProcessing {
	public abstract class DTextWriter : ComponentBase_CoreBase {
		protected DTextWriter ( CoreBase newOwner ) : base ( newOwner ) { }

		protected override IReadOnlyList<(string opCode, Type opType)> AddCommands () => new List<(string opCode, Type opType)> () {
				(nameof(Text), typeof(string)),
				(nameof(Type), typeof(bool)),
				(nameof(Undo), typeof(void)),
				(nameof(Redo), typeof(void)),
				(nameof(Clear), typeof(void)),
			};

		public abstract string Text { get; }
		public abstract bool Type ( InputData input );
		public abstract void Undo ();
		public abstract void Redo ();
		public abstract void Clear ();

		public abstract class DStateInfo : StateInfo {
			public DStateInfo ( DTextWriter owner ) : base ( owner ) {
				Text = owner.Text;
			}

			protected abstract string[] GetUndoList ();
			protected abstract string[] GetRedoList ();
			public readonly string[] UndoList, RedoList;
			public readonly string Text;
			public override string AllInfo () => $"{base.AllInfo ()}{BR}Text:{BR}{Text}{BR}Undo list:{BR}{string.Join ( BR, UndoList )}{BR}Redo list:{BR}{string.Join ( BR, RedoList )}";
		}
	}
}
