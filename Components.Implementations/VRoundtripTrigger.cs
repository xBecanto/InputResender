using Components.Interfaces;
using Components.Library;

namespace Components.Implementations;

/// <summary>A pipeline origin/sink used to trigger and receive real roundtrip network performance tests.</summary>
public abstract class DRoundtripTrigger : ComponentBase<DMainAppCore> {
	protected DRoundtripTrigger ( DMainAppCore owner ) : base ( owner ) { }

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

	public VRoundtripTrigger ( DMainAppCore owner ) : base ( owner ) { }
	public override int ComponentVersion => 1;
	public override void OnBatchReceived ( HInputEventDataHolder[] events ) => Callback?.Invoke ( events );
}

