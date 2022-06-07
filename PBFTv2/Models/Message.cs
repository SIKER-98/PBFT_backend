using System.Collections.Specialized;
using System.Text.Json.Serialization;

namespace PBFTv2.Models;

public class Message
{
    [JsonPropertyName("id")] 
    [JsonInclude] 
    public string Id;

    [JsonPropertyName("content")] 
    [JsonInclude]
    public string Content;

    [JsonPropertyName("type")]
    [JsonInclude]
    public int Type;

    [JsonPropertyName("source")] 
    [JsonInclude]
    public Node Source;

    [JsonPropertyName("destination")] 
    [JsonInclude]
    public Node Destination;

    [JsonPropertyName("client")] 
    [JsonInclude]
    public Node Client;

    [JsonPropertyName("time")] 
    [JsonInclude]
    public string Time;

    [JsonConstructor]
    public Message(string id, string content, int type, Node source, Node destination, Node client)
    {
        Id = id;
        Content = content;
        Type = type;
        Source = source;
        Destination = destination;
        Client = client;
    }

    public Message(string content)
    {
        Id = Guid.NewGuid().ToString();
        Content = content;
        Type = 0;
        Source = null!;
        Destination = null!;
        Client = null!;
    }

    public string GetId() => Id;
    public Node GetSource() => Source;
    public void SetSource(Node source) => Source = source;
    public Node GetDestination() => Destination;
    public void SetDestination(Node destination) => Destination = destination;
    public int GetMessageType() => Type;
    public void SetType(int type) => Type = type;
    public Node GetClient() => Client;

    public Message Clone()
    {
        return new Message(Id, Content, Type, Source, Destination, Client);
    }

    public bool FilterTypes(int type, Node destination)
    {
        return Type == type && Destination.Address == destination.Address && Destination.Port==destination.Port;
    }
}

public enum MessageType
{
    Request=0 ,
    PrePrepare=1 ,
    Prepare=2 ,
    Commit=3 ,
    Reply=4 
}