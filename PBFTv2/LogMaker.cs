using System.Text;
using Microsoft.AspNetCore.Authentication;
using PBFTv2.Controllers;
using PBFTv2.Models;

namespace PBFTv2;

public class LogMaker
{
    private readonly ILogger _logger;

    public LogMaker(ILogger logger)
    {
        _logger = logger;
    }
    
    private string GetData()
    {
        return DateTime.Now.ToString("HH:mm:ss-ffff");
    }

    public void GetRequest(string content)
    {
        _logger.LogWarning("{} - {}", GetData(), content);
    }

    private string GetType(int type)
    {
        switch (type)
        {
            case 0:
                return "Request";
            case 1:
                return "PrePrepare";
            case 2:
                return "Prepare";
            case 3:
                return "Commit";
            case 4:
                return "Reply";
            default:
                return "ERROR";
        }
    }

    public void SendMessage(Message message)
    {
        var text = $"Send {GetType(message.GetMessageType())} to {message.GetDestination().GetPath()}";

        _logger.LogInformation("{} - {}", GetData(), text);
    }

    public void ReceiveMessage(Message message)
    {
        var text = $"Received {GetType(message.GetMessageType())} from {message.GetSource().GetPath()}";
        _logger.LogWarning("{} - {}", GetData(), text);
    }

    public void CustomMessage(Message message, string custom)
    {
        var text = $"CUSTOM {GetType(message.GetMessageType())} from {message.GetSource().GetPath()} - {custom}";
        _logger.LogWarning("{} - {}", GetData(), text);
    }
}