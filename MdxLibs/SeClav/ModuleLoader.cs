using System;
using System.Collections.Generic;
using MdxLibs.Core;

namespace SeClav;
public abstract class DModuleLoader : ComponentBase_CoreBase {
	public DModuleLoader ( CoreBase owner ) : base ( owner ) { }

	protected override IReadOnlyList<(string opCode, Type opType)> AddCommands () => [
		(nameof(RegisterModule), typeof(void)),
		(nameof(GetModule), typeof(IModuleInfo)),
	];

	public abstract void RegisterModule ( IModuleInfo module );
	public abstract IModuleInfo GetModule ( string name );

	public abstract class DStateInfo : StateInfo {
		public readonly string[] Modules;

		public DStateInfo ( DModuleLoader owner, int moduleCnt ) : base ( owner ) { Modules = new string[moduleCnt]; }
		public override string AllInfo () => $"{base.AllInfo ()}{BR}Modules:{BR}{string.Join ( BR, Modules )}";
	}
}

public class VModuleLoader : DModuleLoader {
	readonly Dictionary<string, IModuleInfo> modules = [];

	public override int ComponentVersion => 1;
	public VModuleLoader ( CoreBase owner ) : base ( owner ) { }

	public override void RegisterModule ( IModuleInfo module ) {
		ArgumentNullException.ThrowIfNull ( module );
		if (modules.ContainsKey(module.Name)) throw new InvalidOperationException($"Module with name '{module.Name}' is already registered.");
		modules[module.Name] = module;
	}

	public override IModuleInfo GetModule ( string name ) {
		ArgumentNullException.ThrowIfNull ( name );
		if (modules.TryGetValue ( name, out var module )) return module;
		throw new KeyNotFoundException ($"Module with name '{name}' not found.");
	}

	public override StateInfo Info => new MStateInfo ( this );

	public class MStateInfo : DStateInfo {
		public MStateInfo ( VModuleLoader owner ) : base ( owner, owner.modules.Count ) {
			int ID = 0;
			foreach ( var module in owner.modules )
				Modules[ID++] = $"{module.Key}: {module.Value.Description}";
		}
		public override string AllInfo () => $"{base.AllInfo ()}{BR}Module Count: {Modules.Length}";
	}
}