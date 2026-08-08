using Microsoft.AspNetCore.Components;
using System.Collections.Generic;
using MdxLibs.Core;

namespace InputResender.WebUI.Visualizers;
public class DropDownList ( UI_DropDown uiDropDown ) : IBlazorVisualizer ( uiDropDown ) {
	public string LastError { get; private set; } = null;

	private readonly UI_DropDown uiDropDown = uiDropDown;
	public List<string> Lines => uiDropDown.Value.options;
	public int SelectedIndex => uiDropDown.Value.selID;

	public void SelectOption ( int index ) {
		if ( index < 0 || index >= Lines.Count ) {
			LastError = $"Option {index} is out of range (0-{Lines.Count - 1}).";
			return;
		}
		if ( uiDropDown.IsReadOnly ) return;
		var res = uiDropDown.Validate ( index );
		if ( !res.Item1 ) {
			LastError = res.Item2;
		} else {
			uiDropDown.ApplyValue ( index );
			uiDropDown.NotifyDataChanged ();
			LastError = null;
		}
	}

	//public override void UpdateValue () {
	//	if (uiDropDown.Value.options.Count == 0)
	//		uiDropDown.ApplyValue ( (0, new List<string> { " -- " }) ); // Kinda a dirty hack, should be imrpoved later on.
	//}

	public override void Apply ( object value ) {
		if ( value is ChangeEventArgs ChEvArg ) {
			if ( ChEvArg.Value == null )
				return;
			value = ChEvArg.Value;
		}
		if ( value is int index )
			SelectOption ( index );
		else if ( value is string indexStr && int.TryParse ( indexStr, out int parsedIndex ) )
			SelectOption ( parsedIndex );
		else
			LastError = $"Value {value} is of type {value.GetType()} instead of int or string.";
	}
}