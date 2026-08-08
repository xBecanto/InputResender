using FluentAssertions;
using InputResender.Definitions.InputProcessing;
using InputResender.DefinitionTests.InputProcessing;
using InputResender.Services;
using InputResender.Variants.InputProcessing;
using Xunit;
using Xunit.Abstractions;

namespace InputResender.VariantTests.InputProcessing {
	public class VInputMergerTest : DInputMergerTest {
		public VInputMergerTest ( ITestOutputHelper outputHelper ) : base ( outputHelper ) { }
		public override DInputMerger GenerateTestObject () => new VInputMerger ( OwnerCore );

		[Fact]
		public void NullDataReturnsEmpty () {
			TestObject.ProcessInput ( null ).Should ().NotBeNull ().And.BeEmpty ();
		}

		[Fact]
		public void PressReleaseLeavesEmptyMemory () {
			var events = GenerateInputData ( KeyCode.E );
			TestObject.ClearMemory ();
			Process ( events[0], true, events[0] );
			Process ( events[1], true, events[1], events[0] );
			TestObject.MemoryCount.Should ().Be ( 0 );
		}

		[Fact]
		public void PressReleaseModifier () {
			var events = GenerateInputData ( KeyCode.Shift );
			TestObject.ClearMemory ();
			Process ( events[0], true, events[0] );
			Process ( events[1], true, events[1], events[0] );
			TestObject.MemoryCount.Should ().Be ( 0 );
		}

		[Fact]
		public void PressModifierAndKey () {
			var events = GenerateInputData ( KeyCode.E, KeyCode.Shift );
			TestObject.ClearMemory ();
			// Press Shift, Press E, Release E, Release Shift
			Process ( events[2], true, events[2] );
			Process ( events[0], true, events[0], events[2] );
			Process ( events[1], true, events[1], events[0], events[2] );
			Process ( events[3], true, events[3], events[2] );
			TestObject.MemoryCount.Should ().Be ( 0 );
		}

		[Fact]
		public void ModifierKeyModifierKey () {
			var pressE = GenerateInputData ( KeyCode.E, true );
			var releaseE = GenerateInputData ( KeyCode.E, false );
			var pressQ = GenerateInputData ( KeyCode.Q, true );
			var releaseQ = GenerateInputData ( KeyCode.Q, false );
			var pressShift = GenerateInputData ( KeyCode.Shift, true );
			var releaseShift = GenerateInputData ( KeyCode.Shift, false );
			var pressAlt = GenerateInputData ( KeyCode.Alt, true );
			var releaseAlt = GenerateInputData ( KeyCode.Alt, false );
			TestObject.ClearMemory ();

			Process ( pressShift, true, pressShift );
			Process ( pressE, true, pressE, pressShift );
			Process ( pressAlt, true, pressAlt, pressE, pressShift );
			Process ( releaseE, true, releaseE, pressE, pressShift, pressAlt );
			Process ( pressQ, true, pressQ, pressShift, pressAlt );
			Process ( releaseShift, true, releaseShift, pressQ, pressShift, pressAlt );
			Process ( releaseAlt, true, releaseAlt, pressQ, pressAlt );
			Process ( releaseQ, true, releaseQ, pressQ );
			TestObject.MemoryCount.Should ().Be ( 0 );
		}
	}
}