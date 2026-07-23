//#define VirtConsole
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Components.Implementations;
using Components.Interfaces;
using Components.Library;
using InputResender.WindowsGUI.Commands;

namespace InputResender.WindowsGUI;
internal static class Program {
	[STAThread]
	static void Main ( string[] args ) {
		List<string> Args = [];
		foreach ( var arg in args) {
			if ( Args.Count > 0 && Args[^1].EndsWith ( '+' ) && arg.StartsWith ( '+' ) )
				Args[^1] = Args[^1].TrimEnd ( '+' ) + Environment.NewLine + arg.TrimStart ( '+' );
			else Args.Add ( arg );
		}

		args = Args.ToArray ();
#if VirtConsole
		ApplicationConfiguration.Initialize ();
		Application.Run ( new ConsoleWindow () );
#else
		DMainAppCore initalCore = DMainAppCoreFactory.CreateDefault ();
		ConsoleManager console = new ( Console.WriteLine, Console.ReadLine, Console.Write, Console.Clear, () => Console.ReadKey ( true ).KeyChar, OverwriteConsoleLine );
		console.WriteLine ( "Starting Windows version ..." );
		InputResender.CLI.Program.Main ( args, initalCore, new TopLevelLoader ( initalCore, console ), console );
#endif
	}

#if !VirtConsole
	static void OverwriteConsoleLine (string text) {
		if (Console.CursorTop > 0) Console.SetCursorPosition ( 0, Console.CursorTop - 1 );
		Console.WriteLine ( $"\r{new string ( ' ', Console.WindowWidth - 1 )}\r{text}" );
	}
#endif
}