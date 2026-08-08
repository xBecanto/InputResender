using System;
using System.Runtime.InteropServices;
using FluentAssertions;
using InputResender.Definitions.InputProcessing;
using InputResender.DefinitionTests.InputProcessing;
using InputResender.Windows.Input;
using Xunit.Abstractions;

namespace InputResender.Windows.Tests;
public class WinLLInputStatusExtraAssertable : WinLLInputStatusExtra.WinLLInputStatusParser, CInputLLParser.IInputEventAssertor {
	public CInputLLParser.IInputEventAssertor.InputEventInfoAssertor CreateAssertor () => new WinLLInputStatusExtraAssertor ();


	public class WinLLInputStatusExtraAssertor : CInputLLParser.IInputEventAssertor.InputEventInfoAssertor {
		public uint? ExpTimeOfRegistration;
		public uint? ExpUID;
		public nint? ExpStatusPtr;
		public nint? ExpOrigExtraInfo;
		// General input
		public uint? ExpDWFlags;
		public uint? ExpTime;
		public IntPtr? ExpDWExtraInfo;
		// Keyboard input
		public ushort? ExpVkCode;
		public ushort? ExpScanCode;
		// Mouse input
		public int? ExpDx;
		public int? ExpDy;
		public uint? ExpMouseData;

		public override void Assert ( CInputLLParser.InputEventInfo item ) {
			item.Should ().NotBeNull ();
			var info = item.Should ().BeOfType<WinLLInputStatusExtra> ().Subject;
			if ( ExpTimeOfRegistration.HasValue ) info.TimeOfRegistration.Should ().Be ( ExpTimeOfRegistration.Value );
			if ( ExpUID.HasValue ) info.UID.Should ().Be ( ExpUID.Value );
			if ( ExpStatusPtr.HasValue ) info.StatusPtr.Should ().Be ( ExpStatusPtr.Value );
			if ( ExpOrigExtraInfo.HasValue ) info.StatusOrigExtraInfo.Should ().Be ( ExpOrigExtraInfo.Value );

			if ( ExpDWFlags.HasValue ) info.inputData.DWFlags.Should ().Be ( ExpDWFlags.Value );
			if ( ExpTime.HasValue ) info.inputData.Time.Should ().Be ( ExpTime.Value );
			if ( ExpDWExtraInfo.HasValue ) info.inputData.ExtraInfo.Should ().Be ( ExpDWExtraInfo.Value );
			if ( ExpVkCode.HasValue ) {
				info.inputData.Type.Should ().Be ( HWInput.TypeKEY );
				info.inputData.Data.ki.vkCode.Should ().Be ( ExpVkCode.Value );
			}
			if ( ExpScanCode.HasValue ) {
				info.inputData.Type.Should ().Be ( HWInput.TypeKEY );
				info.inputData.Data.ki.scanCode.Should ().Be ( ExpScanCode.Value );
			}
			if ( ExpDx.HasValue ) {
				info.inputData.Type.Should ().Be ( HWInput.TypeMOUSE );
				info.inputData.Data.mi.dx.Should ().Be ( ExpDx.Value );
			}
			if ( ExpDy.HasValue ) {
				info.inputData.Type.Should ().Be ( HWInput.TypeMOUSE );
				info.inputData.Data.mi.dy.Should ().Be ( ExpDy.Value );
			}
			if ( ExpMouseData.HasValue ) {
				info.inputData.Type.Should ().Be ( HWInput.TypeMOUSE );
				info.inputData.Data.mi.mouseData.Should ().Be ( ExpMouseData.Value );
			}
		}

		protected override void FillInner ( CInputLLParser.InputEventInfo info ) {
			if ( info == null ) return;
			var item = info.Should ().BeOfType<WinLLInputStatusExtra> ().Subject;
			ExpTimeOfRegistration = item.TimeOfRegistration;
			ExpUID = item.UID;
			ExpStatusPtr = item.StatusPtr;
			ExpOrigExtraInfo = item.StatusOrigExtraInfo;
			ExpDWFlags = item.inputData.DWFlags;
			ExpTime = item.inputData.Time;
			ExpDWExtraInfo = item.inputData.ExtraInfo;
			if ( item.inputData.Type == HWInput.TypeKEY ) {
				ExpVkCode = item.inputData.Data.ki.vkCode;
				ExpScanCode = item.inputData.Data.ki.scanCode;
			} else if ( item.inputData.Type == HWInput.TypeMOUSE ) {
				ExpDx = item.inputData.Data.mi.dx;
				ExpDy = item.inputData.Data.mi.dy;
				ExpMouseData = item.inputData.Data.mi.mouseData;
			}
		}
	}
}

public class WinLLInputStatusExtraTest ( ITestOutputHelper outputHelper )
	: CInputLLParserTest<WinLLInputStatusExtra> ( outputHelper ) {
	DateTime? firstInputTime = null;

	protected override CInputLLParser.InputEventParser GetParser () => new WinLLInputStatusExtra.WinLLInputStatusParser ();

	protected override void AssertLoadedData ( WinLLInputStatusExtra info ) {
		HWInputTest.Assert ( info.inputData, extraInfo: info.StatusPtr );
		info.ShouldProcess.Should().BeTrue ();
		info.TimeOfRegistration.Should ().BeInRange (
			HWInput.TimeConvert ( firstInputTime.Value ),
			HWInput.TimeConvert ( DateTime.Now ) );
		info.UID.Should ().BeGreaterThanOrEqualTo ( 42 );
		Marshal.ReadInt32 ( info.StatusPtr ).Should ().Be ( WinLLInputStatusExtra.MARK );
	}

	protected override nint GenerateInputMemory () {
		firstInputTime ??= DateTime.Now;
		return HWInputTest.GenerateInputMemory ( null );
	}
}