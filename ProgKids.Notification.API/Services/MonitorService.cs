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
    private string _rangeTeachers = "Преподаватели!A1:Q5000"; 
    private string _rangeManagers = "Менеджеры!A1:Q5000"; 
    private int _lastRow = 0;
    private const string _channelIdTechSupp = "ecjtcg4t7td1irenib67ggdu7a";
    private const string _channelIdTechNotifications = "4qsc9pn6wtrp3nf4usn9z33ryc";
    private const string _postUrl = "https://msg.progkids.com/api/v4/posts";
    private const string _botApiToken = "qmcxb6tnai8qfr4ayy5ofej1ko";
    private List<int> _failedToSendIds = [];
    private Dictionary<string, int> _columnsIds = new();
    public static bool ServiceOn = true;
    private Timer? _timer = null;


    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine("Starting service...");
            Console.WriteLine("Getting rows...");
            var rows = await GetRowsAsync();
            Console.WriteLine($"Initial rows found: {rows.Count}");
            _lastRow = rows.Count;
            var firstRow = rows.First();
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
            Console.WriteLine($"Found columns : {_columnsIds}");
            Console.WriteLine($"Monitoring started ...");
            await MonitorSpreadsheetForNewRows();

        }
        catch (Exception ex)
        {
            ServiceOn = false;
            Console.WriteLine($"{DateTime.Now.ToShortDateString()} - {DateTime.Now.ToShortTimeString()} Error: {ex.Message}");
            Console.WriteLine("Hint : Check your spreadsheet column names, whether they match the script");
        }
    }
    
    
    private async Task MonitorSpreadsheetForNewRows()
    {
        while (true)
        {
            Console.WriteLine($"Monitoring .. last row ID = {_lastRow}");
            var rows = await GetRowsAsync();
            var rowsCount = rows.Count;
            if (rowsCount > _lastRow)
            {
                for (int i = _lastRow + 1; i <= rowsCount; i++)
                {
                    var currentNewRow = rows[i-1];
                    if (!string.IsNullOrEmpty(currentNewRow[_columnsIds["postId"]]?.ToString()))
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
                    if (!string.IsNullOrWhiteSpace(currentNewRow[_columnsIds["contactDate"]].ToString()))
                        sb.AppendLine($"** Дата связи с учеником ** : {currentNewRow[_columnsIds["contactDate"]]}");
                    if (!string.IsNullOrWhiteSpace(currentNewRow[_columnsIds["contactTime"]].ToString()))
                        sb.AppendLine($"** Время связи с учеником ** : {currentNewRow[_columnsIds["contactTime"]]}");
                    sb.AppendLine("@support");
                    sb.AppendLine("++++++++");
                    if (await SendToMattermost(sb.ToString()) is { } postId)
                    {
                        _lastRow = i;
                        Console.WriteLine($"new ticket : PostID = {postId}");
                        await UpdatePostIdInGoogleSheet(postId, i-1);
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
                Console.WriteLine($"Rows deleted, updating last row  to {rowsCount}");
                _lastRow = rowsCount;
            }
            else
            {
                Console.WriteLine("No new row.");
            }
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    private async Task<IList<IList<object>>> GetRowsAsync()
    {
        try
        {
            var service = await GoogleSheetService.GetSheetsService();
            var request = service.Spreadsheets.Values.Get(_spreadsheetId, _rangeTeachers);
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
        var client = new HttpClient();
        var jsonPayload = new
        {
            message = messageToSend,
            channel_id = _channelIdTechNotifications
        };
        var content = new StringContent(
            Newtonsoft.Json.JsonConvert.SerializeObject(jsonPayload),
            System.Text.Encoding.UTF8,
            "application/json");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _botApiToken);
        
        var response = await client.PostAsync(_postUrl,  content);
        
        if (response.IsSuccessStatusCode)
        {
            var postId = JsonConvert.DeserializeObject<MessageRespose>(await response.Content.ReadAsStringAsync());
            Console.WriteLine($"Message sent to Mattermost successfully. | PostId : {postId}" );
            return postId?.id;
        }
            Console.WriteLine($"Failed to send message to Mattermost. | Repose {await response.Content.ReadAsStringAsync()}");
            return null;
    }
    
   public static async Task<bool> SendUpdateMessage(string postID, string message2)
    {
        var client = new HttpClient();
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

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _botApiToken);
        
        var response = await client.PostAsync(_postUrl,  content);
        
        if (response.IsSuccessStatusCode)
        {
            var postId = JsonConvert.DeserializeObject<MessageRespose>(await response.Content.ReadAsStringAsync());
            Console.WriteLine($"Message sent to Mattermost successfully. | PostId : {postId}" );
            return true;
        }
        Console.WriteLine($"Failed to send message to Mattermost. | Repose {await response.Content.ReadAsStringAsync()}");
        return false;  
    }

    private async Task UpdatePostIdInGoogleSheet(string postId, int rowIndex, bool managerSheet = false)
    {
        try
        {
            
            var rangeTeachers = $"Преподаватели!{GetColumnLetter(_columnsIds["postId"])}{rowIndex + 1}";
            var rangeManagers = $"Менеджеры!{GetColumnLetter(_columnsIds["postId"])}{rowIndex + 1}";
            var service = await GoogleSheetService.GetSheetsService(); 
            var values = new List<IList<object>> { new List<object> { postId } };
            var body = new ValueRange { Values = values };

            var updateRequest = service.Spreadsheets.Values.Update(body, _spreadsheetId, (managerSheet)? rangeManagers : rangeTeachers);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
        
            var updateResponse = await updateRequest.ExecuteAsync();
            Console.WriteLine($"Successfully updated PostId in Google Sheets for row {rowIndex + 1}: {postId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating PostId in Google Sheets: {ex.Message}");
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


    public void RestartTask()
    {
        throw new NotImplementedException();
    }
}