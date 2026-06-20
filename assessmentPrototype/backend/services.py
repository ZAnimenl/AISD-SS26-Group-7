from typing import List

from repositories import TodoRepository
from schemas import TodoCreate, TodoResponse, TodoUpdate


class TodoService:
    def __init__(self, repository: TodoRepository | None = None):
        self.repository = repository or TodoRepository()

    def get_all_todos(self) -> List[TodoResponse]:
        return [TodoResponse.model_validate(todo) for todo in self.repository.find_all()]

    def get_todo_by_id(self, todo_id: int) -> TodoResponse:
        return TodoResponse.model_validate(self.repository.find_by_id(todo_id))

    def create_todo(self, todo_data: TodoCreate) -> TodoResponse:
        return TodoResponse.model_validate(
            self.repository.create(todo_data.title, todo_data.description or "")
        )

    def update_todo(self, todo_id: int, update_data: TodoUpdate) -> TodoResponse:
        todo = self.repository.find_by_id(todo_id)
        for field, value in update_data.model_dump(exclude_unset=True).items():
            setattr(todo, field, value)
        return TodoResponse.model_validate(self.repository.save(todo))

    def toggle_todo_completion(self, todo_id: int) -> TodoResponse:
        todo = self.repository.find_by_id(todo_id)
        todo.completed = not todo.completed
        return TodoResponse.model_validate(self.repository.save(todo))

    def delete_todo(self, todo_id: int) -> dict:
        self.repository.delete_by_id(todo_id)
        return {"message": f"Todo with id '{todo_id}' deleted"}

