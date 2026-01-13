namespace Cuisinier.Infrastructure.Services.Prompts.Components;

public class DietaryConstraintsBuilder : IConstraintBuilder
{
    private readonly Dictionary<string, int?> _weightedOptions;

    public DietaryConstraintsBuilder(Dictionary<string, int?> weightedOptions)
    {
        _weightedOptions = weightedOptions ?? new Dictionary<string, int?>();
    }

    public string Build()
    {
        if (!_weightedOptions.Any(kvp => kvp.Value.HasValue))
            return string.Empty;

        var optionsWithIntensity = new[] { "Équilibré", "Gourmand" };
        var booleanOptions = new[] { "Végan", "Végétarien", "Sans gluten", "Sans lactose" };
        
        var intensityOptions = _weightedOptions
            .Where(kvp => kvp.Value.HasValue && optionsWithIntensity.Contains(kvp.Key))
            .ToList();
        var boolOptions = _weightedOptions
            .Where(kvp => kvp.Value.HasValue && kvp.Value.Value > 0 && booleanOptions.Contains(kvp.Key))
            .ToList();
        
        if (!intensityOptions.Any() && !boolOptions.Any())
            return string.Empty;

        var sb = new System.Text.StringBuilder();
        
        if (intensityOptions.Any())
        {
            sb.AppendLine("\nOptions avec intensité:");
            foreach (var (option, value) in intensityOptions)
            {
                if (value == 0)
                {
                    sb.AppendLine($"- {option}: aucun (0%)");
                }
                else
                {
                    sb.AppendLine($"- {option}: {value}%");
                }
            }
        }
        
        if (boolOptions.Any())
        {
            sb.AppendLine("\nContraintes alimentaires (obligatoires pour tous les plats):");
            foreach (var (option, _) in boolOptions)
            {
                sb.AppendLine($"- {option}");
            }
        }
        
        return sb.ToString();
    }
}
