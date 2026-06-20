from typing import List

from models import Todo


class TodoNotFoundException(Exception):
    pass


class TodoRepository:
    @staticmethod
    def find_all() -> List[Todo]:
        return list(Todo.select())

    @staticmethod
    def find_by_id(todo_id: int) -> Todo:
        try:
            return Todo.get(Todo.id == todo_id)
        except Todo.DoesNotExist as exception:
            raise TodoNotFoundException(f"Todo with id '{todo_id}' not found") from exception

    @staticmethod
    def create(title: str, description: str = "") -> Todo:
        return Todo.create(title=title, description=description)

    @staticmethod
    def save(todo: Todo) -> Todo:
        todo.save()
        return todo

    @staticmethod
    def delete_by_id(todo_id: int) -> bool:
        TodoRepository.find_by_id(todo_id).delete_instance()
        return True

