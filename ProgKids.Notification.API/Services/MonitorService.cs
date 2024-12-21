using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Google.Apis.Sheets.v4.Data;
using Newtonsoft.Json;

namespace ProgKidsNotifier.Services;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;

public class MonitorService : BackgroundService
{
    private string _spreadsheetId = "1rQU7dr22i7aS-tEjEdvyCaqhdjTEuol99L5Hzeo9DEc";
    private string _rangeTeachers = "Преподаватели!A:R";
    private string _rangeManagers = "Менеджеры!A:R";
    private int _lastRow = 0;
    private int _lastRowManagers = 0;
    private const string _channelIdTechSupp = "ecjtcg4t7td1irenib67ggdu7a";
    private const string _channelIdTechNotifications = "4qsc9pn6wtrp3nf4usn9z33ryc";
    private const string _postUrl = "https://msg.progkids.com/api/v4/posts";
    private const string _botApiToken = "qmcxb6tnai8qfr4ayy5ofej1ko";
    private List<int> _failedToSendIds = [];
    private List<int> _failedToSendIdsManagers = [];
    private Dictionary<string, int> _columnsIds = new();
    private Dictionary<string, int> _columnsIdsManagers = new();
    public static bool ServiceOn = true;
    private Timer? _timer = null;


    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine("Starting service...");
            Console.WriteLine("Getting rows...");
            var rows = await GetRowsAsync(teachers: true);
            var rowsManagers = await GetRowsAsync(teachers: false);
            Console.WriteLine($"Initial rows found: {rows.Count}");
            _lastRow = rows.Count;
            _lastRowManagers = rowsManagers.Count;
            var firstRow = rows.First();
            var firstRowManagers = rowsManagers.First();
            Console.WriteLine($"Getting columns ids");
            _columnsIds.Add("ticketId", firstRow.IndexOf("Id заявки"));
            _columnsIds.Add("problem", firstRow.IndexOf("Кратко опишите проблему"));
            _columnsIds.Add("email", firstRow.IndexOf("Электронный адрес ученика"));
            _columnsIds.Add("teacherToggle", firstRow.IndexOf("Укажите ваш ник в Mattermost с @"));
            _columnsIds.Add("status", firstRow.IndexOf("Статус"));
            _columnsIds.Add("agent", firstRow.IndexOf("Кто обрабатывает задачу"));
            _columnsIds.Add("postId", firstRow.IndexOf("PostId"));
            _columnsIds.Add("contactDate", firstRow.IndexOf("Дата связи с учеником"));
            _columnsIds.Add("contactTime", firstRow.IndexOf("Время связи с учеником"));
            // Managers
            _columnsIdsManagers.Add("ticketId", firstRowManagers.IndexOf("Id заявки"));
            _columnsIdsManagers.Add("problem", firstRowManagers.IndexOf("Кратко опишите проблему"));
            _columnsIdsManagers.Add("link", firstRowManagers.IndexOf("Ссылка на сделку в amocrm"));
            _columnsIdsManagers.Add("teacherToggle", firstRowManagers.IndexOf("Укажите ваш ник в Mattermost с @"));
            _columnsIdsManagers.Add("status", firstRowManagers.IndexOf("Статус"));
            _columnsIdsManagers.Add("agent", firstRowManagers.IndexOf("Кто обрабатывает задачу"));
            _columnsIdsManagers.Add("postId", firstRowManagers.IndexOf("PostId"));
            _columnsIdsManagers.Add("contactDate", firstRowManagers.IndexOf("Дата связи с учеником"));
            _columnsIdsManagers.Add("contactTime", firstRowManagers.IndexOf("Время связи с учеником"));
            Console.WriteLine($"Found columns : {_columnsIds.Count} ");
            Console.WriteLine($"Found columns managers : {_columnsIdsManagers.Count}");
            Console.WriteLine($"Monitoring started ...");
            ServiceOn = true;
            await MonitorSpreadsheetForNewRows();
        }
        catch (Exception ex)
        {
            ServiceOn = false;
            Console.WriteLine(
                $"{DateTime.Now.ToShortDateString()} - {DateTime.Now.ToShortTimeString()} Error: {ex.Message}");
            Console.WriteLine("Hint : Check your spreadsheet column names, whether they match the script");
        }
    }


    private async Task MonitorSpreadsheetForNewRows()
    {
        while (true)
        {
            // Console.WriteLine($"[Teacher] Monitoring .. last row ID = {_lastRow}");
            // Console.WriteLine($"[Manager] Monitoring .. last row ID = {_lastRowManagers}");
            var rows = await GetRowsAsync(teachers: true);
            var rowsManagers = await GetRowsAsync(teachers: false);
            var rowsCount = rows.Count;
            var rowsCountManagers = rowsManagers.Count;
            //Teachers 
            if (rowsCount > _lastRow)
            {
                for (int i = _lastRow + 1; i <= rowsCount; i++)
                {
                    var currentNewRow = rows[i - 1];
                    if ((currentNewRow.Count > _columnsIds["postId"]) &&
                        (!string.IsNullOrWhiteSpace(currentNewRow[_columnsIds["postId"]]?.ToString())))
                    {
                        _lastRow = i;
                        continue;
                    }

                    var sb = new StringBuilder();
                    sb.AppendLine("++++++++");
                    sb.Append("** Новый тикет  **\n");
                    sb.AppendLine($"**ID тикета: ** {currentNewRow[_columnsIds["ticketId"]]}");
                    sb.AppendLine($"**Преподаватель: ** {currentNewRow[_columnsIds["teacherToggle"]]}");
                    sb.AppendLine($"**Описание проблемы: ** {currentNewRow[_columnsIds["problem"]]}");
                    sb.AppendLine($"**почта ученика: ** {currentNewRow[_columnsIds["email"]]}");
                    if ((currentNewRow.Count > _columnsIds["contactDate"])
                        && (!string.IsNullOrWhiteSpace(currentNewRow[_columnsIds["contactDate"]].ToString())))
                        sb.AppendLine($"** Дата связи с учеником ** : {currentNewRow[_columnsIds["contactDate"]]}");
                    if ((currentNewRow.Count > _columnsIds["contactTime"])
                        && (!string.IsNullOrWhiteSpace(currentNewRow[_columnsIds["contactTime"]].ToString())))
                        sb.AppendLine($"** Время связи с учеником ** : {currentNewRow[_columnsIds["contactTime"]]}");
                    sb.AppendLine("@support");
                    sb.AppendLine("++++++++");
                    if (await SendToMattermost(sb.ToString()) is { } postId)
                    {
                        _lastRow = i;
                  //      Console.WriteLine($"[Teacher] new ticket : PostID = {postId}");
                        await UpdatePostIdInGoogleSheet(postId, i - 1);
                    }
                    else
                    {
                        if (!_failedToSendIds.Contains(i))
                            _failedToSendIds.Add(i);
                    }
                }
            }
            else if (_lastRow > rowsCount)
            {
             //   Console.WriteLine($"[Teachers] Rows deleted, updating last row  to {rowsCount}");
                _lastRow = rowsCount;
            }
            else
            {
          //      Console.WriteLine("[Teachers] No new row.");
            }

            // MANAGERS ---------------------
            if (rowsCountManagers > _lastRowManagers)
            {
                for (int i = _lastRowManagers + 1; i <= rowsCountManagers; i++)
                {
                    var currentNewRow = rowsManagers[i - 1];
                    if ((currentNewRow.Count > _columnsIdsManagers["postId"]) &&
                        (!string.IsNullOrWhiteSpace(currentNewRow[_columnsIdsManagers["postId"]]?.ToString())))
                    {
                        _lastRowManagers = i;
                        continue;
                    }

                    var sb = new StringBuilder();
                    sb.AppendLine("++++++++");
                    sb.Append("** Новый тикет  **\n");
                    sb.AppendLine($"**ID тикета: ** {currentNewRow[_columnsIdsManagers["ticketId"]]}");
                    sb.AppendLine($"**Менеджер: ** {currentNewRow[_columnsIdsManagers["teacherToggle"]]}");
                    sb.AppendLine($"**Описание проблемы: ** {currentNewRow[_columnsIdsManagers["problem"]]}");
                    sb.AppendLine($"**Ссылка на AmoCRM: ** {currentNewRow[_columnsIdsManagers["link"]]}");
                    if ((currentNewRow.Count > _columnsIdsManagers["contactDate"])
                        && (!string.IsNullOrWhiteSpace(currentNewRow[_columnsIdsManagers["contactDate"]].ToString())))
                        sb.AppendLine(
                            $"** Дата связи с учеником ** : {currentNewRow[_columnsIdsManagers["contactDate"]]}");
                    if ((currentNewRow.Count > _columnsIdsManagers["contactTime"])
                        && (!string.IsNullOrWhiteSpace(currentNewRow[_columnsIdsManagers["contactTime"]].ToString())))
                        sb.AppendLine(
                            $"** Время связи с учеником ** : {currentNewRow[_columnsIdsManagers["contactTime"]]}");
                    sb.AppendLine("@support");
                    sb.AppendLine("++++++++");
                    if (await SendToMattermost(sb.ToString()) is { } postId)
                    {
                        _lastRowManagers = i;
                  //      Console.WriteLine($"[Manager] new ticket : PostID = {postId}");
                        await UpdatePostIdInGoogleSheet(postId, i - 1, managerSheet: true);
                    }
                    else
                    {
                        if (!_failedToSendIdsManagers.Contains(i))
                            _failedToSendIdsManagers.Add(i);
                    }
                }
            }
            else if (_lastRowManagers > rowsCountManagers)
            {
               // Console.WriteLine($"[Manager] Rows deleted, updating last row  to {rowsCountManagers}");
                _lastRowManagers = rowsCountManagers;
            }
            else
            {
              //  Console.WriteLine("[Manager] No new row.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    private async Task<IList<IList<object>>> GetRowsAsync(bool teachers)
    {
        try
        {
            var service = await GoogleSheetService.GetSheetsService();
            var request = service.Spreadsheets.Values.Get(_spreadsheetId, teachers ? _rangeTeachers : _rangeManagers);
            var response = await request.ExecuteAsync();
            return response.Values;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return [];
        }
    }


    private async Task<string?> SendToMattermost(string messageToSend)
    {
        var client = HttpClients.Default;
        
        var jsonPayload = new
        {
            message = messageToSend,
            channel_id = _channelIdTechNotifications
        };
        var content = new StringContent(
            Newtonsoft.Json.JsonConvert.SerializeObject(jsonPayload),
            System.Text.Encoding.UTF8,
            "application/json");
        
        var response = await client.PostAsync(_postUrl, content);

        if (response.IsSuccessStatusCode)
        {
            var postId = await System.Text.Json.JsonSerializer.DeserializeAsync<MonitorService.MessageRespose>(
                await response.Content.ReadAsStreamAsync());
           // Console.WriteLine($"Message sent to Mattermost successfully. | PostId : {postId}");
            return postId?.id;
        }

        //    Console.WriteLine($"Failed to send message to Mattermost. | Repose {await response.Content.ReadAsStringAsync()}");
        return null;
    }

    public static async Task<bool> SendUpdateMessage(string postID, string message2)
    {
        var client = HttpClients.Default;
        var jsonPayload = new
        {
            message = message2,
            channel_id = _channelIdTechNotifications,
            root_id = postID
        };
        var content = new StringContent(
            Newtonsoft.Json.JsonConvert.SerializeObject(jsonPayload),
            System.Text.Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync(_postUrl, content);

        if (response.IsSuccessStatusCode)
        {
            var postId =await System.Text.Json.JsonSerializer.DeserializeAsync<MessageRespose>(
                await response.Content.ReadAsStreamAsync());
         //   Console.WriteLine($"Message sent to Mattermost successfully. | PostId : {postId}");
            return true;
        }

        Console.WriteLine(  
            $"Failed to send message to Mattermost. | Repose {await response.Content.ReadAsStringAsync()}");
        return false;
    }

    private async Task UpdatePostIdInGoogleSheet(string postId, int rowIndex, bool managerSheet = false)
    {
        try
        {
            var rangeTeachers = $"Преподаватели!{GetColumnLetter(_columnsIds["postId"])}{rowIndex + 1}";
            var rangeManagers = $"Менеджеры!{GetColumnLetter(_columnsIdsManagers["postId"])}{rowIndex + 1}";
            var service = await GoogleSheetService.GetSheetsService();
            var values = new List<IList<object>> { new List<object> { postId } };
            var body = new ValueRange { Values = values };

            var updateRequest =
                service.Spreadsheets.Values.Update(body, _spreadsheetId,
                    (managerSheet) ? rangeManagers : rangeTeachers);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;

             await updateRequest.ExecuteAsync();
            //Console.WriteLine($"Successfully updated PostId in Google Sheets for row {rowIndex + 1}: {postId}");
        }
        catch (Exception ex)
        {
            // Console.WriteLine($"Error updating PostId in Google Sheets: {ex.Message}");
        }
    }

    private string GetColumnLetter(int columnIndex)
    {
        int dividend = columnIndex + 1;
        string columnLetter = string.Empty;
        while (dividend > 0)
        {
            int modulo = (dividend - 1) % 26;
            columnLetter = Convert.ToChar(modulo + 65) + columnLetter;
            dividend = (dividend - modulo) / 26;
        }

        return columnLetter;
    }

    public record MessageRespose(string id);


    private static class HttpClients
    {
        public static readonly HttpClient Default = new HttpClient
        {
            // Optionally set default headers, timeouts, etc.\
            DefaultRequestHeaders = { Authorization = new AuthenticationHeaderValue("Bearer", _botApiToken) },
            Timeout = TimeSpan.FromSeconds(15)
        };
    }
}