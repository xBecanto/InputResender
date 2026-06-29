using Components.Interfaces;
using Components.Library;
using Components.Library.ComponentSystem;
using InputResender.Commands;
using System.Linq;

namespace Components.Implementations.UserApps;
public class VTapperLearner : DCommand<DMainAppCore> {
	private VTapperInput Tapper;
	private string LearnedText = "";
	private string[] Triggers;
	private char[] DefaultKeys;
	private int LearnedTextPosition = 0;
	private KeyCode ShiftKey, SwitchKey;
	private Dictionary<char, List<(byte, byte)[]>> MappingsChar = [];
	private Dictionary<(KeyCode, InputData.Modifier), List<(byte, byte)[]>> MappingsKey = [];
	private string LastFeedback = "";
	private bool LastWasCorrect = true;
	private bool ShowAllMappings = false;
	private int DynamicHelpDisplayMode = 0; // 0=X/~, 1=trigger keys, 2=default mappings
	private ComponentUIParametersInfo CachedUIInfo = null;

	public override int ComponentVersion => 1;
	public override string Description => "Helps users learn to use the Tapper chord-input method by guiding them character by character through a practice text.";

	private static List<string> CommandNames = ["TapLearn"];
	private static List<(string, Type)> InterCommands = [
		("assign", null),
		("mapping", null),
		("rip", null),
		("learn", null),
	];

	public VTapperLearner ( DMainAppCore owner, string parentDsc = null )
		: base ( owner, parentDsc, CommandNames, InterCommands ) { }

	private void Setup ( VTapperInput tapper ) {
		Tapper = tapper;
		var triggers = Tapper.GetTriggerKeys ();
		ShiftKey = triggers.Item2;
		SwitchKey = triggers.Item3;
		List<string> TriggerList = [];
		foreach ( var trigger in triggers.Item1 ) TriggerList.Add ( trigger.ToString () );
		TriggerList.Add ( triggers.Item2.ToString () );
		TriggerList.Add ( triggers.Item3.ToString () );
		TriggerList.Add ( triggers.Item4.ToString () );
		Triggers = TriggerList.ToArray ();

		int[] DefaultMappingIDs = [0b10000, 0b01000, 0b00100, 0b00010, 0b00001];
		DefaultKeys = (from mappingID in DefaultMappingIDs select Tapper.GetMapping ( 0, mappingID ).key.ToString ().FirstOrDefault ( '∅' )).Reverse ().ToArray ();

		// Notify UI to refresh with new trigger info
		CachedUIInfo?.NotifyDataChanged ();
	}

	public void Learn ( string text ) {
		LearnedText = text;
		LearnedTextPosition = 0;
		CachedUIInfo?.NotifyDataChanged ();
	}

	public void Reset () {
		LearnedTextPosition = 0;
		CachedUIInfo?.NotifyDataChanged ();
	}

	protected override CommandResult ExecIner ( CommandProcessor<DMainAppCore>.CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
			"assign" => CallName + " assign: Fetch VTapperInput component and assign it to this command.",
			"mapping" => CallName + " mapping <Char> (<MapName> <Combo>)...: Set a key mapping entry.\n\tChar: What character will be written\n\tMapName: {Single|Double|Triple|Shift|Switch}\n\tCombo: 5-char pattern of X and - (e.g. -X-X- means keys 2 and 4 pressed)",
			"rip" => CallName + " rip: Rip all simple mappings from the current TapperInput processor and store them in this component.",
			"learn" => CallName + " learn <text>: Start a learning session with the given text.",
			_ => null
		}, out var helpRes ) ) return helpRes;

		switch ( context.SubAction ) {
		case "assign": {
			DMainAppCore core = context.CmdProc.GetVar<DMainAppCore> ( CoreManagerCommand<DMainAppCore>.ActiveCoreVarName );
			if ( core == null ) return new ( "No active core found." );
			var proc = core.Fetch<VTapperInput> ();
			Setup ( proc );
			return new ( "VTapperInput assigned." );
		}
		case "mapping": {
			if ( Tapper == null )
				return new ErrorCommandResult (
					new ( "No Tapper processor assigned! Call 'assign' first." )
					, new NullReferenceException ( "Missing reference to VTapperInputCommand component" ) );
			char Ch = context.Args.String ( context.ArgID + 1, "Written character", 1, true )[0];
			//string mapName = context.Args.String ( context.ArgID + 2, "MapName", 1, true );
			List<(byte, byte)> mappings = [];
			for ( int argId = context.ArgID + 2; argId < context.Args.ArgC; argId += 2 ) {
				string mapId = context.Args.String ( argId, "MapName", 1, true );
				string comboStr = context.Args.String ( argId + 1, "Combo", 5, true );
				// VTapperInput.MappingNames
				int bMapId = VTapperInput.MappingNames.IndexOf ( mapName => mapId.Equals ( mapName, StringComparison.OrdinalIgnoreCase ) );
				int combo = VTapperInputCommand.ParseComboPattern ( comboStr );
				if ( bMapId < 0 ) return new ( $"Unknown mapping name '{mapId}'." );
				mappings.Add ( ((byte)bMapId, (byte)combo) );
			}
			MappingsChar[Ch] = [mappings.ToArray ()];
			return new ( $"Mapping for character '{Ch}' set." );
		}
		case "rip": {
			if ( Tapper == null )
				return new ErrorCommandResult (
					new ( "No Tapper processor assigned! Call 'assign' first." )
					, new NullReferenceException ( "Missing reference to VTapperInputCommand component" ) );
			for ( int mapID = 0; mapID < 5; mapID++ ) {
				for ( int combo = 0; combo <= 0b11111; combo++ ) {
					var map = Tapper.GetMapping ( mapID, combo );
					char Ch = '\0';
					int key = (int)map.key;
					if ( key >= '0' && key <= '9' && map.mod == InputData.Modifier.None )
						Ch = (char)key;
					else if ( key >= 'A' && key <= 'Z' ) {
						Ch = map.mod == InputData.Modifier.Shift ? (char)key : (char)(key + 32); // lowercase
					}

					(byte, byte)[] step = [((byte)mapID, (byte)combo)];
					if ( Ch != '\0' ) {
						if ( !MappingsChar.ContainsKey ( Ch ) ) MappingsChar[Ch] = [step];
						MappingsChar[Ch].Add ( step );
					} else {
						var keyTuple = (map.key, map.mod);
						if ( !MappingsKey.ContainsKey ( keyTuple ) ) MappingsKey[keyTuple] = [step];
						MappingsKey[keyTuple].Add ( step );
					}
				}
			}
			return new ( $"Mappings ripped from current TapperInput processor. There are now {MappingsChar.Count} character mappings and {MappingsKey.Count} key mappings." );
		}
		case "learn": {
			if ( Tapper == null )
				return new ErrorCommandResult (
					new ( "No Tapper processor assigned! Call 'assign' first." )
					, new NullReferenceException ( "Missing reference to VTapperInputCommand component" ) );
			string text = context.Args.String ( context.ArgID + 1, "Text to learn", 1, true );
			Learn ( text );
			return new ( $"Learning session started with text: '{text}'." );
		}
		default: return new ( $"Unknown sub-action '{context.SubAction}'." );
		}
	}

	private static bool[] ToChord ( byte combo ) => new bool[VTapperInput.MappingCount] {
		(combo & 0b00001) > 0,
		(combo & 0b00010) > 0,
		(combo & 0b00100) > 0,
		(combo & 0b01000) > 0,
		(combo & 0b10000) > 0,
	};

	private bool[] FindMapSwitchCombo ( KeyCode key, byte currentMap ) {
		if ( !MappingsKey.TryGetValue ( (key, InputData.Modifier.None), out var mappings ) )
			return null;
		List<bool[]> options = [];
		foreach ( var mapping in mappings ) {
			foreach ( var (mapID, combo) in mapping ) {
				if ( mapID != currentMap ) continue;
				options.Add ( ToChord ( combo ) );
			}
		}
		if ( options.Count == 0 ) return null;
		if ( options.Count == 1 ) return options[0];
		// return the 'easiest', i.e. one with the least number of True values:
		return options.MinBy ( option => option.Count ( b => b ) );
	}

	private bool[][][] GetHelp ( char C ) {
		if ( !MappingsChar.TryGetValue ( C, out var mappings ) )
			return null;
		List<bool[][]> options = [];
		foreach ( var mapping in mappings ) {
			int currentMap = (int)Tapper.State;
			List<bool[]> steps = [];
			foreach ( var (mapID, combo) in mapping ) {
				if ( mapID != currentMap ) {
					switch ( mapID ) {
					case 3: // Shift
						steps.Add ( FindMapSwitchCombo ( ShiftKey, (byte)currentMap ) );
						currentMap = mapID;
						break;
					case 4: // Switch
						steps.Add ( FindMapSwitchCombo ( SwitchKey, (byte)currentMap ) );
						currentMap = mapID;
						break;
					case 0:
					case 1:
					case 2:
						switch ( currentMap ) { // Return from shift/switch to normal state first
							case 3:
								steps.Add ( FindMapSwitchCombo ( ShiftKey, (byte)currentMap ) );
								break;
							case 4:
								steps.Add ( FindMapSwitchCombo ( ShiftKey, (byte)currentMap ) );
								break;
						}
						for (int i = 0; i < currentMap; i++)
							steps.Add ( ToChord ( combo ) );
						currentMap = 0;
						break;
					}
				}
				steps.Add ( ToChord ( combo ) );
			}
			options.Add ( steps.ToArray () );
		}
		return options.ToArray ();
	}

	/// <summary>Process a typed character from InputData and check if it matches the expected character</summary>
	/// <returns>True if processing was successful (whether correct or incorrect), false if should be ignored</returns>
	public bool ProcessTypedKey ( InputData data ) {
		// Only process Type commands
		if ( data.Cmnd != InputData.Command.Type ) {
			return false;
		}

		// Check if we have text to learn
		if ( string.IsNullOrEmpty ( LearnedText ) ) {
			LastFeedback = "No text loaded for learning!";
			LastWasCorrect = false;
			return true;
		}

		// Check if we've finished the text
		if ( LearnedTextPosition >= LearnedText.Length ) {
			LastFeedback = "✓ Text completed! Reset to start again.";
			LastWasCorrect = true;
			return true;
		}

		// Get expected character
		char expected = LearnedText[LearnedTextPosition];

		// Try to determine what character was typed
		char typed = '\0';
		int key = (int)data.Key;

		// Check for simple alphanumeric characters
		if ( key >= '0' && key <= '9' && data.Modifiers == InputData.Modifier.None ) {
			typed = (char)key;
		} else if ( key >= 'A' && key <= 'Z' ) {
			typed = data.Modifiers == InputData.Modifier.Shift ? (char)key : (char)(key + 32); // lowercase
		} else if ( data.Key == KeyCode.Space && data.Modifiers == InputData.Modifier.None ) {
			typed = ' ';
		}
		// Additional special characters could be mapped here based on KeyCode
		// For now, we'll match based on the mapping system if we don't have a direct match

		// Check if typed character matches expected
		if ( typed == expected ) {
			LearnedTextPosition++;
			LastFeedback = $"✓ Correct! '{expected}'";
			LastWasCorrect = true;

			if ( LearnedTextPosition >= LearnedText.Length ) {
				LastFeedback = "✓✓✓ Text completed! Excellent work!";
			}
		} else {
			// Character mismatch
			if ( typed != '\0' ) {
				LastFeedback = $"✗ Incorrect. Expected '{expected}', got '{typed}'";
			} else {
				LastFeedback = $"✗ Incorrect. Expected '{expected}', got key {data.Key}+{data.Modifiers}";
			}
			LastWasCorrect = false;
		}

		// Notify UI to refresh
		CachedUIInfo?.NotifyDataChanged ();

		return true;
	}

	public override ComponentUIParametersInfo GetUIDescription () {
		if ( CachedUIInfo != null )
			return CachedUIInfo;

		CachedUIInfo = BuildUIDescription ();
		return CachedUIInfo;
	}

	private ComponentUIParametersInfo BuildUIDescription () {
		// --- Current character display ---
		var currentChar = new UI_TextField.Factory ()
			.WithName ( "CurrentChar" )
			.WithLabel ( "Current Character" )
			.WithDescription ( "The character the user is currently expected to type. Underscores highlight it for visual clarity." )
			.WithPureUpdater ( () => LearnedText.Length == 0
				? "_?_"
				: $"_{LearnedText[Math.Min ( LearnedTextPosition, LearnedText.Length - 1 )]}_" )
			.Build ();

		var progressInfo = new UI_TextField.Factory ()
			.WithName ( "ProgressInfo" )
			.WithLabel ( "Progress" )
			.WithDescription ( "How far into the learned text the user currently is." )
			.WithPureUpdater ( () => LearnedText.Length == 0
				? "No text loaded"
				: $"{LearnedTextPosition} / {LearnedText.Length}" )
			.Build ();

		var feedbackField = new UI_TextField.Factory ()
			.WithName ( "Feedback" )
			.WithLabel ( "Feedback" )
			.WithDescription ( "Shows whether the last typed character was correct or incorrect." )
			.WithPureUpdater ( () => string.IsNullOrEmpty ( LastFeedback )
				? "Type a character to begin..."
				: LastFeedback )
			.Build ();

		var sep1 = new UI_Separator.Factory ()
			.WithName ( "Sep1" )
			.WithLabel ( "Help Section" )
			.WithDescription ( "Separator between current character and help section" )
			.AsHeader ()
			.Build ();

		// --- Static trigger keys display ---
		var staticTriggerKeys = new UI_TextField.Factory ()
			.WithName ( "StaticTriggers" )
			.WithLabel ( "Trigger Keys" )
			.WithDescription ( "The keys used as triggers for the Tapper input. These are set when Setup() is called." )
			.WithPureUpdater ( () => Triggers == null || Triggers.Length == 0
				? "Not configured yet"
				: string.Join ( " ", Triggers ) )
			.Build ();

		var staticDefaultMappings = new UI_TextField.Factory ()
			.WithName ( "StaticDefaults" )
			.WithLabel ( "Single-Key Outputs" )
			.WithDescription ( "What each single trigger key produces when pressed alone." )
			.WithPureUpdater ( () => {
				if ( Triggers == null || Triggers.Length < 5 || DefaultKeys == null || DefaultKeys.Length < 5 )
					return "Not configured yet";
				return string.Join ( " ", Enumerable.Range ( 0, 5 ).Select ( i => $"{Triggers[i]}→{DefaultKeys[i]}" ) );
			} )
			.Build ();

		// --- Collapsable all mappings display ---
		var toggleMappingsBtn = new UI_ActionButton.Factory ()
			.WithName ( "ToggleMappings" )
			.WithLabel ( ShowAllMappings ? "▼ Hide All Mappings" : "► Show All Mappings" )
			.WithDescription ( "Toggle display of all registered character and key mappings." )
			.WithOnClick ( () => {
				ShowAllMappings = !ShowAllMappings;
				CachedUIInfo?.NotifyDataChanged ();
			} )
			.Build ();

		var allMappingsContent = new UI_ListView.Factory ()
			.WithName ( "AllMappings" )
			.WithLabel ( "All Registered Mappings" )
			.WithDescription ( "List of all character and key mappings registered in this component." )
			.WithPureUpdater ( () => {
				if ( !ShowAllMappings ) return [];

				var lines = new List<string> ();
				lines.Add ( $"Character mappings: {MappingsChar.Count}" );
				foreach ( var kvp in MappingsChar.OrderBy ( x => x.Key ) ) {
					lines.Add ( $"  '{kvp.Key}': {kvp.Value.Count} option(s)" );
				}
				lines.Add ( "" );
				lines.Add ( $"Key mappings: {MappingsKey.Count}" );
				foreach ( var kvp in MappingsKey.OrderBy ( x => x.Key.Item1 ) ) {
					lines.Add ( $"  {kvp.Key.Item1}+{kvp.Key.Item2}: {kvp.Value.Count} option(s)" );
				}
				return lines;
			} )
			.UpdatedBy ( toggleMappingsBtn )
			.Build ();

		var sep1b = new UI_Separator.Factory ()
			.WithName ( "Sep1b" )
			.WithLabel ( "Dynamic Help" )
			.WithDescription ( "Separator for dynamic help section" )
			.AsMinor ()
			.Build ();

		// --- Dynamic help for current character ---
		var dynamicHelpModeSel = new UI_DropDown.Factory ()
			.WithName ( "DynamicHelpMode" )
			.WithLabel ( "Display Mode" )
			.WithDescription ( "Choose how to display the chord sequences: X/~, trigger keys, or default key mappings." )
			.WithInitialValue ( (DynamicHelpDisplayMode, new List<string> { "X / ~", "Trigger Keys", "Default Mappings" }) )
			.WithSelectionAcceptor ()
			.Build<UI_DropDown> ();

		var dynamicHelpContent = new UI_ListView.Factory ()
			.WithName ( "DynamicHelp" )
			.WithLabel ( "How to Type Current Character" )
			.WithDescription ( "Step-by-step chord sequence to type the current expected character." )
			.WithCombinedUpdater ( ( param, oldVal ) => {
				// Update display mode from dropdown
				var dropdown = dynamicHelpModeSel;
				DynamicHelpDisplayMode = dropdown.Value.selID;

				if ( string.IsNullOrEmpty ( LearnedText ) || LearnedTextPosition >= LearnedText.Length )
					return ["No character to type"];

				char charToType = LearnedText[LearnedTextPosition];
				var helpOptions = GetHelp ( charToType );

				if ( helpOptions == null || helpOptions.Length == 0 )
					return [$"No mapping found for '{charToType}'"];

				// Use first option
				var steps = helpOptions[0];
				var lines = new List<string> { $"To type '{charToType}':" };

				for ( int stepIdx = 0; stepIdx < steps.Length; stepIdx++ ) {
					var fingers = steps[stepIdx];
					if ( fingers == null ) {
						lines.Add ( $"  Step {stepIdx + 1}: (mapping error)" );
						continue;
					}

					string stepDisplay = $"  Step {stepIdx + 1}: ";
					var fingerDisplay = new List<string> ();

					for ( int f = 0; f < 5; f++ ) {
						string display = fingers[f] ? "?" : " ";

						if ( fingers[f] ) {
							switch ( DynamicHelpDisplayMode ) {
							case 0: // X / ~
								display = "X";
								break;
							case 1: // Trigger keys
								display = Triggers != null && f < Triggers.Length ? Triggers[f] : "?";
								break;
							case 2: // Default mappings
								display = DefaultKeys != null && f < DefaultKeys.Length ? DefaultKeys[f].ToString () : "?";
								break;
							}
						} else {
							display = "~";
						}

						fingerDisplay.Add ( display );
					}

					lines.Add ( stepDisplay + string.Join ( " ", fingerDisplay ) );
				}

				return lines;
			} )
			.UpdatedBy ( dynamicHelpModeSel )
			.UpdatedBy ( currentChar )
			.UpdatedBy ( progressInfo )
			.Build ();

		var sep2 = new UI_Separator.Factory ()
			.WithName ( "Sep2" )
			.WithLabel ( "Separator" )
			.WithDescription ( "Separator between help and control buttons" )
			.AsLine ()
			.Build ();

		// --- Navigation / control buttons ---
		var revertOneBtn = new UI_ActionButton.Factory ()
			.WithName ( "RevertOne" )
			.WithLabel ( "← Revert 1" )
			.WithDescription ( "Move one character back in the learned text (does not delete any real input)." )
			.WithOnClick ( () => {
				if ( LearnedTextPosition > 0 ) {
					LearnedTextPosition--;
					CachedUIInfo?.NotifyDataChanged ();
				}
			} )
			.Build ();

		var revertFiveBtn = new UI_ActionButton.Factory ()
			.WithName ( "RevertFive" )
			.WithLabel ( "←←← Revert 5" )
			.WithDescription ( "Move five characters back in the learned text." )
			.WithOnClick ( () => {
				LearnedTextPosition = Math.Max ( 0, LearnedTextPosition - 5 );
				CachedUIInfo?.NotifyDataChanged ();
			} )
			.Build ();

		var resetBtn = new UI_ActionButton.Factory ()
			.WithName ( "Reset" )
			.WithLabel ( "Reset & New Text" )
			.WithDescription ( "Reset progress to the beginning and generate a new learned text." )
			.WithOnClick ( Reset )
			.Build ();

		return new ComponentUIParametersInfo.Factory ()
			.WithDefaultID ()
			.WithComponentType ( GetType () )
			.WithName ( "Tapper Learner" )
			.WithDescription ( "Helps users learn to use the Tapper chord-input method by guiding them character by character through a practice text." )
			.AddParameters (
				currentChar,
				progressInfo,
				feedbackField,
				sep1,
				staticTriggerKeys,
				staticDefaultMappings,
				toggleMappingsBtn,
				allMappingsContent,
				sep1b,
				dynamicHelpModeSel,
				dynamicHelpContent,
				sep2,
				revertOneBtn,
				revertFiveBtn,
				resetBtn
			)
			.Build () as ComponentUIParametersInfo;
	}


	public override StateInfo Info => throw new System.NotImplementedException ();
}