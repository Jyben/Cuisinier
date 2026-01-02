using MudBlazor;

namespace Cuisinier.App.Themes;

public class CuisinierTheme
{
    public static MudTheme GetTheme()
    {
        return new MudTheme()
        {
            PaletteLight = new PaletteLight()
            {
                // Couleurs principales - Thème cuisine chaleureux
                Primary = "#E85A4F",           // Rouge tomate appétissant
                Secondary = "#52B788",         // Vert frais (légumes)
                Tertiary = "#F4A261",          // Orange doux (gourmandise)
                
                // Arrière-plans
                Background = "#FFF8F0",        // Beige très clair (fond principal)
                Surface = "#FFFEFB",           // Blanc cassé (cartes/surfaces)
                
                // Texte
                Dark = "#2D2D2D",              // Marron foncé (texte principal)
                DarkDarken = "#1A1A1A",
                TextPrimary = "#2D2D2D",
                TextSecondary = "#5C5C5C",     // Gris moyen
                TextDisabled = "#A0A0A0",
                
                // États
                Success = "#52B788",           // Vert frais
                Warning = "#F4A261",           // Orange
                Error = "#E85A4F",             // Rouge tomate
                Info = "#4A90E2",              // Bleu ciel
                
                // AppBar
                AppbarBackground = "#FFFFFF",
                AppbarText = "#2D2D2D",
                
                // Drawer
                DrawerBackground = "#FFFFFF",
                DrawerText = "#2D2D2D",
                DrawerIcon = "#E85A4F",
                
                // Lignes et bordures
                LinesDefault = "#E8E8E0",
                LinesInputs = "#D0D0C8",
                Divider = "#E8E8E0",
                DividerLight = "#F0F0E8",
                
                // Table
                TableLines = "#E8E8E0",
                TableStriped = "#FAFAF5",
                TableHover = "#F5F5F0",
                
                // Actions
                ActionDefault = "#8B8B83"
            },
            PaletteDark = new PaletteDark()
            {
                // Mode sombre - version assombrie mais chaleureuse
                Primary = "#FF6B6B",
                Secondary = "#6BCF8A",
                Tertiary = "#FFA866",
                Background = "#1A1A1A",
                Surface = "#2D2D2D",
                Dark = "#F5F5F0",
                TextPrimary = "#F5F5F0",
                TextSecondary = "#C0C0B8",
                TextDisabled = "#707070",
                Success = "#6BCF8A",
                Warning = "#FFA866",
                Error = "#FF6B6B",
                Info = "#6BA3E2",
                AppbarBackground = "#2D2D2D",
                AppbarText = "#F5F5F0",
                DrawerBackground = "#2D2D2D",
                DrawerText = "#F5F5F0",
                DrawerIcon = "#FF6B6B",
                LinesDefault = "#404040",
                LinesInputs = "#505050",
                Divider = "#404040",
                DividerLight = "#353535",
                TableLines = "#404040",
                TableStriped = "#2D2D2D",
                TableHover = "#353535",
                ActionDefault = "#A0A098"
            },
            LayoutProperties = new LayoutProperties()
            {
                DefaultBorderRadius = "12px",
                DrawerWidthLeft = "260px"
            }
        };
    }
}
