using System.Globalization;

namespace Cuisinier.Infrastructure.Services.Prompts.Components;

public class SeasonalConstraintsBuilder : IConstraintBuilder
{
    private readonly DateTime _weekStartDate;
    private readonly bool _enabled;

    public SeasonalConstraintsBuilder(DateTime weekStartDate, bool enabled)
    {
        _weekStartDate = weekStartDate;
        _enabled = enabled;
    }

    public string Build()
    {
        if (!_enabled)
            return string.Empty;

        var monthName = GetMonthName(_weekStartDate);
        
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"\n=== ALIMENTS DE SAISON (CONTRAINTE STRICTE) ===");
        sb.AppendLine($"Mois: {monthName} (basé sur la date de début de semaine: {_weekStartDate:yyyy-MM-dd})");
        sb.AppendLine("\nIMPORTANT - CONTRAINTE OBLIGATOIRE:");
        sb.AppendLine("Tu DOIS utiliser UNIQUEMENT des aliments de saison disponibles en France au mois de " + monthName + ".");
        sb.AppendLine("Les aliments doivent être ceux qui sont naturellement récoltés et disponibles localement en France pendant ce mois.");
        sb.AppendLine("\nCette contrainte est STRICTE et NON NÉGOCIABLE. Tous les ingrédients des recettes doivent respecter la saisonnalité du mois.");
        
        return sb.ToString();
    }

    private static string GetMonthName(DateTime date)
    {
        var culture = new CultureInfo("fr-FR");
        return culture.DateTimeFormat.GetMonthName(date.Month);
    }
}
