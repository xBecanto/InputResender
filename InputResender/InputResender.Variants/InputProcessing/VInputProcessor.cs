using System;
using System.Collections.Generic;
using System.Linq;
using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using MdxLibs.Core;
using DataHolder = InputResender.Definitions.InputProcessing.HInputEventDataHolder;

namespace InputResender.Variants.InputProcessing;
public class VInputProcessor : DInputProcessor {
	public VInputProcessor ( CoreBase owner ) : base ( owner ) { }

	public override int ComponentVersion => 1;

	public bool ProcessingEnabled = true;
	public DHookManager.ConsumingStatus ShouldConsume = DHookManager.ConsumingStatus.Passthrough;
	public ComponentSelector PipelineTarget = null;
	public KeyCode Toggle = KeyCode.None;
	public KeyCode OnHold = KeyCode.None;
	public KeyCode OnRelease = KeyCode.None;
	private bool internalIsEnabled = true;
	public readonly Dictionary<KeyCode, KeyCode> Remap = [];

	public override DHookManager.ConsumingStatus ProcessInput ( DataHolder[] inputCombination ) {
		if ( !ProcessingEnabled ) return DHookManager.ConsumingStatus.Skip;
		if ( inputCombination == null || inputCombination.Length < 1 ) return DHookManager.ConsumingStatus.Error;

		foreach ( var combo in inputCombination ) {
			if ( combo.BeingPressedX && Toggle == (KeyCode)combo.InputCode ) {
				internalIsEnabled = !internalIsEnabled;
				return DHookManager.ConsumingStatus.Consume;
			}
		}

		if ( !internalIsEnabled ) return DHookManager.ConsumingStatus.Skip;

		if ( inputCombination.Length == 1 && (
				(KeyCode)inputCombination[0].InputCode == OnHold
				|| (KeyCode)inputCombination[0].InputCode == OnRelease) )
			return DHookManager.ConsumingStatus.Consume; // Consume the controling events

		if ( OnHold != KeyCode.None && !inputCombination.Any ( combo => OnHold == (KeyCode)combo.InputCode
				&& combo.ValueX >= DataHolder.PressThreshold
			) ) { return DHookManager.ConsumingStatus.Skip; }

		if ( OnRelease != KeyCode.None && inputCombination.Where ( combo => OnRelease == (KeyCode)combo.InputCode )
				.Any ( combo => combo.ValueX >= DataHolder.PressThreshold
			) ) { return DHookManager.ConsumingStatus.Skip; }

		KeyCode key = (KeyCode)inputCombination[0].InputCode;
		if ( Remap.TryGetValue ( key, out var value ) ) key = value;
		InputData ret = new InputData ( this ) {
			Cmnd = inputCombination[0].Pressed >= 1 ? InputData.Command.KeyPress : InputData.Command.KeyRelease
			, DeviceID = inputCombination[0].HookInfo.DeviceID
			, Key = key
			, X = 1
			, Y = 0
			, Z = 0
		};
		ret.Modifiers = ReadModifiers ( inputCombination );
		FireCallback ( ret, PipelineTarget );
		return ShouldConsume;
	}

	public override StateInfo Info => new VStateInfo ( this );

	public class VStateInfo : DStateInfo {
		public VStateInfo ( VInputProcessor owner ) : base ( owner ) { }
	}
}
