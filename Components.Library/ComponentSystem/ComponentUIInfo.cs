namespace Components.Library.ComponentSystem;
public abstract class ComponentUIParameter (string name, string label, string description, Type type ) {
	public enum RelativePosition { Above, Below, Left, Right }

	public ComponentUIParameter Owner;
	public readonly string Name = name;
	public readonly string Label = label;
	public readonly string Description = description;
	public readonly Type Type = type;
	public RelativePosition Position;
	public event Action<ComponentUIParameter> OnDataChanged; // Notify upstream (UI) about change in data source
	public event Action<ComponentUIParameter> OnUpdateRequested; // Notify downstream (data source) about request to fetch new value from data source

	//protected bool Dirty = true;
	protected abstract void RequestUpdateValue ();

	///<summary>Use this at data source to mark the value as dirty and notify the UI to fetch the new value</summary>
	public void NotifyDataChanged () {
		RequestUpdateValue (); // Fetch the new value
	}
	protected void NotifyFinishedUpdate () {
		OnDataChanged?.Invoke ( this );
	}

	///<summary>Use this in the UI to request the latest value from the data source and update the UI accordingly</summary>
	public void RequestUpdate () {
		RequestUpdateValue (); // Fetch the new value
		OnUpdateRequested?.Invoke ( this );
	}
}

/// <summary>
/// Base class for typed UI parameters. Use this when you only care about the data type T
/// and don't need to interact with the factory's CRTP pattern.
/// </summary>
public abstract class ComponentUIParameter<T> : ComponentUIParameter {
	public T Value { get; protected set; }
	public abstract bool IsReadOnly { get; }

	protected ComponentUIParameter ( string name, string label, string description )
		: base ( name, label, description, typeof( T ) ) {
	}

	/// <summary>Test that new value is valid or return error message. Doesn't apply</summary>
	public abstract (bool, string) Validate ( T newVal );

	/// <summary>Apply the new value if valid, call Apply action and request update.</summary>
	/// <returns>Validation status</returns>
	public abstract (bool, string) ApplyValue ( T newVal );
}

/// <summary>
/// Fully typed UI parameter with factory support. This is what factory methods construct.
/// External consumers should prefer using ComponentUIParameter&lt;T&gt; for cleaner code.
/// </summary>
public abstract class ComponentUIParameter<T, TF> : ComponentUIParameter<T> where TF : UI_Factory<T, TF> {
	public override bool IsReadOnly => Validator == null;
	private readonly Func<ComponentUIParameter<T, TF>, T, T> _UpdateValue;
	private readonly Func<T, T, (bool, string)> Validator;
	/// <inheritdoc cref="UI_Factory{T}.WithPureApply(Action{T})"/>
	private readonly Action<T> Apply;

	protected ComponentUIParameter ( UI_Factory<T, TF> factory )
		: base (factory.Name, factory.Label, factory.Description) {
		_UpdateValue = factory.GetUpdater ();
		Value = factory.InitialValue;
		if ( Value == null && _UpdateValue == null )
			throw new InvalidOperationException ( "Initial value cannot be null for a parameter without an updater. Please provide a valid initial value." );
		Value ??= _UpdateValue ( this, default );
		if ( Value == null ) throw new InvalidOperationException ( "Initial value cannot be null. Please provide a valid initial value or ensure that the updater can return a non-null value." );
		(Validator, Apply) = factory.GetValidator ( Value, Value );
	}

	protected override void RequestUpdateValue () {
		//UpdateValue ();
		if ( _UpdateValue == null ) return;
		T newVal = _UpdateValue ( this, Value );
		if ( !Equals ( newVal, Value ) ) {
			Value = newVal;
			NotifyFinishedUpdate ();
		}
	}

	/// <summary>Test that new value is valid or return error message. Doesn't apply</summary>
	public override (bool, string) Validate ( T newVal ) {
		if ( Validator == null )
			return ( false, "This parameter is read-only." );
		return Validator ( newVal, Value );
	}

	/// <summary>Apply the new value if valid, call Apply action (kinda 'main' onUpdate action) and request update that generates new value via Updater and assign to Value.</summary>
	/// <returns>Validation status</returns>
	public override (bool, string) ApplyValue ( T newVal ) {
		if (Validator == null)
			throw new InvalidOperationException ( "This parameter is read-only." );
		var (isValid, errorMessage) = Validator ( newVal, Value );
		if ( !isValid ) return (false, errorMessage);
		Apply?.Invoke ( newVal );
		Value = newVal;
		RequestUpdateValue ();
		return (true, null);
	}
}

public abstract class UI_Factory<T, FT> where FT : UI_Factory<T, FT> {
	public string Name { get; private set; }
	public string Label { get; private set; }
	public string Description { get; private set; }
	private Func<T> PureUpdater;
	private Func<T, T> ValuedUpdater;
	private Func<ComponentUIParameter<T, FT>, T> ReferencedUpdater;
	private Func<ComponentUIParameter<T, FT>, T, T> CombinedUpdater;
	private Func<T, T, (bool, string)> Validator;
	private Action<T> Apply;
	public readonly List<ComponentUIParameter> Updaters = [];
	public bool IsReadonly { get; private set; }
	public bool IsConstant { get; private set; }
	public T InitialValue { get; private set; }
	public ComponentUIParameter.RelativePosition Position { get; private set; } = ComponentUIParameter.RelativePosition.Below;


	public virtual FT WithName ( string name ) { Name = name; return (FT)this; }
	public virtual FT WithLabel ( string label ) { Label = label; return (FT)this; }
	public virtual FT WithDescription ( string description ) { Description = description; return (FT)this; }
	/// <summary>Updater that doesn't rely on anything else. Updater will fetch the new value and later on will be assigned (during ApplyValue->RequestUpdate)</summary>
	public virtual FT WithPureUpdater ( Func<T> updater ) { PureUpdater = updater; return (FT)this; }
	public virtual FT WithReferencedUpdater ( Func<ComponentUIParameter<T, FT>, T> updater ) { ReferencedUpdater = updater; return (FT)this; }
	public virtual FT WithValuedUpdater ( Func<T, T> updater ) { ValuedUpdater = updater; return (FT)this; }
	public virtual FT WithCombinedUpdater ( Func<ComponentUIParameter<T, FT>, T, T> updater ) { CombinedUpdater = updater; return (FT)this; }
	public virtual FT WithPureValidator ( Func<T, T, (bool, string)> validator ) { Validator = validator; return (FT)this; }
	/// <summary>Apply action that will be called during ApplyValue if validation is successful.
	/// Will not set the new value (that is done automatically after this), but serve as a place to perform any side effects.</summary>
	public virtual FT WithPureApply ( Action<T> apply ) { Apply = apply; return (FT)this; }
	public virtual FT WithPosition ( ComponentUIParameter.RelativePosition position ) { Position = position; return (FT)this; }
	public virtual FT PreferOnRight () => WithPosition ( ComponentUIParameter.RelativePosition.Right );

	public virtual FT AssertDynamic () {
		if ( GetUpdater () == null ) throw new InvalidOperationException ( "At least one type of updater must be set for a dynamic parameter." );

		var (validator, applier) = GetValidator ( InitialValue ?? default, InitialValue ?? default );
		if ( validator == null || applier == null ) throw new InvalidOperationException ( "Validator and Apply action must be set together for a dynamic parameter." );

		return (FT)this;
	}


	public FT ForceDynamic () {
		IsReadonly = IsConstant = false;
		WithPureValidator ( DefaultValidator ).WithPureApply ( _ => { } );
		return (FT)this;
	}

	public virtual FT ForceReadOnly () { IsReadonly = true; return (FT)this; }
	public virtual FT ForceConstant () { IsConstant = true; return (FT)this; }
	public virtual FT UpdatedBy ( ComponentUIParameter other ) { Updaters.Add ( other ); return (FT)this; }
	public virtual FT WithInitialValue ( T initialValue ) { InitialValue = initialValue; return (FT)this; }

	public ComponentUIParameter Build () {
		ArgumentNullException.ThrowIfNullOrWhiteSpace ( Name, nameof(Name) );
		ArgumentNullException.ThrowIfNullOrWhiteSpace ( Label, nameof(Label) );
		ArgumentNullException.ThrowIfNullOrWhiteSpace ( Description, nameof(Description) );
		var ret = BuildInner ();
		foreach ( var updater in Updaters )
			updater.OnDataChanged += ( _ ) => ret.RequestUpdate ();
		return ret;
	}
	public P Build<P> () where P : ComponentUIParameter<T, FT> {
		if ( typeof ( P ) != typeof ( ComponentUIParameter<T, FT> ) && !typeof ( P ).IsSubclassOf ( typeof ( ComponentUIParameter<T, FT> ) ) )
			throw new InvalidOperationException ( $"Type parameter P must be ComponentUIParameter<{typeof(T).Name}> or its subclass." );

		return Build () as P;
	}
	protected abstract ComponentUIParameter BuildInner ();
	protected abstract (bool, string) DefaultValidator ( T newVal, T oldVal );

	public Func<ComponentUIParameter<T, FT>, T, T> GetUpdater () {
		Func<ComponentUIParameter<T, FT>, T, T> ret = null;
		if ( IsConstant ) ret = null;
		else if ( PureUpdater != null ) ret = ( _, __ ) => PureUpdater ();
		else if ( ReferencedUpdater != null ) ret = ( param, _ ) => ReferencedUpdater ( param );
		else if ( ValuedUpdater != null ) ret = ( _, oldVal ) => ValuedUpdater ( oldVal );
		else if ( CombinedUpdater != null ) ret = CombinedUpdater;
		return ret;
	}

	public (Func<T, T, (bool, string)>, Action<T>) GetValidator ( T mockOldValue, T newValue ) {
		if (Validator == null ^ Apply == null)
			throw new InvalidOperationException ( "Both Validator and Apply must be set together." );

		if ( Validator != null ) {
			try {
				Validator ( newValue, mockOldValue );
				Apply ( newValue );
			} catch ( Exception ex ) {
				// We don't care about the specific result here (as we might not know any valid value).
				// Aim here is to ensure that these methods are not likely to throw exception with
				throw new InvalidOperationException ( $"Validation or application failed during factory test: {ex.Message}", ex );
			}
		}

		return (Validator, Apply);
	}
}

public class UI_IntField : ComponentUIParameter<int, UI_IntField.Factory> {
	protected UI_IntField (Factory factory) : base (factory) { }

	public class Factory : UI_Factory<int, Factory> {
		protected override UI_IntField BuildInner () {
			return new (this);
		}

		protected override (bool, string) DefaultValidator ( int newVal, int oldVal ) {
			if ( newVal is < -1000 or > 1000 )
				return ( false, "Value must be between -1000 and 1000." );
			return ( true, null );
		}
	}
}

public class UI_ListView : ComponentUIParameter<List<string>, UI_ListView.Factory> {
	public readonly IReadOnlyList<ComponentUIParameter> SubParameters;

	protected UI_ListView (Factory factory) : base (factory) { }

	public class Factory : UI_Factory<List<string>, Factory> {
		public Factory UpdatedByDropDown ( UI_DropDown controller, Func<int, List<string>> updateValue ) {
			WithPureUpdater ( () => {
				ArgumentNullException.ThrowIfNull ( controller, nameof(controller) );
				if ( controller.Value.options == null || controller.Value.options.Count == 0 )
					return [ "No options available." ];
				int selID = controller.Value.selID;
				if ( selID < 0 || selID >= controller.Value.options.Count )
					return [ $"No selection (index {selID})" ];

				return updateValue ( selID );
			} );
			UpdatedBy ( controller );
			return this;
		}

		protected override UI_ListView BuildInner () {
			return new UI_ListView ( this );
		}

		protected override (bool, string) DefaultValidator ( List<string> newVal, List<string> oldVal ) {
			return newVal == null ? ( false, "Value cannot be null." ) : ( true, null );
		}
	}
}

public class UI_DropDown : ComponentUIParameter<(int selID, List<string> options), UI_DropDown.Factory> {
	protected UI_DropDown (Factory factory) : base (factory) { }

	/// <inheritdoc cref="ComponentUIParameter{T}.Validate(T)"/>
	public (bool, string) Validate ( int newSelID ) => Validate ( (newSelID, Value.options) );
	/// <inheritdoc cref="ComponentUIParameter{T}.ApplyValue(T)"/>
	public void ApplyValue ( int newSelID ) => ApplyValue ( (newSelID, Value.options) );

	private static (bool, string) Validate ( int oldVal, int newVal, List<string> options ) {
		if ( options.Count == 0 )
			return (false, "No options available.");
		if ( newVal < 0 || newVal >= options.Count )
			return (false, $"Selection index '{newVal}' is out of range (0-{options.Count - 1}).");
		return (true, null);
	}

	public class Factory : UI_Factory<(int selID, List<string> opts), Factory> {
		private UI_DropDown builtInstance; // Ugly hack, please expand the validation/apply actions with caller reference later on. 🙏
		public Factory WithOptionUpdator ( Func<List<string>> optionGetter ) { WithValuedUpdater ( (old) => (old.selID, optionGetter ()) ); return this; }
		public Factory WithEmptyOption (string emptyOptionText = " -- ") {
			WithInitialValue ( (0, [emptyOptionText]) );
			return this;
		}
		public Factory WithSelectionAcceptor () {
			WithPureValidator ( ( oldV, newV ) => {
					if ( newV.opts.Count == 0 ) return (false, "No options available.");
					if ( newV.selID < 0 || newV.selID >= newV.opts.Count ) return (false, $"Selection index '{newV.selID}' is out of range (0-{newV.opts.Count - 1}).");

					return (true, null);
				}
			);
			WithPureApply ( ( _ ) => { } ); // No side effects, but ensures that ApplyValue can be called without exception
			return this;
		}

		protected override UI_DropDown BuildInner () {
			// if ( InitialValue.Item2 is { Count: > 0 } ) {
			// 	if ( !IsReadonly && ( PureUpdater != null || ReferencedUpdater != null) )
			// 		throw new NotImplementedException ( "fdsa" ); // We'll add support soon enough 😉
			// 	return new (Name, Label, Description, InitialValue.Item2, null);
			// } else
			// 	throw new NotImplementedException ( "asdf" );
			return builtInstance = new (this);
		}

		protected override (bool, string) DefaultValidator ( (int selID, List<string> opts) newVal, (int selID, List<string> opts) oldVal ) {
			if ( newVal.opts.Count == 0 )
				return (false, "No options available.");
			if ( newVal.selID < 0 || newVal.selID >= newVal.opts.Count )
				return (false, $"Selection index '{newVal.selID}' is out of range (0-{newVal.opts.Count - 1}).");
			return (true, null);
		}
	}
}

public class UI_TextField : ComponentUIParameter<string, UI_TextField.Factory> {
	protected UI_TextField (Factory factory) : base (factory) { }

	public class Factory : UI_Factory<string, Factory> {
		protected override UI_TextField BuildInner () {
			return new ( this );
		}

		protected override (bool, string) DefaultValidator ( string newVal, string oldVal ) {
			return string.IsNullOrEmpty ( newVal ) ? ( false, "Value cannot be empty." ) : ( true, null );
		}
	}
}


public class UI_ActionButton : ComponentUIParameter<bool, UI_ActionButton.Factory> {
	protected UI_ActionButton ( Factory factory, Action onClick ) : base ( factory ) {
		OnClick = onClick;
	}

	public readonly Action OnClick;

	public class Factory : UI_Factory<bool, Factory> {
		protected Action _onClick;
		public Factory WithOnClick ( Action onClick ) { _onClick = onClick; return this; }
		protected override UI_ActionButton BuildInner () => new ( this, _onClick );
		protected override (bool, string) DefaultValidator ( bool newVal, bool oldVal )
			=> throw new NotImplementedException ( "Action button doesn't have a value to validate." );
	}
}

public class UI_Separator : ComponentUIParameter<bool, UI_Separator.Factory> {
	public enum SepType { Major, Header, Minor, Line, Space }
	public readonly SepType SeparatorType;

	protected UI_Separator ( Factory factory ) : base ( factory ) {
		SeparatorType = factory.SeparatorType;
	}

	public class Factory : UI_Factory<bool, Factory> {
		public SepType SeparatorType { get; private set; } = SepType.Minor;
		/// <summary>Major separator, usually used to separate different sections. Like 'user' and 'admin' sections.</summary>
		public Factory AsMajor () { SeparatorType = SepType.Major; return this; }
		/// <summary>Header separator, usually used to separate different groups within the same section. Like various 'groups' in some Settings menu.</summary>
		public Factory AsHeader () { SeparatorType = SepType.Header; return this; }
		/// <summary>Minor separator, usually used to separate closely related parameters. Like individual comments in a comment section.</summary>
		public Factory AsMinor () { SeparatorType = SepType.Minor; return this; }
		/// <summary>Prefer simple horizontal line as separator. Doesn't show label by default.</summary>
		public Factory AsLine () { SeparatorType = SepType.Line; return this; }
		 /// <summary>Prefer just empty space as separator. Doesn't show label by default.</summary>
		public Factory AsSpace () { SeparatorType = SepType.Space; return this; }
		protected override UI_Separator BuildInner () => new ( this );
		protected override (bool, string) DefaultValidator ( bool newVal, bool oldVal )
			=> throw new NotImplementedException ( "UI separator doesn't have a value to validate." );
	}
}

public class ComponentUIParametersInfo : ComponentUIParameter {
	public readonly string ComponentName, ComponentDescription;
	public readonly string ComponentID;
	public readonly Type ComponentType;
	public readonly IReadOnlyList<ComponentUIParameter> Parameters;
	public readonly Action OnDestroy;
	public readonly Action<object[]> OnApply;
	public readonly Func<string[], object[], object[]> OnBatchUpdate;
	//public new event Action OnDataChanged, OnUpdateRequested;

	private ComponentUIParametersInfo (
		string componentName
		, string componentID
		, string componentDescription
		, Type componentType
		, IReadOnlyList<ComponentUIParameter> parameters
		, Action onDestroy
		, Action<object[]> onApply
		, Func<string[], object[], object[]> onBatchUpdate = null
		) : base ( componentID, componentName, componentDescription, componentType ) {
		ComponentName = componentName;
		ComponentID = componentID;
		ComponentDescription = componentDescription;
		ComponentType = componentType;
		Parameters = parameters;
		OnDestroy = onDestroy;
		OnApply = onApply;
		OnBatchUpdate = onBatchUpdate;

		foreach ( var param in Parameters ) 			param.Owner = this;
	}


	public new void NotifyDataChanged () {
		foreach ( var param in Parameters )
			param.NotifyDataChanged ();
		NotifyFinishedUpdate ();
	}

	public new void RequestUpdate () {
		foreach ( var param in Parameters )
			param.RequestUpdate ();
		base.RequestUpdate ();
	}
	protected override void RequestUpdateValue () { }

	public class Factory : UI_Factory<IReadOnlyList<ComponentUIParameter>, Factory> {
		private static readonly object IDCounterLock = new ();
		private static int DefaultIDCounter = 21; // Only adults are allowd 😉 Any non-zero number if fine.
		private int GroupID = -1;
		private Type ComponentType;
		private readonly List<ComponentUIParameter> Parameters = [];
		private Action OnDestroy = null;
		private Action<object[]> OnApply = null;
		private Func<string[], object[], object[]> OnBatchUpdate = null;

		public Factory WithComponentType ( Type componentType ) { ComponentType = componentType; return this; }
		public Factory WithGroupID ( int groupID ) { GroupID = groupID; return this; }
		public Factory WithDefaultID () { lock (IDCounterLock) { GroupID = DefaultIDCounter++; } return this; }
		public Factory AddParameter ( ComponentUIParameter parameter ) { Parameters.Add ( parameter ); return this; }
		public Factory AddParameters ( params ComponentUIParameter[] parameters ) { Parameters.AddRange ( parameters ); return this; }
		public Factory WithOnDestroy ( Action onDestroy ) { OnDestroy = onDestroy; return this; }
		public Factory WithOnApply ( Action<object[]> onApply ) { OnApply = onApply; return this; }
		public Factory WithOnBatchUpdate ( Func<string[], object[], object[]> onBatchUpdate ) { OnBatchUpdate = onBatchUpdate; return this; }
		public override Factory WithName ( string name ) => base.WithName ( name ).WithLabel ( name ) as Factory;

		protected override ComponentUIParameter BuildInner () {
			ArgumentNullException.ThrowIfNull ( ComponentType );
			ArgumentOutOfRangeException.ThrowIfNegative ( GroupID );
			return new ComponentUIParametersInfo (
				Name, $"#{GroupID}", Description, ComponentType
				, Parameters.ToArray (), OnDestroy, OnApply, OnBatchUpdate
			);
		}

		protected override (bool, string) DefaultValidator ( IReadOnlyList<ComponentUIParameter> newVal, IReadOnlyList<ComponentUIParameter> oldVal )
			=> throw new NotImplementedException ( "ComponentUIParametersInfo doesn't have a value to validate." );
	}
}