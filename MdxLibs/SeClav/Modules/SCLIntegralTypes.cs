using System;
using System.Collections.Generic;

namespace SeClav.Modules;
public class SCLT_Any : DataTypeDefinition {
	public override string Name => "Any";
	public override string Description => "Represents any data type. Used for generic operations.";
	public override IReadOnlySet<ICommand> Commands => null;
	public override bool TryParse ( ref string line, out IDataType result ) {
		result = null;
		return false;
	}
	public override IDataType Default {
		get => throw new InvalidOperationException ( "DataType Any is only Marker and cannot be used to hold data!" );
	}
}

public class SCLT_Same : DataTypeDefinition {
	public override string Name => "Same";
	public override string Description => "Allows only types passed as 'any' before this point. Used for generic operations.";
	public override IReadOnlySet<ICommand> Commands => null;
	public override bool TryParse ( ref string line, out IDataType result ) {
		result = null;
		return false;
	}
	public override IDataType Default {
		get => throw new InvalidOperationException ( "DataType Same is only Marker and cannot be used to hold data!" );
	}
}

public class SCLT_Void : DataTypeDefinition {
	public override string Name => "Void";
	public override string Description => "Represents no data. Used for commands that do not return a value.";
	public override IReadOnlySet<ICommand> Commands => null;
	public override bool TryParse ( ref string line, out IDataType result ) {
		result = null;
		return false;
	}
	public override IDataType Default {
		get => null;
	}

	public class VoidData : IDataType {
		public DataTypeDefinition Definition { get; private set; }
		public void Assign ( IDataType value ) {
			throw new InvalidOperationException ( "Cannot assign value to Void type." );
		}

		public VoidData ( SCLT_Void typeDefRef ) {
			Definition = typeDefRef;
		}
	}
}