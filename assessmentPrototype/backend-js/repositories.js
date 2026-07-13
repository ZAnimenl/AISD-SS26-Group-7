const fs = require('fs');
const path = require('path');
const { loadEnvironment } = require('./environment.js');
const { createTodo } = require('./models.js');

class TodoRepository {
  constructor(filePath = loadEnvironment().databasePath) {
    this.filePath = filePath;
  }

  list() {
    return this.#read();
  }

  find(id) {
    return this.#read().find((todo) => todo.id === String(id)) ?? null;
  }

  create(input) {
    const todos = this.#read();
    const nextId = String(todos.reduce((maximum, todo) => Math.max(maximum, Number(todo.id) || 0), 0) + 1);
    const todo = createTodo(nextId, input);
    this.#write([...todos, todo]);
    return todo;
  }

  replace(todo) {
    const todos = this.#read();
    const index = todos.findIndex((item) => item.id === String(todo.id));
    if (index < 0) return null;
    todos[index] = todo;
    this.#write(todos);
    return todo;
  }

  delete(id) {
    const todos = this.#read();
    const remaining = todos.filter((todo) => todo.id !== String(id));
    if (remaining.length === todos.length) return false;
    this.#write(remaining);
    return true;
  }

  #read() {
    if (!fs.existsSync(this.filePath)) return [];
    const content = fs.readFileSync(this.filePath, 'utf8').trim();
    return content ? JSON.parse(content) : [];
  }

  #write(todos) {
    fs.mkdirSync(path.dirname(this.filePath), { recursive: true });
    const temporaryPath = `${this.filePath}.tmp`;
    fs.writeFileSync(temporaryPath, JSON.stringify(todos, null, 2));
    fs.renameSync(temporaryPath, this.filePath);
  }
}

module.exports = { TodoRepository };
