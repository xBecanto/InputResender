using System;
using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using MdxLibs.Core;
using MdxLibs.CoreTests;
using Xunit;
using Xunit.Abstractions;

namespace InputResender.DefinitionTests.InputProcessing {
	public abstract class DInputProcessorTest : ComponentTestBase<DInputProcessor> {
		public DInputProcessorTest ( ITestOutputHelper outputHelper ) : base ( outputHelper ) { }
		List<InputData> ProcessedInputs = new List<InputData> ();

		public override CoreBase CreateCoreBase () {
			var ret = new CoreBaseMock ();
			new MInputMerger ( ret );
			new MInputReader ( ret );
			return ret;
		}

		public static HKeyboardEventDataHolder KeyPress ( ComponentBase owner, KeyCode key, VKChange press, int deviceID = 1 ) {
			HHookInfo hookInfo = new ( owner, deviceID, press );
			float val = 0, delta = 0;
			switch ( press ) {
			case VKChange.KeyDown: val = 1; delta = 1; break;
			case VKChange.KeyUp: val = 0; delta = -1; break;
			}
			return new HKeyboardEventDataHolder ( owner, hookInfo, (int)key, val, delta );
		}

		[Fact]
		public void EmptyInputToEmptyOutput () {
			TestObject.Callback = ProcessedCallback;
			TestObject.ProcessInput ( null );
			Thread.Sleep ( 1 );
			ProcessedInputs.Should ().BeEmpty ();
		}
		private void ProcessedCallback (InputData data) {
			ProcessedInputs.Add ( data );
		}

		[Fact]
		public void CustomModifierRecognized () {
			var input = new[] {
				KeyPress (OwnerCore.Fetch<DInputReader> (), KeyCode.E, VKChange.KeyDown),
				KeyPress (OwnerCore.Fetch<DInputReader> (), KeyCode.D1, VKChange.KeyDown),
				KeyPress (OwnerCore.Fetch<DInputReader> (), KeyCode.LShiftKey, VKChange.KeyDown)
			};
			TestObject.SetCustomModifier ( KeyCode.D1, InputData.Modifier.CustMod1 );
			TestObject.ReadModifiers ( input ).Should ().Be ( InputData.Modifier.CustMod1 | InputData.Modifier.Shift );
			TestObject.SetCustomModifier ( KeyCode.D1, InputData.Modifier.None );
			TestObject.ReadModifiers ( input ).Should ().Be ( InputData.Modifier.Shift );
		}
		[Fact]
		public void ChangingSystemModifierThrows_InvalidOperationException () {
			Action actChange = () => TestObject.SetCustomModifier ( KeyCode.ShiftKey, InputData.Modifier.Alt );
			Action actRemove = () => TestObject.SetCustomModifier ( KeyCode.ShiftKey, InputData.Modifier.None );
			actChange.Should ().Throw<InvalidOperationException> ();
			actRemove.Should ().Throw<InvalidOperationException> ();
		}
	}

	public class MInputProcessorTest : DInputProcessorTest {
		public MInputProcessorTest ( ITestOutputHelper outputHelper ) : base ( outputHelper ) { }
		public override MInputProcessor GenerateTestObject () => new MInputProcessor ( OwnerCore );


	}

	public class InputDataTest : SerializableDataHolderTestBase<InputData, ComponentBase> {
		public InputDataTest ( ITestOutputHelper outputHelper ) : base ( outputHelper ) { }

		public override ComponentBase CreateTestObjOwnerComp ( CoreBase core ) => new ComponentMock ( core );

		public override InputData GenerateTestObject ( int variant ) {
			if ( variant < 0 ) return InputData.Empty ( OwnerComp );
			else return new InputData ( OwnerComp ) {
				Cmnd = variant % 2 == 1 ? InputData.Command.KeyRelease : InputData.Command.KeyPress,
				Key = (KeyCode)(variant / 2),
				DeviceID = 1,
				Modifiers = (InputData.Modifier)(variant / 8),
				X = 1 - variant % 2
			};
		}

		public override System.Collections.Generic.List<InputData> GetTestData () {
			var ret = new System.Collections.Generic.List<InputData> ();
			for ( int i = -2; i < 12; i++ ) ret.Add ( GenerateTestObject ( i ) );
			return ret;
		}
	}
}