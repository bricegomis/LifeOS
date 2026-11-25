using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LifeOS.Api.IntegrationTests;

/// <summary>Roadmap step 3: todos, household routines and projects are usable from a Claude conversation, for its household only.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AssistantTaskToolsTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task The_assistant_manages_todos_routines_and_projects_of_its_household_only()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        await using var mcp = await Connect(factory, Guid.NewGuid());
        await using var neighbour = await Connect(factory, Guid.NewGuid());

        var tools = (await mcp.ListToolsAsync()).Select(tool => tool.Name).ToList();
        Assert.Contains("create_todo", tools);
        Assert.Contains("create_task_routine", tools);
        Assert.Contains("close_project", tools);
        Assert.Contains("create_routine", tools); // the sport routine keeps its own tools

        var project = (await Call(mcp, "create_project", new() { ["title"] = "Chambre de Léa", ["notes"] = "Avant Noël." })).GetProperty("project");
        var projectId = project.GetProperty("id").GetString();
        var todo = await Call(mcp, "create_todo", new()
        {
            ["title"] = "Acheter la peinture", ["priority"] = "high", ["tags"] = new[] { "maison" }, ["projectId"] = projectId,
            ["checklist"] = new[] { "Blanc cassé", "Rouleau" },
        });
        var todoId = todo.GetProperty("id").GetString();
        Assert.Equal(2, todo.GetProperty("checklist").GetArrayLength());
        Assert.Equal("Chambre de Léa", todo.GetProperty("projectTitle").GetString());
        Assert.Single((await Call(mcp, "search_todos", new() { ["query"] = "PEINTURE lea" })).EnumerateArray());
        Assert.Equal(["maison"], (await Call(mcp, "get_task_tags", new())).EnumerateArray().Select(t => t.GetString()));

        var itemId = todo.GetProperty("checklist")[0].GetProperty("id").GetString();
        todo = await Call(mcp, "update_checklist_item", new() { ["todoId"] = todoId, ["itemId"] = itemId, ["label"] = "Blanc cassé 2,5 L", ["done"] = true });
        Assert.True(todo.GetProperty("checklist")[0].GetProperty("done").GetBoolean());
        Assert.Equal("done", (await Call(mcp, "complete_todo", new() { ["todoId"] = todoId })).GetProperty("state").GetString());
        Assert.Equal(1, (await Call(mcp, "get_project", new() { ["projectId"] = projectId })).GetProperty("project").GetProperty("done").GetInt32());
        Assert.Equal("done", (await Call(mcp, "close_project", new() { ["projectId"] = projectId, ["action"] = "finish" })).GetProperty("project").GetProperty("state").GetString());

        var routine = await Call(mcp, "create_task_routine", new()
        {
            ["title"] = "Changer les draps", ["recurrence"] = new Dictionary<string, object?> { ["kind"] = "delay", ["delayDays"] = 7 }, ["tags"] = new[] { "maison" },
        });
        var routineId = routine.GetProperty("routine").GetProperty("id").GetString();
        routine = await Call(mcp, "complete_task_routine", new() { ["routineId"] = routineId, ["note"] = "Draps d'hiver" });
        Assert.Equal(1, routine.GetProperty("routine").GetProperty("streak").GetInt32());
        routine = await Call(mcp, "skip_task_routine", new() { ["routineId"] = routineId });
        Assert.Equal(0, routine.GetProperty("routine").GetProperty("streak").GetInt32());
        Assert.Equal("paused", (await Call(mcp, "pause_task_routine", new() { ["routineId"] = routineId })).GetProperty("routine").GetProperty("state").GetString());
        Assert.Contains("week", await Error(mcp, "create_task_routine", new()
        {
            ["title"] = "Poubelles", ["recurrence"] = new Dictionary<string, object?> { ["kind"] = "fixed", ["unit"] = "fortnight" },
        }));
        Assert.Single((await Call(mcp, "get_tasks_today", new())).GetProperty("todos").EnumerateArray());

        Assert.Contains("introuvable", await Error(neighbour, "complete_todo", new() { ["todoId"] = todoId }));
        Assert.Contains("introuvable", await Error(neighbour, "get_task_routine", new() { ["routineId"] = routineId }));
        Assert.Contains("introuvable", await Error(neighbour, "close_project", new() { ["projectId"] = projectId, ["action"] = "reopen" }));
        Assert.Empty((await Call(neighbour, "search_todos", new() { ["state"] = "all" })).EnumerateArray());
        Assert.Empty((await Call(neighbour, "list_task_routines", new() { ["state"] = "all" })).EnumerateArray());
        Assert.Empty((await Call(neighbour, "list_projects", new() { ["state"] = "all" })).EnumerateArray());
    }

    private static async Task<McpClient> Connect(WebApplicationFactory<Program> factory, Guid user)
    {
        return await factory.CreateClient().AsUser(user).ConnectMcpAsync();
    }

    private static async Task<JsonElement> Call(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, Json(arguments));
        var text = Text(result);
        Assert.True(result.IsError != true, $"{tool} failed: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task<string> Error(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, Json(arguments));
        Assert.True(result.IsError, $"{tool} should have failed: {Text(result)}");
        return Text(result);
    }

    private static Dictionary<string, object?> Json(Dictionary<string, object?> arguments) =>
        arguments.ToDictionary(pair => pair.Key, pair => (object?)JsonSerializer.SerializeToElement(pair.Value));

    private static string Text(CallToolResult result) =>
        string.Join("", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
