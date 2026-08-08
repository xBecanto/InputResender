using System;
using System.Collections.Generic;
using System.Linq;
using InputResender.Definitions;
using MdxLibs.Core;
using MdxLibs.Definitions.UI;

namespace InputResender.WebUI;
public class VUIFactoryBlazor : DUIFactory {
	private readonly Dictionary<ComponentBase, ComponentUIParametersInfo> ComponentElements = [];
	private readonly Dictionary<DCommand_CoreBase, ComponentUIParametersInfo> CommandElements = [];
	public IReadOnlyList<ComponentUIParametersInfo> GetAllUiGroups () {
		var ret = ComponentElements.Values.ToList ();
		ret.AddRange ( CommandElements.Values.ToList () );
		return ret;
	}

	public VUIFactoryBlazor ( CoreBase owner ) : base ( owner ) {
		owner.OnComponentAdded += AskComponentForUI;
	}

	private void AskComponentForUI ( CoreBase.ComponentInfo component ) {
		if ( component == null || component.Component == null )
			return;

		if ( component.Component is CommandProcessor cmdProc )
			cmdProc.OnCommandAdded += AskCommandForUI;

		var info = component.Component.GetUIDescription ();
		if ( info == null )
			return;
		RegisterComponentUI ( component.Component, info );
	}
	private void AskCommandForUI ( DCommand_CoreBase cmd ) {
		if ( cmd == null )
			return;
		var info = cmd.GetUIDescription ();
		if ( info == null )
			return;
		RegisterCommandUI ( cmd, info );
	}

	public override void RegisterComponentUI ( ComponentBase owner, ComponentUIParametersInfo info ) {
		ArgumentNullException.ThrowIfNull ( owner );
		ArgumentNullException.ThrowIfNull ( info );
		if ( !ComponentElements.TryAdd ( owner, info ) )
			throw new InvalidOperationException ( "Component element already registered." );
	}
	public override void RegisterCommandUI ( DCommand_CoreBase cmd, ComponentUIParametersInfo info ) {
		ArgumentNullException.ThrowIfNull ( cmd );
		ArgumentNullException.ThrowIfNull ( info );
		if ( !CommandElements.TryAdd ( cmd, info ) )
			throw new InvalidOperationException ( "Command element already registered." );
	}
	public override void UnregisterComponentUI ( ComponentBase owner ) {
		throw new NotImplementedException ();
	}
	public override void UnregisterCommandUI ( ComponentBase owner ) {
		throw new NotImplementedException ();
	}
	public override StateInfo Info { get; }
}