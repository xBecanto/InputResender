using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FluentAssertions;
using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using MdxLibs.Core;
using MdxLibs.CoreTests;
using MdxLibs.Services;
using Xunit;
using Xunit.Abstractions;

namespace InputResender.DefinitionTests.InputProcessing {
	public abstract class DInputReaderTest : ComponentTestBase<DInputReader> {
		protected List<HInputEventDataHolder> EventList;
		protected AutoResetEvent onInputReceived;

		protected DInputReaderTest ( ITestOutputHelper outputHelper ) : base ( outputHelper ) {
			EventList = new List<HInputEventDataHolder> ();
			onInputReceived = new AutoResetEvent ( false );
		}

		[Fact]
		public void SetupSimulateRelease_RaisesCallbackOnce () {
			HInputEventDataHolder inputData = GenerateKeyboardEvent ();
			ExecOnHook ( inputData, () =>
			TestObject.SimulateInput ( inputData, true ),
			true, true );
			EventList.Should ().HaveCount ( 1 );
			// No other callback should processed (some key presses during the test could be an issue).
			// Same input event as was simulated should be captured, but by value, not by reference (for hardware input the dataHolder will not be available and so it should be always recreated).
			EventList[0].Should ().Be ( inputData ).And.NotBeSameAs ( inputData );
		}

		[Fact]
		public void ReleaseNonexistingHookPassesWithNoChange () {
			TestObject.ReleaseHook ( GenerateHookInfo () ).Should ().Be ( 0 );
		}

		[Fact]
		public void SetupReleaseHook () {
			HInputEventDataHolder inputData = GenerateKeyboardEvent ();
			var hooks = TestObject.SetupHook ( inputData.HookInfo, SimpleTestCallback, null );
			hooks.Should ().HaveCount ( 1 );
			var theHook = hooks.First ();
			theHook.Should ().NotBe ( 0 );
			inputData.AddHookIDs ( hooks );
			TestObject.ReleaseHook ( inputData.HookInfo ).Should ().Be ( 1 );
		}

		protected void ExecOnHook (HInputEventDataHolder inputData, Action act, bool shouldRetest, bool shouldReceiveEvent) {
			EventList.Clear ();
			var hooks = TestObject.SetupHook ( inputData.HookInfo, SimpleTestCallback, null );
			inputData.AddHookIDs ( hooks );
			act ();
			TestObject.ReleaseHook ( inputData.HookInfo );
			if ( shouldRetest ) act ();
			onInputReceived.WaitOne ( 100 ).Should ().Be ( shouldReceiveEvent );
		}

		protected HInputEventDataHolder GenerateKeyboardEvent ( HHookInfo hookInfo = null, DInputReader owner = null ) {
			if ( owner == null ) owner = TestObject;
			if ( hookInfo == null ) hookInfo = GenerateHookInfo ();
			return new HKeyboardEventDataHolder ( owner, hookInfo, 1, VKChange.KeyDown );
		}

		protected DHookManager.ConsumingStatus SimpleTestCallback ( DictionaryKey key, HInputEventDataHolder inpudData ) {
			EventList.Add ( inpudData );
			onInputReceived.Set ();
			return DHookManager.ConsumingStatus.Consume;
		}

		HHookInfo GenerateHookInfo ( VKChange keyEvent = VKChange.KeyDown, int DeviceID = 1 ) => new HHookInfo ( TestObject, DeviceID, keyEvent );
	}

	public class MInputReaderTest : DInputReaderTest {
		MLowLevelInput LowLevelInput;

		public MInputReaderTest ( ITestOutputHelper outputHelper ) : base ( outputHelper ) { }

		public override CoreBase CreateCoreBase () {
			var ret = new CoreBaseMock ();
			LowLevelInput = new MLowLevelInput ( ret );
			return ret;
		}
		public override DInputReader GenerateTestObject () => new MInputReader ( OwnerCore );

		[Fact]
		public void SimKeyPressPasses () {
			HInputEventDataHolder inputData = GenerateKeyboardEvent ();
			ExecOnHook ( inputData, () => ((MInputReader)TestObject).SimulateKeyPress ( inputData.HookInfo, KeyCode.E, VKChange.KeyDown ), true, true );
			EventList.Should ().HaveCount ( 1 );
		}
	}
}
