using System;
using FluentAssertions;
using InputResender.Services;
using InputResender.Windows.Input;
using Xunit;

namespace InputResender.Windows.Tests;
public class HWInputTest {
	[Fact]
	public void CanBeLoaded () {
		nint ptr = GenerateInputMemory ();
		HWInput readInput = new ( (nint)VKChange.KeyDown, ptr );
		Assert ( readInput );
	}

	[Fact]
	public void CanBeUpdated () {
		HWInput inputData = GenerateInputData ();
		nint ptr = GenerateInputMemory ( inputData );
		inputData.Data.ki.time = 54321;
		inputData.Data.ki.dwExtraInfo = 42;
		inputData.Data.ki.Write ( ptr );

		HWInput readInput = new ( (nint)VKChange.KeyDown, ptr );
		Assert ( readInput, time: 54321, extraInfo: 42 );
	}

	public static HWInput GenerateInputData () {
		HWInput.KeyboardInput ki = new HWInput.KeyboardInput {
			vkCode = (ushort)KeyCode.E,
			scanCode = (ushort)KeyCode.None,
			dwFlags = HWInput.KeyboardInput.keyDownID | (uint)HWInput.KeyboardInput.CallbackFlags.ValidCallbackFlags,
			time = 12345, // Time will be set by the system
			dwExtraInfo = IntPtr.Zero // Extra info can be set later
		};
		return new ( HWInput.Keyboard, new HWInput.InputUnion { ki = ki } );
	}
	public static nint Assert ( HWInput data, KeyCode? key = KeyCode.E
		, uint? flags = HWInput.KeyboardInput.keyDownID
		| (uint)HWInput.KeyboardInput.CallbackFlags.ValidCallbackFlags
		, uint? time = 12345, nint? extraInfo = 0 ) {
		data.Type.Should ().Be ( HWInput.Keyboard );
		if ( key.HasValue ) data.Data.ki.vkCode.Should ().Be ( (ushort)key.Value );
		data.Data.ki.scanCode.Should ().Be ( (ushort)KeyCode.None );
		if ( flags.HasValue ) data.Data.ki.dwFlags.Should ().Be ( flags.Value );
		if ( time.HasValue ) data.Data.ki.time.Should ().Be ( time.Value ); // Time is set to a fixed value for testing
		if ( extraInfo.HasValue ) data.Data.ki.dwExtraInfo.Should ().Be ( extraInfo.Value ); // Extra info is not set
		return data.Data.ki.dwExtraInfo; // Return the extra info pointer
	}
	public static nint GenerateInputMemory ( HWInput? inputData = null ) {
		inputData ??= GenerateInputData ();
		return inputData.Value.Data.ki.Write ();
	}
}