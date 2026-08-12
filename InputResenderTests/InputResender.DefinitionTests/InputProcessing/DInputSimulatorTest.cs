using System.Collections.Generic;
using FluentAssertions;
using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using MdxLibs.Core;
using MdxLibs.CoreTests;
using MdxLibs.Services;
using Xunit;
using Xunit.Abstractions;

namespace InputResender.DefinitionTests.InputProcessing {
	public abstract class DInputSimulatorTest : ComponentTestBase<DInputSimulator> {
		protected List<(DictionaryKey, HInputEventDataHolder)> CallbackList;

		protected DInputSimulatorTest ( ITestOutputHelper outputHelper ) : base ( outputHelper ) {
			CallbackList = new List<(DictionaryKey, HInputEventDataHolder)> ();
		}

		public override CoreBase CreateCoreBase () {
			var ret = new CoreBaseMock ();
			new MInputMerger ( ret );
			new MInputReader ( ret );
			new MInputProcessor ( ret );
			return ret;
		}

		protected abstract InputData GenerateInputData ( InputData.Command cmnd );
		protected abstract HInputEventDataHolder[] GenerateOutputData ( InputData.Command cmnd );

		[Theory]
		[InlineData(InputData.Command.KeyPress)]
		public void ParseAndSimulate (InputData.Command cmnd) {
			var inputData = GenerateInputData ( cmnd );
			var outputData = GenerateOutputData ( cmnd );

			var parsed = TestObject.ParseCommand ( inputData );
			parsed.Should ().Equal ( outputData );
			if ( parsed == null || parsed.Length == 0 ) return;

			var InputReader = OwnerCore.Fetch<DInputReader> ();
			TestObject.AllowRecapture = true;
			Dictionary<VKChange, DictionaryKey> hookKeys = new Dictionary<VKChange, DictionaryKey> ();
			CallbackList.Clear ();
			foreach ( var e in parsed ) {
				var hooks = InputReader.SetupHook ( e.HookInfo, SimpleCallback, null );
				foreach ( var hookCombo in hooks ) hookKeys.Add ( hookCombo.Key, hookCombo.Value );
			}

			int N = outputData.Length;
			TestObject.Simulate ( parsed ).Should ().Be ( N );
			CallbackList.Should ().HaveCount ( N );
			for (int i = 0; i < N; i++) {
				CallbackList[i].Item2.Should ().Be ( outputData[i] );
				hookKeys.Should ().ContainValue ( CallbackList[i].Item1 );
			}

			foreach ( var e in parsed )
				InputReader.ReleaseHook ( e.HookInfo );
		}

		protected DHookManager.ConsumingStatus SimpleCallback (DictionaryKey hookKey, HInputEventDataHolder data) {
			CallbackList.Add ( (hookKey, data) );
			return DHookManager.ConsumingStatus.Consume;
		}
	}
}