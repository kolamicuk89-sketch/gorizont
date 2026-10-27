namespace Gorizont.Services;

// Город: название (как показываем пользователю) + координаты
public class CityInfo
{
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

// Сводка погоды по городу
public class WeatherData
{
    public double Temperature { get; set; }   // °C
    public double Apparent { get; set; }      // ощущается как, °C
    public int Humidity { get; set; }         // %
    public double WindSpeed { get; set; }     // м/с
    public int WindDirection { get; set; }    // градусы (0 = север)
    public double PressureMm { get; set; }    // мм рт. ст. (пересчитано из гПа)
    public int WeatherCode { get; set; }      // код WMO
    public bool IsDay { get; set; }           // сейчас в этом городе день?
    public bool FromCache { get; set; }       // данные из сохранённого кэша (нет сети)
    public List<HourForecast> Hours { get; set; } = new();
    public List<DayForecast> Days { get; set; } = new();
}

// Одна строка прогноза «по часам»
public class HourForecast
{
    public DateTime Time { get; set; }
    public string HourLabel { get; set; } = "";  // «Сейчас» или «15:00»
    public string TempText { get; set; } = "";   // «12°»
    public string Emoji { get; set; } = "";
}

// Одна карточка прогноза «на день»
public class DayForecast
{
    public DateTime Date { get; set; }
    public string DayName { get; set; } = "";    // «Сегодня», «Завтра», «Пт 30.10»
    public string TempText { get; set; } = "";   // «12° / 4°»
    public string Details { get; set; } = "";    // строка под названием
    public string Emoji { get; set; } = "";
    // Сырые данные для окна «подробнее»
    public int Code { get; set; }
    public double TempMax { get; set; }
    public double TempMin { get; set; }
    public int PrecipProb { get; set; }
    public double WindMax { get; set; }
    public string Sunrise { get; set; } = "";
    public string Sunset { get; set; } = "";
}
