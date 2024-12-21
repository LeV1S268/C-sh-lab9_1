using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text.Json;

// Класс для хранения данных о ценах акций
public class StockData
{
    public string s { get; set; } // Статус
    public List<double> c { get; set; } // Закрытие
    public List<double> h { get; set; } // Максимум
    public List<double> l { get; set; } // Минимум
    public List<double> o { get; set; } // Открытие
    public List<int> t { get; set; } // Временные метки UNIX
    public List<int> v { get; set; } // Объем
};

// Основной класс программы
class Market
{
    // Мьютекс для обеспечения потокобезопасности при записи в файл
    static readonly Mutex mutex = new Mutex();

    // Асинхронное чтение данных из файла
    static async Task ReadFileAsync(List<string> massive, string filePath)
    {
        using (StreamReader sr = new StreamReader(filePath))
        {
            string line;
            while ((line = await sr.ReadLineAsync()) != null)
            {
                massive.Add(line);
            }
        }
    }

    // Запись данных в файл
    static void WriteToFile(string filePath, string text)
    {
        if (!File.Exists(filePath))
        {
            File.Create(filePath); // Создание файла, если его нет
        }
        mutex.WaitOne(); // Блокировка мьютекса
        try
        {
            File.AppendAllText(filePath, text + Environment.NewLine); // Добавление текста в файл
        }
        finally
        {
            mutex.ReleaseMutex(); // Освобождение мьютекса
        }
    }

    // Расчет средней цены по спискам максимальных и минимальных цен
    static double calculateAvgPrice(List<double> highPrice, List<double> lowPrice)
    {
        double totalAvgPrice = 0;
        for (int i = 0; i < highPrice.Count; i++)
        {
            totalAvgPrice += (highPrice[i] + lowPrice[i]) / 2;
        }
        return totalAvgPrice / highPrice.Count;
    }

    // Получение данных с API и запись средней цены в файл
    static async Task GetData(HttpClient client, string quote, string startDate, string endDate, string output)
    {
        string apiKey = "aGR1V3lRaF9nWC14U01ZWDhpQzU2MEtEc0daZkVzdG1nd3FPX1Vndk44UT0"; // API-ключ
        string URL = $"https://api.marketdata.app/v1/stocks/candles/D/{quote}/?from={startDate}&to={endDate}&token={apiKey}";
        HttpClient cl = new HttpClient();

        HttpResponseMessage response = cl.GetAsync(URL).Result; // Запрос данных
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Error! Status code: {response.StatusCode}"); // Обработка ошибки
        }
        else
        {
            var json = await response.Content.ReadAsStringAsync(); // Получение JSON-ответа
            var data = JsonSerializer.Deserialize<StockData>(json); // Десериализация данных
            if (data != null && data.h != null && data.l != null)
            {
                double avgPrice = calculateAvgPrice(data.h, data.l); // Расчет средней цены
                string result = $"{quote}:{avgPrice}"; // Формирование результата
                WriteToFile(output, result); // Запись в файл
                Console.WriteLine($"Average price for {quote}: {avgPrice}"); // Вывод результата
            }
            else Console.WriteLine($"Not enough data for {quote}"); // Если данных недостаточно
        }
    }

    // Точка входа в программу
    static async Task Main(string[] args)
    {
        string apiKey = "aGR1V3lRaF9nWC14U01ZWDhpQzU2MEtEc0daZkVzdG1nd3FPX1Vndk44UT0"; // API-ключ
        string tickerPath = "C:\\Users\\aleks\\OneDrive\\Рабочий стол\\Прога\\СЕМ 3\\lab9\\ticker.txt"; // Путь к файлу тикеров
        string outputPath = "C:\\Users\\aleks\\OneDrive\\Рабочий стол\\Прога\\СЕМ 3\\lab9\\average_prices.txt"; // Путь к файлу для записи результатов

        List<string> ticker = []; // Список для хранения тикеров
        await ReadFileAsync(ticker, tickerPath); // Чтение тикеров из файла

        DateTime endDateTime = DateTime.Now; // Текущая дата
        string endDate = endDateTime.AddMonths(-1).ToString("yyyy-MM-dd"); // Конечная дата
        string startDate = endDateTime.AddYears(-1).AddMonths(1).ToString("yyyy-MM-dd"); // Начальная дата
        Console.WriteLine($"Start Date: {startDate}"); // Вывод начальной даты
        Console.WriteLine($"End Date: {endDate}"); // Вывод конечной даты

        HttpClient client = new HttpClient(); // HTTP-клиент
        client.DefaultRequestHeaders.Clear(); // Очистка заголовков
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json")); // Добавление заголовка
        List<Task> tasks = new List<Task>(); // Список задач

        foreach (var quote in ticker) // Обработка каждого тикера
        {
            tasks.Add(GetData(client, quote, startDate, endDate, outputPath)); // Добавление задачи получения данных
        }
        await Task.WhenAll(tasks); // Ожидание завершения всех задач
    }
}
