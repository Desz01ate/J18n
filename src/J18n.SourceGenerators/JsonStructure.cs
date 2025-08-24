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

public class JsonArrayNode : JsonNode
{
    public List<JsonNode> Items { get; } = new List<JsonNode>();
    public string KeyPath { get; }
    
    public JsonArrayNode(string name, string keyPath) : base(name)
    {
        KeyPath = keyPath;
    }
    
    public void AddItem(JsonNode item)
    {
        Items.Add(item);
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
                else if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    var arrayNode = new JsonArrayNode(property.Name, propertyPath);
                    ParseJsonArrayRecursive(property.Value, arrayNode, propertyPath);
                    parent.AddChild(arrayNode);
                }
                else if (property.Value.ValueKind == JsonValueKind.String)
                {
                    var valueNode = new JsonValueNode(property.Name, propertyPath);
                    parent.AddChild(valueNode);
                }
            }
        }
    }
    
    private static void ParseJsonArrayRecursive(JsonElement arrayElement, JsonArrayNode arrayNode, string arrayPath)
    {
        var arrayEnumerator = arrayElement.EnumerateArray();
        var index = 0;
        
        foreach (var item in arrayEnumerator)
        {
            var itemPath = $"{arrayPath}[{index}]";
            
            if (item.ValueKind == JsonValueKind.Object)
            {
                var objectNode = new JsonObjectNode($"[{index}]");
                ParseJsonElementRecursive(item, objectNode, itemPath);
                arrayNode.AddItem(objectNode);
            }
            else if (item.ValueKind == JsonValueKind.Array)
            {
                var nestedArrayNode = new JsonArrayNode($"[{index}]", itemPath);
                ParseJsonArrayRecursive(item, nestedArrayNode, itemPath);
                arrayNode.AddItem(nestedArrayNode);
            }
            else if (item.ValueKind == JsonValueKind.String)
            {
                var valueNode = new JsonValueNode($"[{index}]", itemPath);
                arrayNode.AddItem(valueNode);
            }
            
            index++;
        }
    }
}