#nullable enable

namespace Components.Interfaces.SeClav.Parsing;

public class SeClavException : Exception {
	public SeClavException ( string message, Exception? innerException = null ) : base ( message, innerException ) { }
	public virtual bool IsWarning => false;
}

public class SCLParsingException : SeClavException {
	public string? SourceLine { get; }

	public SCLParsingException ( string message, string? sourceLine = null, Exception? innerException = null )
		: base ( message, innerException ) {
		SourceLine = sourceLine;
	}
}

public class SCLSyntaxException : SCLParsingException {
	public SCLSyntaxException ( string message, string? sourceLine = null, Exception? innerException = null )
		: base ( message, sourceLine, innerException ) { }
}

public class SCLDirectiveException : SCLParsingException {
	public SCLDirectiveException ( string message, string? sourceLine = null, Exception? innerException = null )
		: base ( message, sourceLine, innerException ) { }
}

public class SCLMacroException : SCLParsingException {
	public SCLMacroException ( string message, string? sourceLine = null, Exception? innerException = null )
		: base ( message, sourceLine, innerException ) { }
}

public class SCLStateTransitionException : SCLParsingException {
	public SCLStateTransitionException ( string message, string? sourceLine = null, Exception? innerException = null )
		: base ( message, sourceLine, innerException ) { }
}

public class SCLCommandArgumentException : SCLParsingException {
	public SCLCommandArgumentException ( string message, string? sourceLine = null, Exception? innerException = null )
		: base ( message, sourceLine, innerException ) { }
}

public class SCLDuplicateDefinitionException : SCLParsingException {
	public SCLDuplicateDefinitionException ( string message, string? sourceLine = null, Exception? innerException = null )
		: base ( message, sourceLine, innerException ) { }
}

public class SCLDuplicateUsingException : SCLParsingException {
	public string ModuleName { get; }

	public SCLDuplicateUsingException ( string moduleName, string? sourceLine = null )
		: base ( $"Module '{moduleName}' is already imported.", sourceLine ) {
		ModuleName = moduleName;
	}

	public override bool IsWarning => true;
}
