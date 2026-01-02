namespace Cuisinier.Core.Entities;

public class MenuSettings
{
    public int Id { get; set; } // Always 1 for default parameters
    public string ParametersJson { get; set; } = string.Empty;
    public DateTime ModificationDate { get; set; }
}

