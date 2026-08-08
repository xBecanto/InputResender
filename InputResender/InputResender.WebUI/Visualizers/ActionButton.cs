using System;
using MdxLibs.Core;

namespace InputResender.WebUI.Visualizers;
public class ActionButton (UI_ActionButton uiActionButton) : IBlazorVisualizer (uiActionButton) {
	public override void Apply (object value) {
		throw new NotImplementedException( "Apply logic not implemented for ActionButton." );
	}

	public void Click () => uiActionButton.OnClick?.Invoke ();
}