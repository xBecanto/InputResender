using Components.Library;
using System;
using System.Collections.Generic;
using System.Linq;

namespace InputResender.UnitTests;

public class GlobalCommandTestException : Exception {
	private string _stackTrace;
	public GlobalCommandTestException ( string message ) : base ( message ) { Init (); }
	public GlobalCommandTestException ( string message, Exception innerException ) : base ( message, innerException ) { Init (); }

	private void Init () {
		var stack = new System.Diagnostics.StackTrace ( 2, true );
		_stackTrace = stack.ToString ();
		var frame = stack.GetFrame ( 0 );
		if ( frame != null ) {
			var method = frame.GetMethod ();
			if ( method != null ) {
				var type = method.DeclaringType;
				if ( type != null ) Source = type.FullName;
			}
		}
	}

	public override string StackTrace => _stackTrace ?? base.StackTrace;
}

public class CommandNotTestedUnregisteredException : GlobalCommandTestException {
	public readonly IReadOnlyList<Type> MissingCommands;
	public readonly IReadOnlyList<Type> TestedCommands;

	public CommandNotTestedUnregisteredException ( IReadOnlyList<Type> missingCommands, IReadOnlyList<Type> testedCommands )
		: base ( FormatMessage ( missingCommands, testedCommands ) ) {
		MissingCommands = missingCommands;
		TestedCommands = testedCommands;
	}

	private static string FormatMessage ( IReadOnlyList<Type> missing, IReadOnlyList<Type> tested ) {
		string missingInfo = string.Join ( '\n', missing.Select ( t => $" - {t}" ) );
		string allTested = string.Join ( '\n', tested.Select ( t => $" - {t}" ) );
		return $"The following commands are not in the list of tested commands:\n{missingInfo}\nCurrently tested commands:\n{allTested}";
	}

	public override string ToString () =>
		$"{GetType ().Name}: {MissingCommands.Count} command(s) unregistered: {string.Join ( ", ", MissingCommands.Select ( t => t.Name ) )}";
}

public class CommandWithoutTestsException : GlobalCommandTestException {
	public readonly IReadOnlyList<Type> CommandsWithoutTests;
	public readonly IReadOnlyList<Type> TestedCommands;

	public CommandWithoutTestsException ( IReadOnlyList<Type> commandsWithoutTests, IReadOnlyList<Type> testedCommands )
		: base ( FormatMessage ( commandsWithoutTests, testedCommands ) ) {
		CommandsWithoutTests = commandsWithoutTests;
		TestedCommands = testedCommands;
	}

	private static string FormatMessage ( IReadOnlyList<Type> withoutTests, IReadOnlyList<Type> tested ) {
		string missingInfo = string.Join ( '\n', withoutTests.Select ( t => $" - {t}" ) );
		string allTested = string.Join ( '\n', tested.Select ( t => $" - {t}" ) );
		return $"The following commands do not have any tested command lines:\n{missingInfo}\nCurrently tested commands:\n{allTested}";
	}

	public override string ToString () =>
		$"{GetType ().Name}: {CommandsWithoutTests.Count} command(s) without tests: {string.Join ( ", ", CommandsWithoutTests.Select ( t => t.Name ) )}";
}

public class CommandLoadingException : GlobalCommandTestException {
	/// <summary>The loader type when the error originates from a loader.</summary>
	public readonly Type LoaderType;
	/// <summary>The command type that the loader tried to reference but is not in the known command list.</summary>
	public readonly Type UnregisteredCommandType;
	/// <summary>Commands that can never be reached through the loading chain.</summary>
	public readonly IReadOnlyList<Type> UnloadableCommands;

	/// <summary>Loader references a command not present in the known command types list.</summary>
	public CommandLoadingException ( Type loaderType, Type unregisteredCommandType, string message )
		: base ( message ) {
		LoaderType = loaderType;
		UnregisteredCommandType = unregisteredCommandType;
	}

	/// <summary>A loader's reflection-based command discovery failed.</summary>
	public CommandLoadingException ( Type loaderType, string message, Exception innerException = null )
		: base ( message, innerException ) {
		LoaderType = loaderType;
	}

	/// <summary>Multiple commands can never be reached through the loading chain.</summary>
	public CommandLoadingException ( IReadOnlyList<Type> unloadableCommands )
		: base ( $"Following commands can never be loaded:\n{string.Join ( '\n', unloadableCommands.Select ( t => $" - {t}" ) )}" ) {
		UnloadableCommands = unloadableCommands;
	}

	public override string ToString () {
		if ( UnloadableCommands != null )
			return $"{GetType ().Name}: {UnloadableCommands.Count} command(s) can never be loaded: {string.Join ( ", ", UnloadableCommands.Select ( t => t.Name ) )}";
		if ( UnregisteredCommandType != null )
			return $"{GetType ().Name} [{LoaderType?.Name ?? "?"}]: unregistered command '{UnregisteredCommandType.Name}' - {Message}";
		return $"{GetType ().Name} [{LoaderType?.Name ?? "?"}]: {Message}";
	}
}

public class CommandHelpValidationException : GlobalCommandTestException {
	public readonly Type CommandType;
	/// <summary>The CommandResult returned for the help query, or null if none was received.</summary>
	public readonly CommandResult HelpResult;

	public CommandHelpValidationException ( Type commandType, CommandResult helpResult, string message = null )
		: base ( message ?? $"Help validation failed for command '{commandType?.Name}': {helpResult?.Message}" ) {
		CommandType = commandType;
		HelpResult = helpResult;
	}

	public override string ToString () =>
		$"{GetType ().Name} [{CommandType?.Name ?? "?"}]: {HelpResult?.Message ?? Message}";
}

public class CommandRegistrationException : GlobalCommandTestException {
	public readonly Type CommandType;
	/// <summary>The field or method name that could not be found or had an invalid signature.</summary>
	public readonly string MemberName;

	public CommandRegistrationException ( Type commandType, string memberName, string message )
		: base ( message ) {
		CommandType = commandType;
		MemberName = memberName;
	}

	public override string ToString () =>
		$"{GetType ().Name} [{CommandType?.Name ?? "?"}]: member '{MemberName}' - {Message}";
}

public class CommandDefinitionException : GlobalCommandTestException {
	public readonly Type CommandType;
	/// <summary>The inter-command type that was invalid, if applicable.</summary>
	public readonly Type InvalidType;
	/// <summary>The sub-command name involved, if applicable.</summary>
	public readonly string SubCommand;

	public CommandDefinitionException ( Type commandType, string message, Type invalidType = null, string subCommand = null )
		: base ( message ) {
		CommandType = commandType;
		InvalidType = invalidType;
		SubCommand = subCommand;
	}

	public override string ToString () {
		var parts = new List<string> { $"{GetType ().Name} [{CommandType?.Name ?? "?"}]" };
		if ( SubCommand != null ) parts.Add ( $"sub-cmd: '{SubCommand}'" );
		if ( InvalidType != null ) parts.Add ( $"invalid type: '{InvalidType.Name}'" );
		parts.Add ( Message );
		return string.Join ( " | ", parts );
	}
}

public class CommandProcessingException : GlobalCommandTestException {
	public readonly IReadOnlyList<GlobalCommandTestException> Errors;

	public CommandProcessingException ( IReadOnlyList<GlobalCommandTestException> errors )
		: base ( FormatMessage ( errors ) ) {
		Errors = errors;
	}

	private static string FormatMessage ( IReadOnlyList<GlobalCommandTestException> errors ) {
		var sb = new System.Text.StringBuilder ( "Errors when processing commands/loaders:\n" );
		foreach ( var group in errors.GroupBy ( e => e.GetType () ) ) {
			sb.AppendLine ( $"[{group.Key.Name}] ({group.Count ()} error(s)):" );
			foreach ( var sameSourceGroup in group.GroupBy ( e => e.StackTrace ) ) {
				var E = sameSourceGroup.First ();
				sb.AppendLine ( $"  - {sameSourceGroup.Count ()} error(s) originated at {E.Source ?? "unknown source"}"
				);
				foreach ( var e in sameSourceGroup ) sb.AppendLine ( $"    {e.Message}" );
				if ( E.StackTrace == null ) continue;

				string stack = E.StackTrace;
				int XunitStart = stack.IndexOf ( "at Xunit." );
				if ( XunitStart >= 0 ) stack = stack[..XunitStart];
				foreach ( var line in stack.Replace ( "   at", "at" )
							 .Split ( new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries ) ) {
					sb.AppendLine ( $"    . {line}" );
				}
			}
		}
		return sb.ToString ().TrimEnd ();
	}

	public override string ToString () => FormatMessage ( Errors );
}

public class CommandDependencyException : GlobalCommandTestException {
	/// <summary>The loader or command type where the dependency problem was found.</summary>
	public readonly Type SourceType;
	/// <summary>The type that was expected to be present but was not, if applicable.</summary>
	public readonly Type DependencyType;
	/// <summary>The sub-command name involved, if applicable.</summary>
	public readonly string SubCommandName;

	public CommandDependencyException ( Type sourceType, string message, Type dependencyType = null, string subCommandName = null )
		: base ( message ) {
		SourceType = sourceType;
		DependencyType = dependencyType;
		SubCommandName = subCommandName;
	}

	public override string ToString () {
		var parts = new List<string> { $"{GetType ().Name} [{SourceType?.Name ?? "?"}]" };
		if ( SubCommandName != null ) parts.Add ( $"sub-cmd: '{SubCommandName}'" );
		if ( DependencyType != null ) parts.Add ( $"dependency: '{DependencyType.Name}'" );
		parts.Add ( Message );
		return string.Join ( " | ", parts );
	}
}
