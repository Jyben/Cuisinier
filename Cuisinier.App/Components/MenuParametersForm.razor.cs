using Cuisinier.Core.DTOs;
using Cuisinier.App.Services;
using MudBlazor;
using Microsoft.AspNetCore.Components;

namespace Cuisinier.App.Components
{
    public partial class MenuParametersForm
    {
        [Parameter] public MenuParameters Parameters { get; set; } = new();
        [Parameter] public EventCallback OnGenerate { get; set; }

        private MudForm? _form;
        private bool _isGenerating = false;
        
        // Use DesiredFoodItem from DesiredFoodsSection component
        
        // List of values for number of dishes/servings
        private List<int> _numberOfDishesValues = new();
        private List<int> _servingsValues = new();
        
        // Dish types
        private List<string> _dishTypeOptions = new()
        {
            "Viande rouge",
            "Viande blanche",
            "Poisson",
            "Légumes"
        };
        private List<string> _dishTypesValues = new();
        private List<int?> _numberOfDishTypesValues = new();
        
        // Desired foods
        private List<MenuParametersFormComponents.DesiredFoodsSection.DesiredFoodItem> _desiredFoodItems = new();
        
        // New banned food
        private string _newBannedFood = "";
        
        // Seasonal options
        private string _seasonalFoodOption = "toujours";
        
        // Options with intensity (slider)
        private List<string> _optionsWithIntensity = new()
        {
            "Équilibré",
            "Gourmand"
        };
        
        // Boolean options (all or nothing)
        private List<string> _booleanOptions = new()
        {
            "Végan",
            "Végétarien",
            "Sans gluten",
            "Sans lactose"
        };
        
        // Time
        private bool _limitPreparationTime = false;
        private bool _limitCookingTime = false;
        private int _preparationTimeMinutes = 15;
        private int _cookingTimeMinutes = 30;

        private bool _isInitialized = false;
        private MenuParameters? _lastParametersHash = null;

        protected override void OnInitialized()
        {
            // Initialize the next Monday's date if not set or invalid
            if (Parameters.WeekStartDate == default || Parameters.WeekStartDate < DateTime.Today.AddYears(-1))
            {
                Parameters.WeekStartDate = GetNextMonday();
            }
            
            if (!_isInitialized)
            {
                InitializeFromParameters();
                _isInitialized = true;
                _lastParametersHash = CreateParametersHash();
            }
        }

        protected override void OnParametersSet()
        {
            // Reset values if Parameters has changed (e.g., when Index.razor loads parameters asynchronously)
            var currentHash = CreateParametersHash();
            if (!_isInitialized || !ParametersHashEquals(_lastParametersHash, currentHash))
            {
                InitializeFromParameters();
                _isInitialized = true;
                _lastParametersHash = currentHash;
            }
        }

        private MenuParameters CreateParametersHash()
        {
            // Create a simple object to compare if parameters have changed
            // Use a shallow copy of key properties
            return new MenuParameters
            {
                NumberOfDishes = Parameters.NumberOfDishes?.Count > 0 ? new List<NumberOfDishesDto>(Parameters.NumberOfDishes) : new List<NumberOfDishesDto>(),
                DishTypes = Parameters.DishTypes != null ? new Dictionary<string, int?>(Parameters.DishTypes) : new Dictionary<string, int?>(),
                BannedFoods = Parameters.BannedFoods != null ? new List<string>(Parameters.BannedFoods) : new List<string>(),
                DesiredFoods = Parameters.DesiredFoods?.Count > 0 ? new List<DesiredFoodDto>(Parameters.DesiredFoods) : new List<DesiredFoodDto>(),
                SeasonalFoods = Parameters.SeasonalFoods,
                WeightedOptions = Parameters.WeightedOptions != null ? new Dictionary<string, int?>(Parameters.WeightedOptions) : new Dictionary<string, int?>(),
                MaxPreparationTime = Parameters.MaxPreparationTime,
                MaxCookingTime = Parameters.MaxCookingTime,
                TotalKcalPerDish = Parameters.TotalKcalPerDish
            };
        }

        private bool ParametersHashEquals(MenuParameters? hash1, MenuParameters? hash2)
        {
            if (hash1 == null && hash2 == null) return true;
            if (hash1 == null || hash2 == null) return false;
            
            // Simple comparison based on key properties
            // Compare counts and some values to detect changes
            return hash1.NumberOfDishes?.Count == hash2.NumberOfDishes?.Count &&
                   hash1.DishTypes?.Count == hash2.DishTypes?.Count &&
                   hash1.BannedFoods?.Count == hash2.BannedFoods?.Count &&
                   hash1.DesiredFoods?.Count == hash2.DesiredFoods?.Count &&
                   hash1.SeasonalFoods == hash2.SeasonalFoods &&
                   hash1.WeightedOptions?.Count == hash2.WeightedOptions?.Count &&
                   hash1.MaxPreparationTime == hash2.MaxPreparationTime &&
                   hash1.MaxCookingTime == hash2.MaxCookingTime &&
                   hash1.TotalKcalPerDish == hash2.TotalKcalPerDish;
        }

        private DateTime GetNextMonday()
        {
            var today = DateTime.Today;
            var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7; // If it's already Monday, take the next one
            return today.AddDays(daysUntilMonday);
        }

        private void InitializeFromParameters()
        {
            // Initialize number of dishes/servings
            if (!Parameters.NumberOfDishes.Any())
            {
                Parameters.NumberOfDishes.Add(new NumberOfDishesDto { NumberOfDishes = 5, Servings = 1 });
            }
            _numberOfDishesValues = Parameters.NumberOfDishes.Select(x => x.NumberOfDishes).ToList();
            _servingsValues = Parameters.NumberOfDishes.Select(x => x.Servings).ToList();
            
            // Initialize dish types (with optional numbers)
            _dishTypesValues = Parameters.DishTypes.Keys.ToList();
            _numberOfDishTypesValues = Parameters.DishTypes.Values.ToList();
            if (!_dishTypesValues.Any())
            {
                _dishTypesValues = new List<string>();
                _numberOfDishTypesValues = new List<int?>();
            }
            
            // Initialize desired foods
            _desiredFoodItems = Parameters.DesiredFoods.Select(a => new MenuParametersFormComponents.DesiredFoodsSection.DesiredFoodItem 
            { 
                Food = a.Food
            }).ToList();
            
            // Initialize season option - default to "toujours" if nothing is returned by API
            // Only use "jamais" if SeasonalFoods is explicitly false AND parameters were loaded from API
            // Otherwise, always default to "toujours"
            if (!Parameters.SeasonalFoods && Parameters.WeekStartDate != default(DateTime) && Parameters.WeekStartDate != GetNextMonday())
            {
                // Parameters were explicitly loaded from API with SeasonalFoods = false
                _seasonalFoodOption = "jamais";
            }
            else
            {
                // Default to "toujours" for new forms or when SeasonalFoods is true
                _seasonalFoodOption = "toujours";
            }
            
            // Initialize times
            if (Parameters.MaxPreparationTime.HasValue)
            {
                _limitPreparationTime = true;
                _preparationTimeMinutes = (int)Parameters.MaxPreparationTime.Value.TotalMinutes;
            }
            if (Parameters.MaxCookingTime.HasValue)
            {
                _limitCookingTime = true;
                _cookingTimeMinutes = (int)Parameters.MaxCookingTime.Value.TotalMinutes;
            }
        }

        public async Task AddDishServings()
        {
            _numberOfDishesValues.Add(5);
            _servingsValues.Add(2);
            await Task.CompletedTask;
        }

        public async Task DeleteDishServings(int index)
        {
            if (_numberOfDishesValues.Count > 1)
            {
                _numberOfDishesValues.RemoveAt(index);
                _servingsValues.RemoveAt(index);
            }
            await Task.CompletedTask;
        }

        public async Task AddDishType()
        {
            var newType = _dishTypeOptions.First();
            _dishTypesValues.Add(newType);
            _numberOfDishTypesValues.Add(null); // Default to no specific number
            await Task.CompletedTask;
        }

        public async Task DeleteDishType(int index)
        {
            if (index < _dishTypesValues.Count)
            {
                _dishTypesValues.RemoveAt(index);
                if (index < _numberOfDishTypesValues.Count)
                {
                    _numberOfDishTypesValues.RemoveAt(index);
                }
            }
            await Task.CompletedTask;
        }

        public async Task AddBannedFood()
        {
            var food = _newBannedFood?.Trim();
            if (!string.IsNullOrWhiteSpace(food) && !Parameters.BannedFoods.Contains(food, StringComparer.OrdinalIgnoreCase))
            {
                Parameters.BannedFoods.Add(food);
                _newBannedFood = "";
                StateHasChanged(); // Force UI update
            }
            await Task.CompletedTask;
        }

        public async Task DeleteBannedFood(int index)
        {
            Parameters.BannedFoods.RemoveAt(index);
            await Task.CompletedTask;
        }

        public async Task AddDesiredFood()
        {
            _desiredFoodItems.Add(new MenuParametersFormComponents.DesiredFoodsSection.DesiredFoodItem { Food = "" });
            await Task.CompletedTask;
        }

        public async Task DeleteDesiredFood(int index)
        {
            if (index < _desiredFoodItems.Count)
            {
                _desiredFoodItems.RemoveAt(index);
            }
            await Task.CompletedTask;
        }

        private async Task GenerateMenu()
        {
            // Sync values from local lists to Parameters
            SyncParametersFromUI();
            
            var request = new MenuGenerationRequest
            {
                Parameters = Parameters
            };

            try
            {
                _isGenerating = true;
                StateHasChanged(); // Force update to display animation
                
                var menu = await MenuApi.GenerateMenuAsync(request);
                Navigation.NavigateTo($"/menu-generation/{menu.Id}");
            }
            catch
            {
                _isGenerating = false;
                StateHasChanged();
                Snackbar.Add("Une erreur est survenue lors de la génération du menu. Veuillez réessayer.", Severity.Error);
            }
        }

        private void SyncParametersFromUI()
        {
            // Sync number of dishes/servings
            Parameters.NumberOfDishes.Clear();
            for (int i = 0; i < _numberOfDishesValues.Count && i < _servingsValues.Count; i++)
            {
                Parameters.NumberOfDishes.Add(new NumberOfDishesDto { NumberOfDishes = _numberOfDishesValues[i], Servings = _servingsValues[i] });
            }
            
            // Sync dish types (with optional numbers)
            Parameters.DishTypes.Clear();
            for (int i = 0; i < _dishTypesValues.Count; i++)
            {
                var type = _dishTypesValues[i];
                var number = i < _numberOfDishTypesValues.Count ? _numberOfDishTypesValues[i] : null;
                Parameters.DishTypes[type] = number; // null = optional type, value = specific number
            }
            
            // Sync desired foods
            Parameters.DesiredFoods.Clear();
            foreach (var item in _desiredFoodItems)
            {
                if (!string.IsNullOrWhiteSpace(item.Food))
                {
                    Parameters.DesiredFoods.Add(new DesiredFoodDto { Food = item.Food, Weight = 0 });
                }
            }
            
            // Sync season option
            Parameters.SeasonalFoods = _seasonalFoodOption == "toujours" || _seasonalFoodOption == "partiellement";
            
            // Sync times
            Parameters.MaxPreparationTime = _limitPreparationTime ? TimeSpan.FromMinutes(_preparationTimeMinutes) : null;
            Parameters.MaxCookingTime = _limitCookingTime ? TimeSpan.FromMinutes(_cookingTimeMinutes) : null;
            
            // Weighted options are already synced because they are directly bound
        }
    }
}
