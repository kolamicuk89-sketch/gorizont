using Microsoft.Maui.Graphics;

namespace Gorizont.Services;

public enum SkyClass { Clear, Cloudy, Rain, Snow, Storm, Fog }

// Каталог: коды погоды WMO -> эмодзи + текст, и палитры неба
public static class WeatherCatalog
{
    // Расшифровка кода погоды: эмодзи, описание, класс для палитры неба
    public static (string Emoji, string Text, SkyClass Class) Describe(int code) => code switch
    {
        0 => ("☀️", "Ясно", SkyClass.Clear),
        1 => ("🌤️", "Преимущественно ясно", SkyClass.Clear),
        2 => ("⛅", "Переменная облачность", SkyClass.Cloudy),
        3 => ("☁️", "Пасмурно", SkyClass.Cloudy),
        45 => ("🌫️", "Туман", SkyClass.Fog),
        48 => ("🌫️", "Изморозь", SkyClass.Fog),
        51 => ("🌦️", "Лёгкая морось", SkyClass.Rain),
        53 => ("🌦️", "Морось", SkyClass.Rain),
        55 => ("🌦️", "Сильная морось", SkyClass.Rain),
        56 or 57 => ("🌧️", "Ледяная морось", SkyClass.Rain),
        61 => ("🌦️", "Небольшой дождь", SkyClass.Rain),
        63 => ("🌧️", "Дождь", SkyClass.Rain),
        65 => ("🌧️", "Сильный дождь", SkyClass.Rain),
        66 or 67 => ("🌧️", "Ледяной дождь", SkyClass.Rain),
        71 => ("🌨️", "Небольшой снег", SkyClass.Snow),
        73 => ("🌨️", "Снег", SkyClass.Snow),
        75 => ("❄️", "Сильный снегопад", SkyClass.Snow),
        77 => ("🌨️", "Снежные зёрна", SkyClass.Snow),
        80 => ("🌦️", "Ливень", SkyClass.Rain),
        81 => ("🌧️", "Сильный ливень", SkyClass.Rain),
        82 => ("⛈️", "Очень сильный ливень", SkyClass.Rain),
        85 or 86 => ("🌨️", "Снежные ливни", SkyClass.Snow),
        95 => ("⛈️", "Гроза", SkyClass.Storm),
        96 or 99 => ("⛈️", "Гроза с градом", SkyClass.Storm),
        _ => ("🌍", "Неизвестная погода", SkyClass.Cloudy),
    };

    // Живое небо: градиент зависит от времени суток и характера погоды
    public static LinearGradientBrush SkyBrush(bool day, SkyClass cls)
    {
        string top, bottom;

        if (day)
        {
            top = cls switch
            {
                SkyClass.Clear => "#1E63C8",
                SkyClass.Cloudy => "#4C7CA8",
                SkyClass.Rain => "#3D5B7A",
                SkyClass.Snow => "#5F7BA8",
                SkyClass.Storm => "#2B3A55",
                SkyClass.Fog => "#6E7E92",
                _ => "#1E63C8",
            };
            bottom = cls switch
            {
                SkyClass.Clear => "#7EC8FF",
                SkyClass.Cloudy => "#A9C4DC",
                SkyClass.Rain => "#7B93AC",
                SkyClass.Snow => "#C9D8EC",
                SkyClass.Storm => "#5F7089",
                SkyClass.Fog => "#BFC9D4",
                _ => "#7EC8FF",
            };
        }
        else
        {
            top = cls switch
            {
                SkyClass.Clear => "#070D24",
                SkyClass.Cloudy => "#0F172B",
                SkyClass.Rain => "#0A1220",
                SkyClass.Snow => "#111A30",
                SkyClass.Storm => "#080C16",
                SkyClass.Fog => "#101726",
                _ => "#070D24",
            };
            bottom = cls switch
            {
                SkyClass.Clear => "#1C2C5A",
                SkyClass.Cloudy => "#27375A",
                SkyClass.Rain => "#22334C",
                SkyClass.Snow => "#31436B",
                SkyClass.Storm => "#1B2540",
                SkyClass.Fog => "#2B3550",
                _ => "#1C2C5A",
            };
        }

        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(top), 0f));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(bottom), 1f));
        return brush;
    }

    // Направление ветра по градусам -> буквы (С, СВ, В, ...)
    public static string Compass(int degrees)
    {
        var dirs = new[] { "С", "СВ", "В", "ЮВ", "Ю", "ЮЗ", "З", "СЗ" };
        return dirs[(((degrees % 360) + 360) % 360) / 45];
    }
}
