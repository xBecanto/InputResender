using System;
using System.Collections.Generic;
using MdxLibs.Services;

namespace MdxLibs.Core {
	public interface IComponentBase {
		public CoreBase Owner { get; }
		public int ComponentVersion { get; }
		public string Name { get; }
		public string VariantName { get; }
		public Type[] AcceptedTypes { get; }

		public void Clear ();
		public object Fetch ( string opCode, Type type );
		public T Fetch<T> ( string opCode );
		public ComponentUIParametersInfo GetUIDescription ();
		public ComponentBase.StateInfo Info { get; }
	}

	public abstract class ComponentBase : IComponentBase {
		public CoreBase Owner { get; private set; }
		public abstract int ComponentVersion { get; }
		public virtual bool PipelineEnabled { get; set; } = true;
		public readonly DateTime CreationTime;
		public readonly IReadOnlyList<(string opCode, Type opType)> SupportedCommands;
		private static int NextID = Random.Shared.Next ();
		public readonly int ID;
		public string Name { get => ID.ToShortCode (); }
		public virtual string VariantName { get => null; }
		public virtual Type[] AcceptedTypes { get => null; }

		protected ComponentBase() {
			CreationTime = DateTime.Now;
			ID = NextID++;
			List<(string opCode, Type opType)> commands = new List<(string opCode, Type opType)>();
			var cmds = AddCommands ();
			commands.AddRange ( cmds );
			SupportedCommands = commands;
		}

		public virtual void Clear () { }

		protected abstract IReadOnlyList<(string opCode, Type opType)> AddCommands ();
		protected virtual void ChangeOwner ( CoreBase newOwner ) {
			Owner?.Unregister ( this );
			(Owner = newOwner).Register ( this, ChangeOwner );
		}

		/// <summary>This method offers access to the 'ChangeOwner' protected method from outside.
		/// Use only as last option. Requires the owner to not be asigned to go through.</summary>
		public static void AssignOwner ( ComponentBase component, CoreBase newOwner ) {
			if (component.Owner != null) throw new InvalidOperationException ( $"Component {component.Name} already has an owner! Unregister it from current owner before assigning a new one." );
			component.ChangeOwner ( newOwner );
		}

		public object Fetch ( string opCode, Type type ) {
			// Can be overriden to provide own Fetch functionality, but not reccomended.
			foreach (var cmd in SupportedCommands ) {
				if ( cmd.opCode == opCode & cmd.opType == type ) return cmd;
			}
			return null;
		}
		public T Fetch<T> ( string opCode ) => (T)Fetch ( opCode, typeof ( T ) );

		public virtual ComponentUIParametersInfo GetUIDescription () { return null; }

		public abstract StateInfo Info { get; }
		public abstract class StateInfo {
			protected const string BR = "\r\n";
			public readonly ComponentBase Owner;
			public StateInfo ( ComponentBase owner ) {
				Owner = owner;
				GeneralInfo = $"Version: {Owner.ComponentVersion}\r\nCreation time: {Owner.CreationTime}\r\nName: {Owner.Name}\r\nVariant: {Owner.GetType ().Name}{BR}";
			}
			public readonly string GeneralInfo;
			public virtual string AllInfo () => GeneralInfo;
		}
	}
	public abstract class ComponentBase_CoreBase : ComponentBase {
		public ComponentBase_CoreBase (CoreBase newOwner) { base.ChangeOwner ( newOwner ); }
		//public new virtual CoreType Owner { get; protected set; }
		public new virtual CoreBase Owner => base.Owner as CoreBase;
		protected new void ChangeOwner ( CoreBase newOwner ) => base.ChangeOwner ( newOwner );
	}

	public class ComponentMock : ComponentBase_CoreBase {
		public ComponentMock ( CoreBase newOwner ) : base ( newOwner ) {}

		public override int ComponentVersion => 1;

		protected override IReadOnlyList<(string opCode, Type opType)> AddCommands () => new List<(string opCode, Type opType)> () {
				(nameof(MockMethod), typeof(void))
			};

		public virtual void MockMethod () { }

		public override StateInfo Info => new MockStateInfo ( this );
		public class MockStateInfo : StateInfo {
			public MockStateInfo ( ComponentBase owner ) : base ( owner ) { }
			public override string AllInfo () => $"{GeneralInfo}";
		}
	}
}
