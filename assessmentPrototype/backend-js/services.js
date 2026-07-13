const { createTodo } = require('./models.js');

class TodoService {
  constructor(repository) {
    this.repository = repository;
  }

  listTodos() {
    return this.repository.list();
  }

  getTodo(id) {
    return this.repository.find(id);
  }

  createTodo(input) {
    return this.repository.create(input);
  }

  updateTodo(id, input) {
    const current = this.repository.find(id);
    if (!current) return null;
    return this.repository.replace(createTodo(current.id, { ...current, ...input }));
  }

  toggleTodo(id) {
    const current = this.repository.find(id);
    if (!current) return null;
    return this.repository.replace({ ...current, completed: !current.completed });
  }

  deleteTodo(id) {
    return this.repository.delete(id);
  }
}

module.exports = { TodoService };
