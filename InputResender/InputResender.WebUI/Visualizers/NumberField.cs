using System;
using MdxLibs.Core;

namespace InputResender.WebUI.Visualizers;
public class NumberField<T> : IBlazorVisualizer where T : struct, IComparable, IConvertible {
	private readonly ComponentUIParameter<T> UiParameter;
	public readonly bool IsReadonly;

	public NumberField ( ComponentUIParameter<T> uiParameter )
		: base (uiParameter) {
		if ( !typeof(T).IsPrimitive || typeof(T) == typeof(bool) )
			throw new ArgumentException ( "Type must be a numeric primitive." );
		UiParameter = uiParameter;
		//IsReadonly = uiParameter.Validator == null;
		IsReadonly = true;
	}

	public T Number => UiParameter.Value;

	public object Value {
		get => UiParameter.Value;
		set {
			if ( IsReadonly )
				throw new InvalidOperationException ( "This field is read-only." );
			// var (isValid, errorMessage) = UiParameter.Validator ( value, UiParameter.Value );
			// if ( !isValid )
			// 	throw new ArgumentException ( $"Invalid value: {errorMessage}" );
			UiParameter.ApplyValue ( (T)value );
		}
	}

	public override void Apply (object value) {
		if ( IsReadonly ) return;
		throw new NotImplementedException ( "Apply logic not implemented for NumberField." );
	}
}