using Microsoft.AspNetCore.Components;
using System.ComponentModel;

namespace CT.AI.Agent.Pages.Components;

public class CTBaseComponent : ComponentBase
{
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RenderFragment ChildContent { get; set; }

    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] 
    public string AddClass { get; set; } = "";

    [Parameter(CaptureUnmatchedValues = true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Dictionary<string, object> AddAttributes { get; set; } = new();
}