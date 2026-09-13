namespace KLCN060.Api.Services;

public interface IChatbotService
{
    Task<string> AskAsync(string cauHoi);
}
