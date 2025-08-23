using System.Collections.Generic;
using System.Text.Json;

namespace J18n.SourceGenerators;

public abstract class JsonNode
{
    public string Name { get; }
    
    protected JsonNode(string name)
    {
        Name = name;
    }
}

public class JsonValueNode : JsonNode
{
    public string KeyPath { get; }
    
    public JsonValueNode(string name, string keyPath) : base(name)
    {
        KeyPath = keyPath;
    }
}

public class JsonObjectNode : JsonNode
{
    public List<JsonNode> Children { get; } = new List<JsonNode>();
    
    public JsonObjectNode(string name) : base(name)
    {
    }
    
    public void AddChild(JsonNode child)
    {
        Children.Add(child);
    }
}

public static class JsonStructureParser
{
    public static JsonObjectNode ParseJsonElement(JsonElement element, string rootName = "")
    {
        var root = new JsonObjectNode(rootName);
        ParseJsonElementRecursive(element, root, "");
        return root;
    }
    
    private static void ParseJsonElementRecursive(JsonElement element, JsonObjectNode parent, string currentPath)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var propertyPath = string.IsNullOrEmpty(currentPath) ? property.Name : $"{currentPath}.{property.Name}";
                
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    var objectNode = new JsonObjectNode(property.Name);
                    ParseJsonElementRecursive(property.Value, objectNode, propertyPath);
                    parent.AddChild(objectNode);
                }
                else if (property.Value.ValueKind == JsonValueKind.String)
                {
                    var valueNode = new JsonValueNode(property.Name, propertyPath);
                    parent.AddChild(valueNode);
                }
            }
        }
    }
}