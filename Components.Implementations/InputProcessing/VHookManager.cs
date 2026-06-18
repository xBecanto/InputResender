using Components.Interfaces;
using Components.Library;
using Components.Library.ComponentSystem;

using CallbackHolder = Components.Interfaces.HCallbackHolder<Components.Interfaces.DHookManager.HookCallback>;
using DeviceID = System.Int32;

namespace Components.Implementations;
public class VHookManager : DHookManager {
	readonly Dictionary<DeviceID, HookGroup> ActiveHooks = new ();
	readonly Dictionary<(DeviceID, CBType), HashSet<CallbackHolder>> Callbacks = new ();
	readonly Dictionary<DictionaryKey, HHookInfo> OwnedHooks = new ();
	private int _verbosity;

	public readonly static VKChange[] SupportedVKs = { VKChange.KeyDown, VKChange.KeyUp, VKChange.MouseMove };
	private static void AssertVKs (params VKChange[] vkChanges) {
		if ( vkChanges == null ) return;
		foreach ( var vkChange in vkChanges )
			if ( !SupportedVKs.Contains ( vkChange ) )
				throw new ArgumentOutOfRangeException ( nameof ( vkChanges ), vkChange, "Unsupported VKChange" );
	}

	public VHookManager ( CoreBase owner ) : base ( owner ) {
	}

	public override int Verbosity { get => _verbosity;
		set {
			bool isVerbose = value > 0;
			_verbosity = isVerbose ? 0 : 1;
			var keyReader = Owner.Fetch<VInputReader_KeyboardHook> ();
			if ( keyReader != null ) keyReader.Verbose = isVerbose;
		}
		}

	public override CallbackHolder AddCallback ( CBType cbType, DeviceID device = -1 ) {
		var key = (device, cbType);
		var ret = new CallbackHolder ( this, ( holder ) => Callbacks[key].Remove ( holder ) ); // holder should be same as ret
		if ( !Callbacks.ContainsKey ( key ) ) Callbacks.Add ( key, new HashSet<CallbackHolder> () );
		Callbacks[key].Add ( ret );
		return ret;
	}

	/// <summary>Return value can be ignored. It is returned only to let caller know if hook was successfully added and to provide info if caller needs that. Otherwise, all important data are stored inside of this component</summary>
	public override ICollection<DictionaryKey> AddHook ( DeviceID device, params VKChange[] vkChanges ) {
		HashSet<DictionaryKey> ret = new ();
		AssertVKs ( vkChanges );
		if ( !vkChanges.Any () ) return ret;

		var inputReader = Owner.Fetch<VInputReader_KeyboardHook> ();
		if ( inputReader == null ) throw new InvalidOperationException ( "VInputReader_KeyboardHook not found!" );
		if ( !ActiveHooks.ContainsKey ( device ) ) ActiveHooks.Add ( device, new HookGroup () );

		HHookInfo keyHookInfo = new ( this, device, vkChanges[0], vkChanges[1..] );
		var setup = inputReader.SetupHook ( keyHookInfo, FastCB, DelayedCB );
		foreach ((VKChange change, DictionaryKey key) in setup) {
			keyHookInfo.AddHookID ( key, change );
		}
		foreach ( var newHook in ActiveHooks[device].Add ( setup ) ) {
			ret.Add ( newHook );
			OwnedHooks.Add ( newHook, keyHookInfo );
		}

		return ret;
	}

	public override ICollection<DictionaryKey> RemoveHook ( DeviceID device, params VKChange[] vKChanges ) {
		if ( !ActiveHooks.ContainsKey ( device ) ) return [];
		var inputReader = Owner.Fetch<VInputReader_KeyboardHook> ();
		if ( inputReader == null ) throw new InvalidOperationException ( "VInputReader_KeyboardHook not found!" );

		var removed = ActiveHooks[device].Remove ( vKChanges );
		foreach (DictionaryKey hookID in removed) {
			inputReader.ReleaseHook ( OwnedHooks[hookID] );
			OwnedHooks.Remove ( hookID );
		}
		return removed;
	}

	public override HHookInfo GetHook ( DeviceID device, VKChange vKChange ) {
		if ( !ActiveHooks.TryGetValue ( device, out HookGroup dev ) ) return null;
		var hookID = dev[vKChange];
		if ( hookID != DictionaryKey.Empty && OwnedHooks.TryGetValue ( hookID, out var hookInfo ) ) return hookInfo;
		return null;
	}

	public override void ClearHooks ( DeviceID device = 0 ) {
		if ( ActiveHooks.ContainsKey ( device ) ) {
			ActiveHooks[device].Clear ();
			ActiveHooks.Remove ( device );
		}
		List<CallbackHolder> cbToRemove = new ();
		foreach ( var cb in Callbacks ) if ( cb.Key.Item1 == device ) cbToRemove.AddRange ( cb.Value );
		foreach ( var cb in cbToRemove ) cb.Unregister ();

	}

	public override Dictionary<DeviceID, DictionaryKey> ListHooks () {
		Dictionary<DeviceID, DictionaryKey> ret = new ();
		foreach ( var deviceHooks in ActiveHooks )
			foreach ( var hook in deviceHooks.Value.AllUniqueHooks )
				ret.Add ( deviceHooks.Key, hook );
		// Shouldn't this maybe return Tuple<VKChange, hookID>?
		return ret;
	}

	private bool FastCB ( DictionaryKey key, HInputEventDataHolder e ) {
		if ( !Callbacks.TryGetValue ( (e.HookInfo.DeviceID, CBType.Fast), out var cbs ) ) return true; // no callback of this type, pass to another callback
		foreach ( var cb in cbs ) if ( !cb.callback ( e ) ) return false;
		return true;
	}
	private void DelayedCB ( DictionaryKey key, HInputEventDataHolder e ) {
		if ( !Callbacks.TryGetValue ( (e.HookInfo.DeviceID, CBType.Delayed), out var cbs ) ) return;
		foreach ( var cb in cbs ) if ( !cb.callback ( e ) ) return;
	}

	public override ComponentUIParametersInfo GetUIDescription () {
		var hooksSeparator = new UI_Separator.Factory ()
			.AsHeader ()
			.WithName ( "HooksSeparator" )
			.WithLabel ( "Hooks Management" )
			.WithDescription ( "Manage active input hooks" )
			.Build ();

		var deviceSelector = new UI_DropDown.Factory ()
			.WithOptionUpdator ( () => {
				if ( ActiveHooks.Count == 0 ) return new List<string> { "No devices" };
				return ActiveHooks.Keys.Select ( d => $"Device {d}" ).ToList ();
			} )
			.WithName ( "DeviceSelector" )
			.WithLabel ( "Select Device" )
			.WithDescription ( "Select a device to view and manage its hooks" )
			.Build<UI_DropDown> ();

		var hooksList = new UI_ListView.Factory ()
			.UpdatedByDropDown ( deviceSelector, ( selID ) => {
				if ( ActiveHooks.Count == 0 || selID < 0 ) return new List<string> { "No hooks registered" };
				var deviceID = ActiveHooks.Keys.ElementAt ( selID );
				if ( !ActiveHooks.TryGetValue ( deviceID, out var hookGroup ) )
					return new List<string> { "Device not found" };

				var hooks = new List<string> ();
				foreach ( var vkChange in SupportedVKs ) {
					var hookID = hookGroup[vkChange];
					if ( hookID != DictionaryKey.Empty ) {
						hooks.Add ( $"{vkChange}: {hookID}" );
					}
				}
				return hooks.Count > 0 ? hooks : new List<string> { "No active hooks for this device" };
			} )
			.WithName ( "HooksList" )
			.WithLabel ( "Active Hooks" )
			.WithDescription ( "List of active hooks for the selected device" )
			.Build ();

		var totalHooksField = new UI_IntField.Factory ()
			.WithName ( "TotalHooks" )
			.WithLabel ( "Total Hooks" )
			.WithDescription ( "Total number of unique hooks registered across all devices" )
			.WithPureUpdater ( () => OwnedHooks.Count )
			.Build ();

		var totalDevicesField = new UI_IntField.Factory ()
			.WithName ( "TotalDevices" )
			.WithLabel ( "Active Devices" )
			.WithDescription ( "Total number of devices with active hooks" )
			.WithPureUpdater ( () => ActiveHooks.Count )
			.Build ();

		var removeHookButton = new UI_ActionButton.Factory ()
			.WithOnClick ( () => {
				if ( deviceSelector.Value.selID < 0 || ActiveHooks.Count == 0 ) return;
				var deviceID = ActiveHooks.Keys.ElementAt ( deviceSelector.Value.selID );
				RemoveHook ( deviceID, SupportedVKs );
			} )
			.WithName ( "RemoveHookButton" )
			.WithLabel ( "Remove All Hooks" )
			.WithDescription ( "Remove all hooks for the selected device" )
			.Build ();

		var clearAllButton = new UI_ActionButton.Factory ()
			.WithOnClick ( () => {
				foreach ( var deviceID in ActiveHooks.Keys.ToList () ) {
					ClearHooks ( deviceID );
				}
			} )
			.WithName ( "ClearAllButton" )
			.WithLabel ( "Clear All Hooks" )
			.WithDescription ( "Remove hooks for all devices" )
			.Build ();

		var callbacksSeparator = new UI_Separator.Factory ()
			.AsHeader ()
			.WithName ( "CallbacksSeparator" )
			.WithLabel ( "Callbacks" )
			.WithDescription ( "View registered callbacks" )
			.Build ();

		var callbacksList = new UI_ListView.Factory ()
			.WithName ( "CallbacksList" )
			.WithLabel ( "Registered Callbacks" )
			.WithDescription ( "List of callback registrations by device and type" )
			.WithPureUpdater ( () => {
				if ( Callbacks.Count == 0 ) return ["No callbacks registered"];
				return Callbacks
					.Select ( kvp => $"Device {kvp.Key.Item1}, {kvp.Key.Item2}: {kvp.Value.Count} callback(s)" )
					.ToList ();
			} )
			.Build ();

		var totalCallbacksField = new UI_IntField.Factory ()
			.WithName ( "TotalCallbacks" )
			.WithLabel ( "Total Callbacks" )
			.WithDescription ( "Total number of callback registrations" )
			.WithPureUpdater ( () => Callbacks.Values.Sum ( cbs => cbs.Count ) )
			.Build ();

		var verbositySeparator = new UI_Separator.Factory ()
			.AsHeader ()
			.WithName ( "VerbositySeparator" )
			.WithLabel ( "Settings" )
			.WithDescription ( "Hook manager settings" )
			.Build ();

		var verbosityDropDown = new UI_DropDown.Factory ()
			.WithInitialValue ( (Verbosity, ["Verbose", "Silent"]) )
			.WithName ( "VerbosityDropDown" )
			.WithLabel ( "Verbosity" )
			.WithDescription ( "Set verbosity level for the hook manager" )
			.Build<UI_DropDown> ();

		var applyVerbosityButton = new UI_ActionButton.Factory ()
			.WithOnClick ( () => {
				Verbosity = verbosityDropDown.Value.selID;
			} )
			.WithName ( "ApplyVerbosityButton" )
			.WithLabel ( "Apply Verbosity" )
			.WithDescription ( "Apply the selected verbosity setting" )
			.Build ();

		return new ComponentUIParametersInfo.Factory ()
			.WithDefaultID ()
			.WithComponentType ( GetType () )
			.AddParameters (
				hooksSeparator, deviceSelector, hooksList, totalHooksField, totalDevicesField,
				removeHookButton, clearAllButton,
				callbacksSeparator, callbacksList, totalCallbacksField,
				verbositySeparator, verbosityDropDown, applyVerbosityButton
			)
			.WithName ( "Hook Manager" )
			.WithLabel ( "VHookManager UI" )
			.WithDescription ( "Manage input hooks, callbacks, and monitor hook activity" )
			.Build () as ComponentUIParametersInfo;
	}

	/// <summary>Since hooks don't map 1:1 to 'hook actions' (e.g. keydown), this class serves as abstraction of such mapping, i.e. presents as Dictionary&lt;VKChange, DictionaryKey&gt; ??Is this still valid after '2 HHookInfo should hold VKChange=>HookD mapping'??</summary>
	protected class HookGroup {
		// This might better be both-way dictionary, but atm the mapping DictKey->VKChange seems to not be needed.
		private readonly Dictionary<VKChange, DictionaryKey> hookDict = new ();
		private readonly HashSet<DictionaryKey> allHooks = new ();

		public HashSet<DictionaryKey> Add ( IDictionary<VKChange, DictionaryKey> setup ) {
			HashSet<DictionaryKey> newHooks = new ();
			foreach ( var (key, value) in setup )
				if ( !hookDict.TryAdd ( key, value ) )
					throw new InvalidOperationException ( $"Hook for {key} already exists" );
				else if ( allHooks.Add ( value ) ) newHooks.Add ( value );
			return newHooks;
		}

		// Maybe some way to storing removed hooks / temporarily disabling them? Not sure where and how to implement it
		public HashSet<DictionaryKey> Remove ( ICollection<VKChange> vkChanges ) {
			HashSet<DictionaryKey> removed = [];
			HashSet<DictionaryKey> candidates = [];
			foreach ( var vkChange in vkChanges ) {
				if ( hookDict.TryGetValue ( vkChange, out var hook ) ) {
					candidates.Add ( hook );
					hookDict.Remove ( vkChange );
				}
			}
			foreach ( var candidate in candidates ) {
				if ( hookDict.ContainsValue ( candidate ) ) continue;
				if ( allHooks.Remove ( candidate ) ) removed.Add ( candidate );
				else throw new InvalidOperationException ( "Hook not found in allHooks" );
			}
			return removed;
		}

		public DictionaryKey this[VKChange vkChange] => hookDict.GetValueOrDefault ( vkChange, DictionaryKey.Empty );

		public IReadOnlyCollection<DictionaryKey> AllUniqueHooks => allHooks.ToList ().AsReadOnly ();

		public void Clear () {
			hookDict.Clear ();
		}
	}
}