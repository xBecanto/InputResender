using System;

namespace MdxLibs.Core {
public abstract class DataHolderBase<CompT> where CompT : ComponentBase {
		public readonly DateTime CreationTime;
		public readonly CompT Owner;

		public DataHolderBase ( CompT owner ) {
			Owner = owner;
			CreationTime = DateTime.Now;
		}
		public virtual bool FullEqCheck { get; protected set; } = true;
		public virtual bool IsSerializable { get; } = false;
		public abstract DataHolderBase<CompT> Clone ();
		public T Clone<T> () where T : DataHolderBase<CompT> => (T)Clone ();
		public abstract override bool Equals ( object obj );
		public abstract override int GetHashCode ();
		public abstract override string ToString ();
	}

	public abstract class SerializableDataHolderBase<CompT> : DataHolderBase<CompT> where CompT : ComponentBase {
		public override bool IsSerializable => true;
		public SerializableDataHolderBase ( CompT owner ) : base ( owner ) { }
		public abstract byte[] Serialize ();
		/// <summary>Note that overwriting might not be always possible (if original structure uses readonly fiels for example).
		/// <para>When overwriting, simply use: samaData.Deserialize(binaryArray); This way it should always return self-reference.</para>
		/// <para>Otherwise use the return value: var ret = otherData.Deserialize(binary); This way it should always return new object.</para></summary>
		public abstract SerializableDataHolderBase<CompT> Deserialize ( byte[] Data, bool overwrite = false );
	}
}