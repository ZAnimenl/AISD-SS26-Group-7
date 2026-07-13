function parseTodoInput(input = {}) {
  if (!input || typeof input !== 'object' || Array.isArray(input)) {
    throw new TypeError('Todo input must be an object.');
  }

  const title = String(input.title ?? '').trim();
  if (!title) {
    throw new Error('Todo title is required.');
  }

  return {
    title,
    description: String(input.description ?? '').trim(),
    completed: Boolean(input.completed)
  };
}

module.exports = { parseTodoInput };
