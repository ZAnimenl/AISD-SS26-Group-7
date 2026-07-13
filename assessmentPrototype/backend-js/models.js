const { parseTodoInput } = require('./schemas.js');

function createTodo(id, input) {
  return { id: String(id), ...parseTodoInput(input) };
}

module.exports = { createTodo, normalizeTodoInput: parseTodoInput };
