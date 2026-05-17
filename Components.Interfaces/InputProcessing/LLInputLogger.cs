using Components.Library;
using System.Collections.Generic;
using System.Drawing;

namespace Components.Interfaces; 
public static class LLInputLogger {
	private static HtmlLogWriter logger;
	private static Dictionary<nint, int> HookIDMapping = [];

	public static CInputLLParser Parser;

	private const string SysCls = "sysMsg";
	private const string NewHookCls = "newHook";
    private const string SeenCls = "seen";
	private const string IsSenderCls = "isSender";
	private const string MaskNotMatchCls = "maskNotMatch";
	private const string FromHookCls = "fromHook";
	private const string FromProbeCls = "fromProbe";
	private const string StoredCls = "stored";
	private const string NewInputCls = "newInput";
	private const string DoneHookCls = "doneHook";
	private const string StartTestCls = "startTest";
	private const string NotifierCls = "notifier";
	private const string ErrorCls = "err";

	private static int msgID = 123;

	//public static Func<int, IntPtr, IntPtr, (ProbeHook.Status, VKChange, KeyCode, uint, nint)> DefaultParser;

	static LLInputLogger () {
		logger = new HtmlLogWriter ();
		/*
     .seen, .isSender, .maskNotMatch, .fromHook, .fromProbe { height: 0.2em; transform: scaleY(0.2); }
     .seen { color: black; opacity: 0.25; }
     .isSender { color: green; opacity: 0.30; }
     .maskNotMatch { color: rosybrown; opacity: 0.50; }
     .fromHook { color: darkblue; opacity: 0.50; }
     .fromProbe { color: lightblue; opacity: 0.90; }
     .stored { color: darkblue; font-weight: bold; }

     .doneHook { height: 0.8em; transform: scaleY(0.8); opacity: 0.4; }
     .newHook { padding-top: 1.8em; border-bottom: black 1px solid; }
     .sys { color: purple;}
     .startTest { color: maroon;}
     .notifier { color: darkorange;}
     .newInput { margin-top: 0.5em; border-top: maroon 1px dashed; }
		*/
		logger.PushStyle ( '.' + string.Join ( ", .", [SeenCls, IsSenderCls, MaskNotMatchCls, FromHookCls, FromProbeCls] ), ("height", "0.2em"), ("transform", "scaleY(0.2)") );
		logger.PushStyleColor ( '.' + SeenCls, "black", 0.25 );
		logger.PushStyleColor ( '.' + IsSenderCls, "green", 0.30 );
		logger.PushStyleColor ( '.' + MaskNotMatchCls, "rosybrown", 0.50 );
		logger.PushStyleColor ( '.' + FromHookCls, "darkblue", 0.50 );
		logger.PushStyleColor ( '.' + FromProbeCls, "lightblue", 0.90 );
		logger.PushStyle ( '.' + StoredCls, ("color", "darkblue"), ("font-weight", "bold") );
		logger.PushStyle ( '.' + DoneHookCls, ("height", "0.8em"), ("transform", "scaleY(0.8)"), ("opacity", "0.4") );
		logger.PushStyle ( '.' + NewInputCls, ("margin-top", "0.5em"), ("border-top", "maroon 1px dashed") );
		logger.PushStyle ( '.' + NewHookCls, ("padding-top", "1.8em"), ("border-bottom", "black 1px solid") );
		logger.PushStyle ( '.' + SysCls, ("color", "purple") );
		logger.PushStyle ( '.' + StartTestCls, ("color", "maroon") );
		logger.PushStyle ( '.' + NotifierCls, ("color", "darkorange") );
		logger.PushStyle('.' + ErrorCls, ("color", "red"), ("font-weight", "bold") );

		Color[] hookColorsRGB = [
			Color.Orange,
			Color.Pink,
			Color.Gold,
			Color.Purple,
			Color.Black,
			Color.Blue,
			Color.Green,
			Color.FromArgb(0x7B, 0x1B, 0x38), // Bordeaux
			Color.Cyan,
			Color.Brown,
			Color.Red,
			Color.Salmon,
			Color.Lavender,
			];
		for ( int m = 8; m > 5; m-- ) {
			// Use darker calars for higher IDs to make enough colors available
			for ( int i = 0; i < hookColorsRGB.Length; i++ ) {
				int id = (8 - m) * hookColorsRGB.Length + i;
				Color clr = hookColorsRGB[i];
				byte R = (byte)((m - 1) * 256 / m + clr.R / m);
				byte G = (byte)((m - 1) * 256 / m + clr.G / m);
				byte B = (byte)((m - 1) * 256 / m + clr.B / m);
				logger.PushStyleBackground ( $".hook{id}", $"#{R:X2}{G:X2}{B:X2}" );
				// It would be nice to have some gradient representing type of message, compementing the color bound to hook ID
			}
		}

		//string[] hookColors = [ "lightsalmon", "lightcyan",
		//	"lavenderblush", "honeydew", "lightyellow",
		//	"thistle", "beige", "cornsilk" ];
	}

	//[System.Diagnostics.Conditional ( "STORE_LL_LOGS" )]
	public static void AssertLLData ( int nCode, IntPtr wParam, IntPtr lParam, char identifier
		, CInputLLParser.InputEventInfoAssertor expected, bool doThrow = false ) {
		if ( Parser == null ) return;
		var info = Parser.Parse ( nCode, wParam, lParam );
		expected.Assert ( info );
		////(ProbeHook.Status Bstatus, VKChange Bchange, KeyCode Bkey, uint Btime, nint BeID) = DefaultParser ( nCode, wParam, lParam );
		//List<string> strings = [];
		//// Test for discrepancies between expected and actual values
		//if ( tStatus.HasValue && Bstatus != tStatus.Value )
		//	strings.Add ( $"Expected status {tStatus}, but got {Bstatus}" );
		//if ( tChange.HasValue && Bchange != tChange.Value )
		//	strings.Add ( $"Expected change {tChange}, but got {Bchange}" );
		//if ( tKey.HasValue && Bkey != tKey.Value )
		//	strings.Add ( $"Expected key {tKey}, but got {Bkey}" );
		//if ( tTime.HasValue && Btime != tTime.Value )
		//	strings.Add ( $"Expected time {tTime}, but got {Btime}" );
		//if ( tExtraInfo.HasValue && BeID != tExtraInfo.Value )
		//	strings.Add ( $"Expected extra info {tExtraInfo}, but got {BeID}" );

		//// If there are any discrepancies, throw an exception or log the message
		//if ( strings.Count > 0 ) {
		//	string msg = $"Asserted LL data: nCode={nCode}, wParam={wParam}, lParam={lParam} => {Bstatus}, {Bchange}, {Bkey}, {Btime}, {BeID} || {string.Join ( " | ", strings )}";
		//	if ( doThrow ) throw new InvalidOperationException ( msg.ToString () );
		//	else Log ( IntPtr.Zero, 'A', msg.ToString () );
		//} else if ( !doThrow ) Log ( IntPtr.Zero, 'A', $"Asserted LL data: nCode={nCode}, wParam={wParam}, lParam={lParam} => {Bstatus}, {Bchange}, {Bkey}, {Btime}, {BeID}" );

		//// Otherwise, log the successful assertion (if throwing status is used, just exit quietly)
		//if ( doThrow ) Log ( IntPtr.Zero, 'A', $"Asserted LL data: nCode={nCode}, wParam={wParam}, lParam={lParam} => {Bstatus}, {Bchange}, {Bkey}, {Btime}, {BeID}" );
		//else Log ( IntPtr.Zero, 'A', $"Asserted LL data: nCode={nCode}, wParam={wParam}, lParam={lParam} => {Bstatus}, {Bchange}, {Bkey}, {Btime}, {BeID}" );
	}

	public static nint[] keywordIDs = { 0, 42, 69, 420, 1337, 666, 1234, 7734 };

	public static bool AllowLogging = true;
	/// <summary>To be used only for debbugging purposes, when you want to log the LL events.
	/// <para>To enable logging, use '#define STORE_LL_LOGS' in the project file.</para>
	[System.Diagnostics.Conditional ( "STORE_LL_LOGS" )]
	public static void Log ( nint hook, char identifier, string msg ) {
		if ( msg == null || !AllowLogging ) return;

		if ( keywordIDs.Contains ( hook ) )  LLInputLogger.SystemMsg ( msg, identifier );
		else LLInputLogger.Msg ( hook, identifier, msg );
	}

	public static void SystemMsg (string msg, char identifier) {
		lock (logger) {
			string cls = SysCls;
			if ( msg.Contains ( " as it is sender" ) ) cls = IsSenderCls;
			else if ( msg.Contains ( "Simulating input: " ) ) cls = NewInputCls;
			else if ( msg.Contains ( " Testing InputSimulation by " ) ) cls = StartTestCls;
			else if ( msg.Contains ( "New hook added" ) ) cls = NewHookCls;
			if ( identifier == 'E' ) cls += ' ' + ErrorCls;
			logger.PushLine ( cls, msgID++.ToShortCode () + " :: " + identifier + " - " + msg );
		}
	}
	public static void Msg ( nint hookID, char identifier, string msg ) {
		lock ( logger ) {
			int mappedID = MapID ( hookID );
			string hookClass = $"hook{mappedID}";
			if ( msg.Contains ( "Unhooked LL hook" ) || msg.Contains ( "Unhooked hook" ) ) {
				foreach ( var element in logger.Body.Children ) {
					if ( element.Class == null ) continue;
					if ( element.Class.Contains ( hookClass ) ) {
						element.Class += " " + DoneHookCls;
						//chronoSB.Replace ( hookID, $"-!!-{hookID}--" );
						element.Content.Replace ( $"#{mappedID}: ", $"-!!-#{mappedID}--: " );
					}
				}
				logger.PushLine ( hookClass + " " + DoneHookCls, $"{msgID++.ToShortCode ()} :: #{mappedID}: {msg}" );
			} else if ( msg.StartsWith ( "Already seen event" ) && logger.Last.Content.Contains ( $" :: #{mappedID}: New probe LL capture" ) ) {
				logger.Last.Content += " -- Already seen event, skipping";
				logger.Last.Class += " " + SeenCls;
			} else if ( msg.StartsWith ( "Event stored. " ) && logger.Last.Content.Contains ( $" :: #{mappedID}: Probe converted event: " ) ) {
				logger.Last.Content += " -- " + msg;
				logger.Last.Class += " " + StoredCls;
			} else if ( msg.StartsWith ( "Processing already in progress" ) && logger.Last.Content.Contains ( $" :: #{mappedID}: New probe LL capture" ) ) {
				logger.Last.Content += " -- Already processing, skipping";
				logger.Last.Class += " " + SeenCls;
			} else if ( msg.StartsWith ( "Not notifying other probes" ) && logger.Last.Content.Contains ( $" :: #{mappedID}: New probe LL capture" ) ) {
				logger.Last.Content += " -- " + msg;
				logger.Last.Class += " " + NotifierCls;
			} else if ( msg.StartsWith ( "Key mask doesn't match" ) && logger.Last.Content.Contains ( $" :: #{mappedID}: Probe converted event: " ) ) {
				logger.Last.Content += " -- " + msg;
				logger.Last.Class += " " + MaskNotMatchCls;
			} else {
				if ( msg.StartsWith ( "Already seen event" ) ) hookClass += " " + SeenCls;
				else if ( msg.StartsWith ( "Event stored. " ) ) hookClass += " " + StoredCls;
				else if ( msg.StartsWith ( "Processing already in progress" ) ) hookClass += " " + SeenCls;
				else if ( msg.StartsWith ( "Not notifying other probes" ) ) hookClass += " " + NotifierCls;
				else if ( msg.StartsWith ( "Key mask doesn't match" ) ) hookClass += " " + MaskNotMatchCls;
				else if ( identifier == 'E' ) hookClass += " " + ErrorCls;

				logger.PushLine ( hookClass, $"{msgID++.ToShortCode ()} :: {identifier}#{mappedID}: {msg}" );
			}
		}
	}

	public static string AsString () {
		lock ( logger ) {
			if ( logger.Body.Children.Count == 0 ) return "No input events logged";
			return logger.ToString ();
		}
	}

	private static int MapID(nint id) {
		int ret = -1;
		if ( LLInputLogger.keywordIDs.Contains ( id ) ) return (int)id;
		if ( HookIDMapping.TryGetValue ( id, out ret ) ) return ret;
		ret = HookIDMapping.Count + 2; // Rnd added to exclude ID #0
		HookIDMapping.Add ( id, ret );
		SystemMsg ( $"New hook added: {id} => #{ret}", 'L' );
		return ret;
	}
}