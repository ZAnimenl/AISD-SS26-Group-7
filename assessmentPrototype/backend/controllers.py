from fastapi import HTTPException

from repositories import TodoNotFoundException
from schemas import TodoCreate, TodoUpdate
from services import TodoService


class TodoController:
    def __init__(self, service: TodoService | None = None):
        self.service = service or TodoService()

    def get_all_todos(self):
        return self.service.get_all_todos()

    def get_todo(self, todo_id: int):
        return self._translate_not_found(self.service.get_todo_by_id, todo_id)

    def create_todo(self, todo: TodoCreate):
        return self.service.create_todo(todo)

    def update_todo(self, todo_id: int, update: TodoUpdate):
        return self._translate_not_found(self.service.update_todo, todo_id, update)

    def toggle_todo_completion(self, todo_id: int):
        return self._translate_not_found(self.service.toggle_todo_completion, todo_id)

    def delete_todo(self, todo_id: int):
        return self._translate_not_found(self.service.delete_todo, todo_id)

    @staticmethod
    def _translate_not_found(callback, *args):
        try:
            return callback(*args)
        except TodoNotFoundException as exception:
            raise HTTPException(status_code=404, detail=str(exception)) from exception

