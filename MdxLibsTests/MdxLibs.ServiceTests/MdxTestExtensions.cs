using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using FluentAssertions.Collections;
using FluentAssertions.Primitives;
using Xunit;

namespace MdxLibs.ServiceTests;
public static class MdxTestExtensions {
	public static StringAssertions MatchAnyRegex ( this StringAssertions str, params Regex[] regexAr ) {
		foreach ( var regex in regexAr )
			if ( regex.Match ( str.Subject ).Success ) return str;
		Assert.Fail ( $"String {str.Subject} doesn't match any of the following regexes:\n{string.Join ( '\n', regexAr.Select ( r => r.ToString () ) )}" );
		return str;
	}

	public static StringAssertions MatchRegex (this StringAssertions str, Regex regex, params string[] values) {
		var match = regex.Match ( str.Subject );
		if (!match.Success ) Assert.Fail ( $"String {str.Subject} doesn't match regex {regex}." );
		// All values should be somewhere in the captured groups:
		var allCaptures = match.Groups.Values.SelectMany ( g => g.Captures ).Select ( c => c.Value ).ToArray ();
		foreach ( string val in values ) {
			if ( string.IsNullOrEmpty ( val ) ) continue;
			allCaptures.Should ().Contain ( val );
		}
		return str;
	}

	public static GenericDictionaryAssertions<IDictionary<T, List<U>>, T, List<U>> OnlyHaveUniqueListValues<T, U> (
		this GenericDictionaryAssertions<IDictionary<T, List<U>>, T, List<U>> dict
		) {
		List<string> Errors = [];
		Dictionary<U, T> reviewed = new ();
		foreach ( var kv in dict.Subject ) {
			foreach ( var item in kv.Value ) {
				if ( reviewed.ContainsKey ( item ) )
					Errors.Add ( $"Value <{item}> is not unique, found in keys <{reviewed[item]}> and <{kv.Key}>." );
				else reviewed[item] = kv.Key;
			}
		}
		if ( Errors.Any () )
			Assert.Fail ( $"Dictionary has non-unique list values:\n{string.Join ( '\n', Errors )}" );
		return dict;
	}
}