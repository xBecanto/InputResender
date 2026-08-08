using System;
using System.Collections.Generic;
using System.Linq;
using SBld = System.Text.StringBuilder;

namespace InputResender.Services; 
public class HtmlLogWriter {
	public readonly Tag Head, Body, Style;
	private readonly Tag document;
	private readonly Dictionary<string, Dictionary<string, string>> Styles;

	public Tag Last => Body.Children.Count > 0 ? Body.Children[^1] : null;

	public HtmlLogWriter () {
		Styles = new ();
		document = new Tag ( "html" );
		document.Attributes.Add ( "lang", "en" );
		document.Children.Add ( Head = new Tag ( "head" ) );
		Head.Children.Add ( new Tag ( "meta" ) { Attributes = { { "charset", "UTF-8" } } } );
		Head.Children.Add ( new Tag ( "title", "Input Log" ) );
		Head.Children.Add ( Style = new Tag ( "style" ) );
		document.Children.Add ( Body = new Tag ( "body" ) );
		Styles.Add ( "body", new () { { "width", "180em" } });
	}

	public void PushStyle ( string element, params (string, string)[] styles ) {
		Dictionary<string, string> styleDict;
		if ( !Styles.TryGetValue ( element, out styleDict ) )
			Styles[element] = (styleDict = new ());
		foreach ( var (key, value) in styles )
			styleDict[key] = value;
	}
	public void PushStyleColor (string element, string color, double opacity = 1.0 ) => PushStyle ( element, ("color", color), ("opacity", opacity.ToString ( "F2" )) );
	public void PushStyleBackground ( string element, string color ) => PushStyle ( element, ("background-color", color) );
	public void PushStyle ( string element, string key, string value ) => PushStyle ( element, (key, value) );

	public void PushLine ( string className, string text) => Body.Children.Add ( Tag.Line ( className, text ) );

	public override string ToString () {
		SBld SB = new ();
		foreach ( var style in Styles ) {
			if ( style.Value.Count == 0 ) continue;
			if ( style.Value.Count == 1 ) {
				var kvp = style.Value.First ();
				SB.AppendLine ( $"\t{style.Key} {{ {kvp.Key}: {kvp.Value}; }}" );
				continue;
			}
			SB.AppendLine ( $"\t{style.Key} {{ " );
			foreach ( (var atr, var val) in style.Value )
				SB.AppendLine ( $"\t\t{atr}: {val};" );
			SB.AppendLine ( "\t}" );
		}
		Style.Content = SB.ToString ();
		SB.Clear ();

		SB.Append ( "<!DOCTYPE html>\n" );
		SB.Append ( document.ToString () );
		SB.Append ( "\n" );
		return SB.ToString ();
	}


	public class Tag {
		public readonly string Name;
		public string Class;
		public readonly Dictionary<string, string> Style;
		public readonly Dictionary<string, string> Attributes;
		public readonly List<Tag> Children;
		public string Content;

		public Tag ( string name, string content = null ) {
			Name = name;
			Style = new ();
			Children = new ();
			Attributes = new ();
			Content = content;
		}

		public static Tag Text ( string content ) => new ( null, content: content );
		public static Tag Line ( string className, string text ) => new Tag ( "div", text ) { Class = className };

		public override string ToString () {
			if ( string.IsNullOrEmpty ( Name ) ) return Content;
			SBld sb = new ();
			sb.Append ( $"<{Name}" );
			if ( !string.IsNullOrEmpty ( Class ) ) sb.Append ( $" class='{Class}'" );
			if ( Style != null && Style.Count > 0 ) {
				sb.Append ( " style='" );
				foreach ( var kvp in Style )
					sb.Append ( $"{kvp.Key}:{kvp.Value};" );
				sb.Append ( "'" );
			}
			sb.Append ( ">" );
			if ( Content != null ) sb.Append ( Content );
			foreach ( var child in Children ) {
				sb.AppendLine ( child.ToString () );
			}
			sb.Append ( $"</{Name}>" );
			return sb.ToString ();
		}
	}
}
