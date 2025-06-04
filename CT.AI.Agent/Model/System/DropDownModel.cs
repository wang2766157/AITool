using System.ComponentModel.DataAnnotations;

namespace CT.AI.Agent.Model.System;
public class DropDownModel
{
    [Key]
    public string ID { get; set; }
    public string Name { get; set; }
    public string Value { get; set; }
    public string Href { get; set; }
    public bool IsDivider { get; set; }
}
