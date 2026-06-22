from contextlib import asynccontextmanager
from typing import List

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

try:
    from config.environment import APP_TITLE, CORS_ORIGINS
except ModuleNotFoundError:
    from environment import APP_TITLE, CORS_ORIGINS
from controllers import TodoController
from models import Todo, db
from schemas import TodoCreate, TodoResponse, TodoUpdate


@asynccontextmanager
async def lifespan(_app: FastAPI):
    if db.is_closed():
        db.connect()
    db.create_tables([Todo], safe=True)
    yield
    if not db.is_closed():
        db.close()


app = FastAPI(title=APP_TITLE, lifespan=lifespan)
app.add_middleware(
    CORSMiddleware,
    allow_origins=CORS_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)
todo_controller = TodoController()


@app.get("/")
def read_root():
    return {"message": "Todo API", "version": "1.0.0"}


@app.get("/api/todos", response_model=List[TodoResponse])
def get_all_todos():
    return todo_controller.get_all_todos()


@app.get("/api/todos/{todo_id}", response_model=TodoResponse)
def get_todo(todo_id: int):
    return todo_controller.get_todo(todo_id)


@app.post("/api/todos", response_model=TodoResponse)
def create_todo(todo: TodoCreate):
    return todo_controller.create_todo(todo)


@app.put("/api/todos/{todo_id}", response_model=TodoResponse)
def update_todo(todo_id: int, update: TodoUpdate):
    return todo_controller.update_todo(todo_id, update)


@app.post("/api/todos/{todo_id}/toggle", response_model=TodoResponse)
def toggle_todo_completion(todo_id: int):
    return todo_controller.toggle_todo_completion(todo_id)


@app.delete("/api/todos/{todo_id}")
def delete_todo(todo_id: int):
    return todo_controller.delete_todo(todo_id)
