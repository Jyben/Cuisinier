using Cuisinier.Core.DTOs;
using Cuisinier.App.Services;
using MudBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cuisinier.App.Components
{
    public partial class MenuParametersForm : IAsyncDisposable
    {
        [Parameter] public MenuParameters Parameters { get; set; } = new();
        [Parameter] public EventCallback OnGenerate { get; set; }

        [Inject] private IMenuApi MenuApi { get; set; } = null!;
        [Inject] private NavigationManager Navigation { get; set; } = null!;
        [Inject] private ISnackbar Snackbar { get; set; } = null!;
        [Inject] private IConfiguration Configuration { get; set; } = null!;
        [Inject] private ILogger<MenuParametersForm> Logger { get; set; } = null!;

        private MudForm? _form;
        private bool _isGenerating = false;
        private HubConnection? _hubConnection;
        private int? _pendingMenuId = null;
        private SemaphoreSlim? _connectionLock = new(1, 1);
        
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
        private int? _maxPreparationTimeMinutes = 15;
        private int? _maxCookingTimeMinutes = 30;
        
        // Calories
        private bool _limitKcal = false;
        private int? _minKcal = null;
        private int? _maxKcal = null;

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
                MinKcalPerDish = Parameters.MinKcalPerDish,
                MaxKcalPerDish = Parameters.MaxKcalPerDish
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
                   hash1.MinKcalPerDish == hash2.MinKcalPerDish &&
                   hash1.MaxKcalPerDish == hash2.MaxKcalPerDish;
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
                _maxPreparationTimeMinutes = (int)Parameters.MaxPreparationTime.Value.TotalMinutes;
            }
            if (Parameters.MaxCookingTime.HasValue)
            {
                _limitCookingTime = true;
                _maxCookingTimeMinutes = (int)Parameters.MaxCookingTime.Value.TotalMinutes;
            }
            
            // Initialize calories
            if (Parameters.MinKcalPerDish.HasValue || Parameters.MaxKcalPerDish.HasValue)
            {
                _limitKcal = true;
                _minKcal = Parameters.MinKcalPerDish;
                _maxKcal = Parameters.MaxKcalPerDish;
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
                
                // Start menu generation (returns immediately)
                var response = await MenuApi.GenerateMenuAsync(request);
                _pendingMenuId = response.MenuId;

                // Initialize SignalR connection to listen for completion
                await InitializeSignalRAsync(response.MenuId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error starting menu generation");
                _isGenerating = false;
                StateHasChanged();
                Snackbar.Add("Une erreur est survenue lors de la génération du menu. Veuillez réessayer.", Severity.Error);
            }
        }

        private async Task InitializeSignalRAsync(int menuId)
        {
            // Check if semaphore is available
            if (_connectionLock == null)
            {
                Logger.LogError("Connection lock is not available, component may be disposed");
                return;
            }

            // Use a semaphore to prevent concurrent connection operations
            await _connectionLock.WaitAsync();
            try
            {
                // Close existing connection if it exists and ensure it's fully disposed
                if (_hubConnection is not null)
                {
                    // Check connection state before attempting to stop
                    if (_hubConnection.State != HubConnectionState.Disconnected)
                    {
                        try
                        {
                            await _hubConnection.StopAsync();
                        }
                        catch (Exception ex)
                        {
                            // Log errors during shutdown at debug level for diagnostics
                            Logger.LogDebug(ex, "Error while stopping SignalR hub connection");
                        }
                    }

                    try
                    {
                        await _hubConnection.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        // Log errors during disposal at debug level for diagnostics
                        Logger.LogDebug(ex, "Error while disposing SignalR hub connection");
                    }
                    
                    _hubConnection = null;
                }

                var apiBaseUrl = Configuration["ApiBaseUrl"] ?? Navigation.BaseUri;
                if (string.IsNullOrWhiteSpace(apiBaseUrl))
                {
                    Logger.LogError("Unable to determine API base URL for SignalR hub connection. Both configuration 'ApiBaseUrl' and navigation base URI are null or empty.");
                    _isGenerating = false;
                    _pendingMenuId = null;
                    StateHasChanged();
                    Snackbar.Add("Erreur de configuration du serveur. Veuillez contacter l'administrateur ou réessayer plus tard.", Severity.Error);
                    return;
                }
                var hubUrl = $"{apiBaseUrl.TrimEnd('/')}/recipeHub";
                
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(hubUrl)
                    .Build();

                // Listen for menu generation completion
                _hubConnection.On<MenuResponse>("MenuGenerated", (menu) =>
                {
                    if (_pendingMenuId == menu.Id)
                    {
                        _isGenerating = false;
                        _pendingMenuId = null;
                        InvokeAsync(() =>
                        {
                            StateHasChanged();
                            Navigation.NavigateTo($"/menu-generation/{menu.Id}");
                        });
                    }
                });

                // Listen for menu generation errors
                _hubConnection.On<int>("MenuGenerationError", (menuId) =>
                {
                    if (_pendingMenuId == menuId)
                    {
                        _isGenerating = false;
                        _pendingMenuId = null;
                        InvokeAsync(() =>
                        {
                            StateHasChanged();
                            Snackbar.Add("Une erreur est survenue lors de la génération du menu. Veuillez réessayer.", Severity.Error);
                        });
                    }
                });

                try
                {
                    await _hubConnection.StartAsync();
                    await _hubConnection.SendAsync("JoinMenuGroup", menuId);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error connecting to SignalR hub");
                    _isGenerating = false;
                    _pendingMenuId = null;
                    StateHasChanged();
                    Snackbar.Add("Impossible de se connecter au serveur pour suivre la génération du menu. Veuillez réessayer.", Severity.Error);
                }
            }
            finally
            {
                // Only release if the lock is still available
                // If it's null, the component has been disposed
                if (_connectionLock != null)
                {
                    _connectionLock.Release();
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            // Check if semaphore is available and not already disposed
            if (_connectionLock == null)
            {
                return;
            }

            // Wait for any ongoing connection operations to complete
            await _connectionLock.WaitAsync();
            try
            {
                if (_hubConnection is not null)
                {
                    // Check connection state before attempting to stop
                    if (_hubConnection.State != HubConnectionState.Disconnected)
                    {
                        try
                        {
                            await _hubConnection.StopAsync();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogDebug(ex, "Error while stopping SignalR hub connection during disposal.");
                        }
                    }

                    try
                    {
                        await _hubConnection.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogDebug(ex, "Error while disposing SignalR hub connection during disposal.");
                    }

                    _hubConnection = null;
                }
            }
            finally
            {
                // Release the lock before setting it to null to avoid race conditions
                var lockToDispose = _connectionLock;
                if (lockToDispose != null)
                {
                    lockToDispose.Release();
                    _connectionLock = null;
                    lockToDispose.Dispose();
                }
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
            Parameters.MaxPreparationTime = _limitPreparationTime && _maxPreparationTimeMinutes.HasValue ? TimeSpan.FromMinutes(_maxPreparationTimeMinutes.Value) : null;
            Parameters.MaxCookingTime = _limitCookingTime && _maxCookingTimeMinutes.HasValue ? TimeSpan.FromMinutes(_maxCookingTimeMinutes.Value) : null;
            
            // Sync calories
            Parameters.MinKcalPerDish = _limitKcal ? _minKcal : null;
            Parameters.MaxKcalPerDish = _limitKcal ? _maxKcal : null;
            
            // Weighted options are already synced because they are directly bound
        }
    }
}
