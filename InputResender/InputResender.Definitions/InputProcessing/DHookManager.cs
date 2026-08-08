using System;
using System.Collections.Generic;
using InputResender.Services;
using MdxLibs.Core;
using MdxLibs.Services;

namespace InputResender.Definitions.InputProcessing;
public abstract class DHookManager : ComponentBase_CoreBase {
	public enum CBType { Fast, Delayed }
	/// <summary>Callback for hooks. Should return true if event should be passed to other hooks, false if it should be consumed.</summary>
	public delegate bool HookCallback ( HInputEventDataHolder data );

	public override int ComponentVersion => 1;
	public override StateInfo Info => null;
	protected override IReadOnlyList<(string opCode, Type opType)> AddCommands () => new List<(string opCode, Type opType)> () {
			(nameof(AddHook), typeof(void)),
			(nameof(RemoveHook), typeof(void)),
			(nameof(ClearHooks), typeof(void)),
			(nameof(AddCallback), typeof(HCallbackHolder<HookCallback>)),
		};

	public DHookManager ( CoreBase owner ) : base ( owner ) { }

	public abstract ICollection<DictionaryKey> AddHook ( int device, params VKChange[] vkChanges );
	public abstract ICollection<DictionaryKey> RemoveHook ( int device, params VKChange[] vkChanges );
	public abstract HHookInfo GetHook ( int device, VKChange vkChange );
	public abstract void ClearHooks ( int device = 0 );
	public abstract Dictionary<int, DictionaryKey> ListHooks ();
	/// <summary>Register callback for all active hooks (in this manager) for given device. Callback should return true if event should be passed to other hooks, false if it should be consumed.</summary>
	public abstract HCallbackHolder<HookCallback> AddCallback ( CBType cbType, int device = -1 );
	public virtual int Verbosity { get; set; } = 0;
}

public class HCallbackHolder<T> : DataHolderBase<ComponentBase> {
	public T callback;
	private readonly Action<HCallbackHolder<T>> remover;
	public HCallbackHolder ( ComponentBase owner, Action<HCallbackHolder<T>> remover ) : base ( owner ) {
		if ( remover == null ) throw new ArgumentNullException ( nameof ( remover ) );
		this.remover = remover;
	}

	public override DataHolderBase<ComponentBase> Clone () => new HCallbackHolder<T> ( Owner, remover ) { callback = callback };
	public override bool Equals ( object obj ) {
		if ( obj is HCallbackHolder<T> holder ) {
			if (holder.callback == null ) return callback == null;
			else return holder.callback.Equals ( callback ) && holder.remover.Equals ( remover );
		} else return false;
	}
	public override int GetHashCode () => callback?.GetHashCode () ?? 0;
	public override string ToString () => callback?.ToString () ?? "null";

	public void Unregister () => remover ( this );
}