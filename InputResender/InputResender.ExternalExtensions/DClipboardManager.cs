using System;
using System.Collections.Generic;
using System.Windows;
using MdxLibs.Core;

namespace InputResender.ExternalExtensions;
public abstract class DClipboardManager : ComponentBase_CoreBase {
	public DClipboardManager ( CoreBase owner ) : base ( owner ) { }

	protected sealed override IReadOnlyList<(string opCode, Type opType)> AddCommands () => new List<(string opCode, Type opType)> () {
			(nameof(SetText), typeof(void)),
			(nameof(GetText), typeof(string)),
			(nameof(StoreGeneric), typeof(void)),
			(nameof(RestoreGeneric), typeof(void))
		};

	public abstract void SetText ( string text );
	public abstract string GetText ();
	public abstract void StoreGeneric ();
	public abstract void RestoreGeneric ();

	public abstract class DStateInfo : StateInfo {
		public readonly string StoredText;
		public DStateInfo ( DClipboardManager owner ) : base ( owner ) => StoredText = owner.GetText ();
		public override string AllInfo () => $"{base.AllInfo ()}{BR}Stored Text: {StoredText}";
	}
}

public class MClipboardManager : DClipboardManager {
	private string storedText = null;
	private object tempData = null;
	public MClipboardManager ( CoreBase owner ) : base ( owner ) { }
	public override int ComponentVersion => 1;

	public override void SetText ( string text ) => Clipboard.SetText ( text );
	public override string GetText () => Clipboard.GetText ();


	public override void RestoreGeneric () {
		if ( tempData == null ) return;
		Clipboard.SetData ( DataFormats.Dib, tempData );
		tempData = null;
	}
	public override void StoreGeneric () {
		if ( tempData != null )
			throw new InvalidOperationException ( "Clipboard already stored. Restore it before storing again." );
		tempData = Clipboard.GetData ( DataFormats.Dib );
	}

	public override StateInfo Info => new VStateInfo ( this );
	public class VStateInfo : DStateInfo {
		public readonly object TempData;

		public VStateInfo ( MClipboardManager owner ) : base ( owner ) => TempData = owner.tempData;

		public override string AllInfo () => $"{base.AllInfo ()}{BR}Temp Data: {(TempData != null ? "Stored" : "None")}: {TempData}";
	}
}