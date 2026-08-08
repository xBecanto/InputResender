using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using FluentAssertions;
using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using InputResender.Windows.Input;
using MdxLibs.Core;
using MdxLibs.CoreTests;
using MdxLibs.Services;
using Xunit;
using Xunit.Abstractions;

namespace InputResender.Windows.Tests {
	public class VWinLowLevelLibsTest : ComponentTestBase<VWinLowLevelLibs> {
		// Inspired by https://www.codeproject.com/Articles/5264831/How-to-Send-Inputs-using-Csharp
		public override CoreBase CreateCoreBase () {
			var ret = new CoreBaseMock ();
			new MInputReader ( ret );
			return ret;
		}
		public override VWinLowLevelLibs GenerateTestObject () => new VWinLowLevelLibs ( OwnerCore );

		static AutoResetEvent waiter;
		static List<(DictionaryKey, HInputData)> messages;

		public VWinLowLevelLibsTest ( ITestOutputHelper outputHelper ) : base ( outputHelper ) {
			waiter = new AutoResetEvent ( false );
			messages = new List<(DictionaryKey, HInputData)> ();
		}

		[Fact]
		public void HL_LL_DataConversion () {
			var inputData = GenerateInputData ();
			var hookID = SetupHook ();
			var HL_Data = TestObject.GetHighLevelData( hookID.Key, OwnerCore.Fetch<DInputReader> (), inputData );
			TestObject.UnhookHookEx ( hookID );
			var LL_Data = TestObject.GetLowLevelData ( HL_Data );
			LL_Data.Should ().Be ( inputData );
			TestObject.ErrorList.Should ().BeEmpty ();
		}

		[Fact]
		public void VKChangeConversion () {
			VKChange[] vals = { VKChange.KeyDown, VKChange.KeyUp };
			foreach (var val in vals ) {
				int code = (int)val;
				code.Should ().BeGreaterThan ( 0 );
				VKChange en = (VKChange)code;
				en.Should ().Be ( val );
			}
			TestObject.ErrorList.Should ().BeEmpty ();
		}

		protected void AssertIntpuData ( HInputData data, uint testStartTime, uint testEndTime, nint extraInfo = 0 ) {
			data.Should ().NotBeNull ();
			data.Pressed.Should ().Be ( VKChange.KeyDown );
			data.Data.Should ().NotBeNull ().And.BeOfType<HWInput> ();
			HWInput hwInput = (HWInput)data.Data;
			hwInput.Type.Should ().Be ( HWInput.TypeKEY );
			hwInput.Data.ki.vkCode.Should ().Be ( (ushort)KeyCode.E );
			hwInput.Data.ki.scanCode.Should ().Be ( (ushort)KeyCode.None );
			hwInput.Data.ki.dwFlags.Should ().Be ( HWInput.KeyboardInput.keyDownID | (uint)HWInput.KeyboardInput.CallbackFlags.ValidCallbackFlags );
			hwInput.Data.ki.time.Should ().BeInRange ( testStartTime, testEndTime );
			hwInput.Data.ki.dwExtraInfo.Should ().Be ( extraInfo );
		}

		[Fact]
		public void CorrectInputDataParsing () {
			uint testStartTime = (uint)(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond);
			var hookID = SetupHook ();
			WinLLInputData data = new ( TestObject, HWInput.KeyboardInput.Create ( VKChange.KeyDown, KeyCode.E ) );
			nint ptr = data.SaveUnmanaged ();
			var parsed = TestObject.ParseHookData ( hookID.Key, (nint)VKChange.KeyDown, ptr );
			uint testEndTime = (uint)(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond);

			AssertIntpuData ( parsed, testStartTime, testEndTime ); // Test initial parsing

			nint hash = parsed.GetFullHash ( (nint)VKChange.KeyDown, ptr );
			parsed.SetExtraInfo ( ptr, hash );
			parsed.ExtraInfo.Should ().Be ( hash );
			AssertIntpuData ( parsed, testStartTime, testEndTime, hash ); // Test correct data inside the managed structure

			parsed.SetExtraInfo ( ptr, hash );
			AssertIntpuData ( parsed, testStartTime, testEndTime, hash ); // Test correct save to unmanaged structure

			TestObject.UnhookHookEx ( hookID );
			data.FreeUnmanaged ( ptr );
		}

		[Fact]
		public void InputDataToStringTest () {
			var inputData = GenerateInputData ();
			var vals = ((HWInput)inputData.Data).Data.ki;
			string ss = inputData.ToString ();
			ss.Should ().Contain ( "K" ).And
				.Contain ( ((KeyCode)vals.vkCode).ToString () ).And
				.Contain ( ((KeyCode)vals.scanCode).ToString () ).And
				.Contain ( vals.time.ToString () );
			TestObject.ErrorList.Should ().BeEmpty ();
		}

		[Fact]
		public void SetUnsetHook () {
			var hookID = SetupHook ();

			TestObject.UnhookHookEx ( hookID );
			hookID.Should ().NotBe ( (IntPtr)null );
			TestObject.ErrorList.Should ().BeEmpty ();
		}

		[Fact]
		public void HookShouldntDuplicate () {
			HHookInfo hookInfo1 = new ( TestObject, 1, VKChange.KeyDown );
			var hooks1 = TestObject.SetHookEx ( hookInfo1, Callback );
			hooks1.Should ().NotBeNull ().And.HaveCount ( 1 );
			HHookInfo hookInfo2 = new ( TestObject, 1, VKChange.KeyDown );
			var hooks2 = TestObject.SetHookEx ( hookInfo2, Callback );
			hooks2.Should ().NotBeNull ().And.BeEmpty ();
			TestObject.ErrorList.Should ().HaveCount ( 1 );
			TestObject.ErrorList[0].Item1.Should ().Be ( nameof ( TestObject.SetHookEx ) );
			TestObject.ErrorList[0].Item2.Message.Should ().Contain ( $"uplicate request for {nameof ( VKChange.KeyDown )}" );
		}

		[Fact]
		public void SimulateInput () {
			var hookID = SetupHook ();
			var inputData = GenerateInputData ();

			try {
				var sent = TestObject.SimulateInput ( 1, new[] { inputData }, inputData.SizeOf, true );
				TestObject.PrintErrors ( Output.WriteLine );
				sent.Should ().Be ( 1 );
			} finally {
				TestObject.UnhookHookEx ( hookID );
			}

			waiter.WaitOne ( 200 ).Should ().BeTrue ();
			messages.Should ().HaveCount ( 1 );
			messages[0].Should ().NotBeNull ();
			messages[0].Item1.GetHashCode ().Should ().NotBe ( 0 );
			messages[0].Item2.Pressed.Should ().Be ( VKChange.KeyDown );
			((HWInput)messages[0].Item2.Data).Data.ki.vkCode.Should ().Be ( (ushort)KeyCode.E );
			TestObject.ErrorList.Should ().BeEmpty ();
		}

		public bool Callback ( DictionaryKey hookKey, HInputData inputData ) {
			messages.Add ( (hookKey, inputData) );
			Thread.MemoryBarrier ();
			waiter.Set ();
			return false; // Consume the event
		}
		public static uint GetPID () => (uint)Process.GetCurrentProcess ().Id;

		public Hook SetupHook () {
			HHookInfo hookInfo = new HHookInfo ( TestObject, 1, VKChange.KeyDown, VKChange.KeyUp );
			var hooks = TestObject.SetHookEx ( hookInfo, Callback );
			TestObject.PrintErrors ( Output.WriteLine );
			return hooks.Any () ? hooks.First ().Value : null;
		}

		public WinLLInputData GenerateInputData () => WinLLInputData.NewKeyboardData ( TestObject, (ushort)KeyCode.E, (ushort)KeyCode.E, 0, 123456, TestObject.GetMessageExtraInfoPtr () );
	}
}