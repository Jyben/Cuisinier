namespace Cuisinier.Core.Entities;

public class MenuSettings
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public string ParametersJson { get; set; } = string.Empty;
    public DateTime ModificationDate { get; set; }
}

