using System.Collections.ObjectModel;

namespace ForkChatDemo.Models;

public enum ChatRole
{
    User,
    Assistant,
    System
}

public class ChatNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public ObservableCollection<ChatNode> Children { get; } = new();

    // 用于UI判断
    public bool HasSiblings => Parent?.Children.Count > 1;
    public int SiblingIndex => Parent?.Children.IndexOf(this) ?? 0;
    public bool IsLastChild => Parent == null || Parent.Children.LastOrDefault() == this;
    
    // 运行时引用（不序列化）
    public ChatNode? Parent { get; set; }

    public ChatNode() { }

    public ChatNode(ChatRole role, string content)
    {
        Role = role;
        Content = content;
    }

    public ChatNode AddChild(ChatRole role, string content)
    {
        var child = new ChatNode(role, content) { Parent = this };
        Children.Add(child);
        return child;
    }

    // 获取显示文本（截断）
    public string DisplayText => Content.Length > 80 
        ? Content[..80].Replace("\n", " ") + "…" 
        : Content.Replace("\n", " ");
        
    // 获取角色标签
    public string RoleLabel => Role switch
    {
        ChatRole.User => "👤 User",
        ChatRole.Assistant => "🤖 Assistant",
        ChatRole.System => "⚙️ System",
        _ => "Unknown"
    };
}