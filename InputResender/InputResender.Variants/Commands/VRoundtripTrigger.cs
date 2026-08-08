using System;
using System.Collections.Generic;
using InputResender.Definitions;
using InputResender.Definitions.InputProcessing;

namespace InputResender.Variants.Commands;

/// <summary>A pipeline origin/sink used to trigger and receive real roundtrip network performance tests.</summary>
public abstract class DRoundtripTrigger : ComponentBase_IRCore {
	protected DRoundtripTrigger ( DInputResenderCore owner ) : base ( owner ) { }

	protected override IReadOnlyList<(string opCode, Type opType)> AddCommands () => [];

	/// <summary>Called when the echo comes back through the pipeline (DInputSimulator → DRoundtripTrigger step).</summary>
	public abstract void OnBatchReceived ( HInputEventDataHolder[] events );

	public override StateInfo Info => new DStateInfo ( this );
	public class DStateInfo : StateInfo {
		public DStateInfo ( DRoundtripTrigger owner ) : base ( owner ) { }
	}
}

public class VRoundtripTrigger : DRoundtripTrigger {
	/// <summary>Set this before each iteration; called on the network thread when the echo arrives.</summary>
	public Action<HInputEventDataHolder[]> Callback;

	public VRoundtripTrigger ( DInputResenderCore owner ) : base ( owner ) { }
	public override int ComponentVersion => 1;
	public override void OnBatchReceived ( HInputEventDataHolder[] events ) => Callback?.Invoke ( events );
}

