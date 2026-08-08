using Xunit;
using Xunit.Abstractions;

namespace InputResender.UnitTests.SystemTests;
public class InputTransformationTest : BaseSystemTest {
	public InputTransformationTest ( ITestOutputHelper output )
		: base (
			output
			, "pipeline safemode off"
			, "core new comp mpacketsender"
			, "load sclModules"
			, "load joiners"
			, "hook manager start"
			, "hook pipeline minimum 2"
			, "hook manager verbosity 2"
			, "sim recapture T"
			) {
	}

	[Fact]
	public void ShowInputStatusInfo () {
		Test ( [
				"fm whitelist SIPtest.scl 0MPd3FUbKTIEiiJTg/O21VDW4fhqeuoflPP9oP2gbLQ=",
			"seclav parse SIPtest.scl",
			"SIP force",
			"SIP assign SIPtest.scl",
			"pipeline new InputProcess exact=SHookManager DInputMerger DInputProcessor",
			"hook add delayed -c Pipeline Keydown KeyUp",
			"sim keydown R",
			], [
				"ScriptedInputProcessor Status:"
				, "InputCombination Count: 1" // It would be nice if multiple results could be merged into groups, where results inside of group would be order sensitive, individual groups wouldn't have to be. This would also allow to skip lines, group would be like Line1, Line 5. Order enforecd, whatever between is skipped. This could expand possibilities of the 'exclusive' sensitivity.
			]
			, TestSensitivity.None
			, TestTimeout.Medium
			, "<ERROR>"
			, "Error processing '"
			, "Error in hook: "
			, "Could not parse line:"
			, "steps, which is below the configured minimum of "
		);
	}

	[Fact]
	public void ShowInputStatusInfoInline () {
		Test ( [
			@"seclav parse SIPtest.scl --inline=""
			@using BasicModule
			@using ScriptedInputProcessor

			@in SIP_Status_t sip_status
			PRINT_SIP_STATUS sip_status""",
			"SIP force",
			"SIP assign SIPtest.scl",
			"pipeline new InputProcess exact=SHookManager DInputMerger DInputProcessor",
			"hook add delayed -c Pipeline Keydown KeyUp",
			"sim keydown R",
			], [
				"ScriptedInputProcessor Status:"
				, "InputCombination Count: 1"
			]
			, TestSensitivity.None
			, TestTimeout.Short
			, "Error in hook: "
			, "Could not parse line:"
			, "steps, which is below the configured minimum of "
		);
	}

	[Fact]
	public void InputPassthrough () {
		Test ( [
			@"seclav parse SIPtest.scl --inline=""
			@using BasicModule
			@using ScriptedInputProcessor

			@in SIP_Status_t sip_status
			PRINT_SIP_STATUS sip_status
			String keyName = GET_SIP_KEY_NAME sip_status 0
			Int pressState = GET_SIP_KEY_STATUS sip_status keyName
			COMPARE_INT pressState 65534
			?< pressState = 0
			?> pressState = 1
			FIRE_KEY sip_status keyName pressState
			""",
			"SIP force",
			"SIP assign SIPtest.scl",
			"pipeline new InputProcess DInputReader DInputMerger DInputProcessor",
			"hook add delayed -c Print Keydown KeyUp",
			"sim keydown R",
			], [
				"Requested simulating input of 0.82 (R)[65K;0;0] Δ[65K;0;0]",
				"Processing event: 0.82 (R)[65K;0;0] Δ[65K;0;0]",
				"ScriptedInputProcessor Status:",
				"Firing key 'R' - Pressed: True",
				"Encountered Input Event: hook catched R (1) : 0:[KeyDown, KeyUp]",
			]
			, TestSensitivity.None
			, TestTimeout.Short
			, "Error in hook: "
			, "Could not parse line:"
			, "steps, which is below the configured minimum of "
		);
	}

	[Fact]
	public void MorseCode () {
		Test (
			[
			"safemode off",
			"""
			seclav parse SipMorse.scl --inline="
			@using BasicModule
			@using ScriptedInputProcessor
			
			@in SIP_Status_t sip_status
			@in Int SettingChanged
			@out Int ConsumeEvent

			String DotKey = \"E\"
			String DashKey = \"T\"
			String DelimiterKey = \"R\"
			SettingChanged = 1 # Enforce update of settings.

			 --> [Init] -sets-> Settings -e-> E -t-> T -n-> Init
			 COMPARE_INT SettingChanged 0
			 ?> emit sets
			 COMPARE_KEYS_3 sip_status DotKey DashKey DelimiterKey
			 ?A emit e; wait
			 ?B emit t; wait
			 FIRE_KEY sip_status \"Space\" 2

			--> Settings -n-> Init
			SettingChanged = 0
			SIP_SETUP sip_status 1 2000
			SIP_RESET_LISTENS sip_status
			SIP_ADD_LISTEN sip_status DotKey
			SIP_ADD_LISTEN sip_status DashKey
			SIP_ADD_LISTEN sip_status DelimiterKey
			emit n

			SIP_3Tree --> E -e-> I -t-> A # ·
			SIP_3Tree --> I -e-> S -t-> U # ··
			SIP_3Tree --> S -e-> H -t-> V # ···
			SIP_3Tree --> H -e-> 5 -t-> 4 # ····
			SIP_3Tree --> 4					# ·····
			SIP_3Tree --> 5					# ····-
			SIP_3Tree --> V        -t-> 3 # ···-
			SIP_3Tree --> 3					# ···--
			SIP_3Tree --> U -e-> F        # ··-
			SIP_3Tree --> F					# ··-·
			SIP_3Tree --> A -e-> R -t-> W # ·-
			SIP_3Tree --> R -e-> L        # ·-·
			SIP_3Tree --> L					# ·-··
			SIP_3Tree --> W -e-> P -t-> J # ·--
			SIP_3Tree --> P					# ·--·
			SIP_3Tree --> J        -T-> 1 # ·---
			SIP_3Tree --> 1					# ·----
			SIP_3Tree --> T -e-> N -t-> M # −
			SIP_3Tree --> N -e-> D -t-> K # −·
			SIP_3Tree --> D -e-> B -T-> X # -··
			SIP_3Tree --> B -e-> 6        # -···
			SIP_3Tree --> 6					# -····
			SIP_3Tree --> K -e-> C -t-> Y # −·−
			SIP_3Tree --> C					# −·−·
			SIP_3Tree --> Y					# −·−-
			SIP_3Tree --> M -e-> G -t-> O # −-
			SIP_3Tree --> G -e-> Z -t-> Q # −-·
			SIP_3Tree --> Q					# −-·-
			SIP_3Tree --> Z -e-> 7        # −-··
			SIP_3Tree --> 7					# −-···
			SIP_3Tree --> O -e-> Separator -t-> Decimal # −−-
			SIP_3Tree --> Separator -E-> 8        # −−-·
			SIP_3Tree --> 8					# −−-··
			SIP_3Tree --> Decimal   -E-> 9 -T-> 0 # −−--
			SIP_3Tree --> 9					# −−--·
			SIP_3Tree --> 0					# −−---
						"
			""",
			"SIP force",
			"SIP assign SipMorse.scl",
			"pipeline new InputProcess exact=SHookManager DInputMerger DInputProcessor",
			"hook add delayed -c Pipeline Keydown KeyUp",
			"sim keypress E E E E R", // H
			"sim keypress E R", // E
			"sim keypress E T E E R", // L
			"sim keypress E T E E R", // L
			"sim keypress T T T R", // O
			], [
				"Firing key 'H' - Pressed: True",
				"Firing key 'H' - Pressed: False",
				"Firing key 'E' - Pressed: True",
				"Firing key 'E' - Pressed: False",
				"Firing key 'L' - Pressed: True",
				"Firing key 'L' - Pressed: False",
				"Firing key 'L' - Pressed: True",
				"Firing key 'L' - Pressed: False",
				"Firing key 'O' - Pressed: True",
				"Firing key 'O' - Pressed: False",
			]
			, TestSensitivity.None
			, TestTimeout.Long
			, "Error in hook: "
			, "Could not parse line:"
			, "steps, which is below the configured minimum of "
		);
	}

	[Fact]
	public void ConditionalConsumingInput () {
		// This tests needs to be updated to work with the HookManager pipeline instead of the direct one.
		// Or actually, maybe the InputReader could be updated to also accept some return info about event consuming.
		// Anyway, this test must somehow pass on the information during 'fast CB' if to consume the event or not.
		Test ( [
				@"seclav parse SIPtest.scl --inline=""
@using BasicModule
@using ScriptedInputProcessor

@in SIP_Status_t sip_status
@in Int SettingChanged
@out Int ConsumeEvent

String DotKey = \""E\""
String DashKey = \""T\""
String ConditionKey = \""R\""
ConsumeEvent = 0

--> [Main]
COMPARE_KEYS_3 sip_status DotKey DashKey ConditionKey
?CA FIRE_KEY sip_status \""oemperiod\"" 2
?CB FIRE_KEY sip_status \""oemdash\"" 2
?CA ConsumeEvent = 0
?CB ConsumeEvent = 1
""",
				"SIP force",
				"SIP assign SIPtest.scl",
				"SIP safemode off",
				"pipeline new InputProcess exact=SHookManager DInputMerger DInputProcessor origin",
				"hook add fast Pipeline Keydown KeyUp",
				"sim keydown R",
				"sim keypress E",
				"sim keyup R",
				"sim keypress T",
			], [
				"Firing key 'oemPeriod' - Pressed: True",
				"Firing key 'oemPeriod' - Pressed: False",
			]
			, TestSensitivity.Order
			, TestTimeout.Short
			, "Firing key 'oemDash'"
			, "Firing key 'Dash'"
			, "Error in hook: "
			, "Could not parse line:"
			, "steps, which is below the configured minimum of "
			);
	}
}