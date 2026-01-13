namespace Cuisinier.Infrastructure.Services.Prompts.Components;

public class CalorieConstraintsBuilder : IConstraintBuilder
{
    private readonly int? _minKcal;
    private readonly int? _maxKcal;
    private readonly bool _includeInConstraints;

    public CalorieConstraintsBuilder(int? minKcal, int? maxKcal, bool includeInConstraints = true)
    {
        _minKcal = minKcal;
        _maxKcal = maxKcal;
        _includeInConstraints = includeInConstraints;
    }

    public string Build()
    {
        if (!_minKcal.HasValue && !_maxKcal.HasValue)
            return string.Empty;

        var sb = new System.Text.StringBuilder();
        
        if (_includeInConstraints)
        {
            sb.AppendLine("\nIMPORTANT - CONTRAINTES OBLIGATOIRES:");
        }
        else
        {
            sb.AppendLine("\nCalories par personne:");
        }

        if (_minKcal.HasValue && _maxKcal.HasValue)
        {
            if (_includeInConstraints)
            {
                sb.AppendLine($"- Chaque recette DOIT avoir entre {_minKcal.Value} et {_maxKcal.Value} kcal PAR PERSONNE. C'est une contrainte stricte.");
            }
            else
            {
                sb.AppendLine($"entre {_minKcal.Value} et {_maxKcal.Value} kcal par personne");
            }
        }
        else if (_minKcal.HasValue)
        {
            if (_includeInConstraints)
            {
                sb.AppendLine($"- Chaque recette DOIT avoir au minimum {_minKcal.Value} kcal PAR PERSONNE. C'est une contrainte stricte.");
            }
            else
            {
                sb.AppendLine($"Calories minimales par personne: {_minKcal.Value} kcal");
            }
        }
        else if (_maxKcal.HasValue)
        {
            if (_includeInConstraints)
            {
                sb.AppendLine($"- Chaque recette DOIT avoir au maximum {_maxKcal.Value} kcal PAR PERSONNE. C'est une contrainte stricte.");
            }
            else
            {
                sb.AppendLine($"Calories maximales par personne: {_maxKcal.Value} kcal");
            }
        }

        return sb.ToString();
    }
}
