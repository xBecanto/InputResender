using System;
using System.Collections.Generic;
using MdxLibs.Core;
using MdxLibs.Definitions;

namespace InputResender.ExternalExtensions;
public class ClipboardManagerCommand : DCommand_CoreBase {
	private static List<string> CommandNames = ["clipboard"];
	private static List<(string, Type)> InterCommands = [
		("setText", typeof( string )),
		("getText", typeof( string )),
		("storeGeneric", typeof( void )),
		("restoreGeneric", typeof( void )),
		("printGeneric", typeof( void )),
	];
	public override string Description => "Manages the clipboard, allowing to set and get text, as well as store and restore the clipboard content in a generic way.";
	public ClipboardManagerCommand ( CoreBase owner, string parentDsc = null )
		: base ( owner, parentDsc, CommandNames, InterCommands ) {
	}

	protected override CommandResult ExecIner ( CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
			"setText" => $"{CallName} setText <Text>: Sets the clipboard text to the specified value.\n\tText: The text to set in the clipboard.",
			"getText" => $"{CallName} getText: Retrieves the current text from the clipboard.",
			"storeGeneric" => $"{CallName} storeGeneric: Stores the current clipboard content in a generic way, allowing it to be restored later.",
			"restoreGeneric" => $"{CallName} restoreGeneric: Restores the previously stored clipboard content if available.",
			"printGeneric" => $"{CallName} printGeneric: Prints the current generic captured data.",
			_ => null
		}, out var helpRes ) ) return helpRes;

		var core = GetCore<CoreBase> ( context );
		if ( core == null ) return new CommandResult ( "Core not found in context." );
		var clipboard = core.Fetch<DClipboardManager> ();
		clipboard ??= new MClipboardManager ( core );

		switch ( context.SubAction ) {
		case "setText": {
			string text = context.Args.String ( context.ArgID + 1, "Text" );
			if ( text == null ) return new CommandResult ( "Text cannot be empty." );
			clipboard.SetText ( text );
			return new CommandResult ( "Clipboard text set successfully." );
		}
		case "getText": {
			string text = clipboard.GetText ();
			return new CommandResult ( $"Clipboard text: {text}" );
		}
		case "storeGeneric": {
			clipboard.StoreGeneric ();
			return new CommandResult ( "Clipboard content stored successfully." );
		}
		case "restoreGeneric": {
			clipboard.RestoreGeneric ();
			return new CommandResult ( "Clipboard content restored successfully." );
		}
		case "printGeneric": {
			if ( clipboard is MClipboardManager mClipboard ) {
				var info = mClipboard.Info as MClipboardManager.VStateInfo;
				string tempDataStatus = info.TempData != null ? "Stored" : "None";
				return new CommandResult ( $"Generic Captured Data: {tempDataStatus}" );
			} else {
				return new CommandResult ( "Current clipboard manager does not support generic data capture." );
			}
		}
		default:
			return new CommandResult ( $"Unknown subcommand '{context.SubAction}'." );
		}
	}
}