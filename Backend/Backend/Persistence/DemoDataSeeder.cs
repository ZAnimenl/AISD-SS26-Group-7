using Backend.Configuration;
using Backend.Domain;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Persistence;

public sealed class DemoDataSeeder(
    OjSharpDbContext dbContext,
    PasswordHasher passwordHasher,
    IOptions<SeedAdminOptions> seedAdminOptions)
{
    public static readonly Guid StudentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid AdminUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid WebDevAssessmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid ApiEndpointTaskId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid BugFixTaskId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid WebComponentTaskId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        seedAdminOptions.Value.Validate();

        var now = DateTimeOffset.UtcNow;
        await EnsureSeedAdminAsync(now, cancellationToken);

        var existingAssessment = await dbContext.Assessments
            .Include(a => a.Questions)
            .FirstOrDefaultAsync(a => a.Id == WebDevAssessmentId, cancellationToken);

        if (existingAssessment is not null)
        {
            return;
        }

        if (!await dbContext.Users.AnyAsync(user => user.Id == StudentUserId || user.Email == "student@example.com", cancellationToken))
        {
            dbContext.Users.Add(new User
            {
                Id = StudentUserId,
                FullName = "Alice Student",
                Email = "student@example.com",
                PasswordHash = passwordHasher.Hash("password"),
                Role = UserRoles.Student,
                Status = UserStatuses.Active,
                CreatedAt = now
            });
        }

        await SeedAssessmentAsync(now, cancellationToken);
    }

    private async Task EnsureSeedAdminAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var options = seedAdminOptions.Value;
        var admin = await dbContext.Users.FirstOrDefaultAsync(user => user.Email == options.Email, cancellationToken);
        if (admin is null)
        {
            dbContext.Users.Add(new User
            {
                Id = AdminUserId,
                FullName = "Ada Admin",
                Email = options.Email,
                PasswordHash = passwordHasher.Hash(options.Password),
                Role = UserRoles.Administrator,
                Status = UserStatuses.Active,
                CreatedAt = now
            });
            return;
        }

        admin.FullName = string.IsNullOrWhiteSpace(admin.FullName) ? "Ada Admin" : admin.FullName;
        admin.PasswordHash = passwordHasher.Hash(options.Password);
        admin.Role = UserRoles.Administrator;
        admin.Status = UserStatuses.Active;
    }

    private async Task SeedAssessmentAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var assessment = new Assessment
        {
            Id = WebDevAssessmentId,
            Title = "Practical Web Development Assessment",
            Description = "Complete the following real-world development tasks. You may use the embedded AI agent for assistance.",
            DurationMinutes = 60,
            Status = AssessmentStatuses.Active,
            AiEnabled = true,
            CreatedAt = now
        };

        assessment.Questions.Add(CreateApiEndpointTask());
        assessment.Questions.Add(CreateBugFixTask());
        assessment.Questions.Add(CreateWebComponentTask());

        dbContext.Assessments.Add(assessment);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Question CreateApiEndpointTask()
    {
        return new Question
        {
            Id = ApiEndpointTaskId,
            AssessmentId = WebDevAssessmentId,
            Title = "Add a User List API Endpoint",
            TaskType = TaskTypes.ApiDevelopment,
            Difficulty = "medium",
            ProblemDescriptionMarkdown = "## Task: Add a User List API Endpoint\n\nYou are working on an existing web application backend. The app already has a basic server setup.\n\n**Your task:** Implement a `GET /api/users` endpoint that returns a JSON array of user objects.\n\n### Requirements\n\n1. The endpoint must return a JSON array.\n2. Each user object must have: `id` (integer), `name` (string), and `email` (string).\n3. Return at least 3 hardcoded users for now.\n4. The response must have HTTP status 200.\n\n### Example Response\n\n```json\n[\n  { \"id\": 1, \"name\": \"Alice\", \"email\": \"alice@example.com\" },\n  { \"id\": 2, \"name\": \"Bob\", \"email\": \"bob@example.com\" },\n  { \"id\": 3, \"name\": \"Charlie\", \"email\": \"charlie@example.com\" }\n]\n```\n\n### Starter Code\n\nThe server is already set up. You just need to add the route handler.",
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(new[] { "python", "javascript" }),
            StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["python"] = "from flask import Flask, jsonify\n\napp = Flask(__name__)\n\n@app.route('/api/health')\ndef health():\n    return jsonify({'status': 'ok'})\n\n# TODO: Add GET /api/users endpoint below\n\n\nif __name__ == '__main__':\n    app.run(port=5000)\n",
                ["javascript"] = "const express = require('express');\nconst app = express();\n\napp.get('/api/health', (req, res) => {\n  res.json({ status: 'ok' });\n});\n\n// TODO: Add GET /api/users endpoint below\n\n\nmodule.exports = app;\n"
            }),
            SortOrder = 1,
            MaxScore = 40,
            TestCases =
            [
                new TestCase
                {
                    Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                    Name = "returns a JSON array with at least 3 users",
                    Visibility = TestCaseVisibilities.Public,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        "import sys\nsys.modules.pop('solution', None)\nimport solution\n\ndef test_returns_json_array():\n    client = solution.app.test_client()\n    resp = client.get('/api/users')\n    assert resp.status_code == 200\n    data = resp.get_json()\n    assert isinstance(data, list)\n    assert len(data) >= 3\n",
                        "const request = require('supertest');\nconst app = require('./solution.js');\n\ntest('returns a JSON array with at least 3 users', async () => {\n  const res = await request(app).get('/api/users');\n  expect(res.status).toBe(200);\n  expect(Array.isArray(res.body)).toBe(true);\n  expect(res.body.length).toBeGreaterThanOrEqual(3);\n});\n"))
                },
                new TestCase
                {
                    Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                    Name = "each user has id, name, email",
                    Visibility = TestCaseVisibilities.Hidden,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        "import sys\nsys.modules.pop('solution', None)\nimport solution\n\ndef test_user_fields():\n    client = solution.app.test_client()\n    resp = client.get('/api/users')\n    data = resp.get_json()\n    for user in data:\n        assert 'id' in user\n        assert 'name' in user\n        assert 'email' in user\n",
                        "const request = require('supertest');\nconst app = require('./solution.js');\n\ntest('each user has id, name, email', async () => {\n  const res = await request(app).get('/api/users');\n  for (const user of res.body) {\n    expect(user).toHaveProperty('id');\n    expect(user).toHaveProperty('name');\n    expect(user).toHaveProperty('email');\n  }\n});\n"))
                }
            ]
        };
    }

    private Question CreateBugFixTask()
    {
        return new Question
        {
            Id = BugFixTaskId,
            AssessmentId = WebDevAssessmentId,
            Title = "Fix the Shopping Cart Total",
            TaskType = TaskTypes.BugFix,
            Difficulty = "easy",
            ProblemDescriptionMarkdown = "## Task: Fix the Shopping Cart Total\n\nA teammate wrote a shopping cart module, but the `calculate_total` function has bugs. Customers are reporting incorrect totals.\n\n**Your task:** Find and fix the bugs so the function correctly calculates the total price.\n\n### Requirements\n\n1. The function receives a list of cart items. Each item has `name`, `price`, and `quantity`.\n2. The total should be the sum of `price * quantity` for each item.\n3. Items with `quantity <= 0` should be skipped (not counted).\n4. If the cart is empty, return `0`.\n\n### Example\n\n```\ncart = [\n  { \"name\": \"Laptop\", \"price\": 999.99, \"quantity\": 1 },\n  { \"name\": \"Mouse\",  \"price\": 29.99,  \"quantity\": 2 },\n  { \"name\": \"Returned\", \"price\": 49.99, \"quantity\": 0 }\n]\n# Expected total: 999.99 + 59.98 = 1059.97\n```\n\n### Starter Code\n\nThe buggy code is provided below. Fix it without rewriting from scratch.",
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(new[] { "python", "javascript" }),
            StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["python"] = "def calculate_total(cart):\n    total = 0\n    for item in cart:\n        # BUG: something is wrong with this calculation\n        total += item['price'] + item['quantity']\n    return total\n",
                ["javascript"] = "function calculateTotal(cart) {\n  let total = 0;\n  for (const item of cart) {\n    // BUG: something is wrong with this calculation\n    total += item.price + item.quantity;\n  }\n  return total;\n}\n\nmodule.exports = { calculateTotal };\n"
            }),
            SortOrder = 2,
            MaxScore = 30,
            TestCases =
            [
                new TestCase
                {
                    Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
                    Name = "basic cart total",
                    Visibility = TestCaseVisibilities.Public,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        "from solution import calculate_total\n\ndef test_basic_total():\n    cart = [\n        {'name': 'Laptop', 'price': 999.99, 'quantity': 1},\n        {'name': 'Mouse', 'price': 29.99, 'quantity': 2},\n    ]\n    assert abs(calculate_total(cart) - 1059.97) < 0.01\n",
                        "const { calculateTotal } = require('./solution.js');\n\ntest('basic cart total', () => {\n  const cart = [\n    { name: 'Laptop', price: 999.99, quantity: 1 },\n    { name: 'Mouse', price: 29.99, quantity: 2 },\n  ];\n  expect(calculateTotal(cart)).toBeCloseTo(1059.97);\n});\n"))
                },
                new TestCase
                {
                    Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),
                    Name = "skips zero quantity items",
                    Visibility = TestCaseVisibilities.Hidden,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        "from solution import calculate_total\n\ndef test_skip_zero_quantity():\n    cart = [\n        {'name': 'Book', 'price': 15.00, 'quantity': 3},\n        {'name': 'Returned', 'price': 49.99, 'quantity': 0},\n    ]\n    assert abs(calculate_total(cart) - 45.00) < 0.01\n",
                        "const { calculateTotal } = require('./solution.js');\n\ntest('skips zero quantity items', () => {\n  const cart = [\n    { name: 'Book', price: 15.00, quantity: 3 },\n    { name: 'Returned', price: 49.99, quantity: 0 },\n  ];\n  expect(calculateTotal(cart)).toBeCloseTo(45.00);\n});\n"))
                },
                new TestCase
                {
                    Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                    Name = "empty cart returns zero",
                    Visibility = TestCaseVisibilities.Hidden,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        "from solution import calculate_total\n\ndef test_empty_cart():\n    assert calculate_total([]) == 0\n",
                        "const { calculateTotal } = require('./solution.js');\n\ntest('empty cart returns zero', () => {\n  expect(calculateTotal([])).toBe(0);\n});\n"))
                }
            ]
        };
    }

    private Question CreateWebComponentTask()
    {
        return new Question
        {
            Id = WebComponentTaskId,
            AssessmentId = WebDevAssessmentId,
            Title = "Build a Todo List Module",
            TaskType = TaskTypes.WebApplication,
            Difficulty = "medium",
            ProblemDescriptionMarkdown = "## Task: Build a Todo List Module\n\nYou are building a backend module for a todo list application. The data layer is already planned; you need to implement the core logic.\n\n**Your task:** Implement a `TodoList` class with the following methods:\n\n### Requirements\n\n1. `add(text)` - Add a new todo item. Each item gets a unique auto-incrementing `id` starting from 1. New items start with `completed = False`.\n2. `complete(id)` - Mark a todo item as completed. If the id does not exist, do nothing.\n3. `remove(id)` - Remove a todo item by id. If the id does not exist, do nothing.\n4. `get_pending()` - Return a list of all incomplete todo items, each as `{ id, text, completed }`.\n5. `get_all()` - Return a list of all todo items.\n\n### Example\n\n```python\ntodos = TodoList()\ntodos.add('Buy groceries')\ntodos.add('Write report')\ntodos.complete(1)\nprint(todos.get_pending())  # [{ id: 2, text: 'Write report', completed: False }]\nprint(todos.get_all())      # [{ id: 1, ... completed: True }, { id: 2, ... completed: False }]\n```",
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(new[] { "python", "javascript" }),
            StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["python"] = "class TodoList:\n    def __init__(self):\n        self.items = []\n        self.next_id = 1\n\n    def add(self, text):\n        # TODO: implement\n        pass\n\n    def complete(self, item_id):\n        # TODO: implement\n        pass\n\n    def remove(self, item_id):\n        # TODO: implement\n        pass\n\n    def get_pending(self):\n        # TODO: implement\n        pass\n\n    def get_all(self):\n        # TODO: implement\n        pass\n",
                ["javascript"] = "class TodoList {\n  constructor() {\n    this.items = [];\n    this.nextId = 1;\n  }\n\n  add(text) {\n    // TODO: implement\n  }\n\n  complete(id) {\n    // TODO: implement\n  }\n\n  remove(id) {\n    // TODO: implement\n  }\n\n  getPending() {\n    // TODO: implement\n  }\n\n  getAll() {\n    // TODO: implement\n  }\n}\n\nmodule.exports = { TodoList };\n"
            }),
            SortOrder = 3,
            MaxScore = 30,
            TestCases =
            [
                new TestCase
                {
                    Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    Name = "add and get all",
                    Visibility = TestCaseVisibilities.Public,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        "from solution import TodoList\n\ndef test_add_and_get_all():\n    todos = TodoList()\n    todos.add('Task A')\n    todos.add('Task B')\n    items = todos.get_all()\n    assert len(items) == 2\n    assert items[0]['id'] == 1\n    assert items[0]['text'] == 'Task A'\n    assert items[1]['id'] == 2\n",
                        "const { TodoList } = require('./solution.js');\n\ntest('add and get all', () => {\n  const todos = new TodoList();\n  todos.add('Task A');\n  todos.add('Task B');\n  const items = todos.getAll();\n  expect(items.length).toBe(2);\n  expect(items[0].id).toBe(1);\n  expect(items[0].text).toBe('Task A');\n});\n"))
                },
                new TestCase
                {
                    Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                    Name = "complete and get pending",
                    Visibility = TestCaseVisibilities.Hidden,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        "from solution import TodoList\n\ndef test_complete_and_pending():\n    todos = TodoList()\n    todos.add('Task A')\n    todos.add('Task B')\n    todos.complete(1)\n    pending = todos.get_pending()\n    assert len(pending) == 1\n    assert pending[0]['text'] == 'Task B'\n    assert pending[0]['completed'] == False\n",
                        "const { TodoList } = require('./solution.js');\n\ntest('complete and get pending', () => {\n  const todos = new TodoList();\n  todos.add('Task A');\n  todos.add('Task B');\n  todos.complete(1);\n  const pending = todos.getPending();\n  expect(pending.length).toBe(1);\n  expect(pending[0].text).toBe('Task B');\n  expect(pending[0].completed).toBe(false);\n});\n"))
                },
                new TestCase
                {
                    Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                    Name = "remove item",
                    Visibility = TestCaseVisibilities.Hidden,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        "from solution import TodoList\n\ndef test_remove():\n    todos = TodoList()\n    todos.add('Task A')\n    todos.add('Task B')\n    todos.remove(1)\n    items = todos.get_all()\n    assert len(items) == 1\n    assert items[0]['text'] == 'Task B'\n",
                        "const { TodoList } = require('./solution.js');\n\ntest('remove item', () => {\n  const todos = new TodoList();\n  todos.add('Task A');\n  todos.add('Task B');\n  todos.remove(1);\n  const items = todos.getAll();\n  expect(items.length).toBe(1);\n  expect(items[0].text).toBe('Task B');\n});\n"))
                }
            ]
        };
    }

    private static Dictionary<string, string> TestCode(string python, string javascript)
    {
        return new Dictionary<string, string>
        {
            ["python"] = python,
            ["javascript"] = javascript
        };
    }
}
