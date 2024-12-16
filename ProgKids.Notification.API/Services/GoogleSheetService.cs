using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;

namespace ProgKidsNotifier.Services;

public class GoogleSheetService
{
    private static SheetsService _sheetsService;
    
    public static async Task<SheetsService> GetSheetsService()
    {
        try
        {
            if (_sheetsService == null)
            {
                var credential = GoogleCredential.FromFile("key.json")
                    .CreateScoped(SheetsService.Scope.Spreadsheets);

                _sheetsService = new SheetsService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "ProgKids notifier service",
                });
            }

            return _sheetsService;
        }
        catch (Exception e)
        {
            Console.WriteLine($"{DateTime.Now.ToLongDateString()} | {DateTime.Now.ToLongTimeString()} |  Problem with getting Google Sheets service: {e.Message}");
            throw;
        }
    } 
}