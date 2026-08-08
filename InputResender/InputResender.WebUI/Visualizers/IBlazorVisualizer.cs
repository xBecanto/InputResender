using System;
using MdxLibs.Core;

namespace InputResender.WebUI.Visualizers;
public abstract class IBlazorVisualizer {
	public readonly string Name, Label, Description;
	private readonly ComponentUIParameter _parameter;
	public event Action OnValueChanged;

	protected IBlazorVisualizer (ComponentUIParameter parameter) {
		_parameter = parameter;
		Name = parameter.Name;
		Label = parameter.Label;
		Description = parameter.Description;
		parameter.OnDataChanged += ( _ ) => UpdateValue ();
	}

	public void RequestUpdate () {
		_parameter.RequestUpdate ();
	}

	public virtual void UpdateValue () {
		// If the UI is directly accessing the parameter.Value, this can be left empty.
		// Otherwise, override this to fetch the new value from the parameter and update the UI accordingly.
		OnValueChanged?.Invoke ();
	}
	public abstract void Apply (object value);
}