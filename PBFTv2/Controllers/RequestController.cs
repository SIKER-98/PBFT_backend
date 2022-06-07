using System.Transactions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Abstractions;
using PBFTv2.Models;

namespace PBFTv2.Controllers;

[ApiController]
[Route("[controller]")]
public class RequestController : Controller
{
    #region Variables

    // log maker 
    private readonly LogMaker _logMaker;

    // lista wezlow
    private static List<Node> _nodes = new List<Node>
    {
        new Node("localhost", 7701),
        new Node("localhost", 7702),
        new Node("localhost", 7703),
        new Node("localhost", 7704),
    };

    // message history
    private static Dictionary<string, List<Message>> _messageHistory = new Dictionary<string, List<Message>>();

    private static Dictionary<string, int> requestCounter = new Dictionary<string, int>();

    // obiekt synchronizacji
    private static readonly object LockHistory = new();
    private static readonly object LockNode = new();

    // informacje o wezle
    private static Node _thisNode;

    private static String Status = "normal";

    public RequestController(ILogger<RequestController> logger)
    {
        _logMaker = new LogMaker(logger);
    }

    #endregion

    [HttpPost("/status", Name = "Change Status")]
    public async Task<IActionResult> PostStatus(String status)
    {
        Status = status;

        return Ok();
    }

    #region SendingRequests

    /// <summary>
    /// Odebranie REQUEST-a i rozeslanie PREPREPARE
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
    [HttpPost("/request", Name = "Get Request")]
    public async Task<IActionResult> GetRequest(Node replyNode, string content)
    {
        if (_thisNode is null)
        {
            _thisNode = new Node(HttpContext.Request.GetDisplayUrl());
        }

        _logMaker.GetRequest(content);

        if (Status == "disabled")
            return Ok();

        // utworzenie wiadomosci
        var message = new Message(content);
        message.Client = replyNode;

        AddMessageToHistory(message);

        using var httpClient = new HttpClient();

        foreach (var node in _nodes)
        {
            if (node.Address == _thisNode.Address && node.Port * 1 == _thisNode.Port * 1)
                continue;

            var prePrepareMessage = message.Clone();
            prePrepareMessage.SetSource(_thisNode);
            prePrepareMessage.SetDestination(node);
            prePrepareMessage.SetType(1);

            // prePrepareMessage.Client = new Node("localhost", 7700);

            var jsonContent = JsonContent.Create(prePrepareMessage);

            _logMaker.SendMessage(prePrepareMessage);

            try
            {
                AddMessageToHistory(prePrepareMessage);
                await httpClient.PostAsync(node.GetPath() + "/preprepare", jsonContent);
            }
            catch (Exception e)
            {
                _logMaker.CustomMessage(prePrepareMessage, "FAILED");
            }
        }

        return Ok();
    }

    /// <summary>
    /// Odebranie PREPREPARE i rozeslanie PREPARE
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    [HttpPost("/preprepare", Name = "PrePrepare Post")]
    public async Task<IActionResult> PrePreparePost(Message message)
    {
        if (_thisNode is null)
        {
            _thisNode = new Node(HttpContext.Request.GetDisplayUrl());
        }
        
        if (!requestCounter.ContainsKey(message.GetId()))
            requestCounter.Add(message.GetId(), 0);

        requestCounter[message.Id]++;

        _logMaker.ReceiveMessage(message);
        AddMessageToHistory(message);

        if (Status == "disabled")
            return Ok();


        using var httpClient = new HttpClient();

        foreach (var node in _nodes)
        {
            if (node.Address == _thisNode.Address && node.Port * 1 == _thisNode.Port * 1)
                continue;

            var prepareMessage = message.Clone();
            prepareMessage.SetSource(_thisNode);
            prepareMessage.SetDestination(node);
            prepareMessage.SetType(2);

            var jsonContent = JsonContent.Create(prepareMessage);

            _logMaker.SendMessage(prepareMessage);

            try
            {
                AddMessageToHistory(prepareMessage);
                await httpClient.PostAsync(node.GetPath() + "/prepare", jsonContent);
            }
            catch (Exception e)
            {
                _logMaker.CustomMessage(prepareMessage, "FAILED");
            }
        }

        return Ok();
    }

    /// <summary>
    /// Otrzymanie PREPARE i jezeli wyslanie COMMIT jezeli odpowiednia ilosc
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    [HttpPost("/prepare", Name = "Prepare Post")]
    public async Task<IActionResult> PreparePost(Message message)
    {
        if (_thisNode is null)
        {
            _thisNode = new Node(HttpContext.Request.GetDisplayUrl());
        }

        if (!requestCounter.ContainsKey(message.GetId()))
            requestCounter.Add(message.GetId(), 0);

        requestCounter[message.Id]++;
        
        _logMaker.ReceiveMessage(message);
        Console.WriteLine(requestCounter[message.Id]);
        if (!(AddMessageToHistoryAndCount(message, 2) != 2 || requestCounter[message.Id] != 2))
        {
            return Accepted();
        }

        if (Status == "disabled")
            return Ok();

        using var httpClient = new HttpClient();

        foreach (var node in _nodes)
        {
            if (node.Address == _thisNode.Address && node.Port * 1 == _thisNode.Port * 1)
                continue;

            var commitMessage = message.Clone();
            commitMessage.SetSource(_thisNode);
            commitMessage.SetDestination(node);
            commitMessage.SetType(3);

            var jsonContent = JsonContent.Create(commitMessage);

            _logMaker.SendMessage(commitMessage);

            try
            {
                AddMessageToHistory(commitMessage);
                await httpClient.PostAsync(node.GetPath() + "/commit", jsonContent);
            }
            catch (Exception e)
            {
                _logMaker.CustomMessage(commitMessage, "FAILED");
            }
        }

        return Ok();
    }

    [HttpPost("/commit", Name = "Commit Post")]
    public async Task<IActionResult> CommitPost(Message message)
    {
        _logMaker.ReceiveMessage(message);
        if (AddMessageToHistoryAndCount(message, 3) != 2)
            return Accepted();

        if (Status == "disabled")
            return Ok();

        using var httpClient = new HttpClient();

        var replyMessage = message.Clone();
        replyMessage.SetSource(_thisNode);
        replyMessage.SetDestination(message.GetClient());
        replyMessage.SetType(4);

        var jsonContent = JsonContent.Create(replyMessage);

        _logMaker.SendMessage(replyMessage);

        try
        {
            AddMessageToHistory(replyMessage);
            await httpClient.PostAsync(message.GetClient().GetPath() + "/reply", jsonContent);
        }
        catch (Exception e)
        {
            _logMaker.CustomMessage(replyMessage, "FAILED");
        }

        return Ok();
    }

    [HttpPost("/reply", Name = "Reply Post")]
    public Task<IActionResult> ReplyPost(Message message)
    {
        _logMaker.ReceiveMessage(message);
        lock (LockHistory)
        {
            AddMessageToHistory(message);
        }

        if (Status == "disabled")
            return Task.FromResult<IActionResult>(Ok());

        return Task.FromResult<IActionResult>(Ok());
    }

    #endregion

    #region History

    private void AddMessageToHistory(Message message)
    {
        lock (LockHistory)
        {
            if (!_messageHistory.ContainsKey(message.GetId()))
                _messageHistory.Add(message.GetId(), new List<Message>());

            message.Time = DateTime.Now.ToString("HH:mm:ss-ffff");
            _messageHistory[message.GetId()].Add(message);
        }
    }

    private int AddMessageToHistoryAndCount(Message message, int type)
    {
        List<Message> filtered;
        lock (LockHistory)
        {
            if (!_messageHistory.ContainsKey(message.GetId()))
                _messageHistory.Add(message.GetId(), new List<Message>());

            message.Time = DateTime.Now.ToString("HH:mm:ss-ffff");
            _messageHistory[message.GetId()].Add(message);


            filtered = _messageHistory[message.GetId()]
                .Where(m => m.FilterTypes(type, _thisNode)).ToList();
        }

        return filtered.Count;
    }

    [HttpGet("/history", Name = "Get History")]
    public Task<IActionResult> GetMessageHistory()
    {
        IDictionary<string, List<Message>> content;
        lock (LockHistory)
        {
            try
            {
                content = _messageHistory;
            }
            catch (Exception e)
            {
                content = new Dictionary<string, List<Message>>();
            }
        }

        return Task.FromResult<IActionResult>(Ok(content));
    }

    [HttpGet("/history/key", Name = "Get History Key")]
    public Task<IActionResult> GetMessageHistoryKey()
    {
        IList<string> keys;
        lock (LockHistory)
        {
            try
            {
                keys = _messageHistory.Keys.ToList();
            }
            catch (Exception e)
            {
                keys = new List<string>();
            }
        }

        return Task.FromResult<IActionResult>(Ok(keys));
    }

    [HttpGet("/history/records", Name = "Get Key History")]
    public Task<IActionResult> GetMessageKeyHistory(string key)
    {
        IList<Message> messages;
        lock (LockHistory)
        {
            try
            {
                messages = _messageHistory[key];
            }
            catch (Exception e)
            {
                messages = new List<Message>();
            }
        }

        return Task.FromResult<IActionResult>(Ok(messages));
    }

    #endregion
}