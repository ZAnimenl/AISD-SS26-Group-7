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
    public static readonly Guid ApiTaskId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid BugFixTaskId = Guid.Parse("55555555-5555-5555-5555-555555555555");

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

        assessment.Questions.Add(CreateApiTask());
        assessment.Questions.Add(CreateBugFixTask());

        dbContext.Assessments.Add(assessment);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Question CreateApiTask()
    {
        return new Question
        {
            Id = ApiTaskId,
            AssessmentId = WebDevAssessmentId,
            Title = "Build a User Management API",
            TaskType = TaskTypes.ApiDevelopment,
            Difficulty = "medium",
            ProblemDescriptionMarkdown = """
## Task: Build a User Management API

You are working on an existing web application backend. The project already has a Flask/Express server (`app.py`/`app.js`) and a data layer (`models.py`/`models.js`) with sample users.

**Your task:** Implement the route handlers in `routes.py`/`routes.js` to complete the API.

### Requirements

1. `GET /api/users` — return a JSON array of all users.
2. `POST /api/users` — accept a JSON body `{ "name": "...", "email": "..." }`, create a new user, and return it with HTTP 201.
3. `GET /api/users/<id>` — return the user with the given id, or HTTP 404 if not found.

### File Overview

| File | Purpose |
|---|---|
| `app.py` / `app.js` | Server entry point — **do not modify** |
| `models.py` / `models.js` | Data layer with sample users — **do not modify** |
| `routes.py` / `routes.js` | Route handlers — **implement here** |
""",
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(new[] { "python", "javascript" }),
            StarterCodeJson = JsonDocumentSerializer.Serialize(StarterFiles(
                python: new Dictionary<string, string>
                {
                    ["app.py"] = """
from flask import Flask
from routes import register_routes

app = Flask(__name__)
register_routes(app)

if __name__ == '__main__':
    app.run(port=5000)
""",
                    ["models.py"] = """
users = [
    {"id": 1, "name": "Alice", "email": "alice@example.com"},
    {"id": 2, "name": "Bob", "email": "bob@example.com"},
]

def get_all_users():
    return list(users)

def add_user(name, email):
    new_id = max(u["id"] for u in users) + 1 if users else 1
    user = {"id": new_id, "name": name, "email": email}
    users.append(user)
    return user

def find_user(user_id):
    return next((u for u in users if u["id"] == user_id), None)
""",
                    ["routes.py"] = """
from flask import jsonify, request
from models import get_all_users, add_user, find_user

def register_routes(app):
    @app.route('/api/health')
    def health():
        return jsonify({'status': 'ok'})

    # TODO: Add GET /api/users — return all users as JSON array
    # TODO: Add POST /api/users — create user from JSON body, return 201
    # TODO: Add GET /api/users/<int:user_id> — return one user or 404
"""
                },
                javascript: new Dictionary<string, string>
                {
                    ["app.js"] = """
const express = require('express');
const { registerRoutes } = require('./routes');

const app = express();
app.use(express.json());
registerRoutes(app);

module.exports = app;
""",
                    ["models.js"] = """
const users = [
  { id: 1, name: 'Alice', email: 'alice@example.com' },
  { id: 2, name: 'Bob', email: 'bob@example.com' },
];

function getAllUsers() { return [...users]; }

function addUser(name, email) {
  const newId = users.length > 0 ? Math.max(...users.map(u => u.id)) + 1 : 1;
  const user = { id: newId, name, email };
  users.push(user);
  return user;
}

function findUser(id) { return users.find(u => u.id === id) || null; }

module.exports = { getAllUsers, addUser, findUser };
""",
                    ["routes.js"] = """
const { getAllUsers, addUser, findUser } = require('./models');

function registerRoutes(app) {
  app.get('/api/health', (req, res) => {
    res.json({ status: 'ok' });
  });

  // TODO: Add GET /api/users — return all users as JSON array
  // TODO: Add POST /api/users — create user from JSON body, return 201
  // TODO: Add GET /api/users/:id — return one user or 404
}

module.exports = { registerRoutes };
"""
                })),
            SortOrder = 1,
            MaxScore = 50,
            TestCases =
            [
                new TestCase
                {
                    Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                    Name = "GET /api/users returns a JSON array",
                    Visibility = TestCaseVisibilities.Public,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        python: """
import app as solution_app

def test_get_users():
    client = solution_app.app.test_client()
    resp = client.get('/api/users')
    assert resp.status_code == 200
    data = resp.get_json()
    assert isinstance(data, list)
    assert len(data) >= 2
""",
                        javascript: """
const request = require('supertest');
const app = require('./app');

test('GET /api/users returns a JSON array', async () => {
  const res = await request(app).get('/api/users');
  expect(res.status).toBe(200);
  expect(Array.isArray(res.body)).toBe(true);
  expect(res.body.length).toBeGreaterThanOrEqual(2);
});
"""))
                },
                new TestCase
                {
                    Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                    Name = "POST /api/users creates a new user",
                    Visibility = TestCaseVisibilities.Hidden,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        python: """
import app as solution_app

def test_post_user():
    client = solution_app.app.test_client()
    resp = client.post('/api/users', json={'name': 'Charlie', 'email': 'charlie@example.com'})
    assert resp.status_code == 201
    data = resp.get_json()
    assert data['name'] == 'Charlie'
    assert 'id' in data
""",
                        javascript: """
const request = require('supertest');
const app = require('./app');

test('POST /api/users creates a new user', async () => {
  const res = await request(app)
    .post('/api/users')
    .send({ name: 'Charlie', email: 'charlie@example.com' });
  expect(res.status).toBe(201);
  expect(res.body.name).toBe('Charlie');
  expect(res.body).toHaveProperty('id');
});
"""))
                },
                new TestCase
                {
                    Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
                    Name = "GET /api/users/:id returns 404 for missing user",
                    Visibility = TestCaseVisibilities.Hidden,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        python: """
import app as solution_app

def test_user_not_found():
    client = solution_app.app.test_client()
    resp = client.get('/api/users/9999')
    assert resp.status_code == 404
""",
                        javascript: """
const request = require('supertest');
const app = require('./app');

test('GET /api/users/:id returns 404 for missing user', async () => {
  const res = await request(app).get('/api/users/9999');
  expect(res.status).toBe(404);
});
"""))
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
            Title = "Fix the Shopping Cart Module",
            TaskType = TaskTypes.BugFix,
            Difficulty = "easy",
            ProblemDescriptionMarkdown = """
## Task: Fix the Shopping Cart Module

A teammate wrote a shopping cart with a discount system, but customers are reporting incorrect totals. The project has two files and **both contain bugs**.

**Your task:** Find and fix the bugs in both files.

### File Overview

| File | Purpose | Bug hint |
|---|---|---|
| `cart.py` / `cart.js` | Cart class with `calculate_total()` and `calculate_total_with_discount()` | The total calculation uses the wrong operator |
| `discounts.py` / `discounts.js` | `apply_discount(total, percent)` helper | The discount is applied incorrectly |

### Expected Behavior

```
cart.add_item("Laptop", 999.99, 1)
cart.add_item("Mouse",   29.99, 2)
cart.calculate_total()                   # → 1059.97  (999.99×1 + 29.99×2)
cart.calculate_total_with_discount(10)   # → 953.973  (1059.97 × 0.90)
```
""",
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(new[] { "python", "javascript" }),
            StarterCodeJson = JsonDocumentSerializer.Serialize(StarterFiles(
                python: new Dictionary<string, string>
                {
                    ["cart.py"] = """
from discounts import apply_discount

class ShoppingCart:
    def __init__(self):
        self.items = []

    def add_item(self, name, price, quantity):
        self.items.append({'name': name, 'price': price, 'quantity': quantity})

    def calculate_total(self):
        total = 0
        for item in self.items:
            # BUG: something is wrong with this calculation
            total += item['price'] + item['quantity']
        return total

    def calculate_total_with_discount(self, discount_percent):
        total = self.calculate_total()
        return apply_discount(total, discount_percent)
""",
                    ["discounts.py"] = """
def apply_discount(total, discount_percent):
    # BUG: the discount is not calculated as a percentage
    if discount_percent > 0:
        return total - discount_percent
    return total
"""
                },
                javascript: new Dictionary<string, string>
                {
                    ["cart.js"] = """
const { applyDiscount } = require('./discounts');

class ShoppingCart {
  constructor() {
    this.items = [];
  }

  addItem(name, price, quantity) {
    this.items.push({ name, price, quantity });
  }

  calculateTotal() {
    let total = 0;
    for (const item of this.items) {
      // BUG: something is wrong with this calculation
      total += item.price + item.quantity;
    }
    return total;
  }

  calculateTotalWithDiscount(discountPercent) {
    const total = this.calculateTotal();
    return applyDiscount(total, discountPercent);
  }
}

module.exports = { ShoppingCart };
""",
                    ["discounts.js"] = """
function applyDiscount(total, discountPercent) {
  // BUG: the discount is not calculated as a percentage
  if (discountPercent > 0) {
    return total - discountPercent;
  }
  return total;
}

module.exports = { applyDiscount };
"""
                })),
            SortOrder = 2,
            MaxScore = 50,
            TestCases =
            [
                new TestCase
                {
                    Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),
                    Name = "basic cart total",
                    Visibility = TestCaseVisibilities.Public,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        python: """
from cart import ShoppingCart

def test_basic_total():
    cart = ShoppingCart()
    cart.add_item('Laptop', 999.99, 1)
    cart.add_item('Mouse', 29.99, 2)
    assert abs(cart.calculate_total() - 1059.97) < 0.01
""",
                        javascript: """
const { ShoppingCart } = require('./cart');

test('basic cart total', () => {
  const cart = new ShoppingCart();
  cart.addItem('Laptop', 999.99, 1);
  cart.addItem('Mouse', 29.99, 2);
  expect(cart.calculateTotal()).toBeCloseTo(1059.97);
});
"""))
                },
                new TestCase
                {
                    Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Name = "10% discount applied correctly",
                    Visibility = TestCaseVisibilities.Hidden,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        python: """
from cart import ShoppingCart

def test_discount():
    cart = ShoppingCart()
    cart.add_item('Book', 100.00, 1)
    result = cart.calculate_total_with_discount(10)
    assert abs(result - 90.00) < 0.01
""",
                        javascript: """
const { ShoppingCart } = require('./cart');

test('10% discount applied correctly', () => {
  const cart = new ShoppingCart();
  cart.addItem('Book', 100.00, 1);
  expect(cart.calculateTotalWithDiscount(10)).toBeCloseTo(90.00);
});
"""))
                },
                new TestCase
                {
                    Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    Name = "zero discount returns full total",
                    Visibility = TestCaseVisibilities.Hidden,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        python: """
from cart import ShoppingCart

def test_zero_discount():
    cart = ShoppingCart()
    cart.add_item('Book', 50.00, 2)
    result = cart.calculate_total_with_discount(0)
    assert abs(result - 100.00) < 0.01
""",
                        javascript: """
const { ShoppingCart } = require('./cart');

test('zero discount returns full total', () => {
  const cart = new ShoppingCart();
  cart.addItem('Book', 50.00, 2);
  expect(cart.calculateTotalWithDiscount(0)).toBeCloseTo(100.00);
});
"""))
                }
            ]
        };
    }

    private static Dictionary<string, Dictionary<string, string>> StarterFiles(
        Dictionary<string, string> python,
        Dictionary<string, string> javascript)
    {
        return new Dictionary<string, Dictionary<string, string>>
        {
            ["python"] = python,
            ["javascript"] = javascript
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
