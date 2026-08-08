using System;
using System.Collections.Generic;
using MdxLibs.Core;

namespace InputResender.Definitions.InputProcessing;
public class CInputLLParser : AContractorBase<CInputLLParser, CoreBase> {
	public override int ComponentVersion => 1;
	private readonly Dictionary<Type, InputEventParser> Parsers;

	public CInputLLParser (CoreBase owner) : base (owner) {
		Parsers = [];
	}

	public sealed override CoreBase Owner { get => throw new AccessViolationException ("Access to Owner is not allowed in Contractors"); }

	public override void LoadFrom ( CInputLLParser other ) => throw new NotImplementedException ();
	public override void SaveTo ( CInputLLParser other ) => throw new NotImplementedException ();

	public InputEventInfo Parse ( int nCode, nint wParam, nint lParam ) {
		lock ( Parsers ) {
			if ( Parsers == null ) return null;
			foreach ( InputEventParser p in Parsers.Values ) {
				var tmp = p.Parse ( nCode, wParam, lParam );
				if ( tmp != null ) return tmp;
			}
			return null;
		}
	}

	public void Register (InputEventParser parser ) {
		if ( parser == null ) return;
		Type type = parser.GetType ();
		lock ( Parsers ) {
			if ( !Parsers.TryAdd ( type, parser ) ) Parsers[type] = parser;
		}
	}



	public override StateInfo Info => new VStateInfo ( this );
	public class VStateInfo : StateInfo {
		public VStateInfo ( CInputLLParser owner ) : base ( owner ) {
			RegisteredParsers = GetParsers ();
		}
		public readonly string[] RegisteredParsers;
		protected string[] GetParsers () {
			var inputParser = (CInputLLParser)Owner;
			string[] registeredParsers = new string[inputParser.Parsers.Count];
			int i = 0;
			foreach ( var kvp in inputParser.Parsers )
				registeredParsers[i++] = $"{kvp.Key} -> {kvp.Value}";
			return registeredParsers;
		}
		public override string AllInfo () => $"{base.AllInfo ()}{BR}Hooks:{BR}{string.Join ( BR, GetParsers () )}";
	}

	public abstract class InputEventInfo : IDisposable {
		public bool CanConsume, ShouldProcess;
		public abstract void Dispose ();
		protected abstract bool Equals ( InputEventInfo other );
		public override sealed bool Equals ( object obj ) {
			if ( obj == null ) return false;
			if ( obj.GetType () != GetType () ) return false;
			InputEventInfo other = obj as InputEventInfo;
			bool ret = other.ShouldProcess == ShouldProcess;
			ret &= other.CanConsume == CanConsume;
			ret &= Equals ( other );
			return ret;
		}
	}
	public abstract class InputEventParser : IDisposable {
		public abstract InputEventInfo Parse ( int nCode, nint wParam, nint lParam );
		public abstract void Dispose ();
	}


	public interface IInputEventAssertor {
		InputEventInfoAssertor CreateAssertor ();

		public abstract class InputEventInfoAssertor {
			public bool? ExpCanConsume, ExpShouldProcess;
			public abstract void Assert ( InputEventInfo info );
			protected abstract void FillInner ( InputEventInfo info );
			public void Fill ( InputEventInfo info ) {
				ArgumentNullException.ThrowIfNull ( info, nameof ( info ) );
				ExpCanConsume = info.CanConsume;
				ExpShouldProcess = info.ShouldProcess;
				FillInner ( info );
			}
		}
	}
}