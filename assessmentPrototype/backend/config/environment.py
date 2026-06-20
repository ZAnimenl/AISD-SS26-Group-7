import os
from pathlib import Path

APP_TITLE = "Todo List API"
CORS_ORIGINS = ["http://localhost:5173"]
DATABASE_PATH = os.getenv(
    "TODO_DATABASE_PATH",
    str(Path(__file__).resolve().parents[1] / "todos.db"),
)

