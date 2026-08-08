using System;
using MdxLibs.Core;

namespace InputResender.WebUI.Visualizers;
public class TextField (UI_TextField uiTextField) : IBlazorVisualizer (uiTextField) {
	private readonly UI_TextField UiTextField = uiTextField;

	public bool IsReadonly => UiTextField.IsReadOnly;

	public string Value {
		get => UiTextField.Value;
		set {
			if ( UiTextField.IsReadOnly )
				throw new InvalidOperationException ( "This field is read-only." );
			var (isValid, errorMessage) = UiTextField.ApplyValue ( value );
			if ( !isValid )
				throw new ArgumentException ( $"Invalid value: {errorMessage}" );
		}
	}

	public override void Apply (object value) {
		if ( IsReadonly ) return;
		throw new NotImplementedException ( "Apply logic not implemented for TextField." );
	}
}