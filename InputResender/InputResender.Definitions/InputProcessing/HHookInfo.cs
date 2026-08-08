using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using InputResender.Services;
using MdxLibs.Core;
using MdxLibs.Services;

namespace InputResender.Definitions.InputProcessing {
	public class HHookInfo : DataHolderBase<ComponentBase> {
		/// <summary>Internaly modifiable list of possible input event types.<para>Keep in mind, that hookIDs doesn't need to fit 1to1 with changeMask. That is dependent on low-level specs.</para></summary>
		protected virtual Dictionary<VKChange, DictionaryKey> changeMask {  get; set; }
		public virtual int DeviceID { get; protected set; }
		/// <summary>Should be probably bound together with LatestChangeType as tuple</summary>
		public virtual int LatestDeviceID { get; protected set; }
		public virtual DLowLevelInput HookLLCallback { get; protected set; }
		public virtual ImmutableList<VKChange> ChangeMask { get => changeMask.Keys.ToImmutableList (); }
		/// <summary>List of all assigned hookIDs.<para>Low-Level parser is needed to bind hookID to corresponding ChangeMask(s), since it's dependent on low-level implementation.</para></summary>
		public virtual ImmutableList<DictionaryKey> HookIDs { get => changeMask.Values.Distinct ().ToImmutableList (); }

		public HHookInfo ( ComponentBase owner, int deviceID, VKChange firstAcceptedChange, params VKChange[] acceptedChanges ) : this ( owner, deviceID ) {
			changeMask.Add ( firstAcceptedChange, DictionaryKey.Empty );
			for (int i = 0; i < acceptedChanges.Length; i++)
				changeMask.Add ( acceptedChanges[i], DictionaryKey.Empty );
		}
		private HHookInfo (ComponentBase owner, int deviceID) : base ( owner ) {
			DeviceID = deviceID;
			changeMask = new ();
		}

		public virtual void AssignHookCallback (DLowLevelInput hookCallback) { HookLLCallback = hookCallback; }
		public virtual void AddHookID ( DictionaryKey hookID, VKChange vkChange ) => changeMask[vkChange] = hookID;
		public virtual void RemoveHookID ( DictionaryKey hookID, VKChange vkChange ) {
			if ( !changeMask.ContainsKey ( vkChange ) ) throw new KeyNotFoundException ( $"Couldn't find hookID for {vkChange}" );
			changeMask[vkChange] = DictionaryKey.Empty;
		}

		public override DataHolderBase<ComponentBase> Clone () {
			var ret = new HHookInfo ( Owner, DeviceID );
			foreach ( var (key, value) in changeMask )
				ret.changeMask.Add ( key, value );
			return ret;
		}
		public override bool Equals ( object obj ) {
			if ( obj == null ) return false;
			if ( obj.GetType () != GetType () ) return false;
			var item = (HHookInfo)obj;
			int maskN = ChangeMask.Count;
			if ( maskN != item.ChangeMask.Count ) return false;
			if ( DeviceID != item.DeviceID ) return false;
			if ( !ChangeMask.SequenceEqual ( item.ChangeMask ) ) return false;
			return true;
		}

		/// <summary>LHS ∈ RHS, or that RHS isn't missing any data provided by LHS</summary>
		public static bool operator < ( HHookInfo lhs, HHookInfo rhs ) => DoesContain ( lhs, rhs, lhs.HookIDs.ToArray () );
		/// <summary>RHS ∈ LHS, or that LHS isn't missing any data provided by RHS</summary>
		public static bool operator > ( HHookInfo lhs, HHookInfo rhs ) => DoesContain ( rhs, lhs, rhs.HookIDs.ToArray () );
		/// <summary>LHS ∈ RHS, or that RHS isn't missing any data provided by LHS, testing only given HookID instead of all</summary>
		public static bool operator < ( (HHookInfo hookInfo, DictionaryKey hookID) lhs, HHookInfo rhs ) => DoesContain ( lhs.hookInfo, rhs, lhs.hookID );
		/// <summary>LHS ∈ RHS, or that RHS isn't missing any data provided by LHS, testing only given HookID instead of all</summary>
		public static bool operator > ( (HHookInfo hookInfo, DictionaryKey hookID) lhs, HHookInfo rhs ) => DoesContain ( rhs, lhs.hookInfo, lhs.hookID );
		private static bool DoesContain ( HHookInfo smaller, HHookInfo larger, params DictionaryKey[] hookIDs ) {
			bool ret = smaller.DeviceID == larger.DeviceID;
			// (∀vk=>hookID : hookID ∈ hookIDs ) ⇒ (vk=>hookID ∈ larger)
			Dictionary<VKChange, DictionaryKey> existingHooks = new ();
			foreach ( var (key, value) in smaller.changeMask )
				if ( hookIDs.Contains ( value ) )
					existingHooks.Add ( key, value );
			foreach ( var (key, value) in existingHooks ) {
				if ( !larger.changeMask.ContainsKey ( key ) ) return false;
				ret &= larger.changeMask[key] == value;
			}
			return ret;
		}

		public override int GetHashCode () => DeviceID.GetHashCode () ^ ChangeMask.CalcSetHash ();
		public override string ToString () => $"{DeviceID}:[{string.Join(", ", ChangeMask)}]";
	}
}