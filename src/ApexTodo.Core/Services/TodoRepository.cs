using ApexTodo.Core.Data;
using ApexTodo.Core.Models;
using Dapper;

namespace ApexTodo.Core.Services;

public class TodoRepository
{
    private readonly DatabaseContext _db;

    public TodoRepository(DatabaseContext db)
    {
        _db = db;
    }

    public List<TodoItem> GetAll()
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        return conn.Query("""
            SELECT id AS Id, text AS Text, completed AS Completed,
                   created_at AS CreatedAt, completed_at AS CompletedAt,
                   sort_order AS SortOrder
            FROM todos
            ORDER BY completed ASC, sort_order ASC, created_at DESC
            """).Select(r => new TodoItem
        {
            Id = (string)r.Id,
            Text = (string)r.Text,
            Completed = (long)r.Completed == 1,
            CreatedAt = DateTime.Parse((string)r.CreatedAt),
            CompletedAt = r.CompletedAt != null ? DateTime.Parse((string)r.CompletedAt) : null,
            SortOrder = (int)(long)r.SortOrder
        }).ToList();
    }

    public TodoItem Add(string text)
    {
        var item = new TodoItem
        {
            Id = Guid.NewGuid().ToString(),
            Text = text,
            CreatedAt = DateTime.Now,
            SortOrder = GetNextSortOrder()
        };

        using var conn = _db.CreateConnection();
        conn.Open();
        conn.Execute("""
            INSERT INTO todos (id, text, completed, created_at, sort_order)
            VALUES (@Id, @Text, 0, @CreatedAt, @SortOrder)
            """, item);

        return item;
    }

    public void Toggle(string id)
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        conn.Execute("""
            UPDATE todos
            SET completed = CASE WHEN completed = 1 THEN 0 ELSE 1 END,
                completed_at = CASE WHEN completed = 1 THEN NULL ELSE @Now END
            WHERE id = @Id
            """, new { Id = id, Now = DateTime.Now.ToString("o") });
    }

    public void Delete(string id)
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        conn.Execute("DELETE FROM todos WHERE id = @Id", new { Id = id });
    }

    public void UpdateText(string id, string text)
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        conn.Execute("UPDATE todos SET text = @Text WHERE id = @Id", new { Id = id, Text = text });
    }

    public void Reorder(List<string> orderedIds)
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        using var transaction = conn.BeginTransaction();
        for (int i = 0; i < orderedIds.Count; i++)
        {
            conn.Execute("UPDATE todos SET sort_order = @Order WHERE id = @Id",
                new { Order = i, Id = orderedIds[i] }, transaction);
        }
        transaction.Commit();
    }

    private int GetNextSortOrder()
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        var max = conn.ExecuteScalar<long?>("SELECT MAX(sort_order) FROM todos WHERE completed = 0");
        return (int)(max ?? 0) + 1;
    }
}
