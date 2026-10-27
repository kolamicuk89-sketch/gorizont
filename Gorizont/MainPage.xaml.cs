using System.Globalization;
using System.Text.Json;
using Gorizont.Services;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;

namespace Gorizont;

// Режим темы: Авто (по реальному дню/ночи в выбранном городе), Светлая, Тёмная
public enum ThemeMode { Auto = 0, Light = 1, Dark = 2 }

public partial class MainPage : ContentPage
{
    // Города «из коробки»
    static readonly CityInfo[] DefaultCities =
    {
        new() { Name = "Москва", Latitude = 55.7522, Longitude = 37.6156 },
        new() { Name = "Санкт-Петербург", Latitude = 59.9386, Longitude = 30.3141 },
        new() { Name = "Сочи", Latitude = 43.5855, Longitude = 39.7231 },
        new() { Name = "Казань", Latitude = 55.7963, Longitude = 49.1088 },
        new() { Name = "Нью-Йорк", Latitude = 40.7128, Longitude = -74.0060 },
    };

    readonly WeatherService _weather = new();

    List<CityInfo> _cities = new();
    int _cityIndex;
    WeatherData? _data;
    bool _busy;
    bool _appeared;
    bool _layoutReady;
    bool _isLandscape;
    ThemeMode _themeMode = ThemeMode.Auto;

    public MainPage()
    {
        InitializeComponent();
        LoadSavedState();
        this.SizeChanged += OnPageSizeChanged;
    }

    // ============================ ПОВОРОТ ЭКРАНА ============================

    void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (Width <= 0 || Height <= 0)
            return;
        ApplyLayout(Width > Height);
    }

    // Главная «магия» масштабирования: одна и та же разметка,
    // но при повороте элементы переставляются по сетке и меняют размеры
    void ApplyLayout(bool landscape)
    {
        if (_layoutReady && landscape == _isLandscape)
            return;
        _layoutReady = true;
        _isLandscape = landscape;

        RootGrid.ColumnDefinitions = landscape
            ? new ColumnDefinitionCollection
              {
                  new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) },
                  new ColumnDefinition { Width = GridLength.Star },
              }
            : new ColumnDefinitionCollection
              {
                  new ColumnDefinition { Width = GridLength.Star },
              };

        // Шапка всегда сверху на всю ширину
        Grid.SetRow(Header, 0);
        Grid.SetColumn(Header, 0);
        Grid.SetColumnSpan(Header, 2);
        Grid.SetRowSpan(Header, 1);

        if (landscape)
        {
            // Слева — большой блок «сейчас», справа — всё остальное
            Grid.SetRow(Hero, 1); Grid.SetRowSpan(Hero, 4);
            Grid.SetColumn(Hero, 0); Grid.SetColumnSpan(Hero, 1);

            Grid.SetRow(Chips, 1); Grid.SetColumn(Chips, 1);
            Grid.SetRowSpan(Chips, 1); Grid.SetColumnSpan(Chips, 1);

            Grid.SetRow(HourSection, 2); Grid.SetColumn(HourSection, 1);
            Grid.SetRowSpan(HourSection, 1); Grid.SetColumnSpan(HourSection, 1);

            Grid.SetRow(DaySection, 3); Grid.SetColumn(DaySection, 1);
            Grid.SetRowSpan(DaySection, 2); Grid.SetColumnSpan(DaySection, 1);

            WeatherEmoji.FontSize = 52;
            TempLabel.FontSize = 78;
            CityLabel.FontSize = 22;
            TempLabel.Margin = new Thickness(0, -4, 0, 0);
        }
        else
        {
            // Всё в одну колонку: шапка, герой, чипы, часы, дни
            Grid.SetRow(Hero, 1); Grid.SetRowSpan(Hero, 1);
            Grid.SetColumn(Hero, 0); Grid.SetColumnSpan(Hero, 1);

            Grid.SetRow(Chips, 2); Grid.SetColumn(Chips, 0);
            Grid.SetRowSpan(Chips, 1); Grid.SetColumnSpan(Chips, 1);

            Grid.SetRow(HourSection, 3); Grid.SetColumn(HourSection, 0);
            Grid.SetRowSpan(HourSection, 1); Grid.SetColumnSpan(HourSection, 1);

            Grid.SetRow(DaySection, 4); Grid.SetColumn(DaySection, 0);
            Grid.SetRowSpan(DaySection, 1); Grid.SetColumnSpan(DaySection, 1);

            WeatherEmoji.FontSize = 68;
            TempLabel.FontSize = 96;
            CityLabel.FontSize = 26;
            TempLabel.Margin = new Thickness(0, -8, 0, 0);
        }
    }

    // ============================ ЖИЗНЕННЫЙ ЦИКЛ ============================

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        AttachShake();

        if (!_layoutReady && Width > 0)
            ApplyLayout(Width > Height);

        if (!_appeared)
        {
            _appeared = true;
            ApplyThemeIcon();
            ApplyTheme();
            await PlayEntranceAsync();
        }

        if (_data is null && !_busy)
            await LoadCityAsync(manual: false);
    }

    protected override void OnDisappearing()
    {
        DetachShake();
        base.OnDisappearing();
    }

    // ============================ ТРЯСКА = ОБНОВИТЬ ============================

    void AttachShake()
    {
        try
        {
            Accelerometer.Default.ShakeDetected += OnShakeDetected;
            if (!Accelerometer.Default.IsMonitoring)
                Accelerometer.Default.Start(SensorSpeed.Game);
        }
        catch
        {
            // Устройство без акселерометра (например, ПК) — просто нет жеста тряски
        }
    }

    void DetachShake()
    {
        try
        {
            Accelerometer.Default.ShakeDetected -= OnShakeDetected;
            if (Accelerometer.Default.IsMonitoring)
                Accelerometer.Default.Stop();
        }
        catch
        {
            // Игнорируем: сенсор недоступен
        }
    }

    void OnShakeDetected(object? sender, EventArgs e) => _ = LoadCityAsync(manual: true);

    // ============================ ЗАГРУЗКА ПОГОДЫ ============================

    async Task LoadCityAsync(bool manual)
    {
        if (_busy || _cities.Count == 0)
            return;

        _busy = true;
        if (manual)
            TheRefreshView.IsRefreshing = true;

        var city = _cities[Math.Clamp(_cityIndex, 0, _cities.Count - 1)];
        CityLabel.Text = city.Name;

        try
        {
            var data = await _weather.GetForecastAsync(city);
            _data = data;
            RenderData(data);
            if (manual)
                Haptic();
        }
        catch
        {
            // Нет сети — пробуем показать последний успешный ответ
            var cached = _weather.TryLoadCached(city);
            if (cached is not null)
            {
                _data = cached;
                RenderData(cached);
                UpdatedLabel.Text = "Нет сети — показаны сохранённые данные";
            }
            else
            {
                ShowOffline();
            }
        }
        finally
        {
            _busy = false;
            TheRefreshView.IsRefreshing = false;
        }
    }

    void RenderData(WeatherData d)
    {
        var info = WeatherCatalog.Describe(d.WeatherCode);

        WeatherEmoji.Text = info.Emoji;
        TempLabel.Text = $"{Math.Round(d.Temperature)}°";
        CondLabel.Text = info.Text;
        FeelsLabel.Text = $"Ощущается как {Math.Round(d.Apparent)}°";
        WindLabel.Text = $"{d.WindSpeed:F0} м/с {WeatherCatalog.Compass(d.WindDirection)}";
        HumLabel.Text = $"{d.Humidity}%";
        PressLabel.Text = $"{d.PressureMm:F0} мм";
        UpdatedLabel.Text = d.FromCache ? "Сохранённые данные" : $"Обновлено в {DateTime.Now:HH:mm}";

        HourCollection.ItemsSource = d.Hours;
        BindableLayout.SetItemsSource(DayStack, d.Days);

        ApplyTheme(); // небо подстраивается под погоду и день/ночь в городе
        _ = PopAsync(TempLabel);
        _ = PopAsync(WeatherEmoji);
    }

    void ShowOffline()
    {
        WeatherEmoji.Text = "📡";
        TempLabel.Text = "—";
        CondLabel.Text = "Нет сети";
        FeelsLabel.Text = "Проверь интернет и потяни экран вниз";
        WindLabel.Text = "—";
        HumLabel.Text = "—";
        PressLabel.Text = "—";
        UpdatedLabel.Text = "";
    }

    // ============================ ТЕМА И ЖИВОЕ НЕБО ============================

    bool EffectiveDay() => _themeMode switch
    {
        ThemeMode.Light => true,
        ThemeMode.Dark => false,
        _ => _data?.IsDay ?? (DateTime.Now.Hour is >= 7 and < 19),
    };

    void ApplyTheme()
    {
        var day = EffectiveDay();
        // Меняем общесистемную тему — все карточки/текст перекрашиваются через AppThemeBinding
        Application.Current?.UserAppTheme = day ? AppTheme.Light : AppTheme.Dark;

        var cls = _data is null ? SkyClass.Clear : WeatherCatalog.Describe(_data.WeatherCode).Class;
        ApplySkyAnimated(WeatherCatalog.SkyBrush(day, cls));
    }

    async void ApplySkyAnimated(LinearGradientBrush brush)
    {
        if (RootGrid.Background is null)
        {
            RootGrid.Background = brush;
            return;
        }
        await RootGrid.FadeTo(0.45, 130, Easing.CubicIn);
        RootGrid.Background = brush;
        await RootGrid.FadeTo(1, 230, Easing.CubicOut);
    }

    void ApplyThemeIcon() => ThemeIcon.Text = _themeMode switch
    {
        ThemeMode.Light => "☀️",
        ThemeMode.Dark => "🌙",
        _ => "🌗",
    };

    void OnThemeClicked(object? sender, TappedEventArgs e)
    {
        Haptic();
        _themeMode = (ThemeMode)(((int)_themeMode + 1) % 3);
        Preferences.Default.Set("theme_mode", (int)_themeMode);
        ApplyThemeIcon();
        ApplyTheme();
        _ = PopAsync(ThemeButton);
    }

    // ============================ ГОРОДА ============================

    async void OnCityClicked(object? sender, TappedEventArgs e)
    {
        Haptic();
        var names = _cities.Select(c => c.Name).ToArray();
        var picked = await DisplayActionSheet("Город", "Отмена", "🗑 Удалить текущий", names);

        if (picked == "🗑 Удалить текущий")
        {
            if (_cities.Count <= 1)
            {
                await DisplayAlert("Город", "Должен остаться хотя бы один город.", "Ок");
                return;
            }
            _cities.RemoveAt(Math.Clamp(_cityIndex, 0, _cities.Count - 1));
            _cityIndex = Math.Clamp(_cityIndex, 0, _cities.Count - 1);
            SaveCities();
            _data = null;
            await LoadCityAsync(manual: false);
            return;
        }

        if (string.IsNullOrEmpty(picked) || picked == "Отмена")
            return;

        var idx = _cities.FindIndex(c => c.Name == picked);
        if (idx < 0 || idx == _cityIndex)
            return;

        _cityIndex = idx;
        SaveCityIndex();
        _data = null;
        Haptic();
        await LoadCityAsync(manual: false);
    }

    async void OnAddClicked(object? sender, TappedEventArgs e)
    {
        Haptic();
        _ = PopAsync(AddButton);

        var query = await DisplayPromptAsync("Новый город", "Введи название города:", placeholder: "Например: Дубай");
        if (string.IsNullOrWhiteSpace(query))
            return;

        List<CityInfo> found;
        try
        {
            found = await _weather.SearchCityAsync(query.Trim());
        }
        catch
        {
            await DisplayAlert("Нет сети", "Не удалось выполнить поиск. Проверь интернет.", "Ок");
            return;
        }

        if (found.Count == 0)
        {
            await DisplayAlert("Не нашли", $"Города «{query}» нет в базе. Попробуй другое написание.", "Ок");
            return;
        }

        string pick = found[0].Name;
        if (found.Count > 1)
            pick = await DisplayActionSheet("Уточни город", "Отмена", null, found.Select(c => c.Name).ToArray()) ?? "";

        var city = found.FirstOrDefault(c => c.Name == pick);
        if (city is null)
            return;

        if (_cities.Any(c => Math.Abs(c.Latitude - city.Latitude) < 0.01 && Math.Abs(c.Longitude - city.Longitude) < 0.01))
        {
            await DisplayAlert("Уже есть", "Этот город уже в списке.", "Ок");
            return;
        }

        _cities.Add(city);
        _cityIndex = _cities.Count - 1;
        SaveCities();
        Haptic();
        _data = null;
        await LoadCityAsync(manual: false);
    }

    // ============================ ЖЕСТЫ: СВАЙП И ОБНОВЛЕНИЕ ============================

    void OnSwipedLeft(object? sender, SwipedEventArgs e) => SwitchCity(+1);

    void OnSwipedRight(object? sender, SwipedEventArgs e) => SwitchCity(-1);

    async void SwitchCity(int delta)
    {
        if (_cities.Count < 2)
            return;
        Haptic();
        _cityIndex = (_cityIndex + delta + _cities.Count) % _cities.Count;
        SaveCityIndex();
        _data = null;
        await LoadCityAsync(manual: false);
    }

    async void OnRefreshing(object? sender, EventArgs e) => await LoadCityAsync(manual: true);

    // ============================ ТАП ПО ДНЮ ============================

    async void OnDayTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Element)?.BindingContext is DayForecast day)
        {
            Haptic();
            var info = WeatherCatalog.Describe(day.Code);
            await DisplayAlert(
                day.DayName,
                $"{day.Emoji} {info.Text}\n\n" +
                $"Днём {Math.Round(day.TempMax)}°, ночью {Math.Round(day.TempMin)}°\n" +
                $"Вероятность осадков: {day.PrecipProb}%\n" +
                $"Ветер до {day.WindMax:F0} м/с\n" +
                $"Восход: {day.Sunrise}  ·  Закат: {day.Sunset}",
                "Закрыть");
        }
    }

    // ============================ АНИМАЦИИ И ВИБРАЦИЯ ============================

    async Task PlayEntranceAsync()
    {
        Header.Opacity = 0;
        VisualElement[] sections = { Hero, Chips, HourSection, DaySection };
        foreach (var s in sections)
        {
            s.Opacity = 0;
            s.TranslationY = 26;
        }

        _ = Header.FadeTo(1, 260, Easing.CubicOut);
        foreach (var s in sections)
        {
            _ = s.FadeTo(1, 320, Easing.CubicOut);
            await s.TranslateTo(0, 0, 320, Easing.CubicOut);
            await Task.Delay(50);
        }
    }

    async Task PopAsync(VisualElement v)
    {
        await v.ScaleTo(1.12, 110, Easing.CubicOut);
        await v.ScaleTo(1, 170, Easing.CubicIn);
    }

    void Haptic()
    {
        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch
        {
            // Вибромотор недоступен (например, эмулятор) — не страшно
        }
    }

    // ============================ СОХРАНЕНИЕ СОСТОЯНИЯ ============================

    void LoadSavedState()
    {
        try
        {
            var json = Preferences.Default.Get("cities_json", "");
            _cities = string.IsNullOrWhiteSpace(json)
                ? new List<CityInfo>(DefaultCities)
                : JsonSerializer.Deserialize<List<CityInfo>>(json) ?? new List<CityInfo>(DefaultCities);
        }
        catch
        {
            _cities = new List<CityInfo>(DefaultCities);
        }
        if (_cities.Count == 0)
            _cities = new List<CityInfo>(DefaultCities);

        _cityIndex = Math.Clamp(Preferences.Default.Get("city_index", 0), 0, _cities.Count - 1);
        _themeMode = (ThemeMode)Math.Clamp(Preferences.Default.Get("theme_mode", 0), 0, 2);

        CityLabel.Text = _cities[_cityIndex].Name;
    }

    void SaveCities()
    {
        Preferences.Default.Set("cities_json", JsonSerializer.Serialize(_cities));
        Preferences.Default.Set("city_index", _cityIndex);
    }

    void SaveCityIndex() => Preferences.Default.Set("city_index", _cityIndex);
}
