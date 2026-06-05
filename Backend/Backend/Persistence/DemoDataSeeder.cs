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
            Title = "Product Dashboard Assessment",
            Description = "Build and fix features for the Product Dashboard web application. Use the Preview panel to see your app come alive as you pass each test.",
            DurationMinutes = 60,
            Status = AssessmentStatuses.Active,
            AiEnabled = true,
            SharedPrototypeReference = "product-dashboard",
            SharedPrototypeVersion = "demo-v1",
            SharedPrototypeMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["display_name"] = "Product Dashboard",
                ["student_setup"] = "platform_native"
            }),
            CreatedAt = now
        };

        assessment.Questions.Add(CreateProductApiTask());
        assessment.Questions.Add(CreatePriceCalculationBugFixTask());

        dbContext.Assessments.Add(assessment);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────
    // Task 1 — Backend: Product API for the Dashboard
    // ──────────────────────────────────────────────────────────────
    private Question CreateProductApiTask()
    {
        return new Question
        {
            Id = ApiTaskId,
            AssessmentId = WebDevAssessmentId,
            Title = "Implement the Product API",
            TaskType = TaskTypes.RestApiDevelopment,
            Difficulty = "medium",
            VerificationMode = VerificationModes.ApiResponseCheck,
            StarterPrototypeReference = "product-dashboard",
            StarterFilesMetadataJson = JsonDocumentSerializer.Serialize(TaskStarterMetadata("routes.py", "routes.js")),
            VerificationMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["primary_view"] = "api_response_check",
                ["endpoint"] = "/api/products"
            }),
            GradingConfigurationJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["runner"] = "automated_tests"
            }),
            ProblemDescriptionMarkdown = """
## Task: Implement the Product API

The Product Dashboard needs a backend API to serve product data. The project has an app entry point and a data layer already set up.

**Your task:** Implement the route handlers in `routes.py` / `routes.js`.

### Requirements

1. `GET /api/products` — return a JSON array of all products.
2. `POST /api/products` — accept `{ "name", "price", "stock", "category" }`, create and return the product with HTTP 201.
3. `GET /api/products/<id>` — return a single product, or HTTP 404.

### File Overview

| File | Purpose |
|---|---|
| `app.py` / `app.js` | Server entry point — **do not modify** |
| `models.py` / `models.js` | Product data layer with sample data — **do not modify** |
| `routes.py` / `routes.js` | Route handlers — **implement here** |

### Hint

Check the Preview panel below to see the dashboard update as your tests pass.
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
products = [
    {"id": 1, "name": "Wireless Keyboard", "price": 49.99, "stock": 24, "category": "Electronics"},
    {"id": 2, "name": "USB-C Hub", "price": 35.00, "stock": 0, "category": "Electronics"},
    {"id": 3, "name": "Monitor Stand", "price": 79.99, "stock": 12, "category": "Furniture"},
    {"id": 4, "name": "Desk Lamp", "price": 22.50, "stock": 8, "category": "Furniture"},
    {"id": 5, "name": "Webcam HD", "price": 64.99, "stock": 3, "category": "Electronics"},
]

def get_all_products():
    return list(products)

def add_product(name, price, stock, category):
    new_id = max(p["id"] for p in products) + 1 if products else 1
    product = {"id": new_id, "name": name, "price": price, "stock": stock, "category": category}
    products.append(product)
    return product

def find_product(product_id):
    return next((p for p in products if p["id"] == product_id), None)
""",
                    ["routes.py"] = """
from flask import jsonify, request
from models import get_all_products, add_product, find_product

def register_routes(app):
    @app.route('/api/health')
    def health():
        return jsonify({'status': 'ok'})

    # TODO: GET /api/products — return all products as JSON array
    # TODO: POST /api/products — create product from JSON body, return 201
    # TODO: GET /api/products/<int:product_id> — return one product or 404
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
const products = [
  { id: 1, name: 'Wireless Keyboard', price: 49.99, stock: 24, category: 'Electronics' },
  { id: 2, name: 'USB-C Hub', price: 35.00, stock: 0, category: 'Electronics' },
  { id: 3, name: 'Monitor Stand', price: 79.99, stock: 12, category: 'Furniture' },
  { id: 4, name: 'Desk Lamp', price: 22.50, stock: 8, category: 'Furniture' },
  { id: 5, name: 'Webcam HD', price: 64.99, stock: 3, category: 'Electronics' },
];

function getAllProducts() { return [...products]; }

function addProduct(name, price, stock, category) {
  const newId = products.length > 0 ? Math.max(...products.map(p => p.id)) + 1 : 1;
  const product = { id: newId, name, price, stock, category };
  products.push(product);
  return product;
}

function findProduct(id) { return products.find(p => p.id === id) || null; }

module.exports = { getAllProducts, addProduct, findProduct };
""",
                    ["routes.js"] = """
const { getAllProducts, addProduct, findProduct } = require('./models');

function registerRoutes(app) {
  app.get('/api/health', (req, res) => {
    res.json({ status: 'ok' });
  });

  // TODO: GET /api/products — return all products as JSON array
  // TODO: POST /api/products — create product from JSON body, return 201
  // TODO: GET /api/products/:id — return one product or 404
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
                    Name = "GET /api/products returns product list array",
                    Visibility = TestCaseVisibilities.Public,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        python: """
import app as solution_app

def test_get_products():
    client = solution_app.app.test_client()
    resp = client.get('/api/products')
    assert resp.status_code == 200
    data = resp.get_json()
    assert isinstance(data, list)
    assert len(data) >= 5
""",
                        javascript: """
const request = require('supertest');
const app = require('./app');

test('GET /api/products returns product list array', async () => {
  const res = await request(app).get('/api/products');
  expect(res.status).toBe(200);
  expect(Array.isArray(res.body)).toBe(true);
  expect(res.body.length).toBeGreaterThanOrEqual(5);
});
"""))
                },
                new TestCase
                {
                    Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                    Name = "POST /api/products creates a new product",
                    Visibility = TestCaseVisibilities.Hidden,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        python: """
import app as solution_app

def test_create_product():
    client = solution_app.app.test_client()
    resp = client.post('/api/products', json={
        'name': 'Standing Desk', 'price': 299.99, 'stock': 5, 'category': 'Furniture'
    })
    assert resp.status_code == 201
    data = resp.get_json()
    assert data['name'] == 'Standing Desk'
    assert 'id' in data
""",
                        javascript: """
const request = require('supertest');
const app = require('./app');

test('POST /api/products creates a new product', async () => {
  const res = await request(app)
    .post('/api/products')
    .send({ name: 'Standing Desk', price: 299.99, stock: 5, category: 'Furniture' });
  expect(res.status).toBe(201);
  expect(res.body.name).toBe('Standing Desk');
  expect(res.body).toHaveProperty('id');
});
"""))
                },
                new TestCase
                {
                    Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
                    Name = "GET /api/products/:id returns 404 for missing product",
                    Visibility = TestCaseVisibilities.Hidden,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        python: """
import app as solution_app

def test_product_not_found():
    client = solution_app.app.test_client()
    resp = client.get('/api/products/9999')
    assert resp.status_code == 404
""",
                        javascript: """
const request = require('supertest');
const app = require('./app');

test('GET /api/products/:id returns 404 for missing product', async () => {
  const res = await request(app).get('/api/products/9999');
  expect(res.status).toBe(404);
});
"""))
                }
            ]
        };
    }

    // ──────────────────────────────────────────────────────────────
    // Task 2 — Bug Fix: Price calculation & discount for Dashboard
    // ──────────────────────────────────────────────────────────────
    private Question CreatePriceCalculationBugFixTask()
    {
        return new Question
        {
            Id = BugFixTaskId,
            AssessmentId = WebDevAssessmentId,
            Title = "Fix the Price Calculator",
            TaskType = TaskTypes.BugFix,
            Difficulty = "easy",
            VerificationMode = VerificationModes.RegressionTest,
            StarterPrototypeReference = "product-dashboard",
            StarterFilesMetadataJson = JsonDocumentSerializer.Serialize(TaskStarterMetadata("calculator.py", "calculator.js")),
            VerificationMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["primary_view"] = "regression_test",
                ["focus"] = "price_calculation"
            }),
            GradingConfigurationJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["runner"] = "automated_tests"
            }),
            ProblemDescriptionMarkdown = """
## Task: Fix the Price Calculator

The Product Dashboard has a price calculator module used to compute order totals and apply discounts. Customers are reporting incorrect totals. **Both files contain bugs.**

**Your task:** Find and fix the bugs in both files.

### File Overview

| File | Purpose | Bug hint |
|---|---|---|
| `calculator.py` / `calculator.js` | `calculate_order_total(items)` computes subtotal | Wrong arithmetic operator |
| `discounts.py` / `discounts.js` | `apply_discount(total, percent)` applies a percentage discount | Discount not applied as percentage |

### Expected Behavior

```
items = [
    { "name": "Keyboard", "price": 49.99, "quantity": 2 },
    { "name": "Mouse",    "price": 25.00, "quantity": 1 },
]
calculate_order_total(items)              # 124.98  (49.99*2 + 25.00*1)
calculate_discounted_total(items, 10)     # 112.482 (124.98 * 0.90)
```

Check the Preview panel to see the dashboard stats update as you fix each bug.
""",
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(new[] { "python", "javascript" }),
            StarterCodeJson = JsonDocumentSerializer.Serialize(StarterFiles(
                python: new Dictionary<string, string>
                {
                    ["calculator.py"] = """
from discounts import apply_discount

def calculate_order_total(items):
    total = 0
    for item in items:
        # BUG: wrong operator — should multiply price by quantity
        total += item['price'] + item['quantity']
    return total

def calculate_discounted_total(items, discount_percent):
    total = calculate_order_total(items)
    return apply_discount(total, discount_percent)
""",
                    ["discounts.py"] = """
def apply_discount(total, discount_percent):
    # BUG: subtracts the raw number instead of calculating percentage
    if discount_percent > 0:
        return total - discount_percent
    return total
"""
                },
                javascript: new Dictionary<string, string>
                {
                    ["calculator.js"] = """
const { applyDiscount } = require('./discounts');

function calculateOrderTotal(items) {
  let total = 0;
  for (const item of items) {
    // BUG: wrong operator — should multiply price by quantity
    total += item.price + item.quantity;
  }
  return total;
}

function calculateDiscountedTotal(items, discountPercent) {
  const total = calculateOrderTotal(items);
  return applyDiscount(total, discountPercent);
}

module.exports = { calculateOrderTotal, calculateDiscountedTotal };
""",
                    ["discounts.js"] = """
function applyDiscount(total, discountPercent) {
  // BUG: subtracts the raw number instead of calculating percentage
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
                    Name = "calculate_order_total returns correct total for product list",
                    Visibility = TestCaseVisibilities.Public,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        python: """
from calculator import calculate_order_total

def test_order_total():
    items = [
        {'name': 'Keyboard', 'price': 49.99, 'quantity': 2},
        {'name': 'Mouse', 'price': 25.00, 'quantity': 1},
    ]
    assert abs(calculate_order_total(items) - 124.98) < 0.01
""",
                        javascript: """
const { calculateOrderTotal } = require('./calculator');

test('calculate_order_total returns correct total for product list', () => {
  const items = [
    { name: 'Keyboard', price: 49.99, quantity: 2 },
    { name: 'Mouse', price: 25.00, quantity: 1 },
  ];
  expect(calculateOrderTotal(items)).toBeCloseTo(124.98);
});
"""))
                },
                new TestCase
                {
                    Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Name = "10% discount applied correctly to product stats",
                    Visibility = TestCaseVisibilities.Hidden,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        python: """
from calculator import calculate_discounted_total

def test_discount():
    items = [{'name': 'Monitor', 'price': 200.00, 'quantity': 1}]
    result = calculate_discounted_total(items, 10)
    assert abs(result - 180.00) < 0.01
""",
                        javascript: """
const { calculateDiscountedTotal } = require('./calculator');

test('10% discount applied correctly to product stats', () => {
  const items = [{ name: 'Monitor', price: 200.00, quantity: 1 }];
  expect(calculateDiscountedTotal(items, 10)).toBeCloseTo(180.00);
});
"""))
                },
                new TestCase
                {
                    Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    Name = "zero discount displays full total in dashboard",
                    Visibility = TestCaseVisibilities.Hidden,
                    TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(
                        python: """
from calculator import calculate_discounted_total

def test_zero_discount():
    items = [{'name': 'Lamp', 'price': 22.50, 'quantity': 4}]
    result = calculate_discounted_total(items, 0)
    assert abs(result - 90.00) < 0.01
""",
                        javascript: """
const { calculateDiscountedTotal } = require('./calculator');

test('zero discount displays full total in dashboard', () => {
  const items = [{ name: 'Lamp', price: 22.50, quantity: 4 }];
  expect(calculateDiscountedTotal(items, 0)).toBeCloseTo(90.00);
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

    private static Dictionary<string, Dictionary<string, string>> TaskStarterMetadata(string pythonEditableFile, string javascriptEditableFile)
    {
        return new Dictionary<string, Dictionary<string, string>>
        {
            ["python"] = new Dictionary<string, string>
            {
                [pythonEditableFile] = "editable"
            },
            ["javascript"] = new Dictionary<string, string>
            {
                [javascriptEditableFile] = "editable"
            }
        };
    }
}
