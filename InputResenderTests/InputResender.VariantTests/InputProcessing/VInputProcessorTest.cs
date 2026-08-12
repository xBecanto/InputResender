using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using InputResender.DefinitionTests.InputProcessing;
using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using InputResender.Variants.InputProcessing;
using Xunit;
using Xunit.Abstractions;

namespace InputResender.VariantTests.InputProcessing {
	public class VInputProcessorTest ( ITestOutputHelper outputHelper ) : DInputProcessorTest ( outputHelper ) {
		private readonly List<InputData> ProcessedInputs = new List<InputData> ();

		public override VInputProcessor GenerateTestObject () => new VInputProcessor ( OwnerCore );

		private void ProcessedCallback ( InputData data ) => ProcessedInputs.Add ( data );

		private HInputEventDataHolder[] CreateInput ( params KeyCode[] keys ) =>
			keys.Select ( key => (HInputEventDataHolder)KeyPress ( OwnerCore.Fetch<DInputReader> (), key, VKChange.KeyDown ) ).ToArray ();

		[Fact]
		public void ProcessingEnabledFalse_ForcesSkip () {
			var processor = (VInputProcessor)TestObject;
			processor.ProcessingEnabled = false;
			TestObject.Callback = ProcessedCallback;

			var result = TestObject.ProcessInput ( CreateInput ( KeyCode.E ) );

			result.Should ().Be ( DHookManager.ConsumingStatus.Skip );
			ProcessedInputs.Should ().BeEmpty ();
		}

		[Fact]
		public void ProcessingEnabledFalse_ForcesSkipWithoutInteraction () {
			var processor = (VInputProcessor)TestObject;
			processor.ProcessingEnabled = false;
			processor.Toggle = KeyCode.Tab;
			processor.OnRelease = KeyCode.B;
			TestObject.Callback = ProcessedCallback;

			var result = TestObject.ProcessInput ( CreateInput ( KeyCode.E ) );
			result &= TestObject.ProcessInput ( CreateInput ( KeyCode.B ) );
			result &= TestObject.ProcessInput ( CreateInput ( KeyCode.C ) );
			result &= TestObject.ProcessInput ( CreateInput ( KeyCode.Tab ) );
			result &= TestObject.ProcessInput ( CreateInput ( KeyCode.Space ) );
			result &= TestObject.ProcessInput ( CreateInput ( KeyCode.Tab ) );
			result &= TestObject.ProcessInput ( CreateInput ( KeyCode.B ) );
			result &= TestObject.ProcessInput ( CreateInput ( KeyCode.E ) );

			result.Should ().Be ( DHookManager.ConsumingStatus.Skip );
			ProcessedInputs.Should ().BeEmpty ();
		}

		[Fact]
		public void ShouldConsume_ControlsReturnValueAfterProcessing () {
			var processor = (VInputProcessor)TestObject;
			processor.ShouldConsume = DHookManager.ConsumingStatus.Passthrough;
			TestObject.Callback = ProcessedCallback;

			var result = TestObject.ProcessInput ( CreateInput ( KeyCode.E ) );
			result.Should ().Be (DHookManager.ConsumingStatus.Passthrough);
			ProcessedInputs.Should ().ContainSingle ();

			ProcessedInputs.Clear ();
			processor.ShouldConsume = DHookManager.ConsumingStatus.Consume;
			result = TestObject.ProcessInput ( CreateInput ( KeyCode.E ) );
			result.Should ().Be (DHookManager.ConsumingStatus.Consume);
			ProcessedInputs.Should ().ContainSingle ();
		}

		[Fact]
		public void ToggleKey_TogglesProcessingState () {
			var processor = (VInputProcessor)TestObject;
			processor.Toggle = KeyCode.Tab;
			TestObject.Callback = ProcessedCallback;
			var toggleInput = new HInputEventDataHolder[] { KeyPress ( OwnerCore.Fetch<DInputReader> (), KeyCode.Tab, VKChange.KeyDown ) };

			var result = TestObject.ProcessInput ( CreateInput ( KeyCode.A ) );
			result.Should ().Be ( DHookManager.ConsumingStatus.Passthrough );
			ProcessedInputs.Should ().ContainSingle ();
			ProcessedInputs[0].Key.Should ().Be ( KeyCode.A );
			ProcessedInputs.Clear ();

			result = TestObject.ProcessInput ( toggleInput );
			result.Should ().Be ( DHookManager.ConsumingStatus.Consume );
			ProcessedInputs.Should ().BeEmpty ();

			result = TestObject.ProcessInput ( CreateInput ( KeyCode.E ) );
			result.Should ().Be ( DHookManager.ConsumingStatus.Skip );
			ProcessedInputs.Should ().BeEmpty ();

			result = TestObject.ProcessInput ( toggleInput );
			result.Should ().Be ( DHookManager.ConsumingStatus.Consume );
			// Even when set to 'passthrough', consume the switching input event (might later on be setup separatetly)
			ProcessedInputs.Should ().BeEmpty ();

			result = TestObject.ProcessInput ( CreateInput ( KeyCode.E ) );
			result.Should ().Be ( DHookManager.ConsumingStatus.Passthrough );
			ProcessedInputs.Should ().ContainSingle ();
			ProcessedInputs[0].Key.Should ().Be ( KeyCode.E );
		}

		[Fact]
		public void OnHold_OnlyProcessesWhileTargetKeyIsHeld () {
			var processor = (VInputProcessor)TestObject;
			processor.OnHold = KeyCode.ShiftKey;
			TestObject.Callback = ProcessedCallback;

			var blockedInput = new HInputEventDataHolder[] {
				KeyPress ( OwnerCore.Fetch<DInputReader> (), KeyCode.E, VKChange.KeyDown ),
				KeyPress ( OwnerCore.Fetch<DInputReader> (), KeyCode.ShiftKey, VKChange.KeyUp )
			};
			var result = TestObject.ProcessInput ( blockedInput );
			result.Should ().Be ( DHookManager.ConsumingStatus.Skip );
			ProcessedInputs.Should ().BeEmpty ();

			ProcessedInputs.Clear ();
			var allowedInput = new HInputEventDataHolder[] {
				KeyPress ( OwnerCore.Fetch<DInputReader> (), KeyCode.E, VKChange.KeyDown ),
				KeyPress ( OwnerCore.Fetch<DInputReader> (), KeyCode.ShiftKey, VKChange.KeyDown )
			};
			result = TestObject.ProcessInput ( allowedInput );
			result.Should ().Be (DHookManager.ConsumingStatus.Passthrough);
			ProcessedInputs.Should ().ContainSingle ();
			ProcessedInputs[0].Key.Should ().Be ( KeyCode.E );
		}

		[Fact]
		public void OnRelease_OnlyProcessesWhenTargetKeyIsNotHeld () {
			var processor = (VInputProcessor)TestObject;
			processor.OnRelease = KeyCode.ShiftKey;
			TestObject.Callback = ProcessedCallback;

			var blockedInput = new HInputEventDataHolder[] {
				KeyPress ( OwnerCore.Fetch<DInputReader> (), KeyCode.E, VKChange.KeyDown ),
				KeyPress ( OwnerCore.Fetch<DInputReader> (), KeyCode.ShiftKey, VKChange.KeyDown )
			};
			var result = TestObject.ProcessInput ( blockedInput );
			result.Should ().Be (DHookManager.ConsumingStatus.Skip);
			ProcessedInputs.Should ().BeEmpty ();

			ProcessedInputs.Clear ();
			var allowedInput = new HInputEventDataHolder[] {
				KeyPress ( OwnerCore.Fetch<DInputReader> (), KeyCode.E, VKChange.KeyDown ),
				KeyPress ( OwnerCore.Fetch<DInputReader> (), KeyCode.ShiftKey, VKChange.KeyUp )
			};
			result = TestObject.ProcessInput ( allowedInput );
			result.Should ().Be (DHookManager.ConsumingStatus.Passthrough);
			ProcessedInputs.Should ().ContainSingle ();
			ProcessedInputs[0].Key.Should ().Be ( KeyCode.E );
		}

		[Fact]
		public void Remap_ChangesOutputKey () {
			var processor = (VInputProcessor)TestObject;
			processor.Remap[KeyCode.E] = KeyCode.F;
			TestObject.Callback = ProcessedCallback;

			var result = TestObject.ProcessInput ( CreateInput ( KeyCode.E ) );

			result.Should ().Be (DHookManager.ConsumingStatus.Passthrough);
			ProcessedInputs.Should ().ContainSingle ();
			ProcessedInputs[0].Key.Should ().Be ( KeyCode.F );
		}
	}
}