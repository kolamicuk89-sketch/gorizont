using System.Globalization;
using System.Text.Json;
using Microsoft.Maui.Storage;

namespace Gorizont.Services;

// Сервис погоды: качает данные с open-meteo (бесплатный, без ключей),
// кэширует последний успешный ответ и умеет искать города
public class WeatherService
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    const string GeocodingUrl = "https://geocoding-api.open-meteo.com/v1/search";

    // Текущая погода + 24 часа + 5 дней
    public async Task<WeatherData> GetForecastAsync(CityInfo city)
    {
        double lat = Math.Round(city.Latitude, 4);
        double lon = Math.Round(city.Longitude, 4);
        string url =
            "https://api.open-meteo.com/v1/forecast" +
            $"?latitude={lat.ToString(CultureInfo.InvariantCulture)}" +
            $"&longitude={lon.ToString(CultureInfo.InvariantCulture)}" +
            "&current=temperature_2m,relative_humidity_2m,apparent_temperature,is_day,weather_code,wind_speed_10m,wind_direction_10m,pressure_msl" +
            "&hourly=temperature_2m,weather_code" +
            "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max,wind_speed_10m_max,sunrise,sunset" +
            "&timezone=auto&forecast_days=6&wind_speed_unit=ms";

        string json = await Http.GetStringAsync(url);
        Preferences.Default.Set(CacheKey(city), json); // сохранили на случай "нет сети"
        return Parse(json, fromCache: false);
    }

    // Последние успешные данные из кэша (когда интернета нет)
    public WeatherData? TryLoadCached(CityInfo city)
    {
        try
        {
            var json = Preferences.Default.Get(CacheKey(city), "");
            return string.IsNullOrEmpty(json) ? null : Parse(json, fromCache: true);
        }
        catch
        {
            return null;
        }
    }

    static string CacheKey(CityInfo c) =>
        $"wx_{Math.Round(c.Latitude, 2).ToString(CultureInfo.InvariantCulture)}_{Math.Round(c.Longitude, 2).ToString(CultureInfo.InvariantCulture)}";

    // Разбор JSON-ответа open-meteo
    static WeatherData Parse(string json, bool fromCache)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var cur = root.GetProperty("current");
        double offset = root.GetProperty("utc_offset_seconds").GetDouble();

        var data = new WeatherData
        {
            Temperature = cur.GetProperty("temperature_2m").GetDouble(),
            Apparent = cur.GetProperty("apparent_temperature").GetDouble(),
            Humidity = cur.GetProperty("relative_humidity_2m").GetInt32(),
            IsDay = cur.GetProperty("is_day").GetInt32() == 1,
            WeatherCode = cur.GetProperty("weather_code").GetInt32(),
            WindSpeed = cur.GetProperty("wind_speed_10m").GetDouble(),
            WindDirection = cur.GetProperty("wind_direction_10m").GetInt32(),
            PressureMm = cur.GetProperty("pressure_msl").GetDouble() * 0.750062, // гПа -> мм рт. ст.
            FromCache = fromCache,
        };

        // --- Прогноз по часам: ближайшие 24 часа по времени ГОРОДА ---
        var hours = new List<HourForecast>();
        var hTimes = root.GetProperty("hourly").GetProperty("time");
        var hTemps = root.GetProperty("hourly").GetProperty("temperature_2m");
        var hCodes = root.GetProperty("hourly").GetProperty("weather_code");

        var cityNow = DateTime.UtcNow.AddSeconds(offset);
        var startFrom = new DateTime(cityNow.Year, cityNow.Month, cityNow.Day, cityNow.Hour, 0, 0);

        int start = 0;
        for (int i = 0; i < hTimes.GetArrayLength(); i++)
        {
            if (DateTime.Parse(hTimes[i].GetString()!) >= startFrom) { start = i; break; }
        }

        bool first = true;
        for (int i = start; i < hTimes.GetArrayLength() && hours.Count < 24; i++)
        {
            var t = DateTime.Parse(hTimes[i].GetString()!);
            hours.Add(new HourForecast
            {
                Time = t,
                HourLabel = first ? "Сейчас" : t.ToString("HH:mm"),
                TempText = $"{Math.Round(hTemps[i].GetDouble())}°",
                Emoji = WeatherCatalog.Describe(hCodes[i].GetInt32()).Emoji,
            });
            first = false;
        }
        data.Hours = hours;

        // --- Прогноз на дни ---
        var days = new List<DayForecast>();
        var daily = root.GetProperty("daily");
        var ru = new CultureInfo("ru-RU");

        for (int i = 0; i < daily.GetProperty("time").GetArrayLength(); i++)
        {
            var date = DateTime.Parse(daily.GetProperty("time")[i].GetString()!);
            double max = daily.GetProperty("temperature_2m_max")[i].GetDouble();
            double min = daily.GetProperty("temperature_2m_min")[i].GetDouble();
            int prob = daily.GetProperty("precipitation_probability_max")[i].GetInt32();
            double windMax = daily.GetProperty("wind_speed_10m_max")[i].GetDouble();
            int code = daily.GetProperty("weather_code")[i].GetInt32();

            string name;
            if (i == 0) name = "Сегодня";
            else if (i == 1) name = "Завтра";
            else
            {
                var abbr = date.ToString("ddd", ru);
                name = char.ToUpper(abbr[0]) + abbr[1..] + " " + date.ToString("dd.MM");
            }

            days.Add(new DayForecast
            {
                Date = date,
                DayName = name,
                Emoji = WeatherCatalog.Describe(code).Emoji,
                Code = code,
                TempMax = max,
                TempMin = min,
                TempText = $"{Math.Round(max)}° / {Math.Round(min)}°",
                PrecipProb = prob,
                WindMax = windMax,
                Details = prob > 0
                    ? $"💧 {prob}%  ·  💨 до {windMax:F0} м/с"
                    : $"💨 до {windMax:F0} м/с",
                Sunrise = daily.GetProperty("sunrise")[i].GetString()!.Substring(11, 5),
                Sunset = daily.GetProperty("sunset")[i].GetString()!.Substring(11, 5),
            });
        }
        data.Days = days;

        return data;
    }

    // Поиск города по названию (геокодинг open-meteo, на русском)
    public async Task<List<CityInfo>> SearchCityAsync(string query)
    {
        string url = $"{GeocodingUrl}?name={Uri.EscapeDataString(query)}&count=5&language=ru&format=json";
        string json = await Http.GetStringAsync(url);

        var list = new List<CityInfo>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("results", out var results))
            return list;

        foreach (var r in results.EnumerateArray())
        {
            var name = r.GetProperty("name").GetString() ?? "";
            var country = r.TryGetProperty("country", out var c) ? c.GetString() : null;
            var admin = r.TryGetProperty("admin1", out var a) ? a.GetString() : null;

            string display = name;
            if (!string.IsNullOrEmpty(admin) && admin != name) display += $", {admin}";
            if (!string.IsNullOrEmpty(country)) display += $", {country}";

            list.Add(new CityInfo
            {
                Name = display,
                Latitude = r.GetProperty("latitude").GetDouble(),
                Longitude = r.GetProperty("longitude").GetDouble(),
            });
        }

        // убираем дубли по координатам
        return list
            .GroupBy(c => (Math.Round(c.Latitude, 2), Math.Round(c.Longitude, 2)))
            .Select(g => g.First())
            .ToList();
    }
}
