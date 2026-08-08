using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FluentAssertions;
using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using MdxLibs.CoreTests;
using MdxLibs.Services;
using Xunit;
using Xunit.Abstractions;

namespace InputResender.DefinitionTests.InputProcessing {
	public abstract class DLowLevelInputTest<T> : ComponentTestBase<T> where T : DLowLevelInput {
		protected AutoResetEvent onInputReceived;
		protected List<HInputData> EventList;
		protected readonly KeyCode SimKey;

		public DLowLevelInputTest ( ITestOutputHelper outputHelper, KeyCode simKey ) : base ( outputHelper ) {
			onInputReceived = new AutoResetEvent ( false );
			EventList = new List<HInputData> ();
			SimKey = simKey;
		}

		[Fact]
		public void SetupSimulateRelease_RaisesCallbackOnce () {
			Output.WriteLine ( $"TestObject: {TestObject.Name} created on {TestObject.CreationTime:hh:mm:ss:fffffff}" );
			FetchHLData ( out var HLData, out uint dataCnt, out int dataSize );
			ExecOnHook ( HLData, () =>
				TestObject.SimulateInput ( dataCnt, HLData, dataSize ),
				true, true );
			EventList.Should ().HaveCount ( (int)dataCnt );
			EventList.Should ().Equal ( HLData ).And.NotBeSameAs ( HLData );
		}

		[Fact]
		public void NegNCodeSkipsProcessing () {
			Output.WriteLine ( $"TestObject: {TestObject.Name} created on {TestObject.CreationTime:hh:mm:ss:fffffff}" );
			FetchHLData ( out var HLData, out uint dataCnt, out int dataSize );
			ExecOnHook ( HLData, () => {
				TestObject.SimulateInput ( dataCnt, HLData, dataSize, false );
				if ( !TryForceCallbackSkip ( false ) )
					Assert.Fail ( "This function cannot be tested." );
				TestObject.SimulateInput ( dataCnt, HLData, dataSize, true );
				TestObject.SimulateInput ( dataCnt, HLData, dataSize, false );
			}, false, false );
		}

		[Fact]
		public void ReleaseNonexistingHookThrowsKeyNotFound () {
			Hook nonexistingHook = new Hook ( TestObject, NewHookInfo (), new DictionaryKey ( 1234 ), SimpleTestCallback );
			nonexistingHook.UpdateHookID ( 4321 );
			Action act = () => TestObject.UnhookHookEx ( nonexistingHook );
			act.Should ().Throw<KeyNotFoundException> ();
		}

		protected abstract HInputData[] GenerateKeyboardEvent ( T owner );
		/// <summary>If possible, set wheter callback for next (one or all) raised hook should be skipped or processed (i.e. should next callback have neg. or pos. 'nCode' param.</summary>
		/// <returns>True if setup was succesful, false if this function is not supported.</returns>
		protected abstract bool TryForceCallbackSkip ( bool shouldProcess );

		protected void ExecOnHook ( HInputData[] HLData, Action act, bool shouldRetest, bool shouldReceiveEvent ) {
			EventList.Clear ();
			var hookInfo = NewHookInfo ();
			int expHookCnt = hookInfo.ChangeMask.Count;
			var hooks = TestObject.SetHookEx ( hookInfo, SimpleTestCallback );
			hooks.Should ().HaveCount ( expHookCnt );
			Hook hook = hooks.First ().Value;
			for ( int i = 0; i < HLData.Length; i++ ) HLData[i].UpdateByHook ( TestObject, hook.Key );
			act ();
			TestObject.UnhookHookEx ( hook );
			if ( shouldRetest ) act ();
			onInputReceived.WaitOne ( 100 ).Should ().Be ( shouldReceiveEvent );
			if ( !shouldReceiveEvent ) EventList.Should ().HaveCount ( 0 );
		}

		protected virtual bool SimpleTestCallback ( DictionaryKey hookKey, HInputData inputData ) {
			EventList.Add ( inputData );
			onInputReceived.Set ();
			return false;
		}

		private void FetchHLData ( out HInputData[] HLData, out uint dataCnt, out int dataSize ) {
			HLData = GenerateKeyboardEvent ( TestObject );
			dataCnt = (uint)HLData.Length;
			dataSize = 0;
			if ( HLData != null && dataCnt > 0 ) {
				dataSize = HLData[0].SizeOf;
				for ( uint i = dataCnt - 1; i >= 1; i-- ) {
					if ( HLData[i].SizeOf != dataSize ) throw new DataMisalignedException ( "Simulating input with different Data size is not supported!" );
				}
			}
		}
		private HHookInfo NewHookInfo () => new HHookInfo ( TestObject, 1, VKChange.KeyDown, VKChange.KeyUp );
	}
}