using Components.Interfaces;
using Components.Library;
using InputResender.Services;
using System;
using System.Xml;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace InputResender.CLI;
public class Config : ComponentBase<DMainAppCore> {
	const string ConfigFileName = "config.xml";

	private bool SkipAutoSave = false;
	private string homePath, savePath;
	private readonly Dictionary<string, string[]> autoCommands = new () {
		{"initCmds", new string[] { "loadall", "clear", "safemode on", "core new", "core own", "conns force init", "loglevel all", "network callback recv print", "network callback newconn print", "hook manager start" } }
	};
	private string autostartName = "initCmds";
	private bool printAutoCommands = true;
	private int maxOnelinerLength = 125;
	private PrintFormat responsePrintFormat = PrintFormat.Normal;
	private PasswordHolder configPassword = null;
	private readonly Dictionary<string, string> EnvVars = [];

	public Config ( string initPath, PasswordHolder password, DMainAppCore owner ) : base ( owner ) {
		var searchOptions = FileAccessService.SearchOptions.ProjectFolder
			| FileAccessService.SearchOptions.SolutionFolder
			| FileAccessService.SearchOptions.SubDirectories;
		if ( string.IsNullOrWhiteSpace ( initPath ) ) initPath = AppDomain.CurrentDomain.BaseDirectory;
		if ( initPath == "default" ) {
			savePath = initPath;
			homePath = Path.GetDirectoryName ( savePath );
			configPassword = password;
		} else {
			savePath = FileManager.FileService.GetAssetPath ( initPath, ConfigFileName, searchOptions );
			homePath = Path.GetDirectoryName ( savePath );
			configPassword = password;
			Load ();
		}
	}

	public bool IsInitialized => configPassword != null && Owner != null;

	public override int ComponentVersion => 1;

	protected override IReadOnlyList<(string opCode, Type opType)> AddCommands () => new List<(string opCode, Type opType)> () {
		(nameof(Save), typeof(void)),
		(nameof(Load), typeof(bool)),
		(nameof(FetchAutoCommands), typeof(IReadOnlyCollection<string>)),
		(nameof(AddAutoCommand), typeof(void)),
		(nameof(SetNewPassword), typeof(void))
	};

	private IFileManager FileManager {
		get {
			var fileManager = Owner.Fetch<DFileManager> ();
			if ( fileManager == null ) throw new Exception ( "DFileManager not found in active core." );
			if ( fileManager.FileManagerWrapper != null ) return fileManager.FileManagerWrapper;
			return fileManager;
		}
	}

	public enum PrintFormat { None, Batch, ErrOnly, Normal, Full }

	public HashSet<string> ValidAutostartNames { get => autoCommands.Keys.ToHashSet (); }
	public string AutostartName { get => autostartName; set { autostartName = value; Save (); } }
	public string HomePath {
		get => homePath; set {
			if ( string.IsNullOrEmpty ( value ) )
				throw new ArgumentException ( "HomePath cannot be empty." );
			if ( File.Exists ( value ) ) value = Path.GetDirectoryName ( value );
			if ( !Directory.Exists ( value ) )
				throw new ArgumentException ( "HomePath must be an existing directory or a valid file path." );
			homePath = value;
			savePath = Path.Combine ( HomePath, "config.xml" );
			Owner.Fetch<CommandProcessor<DMainAppCore>> ()
				.SetVar ( Components.Interfaces.Commands.FileManagerCommand.HOME_PATH_VAR_NAME, homePath );
			Save ();
		}
	}
	public string SavePath {
		get => savePath; set {
			if ( string.IsNullOrEmpty ( value ) )
				throw new ArgumentException ( "SavePath cannot be empty." );
			if ( Directory.Exists ( value ) )
				value = Path.Combine ( value, "config.xml" );
			else if ( !File.Exists ( value ) ) {
				string dir = Path.GetDirectoryName ( value );
				if ( !Directory.Exists ( dir ) )
					throw new ArgumentException ( "SavePath must be an existing file path or a valid directory." );
			} else if ( Path.GetExtension ( value ) != ".xml" )
				throw new ArgumentException ( "SavePath must have .xml extension." );
			savePath = value;
			Save ();
		}
	}
	public bool PrintAutoCommands { get => printAutoCommands; set { printAutoCommands = value; Save (); } }
	public int MaxOnelinerLength { get => maxOnelinerLength; set { maxOnelinerLength = value; Save (); } }
	public PrintFormat ResponsePrintFormat { get => responsePrintFormat; set { responsePrintFormat = value; Save (); } }
	public void SetNewPassword ( PasswordHolder newPassword ) {
		configPassword = newPassword;
		Save ();
	}

	public string GetEnv ( string name ) => EnvVars.GetValueOrDefault ( name );
	public void SetEnv ( string name, string value ) {
		if ( value == null ) EnvVars.Remove ( name );
		else EnvVars[name] = value;
	}

	public IReadOnlyCollection<string> FetchAutoCommands ( string key ) {
		if ( string.IsNullOrEmpty ( key ) ) return Array.Empty<string> ();
		if ( !autoCommands.TryGetValue ( key, out string[] ret ) )
			return Array.Empty<string> ();
		string[] ret2 = new string[ret.Length];
		ret.CopyTo ( ret2, 0 );
		return ret2;
	}
	public void AddAutoCommand ( string key, IEnumerable<string> commands ) =>
		autoCommands[key] = commands.ToArray ();


	public void Save () {
		if ( configPassword == null ) throw new InvalidOperationException ( "Config not initialized!" );
		if ( SkipAutoSave ) return;
		if ( string.IsNullOrEmpty ( SavePath ) ) return;

		XmlDocument doc = new ();
		XmlElement root = doc.CreateElement ( "Config" );
		doc.AppendChild ( root );
		root.AppendChild ( doc.CreateElement ( "HomePath" ) ).InnerText = homePath;
		root.AppendChild ( doc.CreateElement ( "AutostartName" ) ).InnerText = autostartName;
		root.AppendChild ( doc.CreateElement ( "PrintAutoCommands" ) ).InnerText = PrintAutoCommands ? "T" : "F";
		root.AppendChild ( doc.CreateElement ( "MaxOnelinerLength" ) ).InnerText = MaxOnelinerLength.ToString ();
		root.AppendChild ( doc.CreateElement ( "ResponsePrintFormat" ) ).InnerText = ResponsePrintFormat.ToString ();

		XmlElement autoCmds = doc.CreateElement ( "AutoCommands" );
		root.AppendChild ( autoCmds );
		foreach ( var pair in autoCommands ) {
			XmlElement key = doc.CreateElement ( pair.Key );
			autoCmds.AppendChild ( key );
			for ( int i = 0; i < pair.Value.Length; i++ ) {
				XmlElement cmd = doc.CreateElement ( $"C{i}" );
				cmd.InnerText = pair.Value[i];
				key.AppendChild ( cmd );
			}
		}

		FileManager.WriteFileWithHeader ( SavePath, doc.OuterXml, configPassword );
	}

	public bool Load ( string path = null ) {
		if ( configPassword == null ) throw new InvalidOperationException ( "Config not initialized!" );
		bool oldSkipAutoSave = SkipAutoSave;
		SkipAutoSave = true;
		if ( string.IsNullOrEmpty ( path ) ) path = Path.Combine ( HomePath, "config.xml" );

		string content = FileManager.ReadFileWithHeader ( path, configPassword );

		HomePath = path;

		if (!File.Exists ( SavePath ) ) {
			// Create default config
			//Save (); Moving this to higher level
			SkipAutoSave = oldSkipAutoSave;
			return true; // Shouldn't this be false??? 🤔
		} else {
			XmlDocument doc = new ();
			doc.LoadXml ( content );
			XmlElement root = doc.DocumentElement;
			if ( root.Name != "Config" ) throw new ArgumentException ( "Invalid root element." );
			foreach ( XmlNode node in root.ChildNodes ) {
				switch ( node.Name ) {
				case "HomePath":
					// If HomePath is different than current (but has valid config file) switch to loading that config (recursively). Otherwise store this path as alternative path (will be used when searching for extra files)
					if ( node.InnerText == HomePath || node.InnerText == SavePath )
						break;
					HomePath = node.InnerText;
					if ( Load ( SavePath ) ) {
						SkipAutoSave = oldSkipAutoSave;
						return true;
					}
					break;
				case "AutostartName": autostartName = node.InnerText; break;
				case "PrintAutoCommands": PrintAutoCommands = node.InnerText == "T"; break;
				case "MaxOnelinerLength": if ( int.TryParse ( node.InnerText, out int maxOnelinerLength ) ) MaxOnelinerLength = maxOnelinerLength; break;
				case "ResponsePrintFormat": if ( Enum.TryParse ( node.InnerText, true, out PrintFormat format ) ) ResponsePrintFormat = format; break;
				case "AutoCommands":
					autoCommands.Clear ();
					LoadAutoCommands ( node, autoCommands );
					break;
				case "EnvVars":
					EnvVars.Clear ();
					foreach ( XmlNode envVar in node.ChildNodes ) {
						string varName = null, varVal = null;
						foreach ( XmlNode varData in envVar.ChildNodes ) {
							if (varData.Name == "Name") varName = varData.InnerText;
							if  (varData.Name == "Value") varVal = varData.InnerText;
						}
						if ( varName != null && varVal != null )
							EnvVars.Add ( varName, varVal );
					}

					break;
				}
			}
			SkipAutoSave = oldSkipAutoSave;
			return true;
		}
	}

	public static int LoadAutoCommands ( XmlNode root, Dictionary<string, string[]> autoCommands ) {
		int loadedCnt = 0;
		foreach ( XmlNode key in root.ChildNodes ) {
			if ( key.NodeType != XmlNodeType.Element ) continue;
			Dictionary<string, string> cmds = new ();
			int cmdIndex = 0;
			foreach ( XmlNode cmd in key.ChildNodes ) {
				if ( cmd.NodeType != XmlNodeType.Element ) continue;
				cmds.Add ( $"{cmd.Name}_{cmdIndex++}", cmd.InnerText );
			}

			autoCommands[key.Name] = cmds.OrderBy ( c => c.Key ).Select ( c => c.Value ).ToArray ();
			loadedCnt++;
		}

		return loadedCnt;
	}

	public override StateInfo Info => new VStateInfo ( this );

	public class VStateInfo ( Config owner ) : StateInfo ( owner ) {
		public readonly string HomePath = owner.HomePath;
		public readonly string SavePath = owner.SavePath;
		public readonly string AutostartName = owner.AutostartName;
		public readonly bool PrintAutoCommands = owner.PrintAutoCommands;
		public readonly int MaxOnelinerLength = owner.MaxOnelinerLength;
		public readonly PrintFormat ResponsePrintFormat = owner.ResponsePrintFormat;

		public override string AllInfo () {
			string[] rets = [
				base.AllInfo (),
				$"Home Path: {HomePath}",
				$"Save Path: {SavePath}",
				$"Autostart Name: {AutostartName}",
				$"Print Auto Commands: {PrintAutoCommands}",
				$"Max Oneliner Length: {MaxOnelinerLength}",
				$"Response Print Format: {ResponsePrintFormat}"
			];
			return string.Join ( BR, rets );
		}
	}
}