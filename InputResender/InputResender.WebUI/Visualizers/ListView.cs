using System;
using System.Collections.Generic;
using MdxLibs.Core;

namespace InputResender.WebUI.Visualizers;
public class ListView (UI_ListView uiListView) : IBlazorVisualizer (uiListView) {
	private readonly UI_ListView UiListView = uiListView;

	public List<string> Items => UiListView.Value;

	public override void Apply (object value) {
		throw new NotImplementedException ( "Apply logic not implemented for ListView." );
	}
}