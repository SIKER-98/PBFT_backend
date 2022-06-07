using System.Text;
using System.Text.Json.Serialization;

namespace PBFTv2.Models;

public class Node
{
    [JsonConstructor]
    public Node(string address, int port)
    {
        Address = address;
        Port = port;
    }

    public Node(string address)
    {
        var separator = new[] { '/', ':' };
        var splited = address.Split(separator);

        Address = splited[3];
        Port = int.Parse(splited[4]);
    }

    [JsonPropertyName("address")]
    [JsonInclude]
    public string Address { get; }

    [JsonPropertyName("port")]
    [JsonInclude]
    public int Port { get; }

    public string GetPath()
    {
        var path = new StringBuilder();
        path.Append("https://");
        path.Append($"{Address}:{Port}");

        return path.ToString();
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is not Node node)
            return false;

        return node.Address == Address && node.Port == Port;
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(Address, Port);
    }
}