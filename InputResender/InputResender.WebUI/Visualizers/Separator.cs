using System;
using MdxLibs.Core;
using Microsoft.AspNetCore.Components;

namespace InputResender.WebUI.Visualizers;
public class Separator (UI_Separator ui_separator) : IBlazorVisualizer (ui_separator) {
	public override void Apply (object value) {
		throw new NotImplementedException ( "Apply logic not implemented for Separator." );
	}

	public RenderFragment Render () {
		RenderFragment test = builder => {
			builder.AddMarkupContent ( 0,
				ui_separator.SeparatorType switch {
					UI_Separator.SepType.Major =>
						$"<hr style=\"border-top: 2px solid #000; margin: 10px 0;\" /><h3 style=\"margin: 0;\">{ui_separator.Label}</h3><hr style=\"border-top: 2px solid #000; margin: 10px 0;\" />"
					, UI_Separator.SepType.Header =>
						$"<hr style=\"border-top: 1px solid #000; margin: 10px 0;\" /><h4 style=\"margin: 0;\">{ui_separator.Label}</h4><hr style=\"border-top: 1px solid #000; margin: 10px 0;\" />"
					, UI_Separator.SepType.Minor =>
						$"<p style=\"margin: 0;\">{ui_separator.Label}</p><hr style=\"border-top: 1px dashed #000; margin: 10px 0;\" />"
					, UI_Separator.SepType.Line  => $"<hr style=\"border-top: 1px solid #000; margin: 10px 0;\" />"
					, UI_Separator.SepType.Space => $"<div style=\"height: 20px;\"></div>"
					, _                          => $"<hr style=\"border-top: 1px dashed #000; margin: 10px 0;\" />"
				}
			);
		};
		return test;
	}
}