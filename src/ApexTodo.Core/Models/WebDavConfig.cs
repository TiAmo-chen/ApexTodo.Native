namespace ApexTodo.Core.Models;

public class WebDavConfig
{
    public bool Enabled { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string RemotePath { get; set; } = "/ApexTodo/todo.db";
    public int IntervalMinutes { get; set; } = 60;
}
