using KLCN060.Api.Services.Chatbot;

namespace KLCN060.Api.Services;

public interface IChatbotService
{
    Task<ChatbotAnswer> AskAsync(string cauHoi);
}
