using System;
using System.Collections.Generic;
using MdxLibs.Core;
using Microsoft.JSInterop;

namespace InputResender.WebUI;
public class EnvironmentHolder {
	public readonly VWebServerBlazor ServerComponent;
	public readonly CoreBase OwnerCore;
	private readonly Dictionary<string, ComponentUIParametersInfo> _componentUIs;
	public readonly IReadOnlyDictionary<string, ComponentUIParametersInfo> ComponentUIs;
	private readonly List<string> FooterMsgs;
	public readonly IReadOnlyList<string> Footer;
	public event Action RedrawRequested;

	public IJSRuntime JSRuntime;
	private string _theme = "dark";

	public string SelectedTheme {
		get => _theme;
		set {
			if ( value == _theme ) return;
			_theme = value;
			JSRuntime.InvokeVoidAsync ( "ApplyTheme" ).AsTask ().Wait ();
			 RedrawRequested?.Invoke ();
		}
	}

	public EnvironmentHolder ( VWebServerBlazor owner ) {
		ArgumentNullException.ThrowIfNull ( owner, nameof ( owner ) );
		FooterMsgs = [];
		Footer = FooterMsgs.AsReadOnly ();
		ServerComponent = owner;
		OwnerCore = owner.Owner;
		_componentUIs = new ();
		ComponentUIs = _componentUIs.AsReadOnly ();

		UpdateGUI ();
	}

	public void UpdateGUI () {
		_componentUIs.Clear ();
		var blazorFactory = OwnerCore.Fetch<VUIFactoryBlazor> ();
		if ( blazorFactory == null )
			return;
		var components = blazorFactory.GetAllUiGroups ();
		if ( components == null )
			return;

		//return components.Select (c => (GetNavLinkHref(c.ComponentName), c)).ToList ();
		foreach ( var compParamGroup in components ) {
			if ( _componentUIs.ContainsKey ( compParamGroup.Name ) ) {
				PushFooterMsg ( $"Page with the name '{compParamGroup.Name} already exists" );
				continue;
			}
			_componentUIs[compParamGroup.ComponentName] = compParamGroup;
		}
	}

	public void PushFooterMsg ( string msg ) {
		if ( string.IsNullOrEmpty ( msg ) ) return;

		FooterMsgs.AddRange ( msg.Split ( Environment.NewLine.ToCharArray () ) );
		while ( FooterMsgs.Count > 8 ) FooterMsgs.RemoveAt ( 0 );
	}
}