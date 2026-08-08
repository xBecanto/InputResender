using System;
using System.Collections.Generic;
using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using MdxLibs.Services;

namespace InputResender.Windows.Input;
internal class HookGroupCollection {
	private class HookGroup {
		public Hook HookObj;
		public DictionaryKey HookID;
		public int VKType;
		public List<VKChange> VKChanges;

		public void Clear () {
			HookObj = null;
			HookID = DictionaryKey.Empty;
			VKType = -1;
			VKChanges.Clear ();
		}
	}

	private VWinLowLevelLibs Owner;
	private Dictionary<VKChange, HookGroup> HooksByVKChange;
	private Dictionary<int, HookGroup> HooksByVKType;
	private Dictionary<DictionaryKey, HookGroup> HooksByHookID;
	public HookGroupCollection ( VWinLowLevelLibs owner ) {
		HooksByVKChange = new ();
		HooksByVKType = new ();
		HooksByHookID = new ();
		Owner = owner;
	}

	/*private HookGroup this[VKChange vkChange] => HooksByVKChange.TryGetValue ( vkChange, out HookGroup hook) ? hook : null;
	private HookGroup this[int vkType] */
	public Tuple<Hook, int> this[VKChange vkChange] => HooksByVKChange.TryGetValue(vkChange, out HookGroup hook) ? new (hook.HookObj, hook.VKType) : null;
	public Tuple<Hook, int, List<VKChange>> this[DictionaryKey hookID] => HooksByHookID.TryGetValue(hookID, out HookGroup hook) ? new (hook.HookObj, hook.VKType, hook.VKChanges) : null;

	public enum UpdateStatus { KeyNotFound, AlreadyExists, Updated }
	public UpdateStatus TryUpdateWithVKChange ( int vkType, VKChange vkChange ) {
		if ( !HooksByVKType.TryGetValue ( vkType, out var hook ) ) return UpdateStatus.KeyNotFound;

		bool inHook = hook.VKChanges.Contains ( vkChange );
		bool inDict = HooksByVKChange.ContainsKey ( vkChange );

		if ( inHook & !inDict ) throw new ArgumentException ( $"VKChange {vkChange} is registered in hook {hook} but not in {nameof ( HooksByVKChange )}!" );
		else if ( !inHook & inDict ) throw new ArgumentException ( $"VKChange {vkChange} is registered in {nameof ( HooksByVKChange )}, but not in hook {hook}!" );
		else if ( inHook ) return UpdateStatus.AlreadyExists;

		hook.VKChanges.Add ( vkChange );
		HooksByVKChange.Add ( vkChange, hook );
		return UpdateStatus.Updated;
	}

	public bool OwnsHookID ( DictionaryKey hookID ) => HooksByHookID.ContainsKey (hookID);
	public bool OwnsHookForVKChange ( VKChange vkChange ) => HooksByVKChange.ContainsKey ( vkChange );

	public bool AddHook (Hook hook, int vkType, ICollection<VKChange> vkChanges) {
		if (HooksByHookID.ContainsKey(hook.Key)) {
			Owner.ErrorList.Add ( (nameof ( AddHook ), new Exception ( $"Hook with ID {hook.Key} is already registered!" )) );
			return false;
		}
		if (HooksByVKType.ContainsKey(vkType)) {
			Owner.ErrorList.Add ( (nameof ( AddHook ), new Exception ( $"Hook for type #{vkType} is already registered!" )) );
			return false;
		}
		foreach ( var vkChange in vkChanges ) {
			if ( HooksByVKChange.ContainsKey ( vkChange ) ) {
				Owner.ErrorList.Add ( (nameof ( AddHook ), new Exception ( $"For {vkChange} there is already {HooksByVKChange[vkChange].HookID}!" )) );
				return false;
			}
		}

		HookGroup hookGroup = new HookGroup () {
			HookID = hook.Key,
			VKType = vkType,
			HookObj = hook,
			VKChanges = new ()
		};

		HooksByHookID.Add ( hook.Key, hookGroup );
		HooksByVKType.Add ( vkType, hookGroup );
		foreach ( var vkChange in vkChanges ) {
			hookGroup.VKChanges.Add ( vkChange );
			HooksByVKChange.Add ( vkChange, hookGroup );
		}
		return true;
	}

	public bool RemoveHookByID ( DictionaryKey hookID ) {
		if (!HooksByHookID.TryGetValue(hookID, out var hook)) {
			Owner.ErrorList.Add ( ( nameof ( RemoveHookByID ), new ArgumentOutOfRangeException ( nameof ( hookID ), $"No hook with ID {hookID} is found!" ) ) );
			return false;
		}

		bool status = true;
		status &= HooksByHookID.Remove ( hook.HookID );
		status &= HooksByVKType.Remove ( hook.VKType );
		foreach ( VKChange vkChange in hook.VKChanges )
			status &= HooksByVKChange.Remove ( vkChange );
		hook.Clear ();
		return status;
	}

	public string[] GetInfo () {
		List<string> ret = new ();
		foreach ( var hookGroup in HooksByHookID.Values )
			ret.Add ( $"{string.Join ( ", ", hookGroup.VKChanges )} => ({hookGroup.HookObj}){hookGroup.HookObj}" );
		return ret.ToArray ();
	}














	/*public DictionaryKey this[VKChange vkChange] {
		get {
			if ( !HooksByVKChange.TryGetValue ( vkChange, out HookGroup hookGroup ) ) {
				Owner.ErrorList.Add ( (nameof ( HookGroupCollection ) + " : Get[VKChange] ", new KeyNotFoundException ( $"No hook group for {vkChange} was found!" )) );
				return DictionaryKey.Empty;
			}
			return hookGroup.HookID;
		}
	}
	public DictionaryKey this[int vkType] {
		get {
			foreach ( var hookGroup in HooksByVKChange.Values ) {
				if ( hookGroup.VKType == vkType ) return hookGroup.HookID;
			}
			Owner.ErrorList.Add ( (nameof ( HookGroupCollection ) + " : Get[int] ", new KeyNotFoundException ( $"No hook group for {vkType} was found!" )) );
			return DictionaryKey.Empty;
		}
	}

	public void UpdateVKChanges (Dictionary<int, List<VKChange>> vkTypeDict) {
		foreach ((int vkType, var vkChangelist) in vkTypeDict) {

		}
	}

	public bool OwnsHookForVKChange ( VKChange vkChange ) => HooksByVKChange.ContainsKey ( vkChange );
	public bool OwnsHookID ( DictionaryKey hookID ) => HooksByVKChange.Values.Any ( ( hookGroup ) => hookGroup.HookID == hookID );
	public void Remove ( DictionaryKey hookID ) {
		var keys = HooksByVKChange.Keys.Where ( ( vkChange ) => HooksByVKChange[vkChange].HookID == hookID ).ToList ();
		if ( keys.Count == 0 ) {
			Owner.ErrorList.Add ( (nameof ( HookGroupCollection ) + " : Remove ", new KeyNotFoundException ( $"No hook group for {hookID} was found!" )) );
			return;
		}
	}

	public string[] GetInfo () {
		Dictionary<HookGroup, List<VKChange>> hooks = new ();
		foreach ( var (vkChange, hookGroup) in HooksByVKChange ) {
			if ( !hooks.ContainsKey ( hookGroup ) ) hooks.Add ( hookGroup, new List<VKChange> () );
			hooks[hookGroup].Add ( vkChange );
		}

		List<string> ret = new ();
		foreach ( var (hookGroup, vkChanges) in hooks )
			ret.Add ($"{string.Join ( ", ", vkChanges )} => ({hookGroup.HookID}){hookGroup.HookObj}");
		return ret.ToArray ();
	}*/
}