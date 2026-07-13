const express = require('express');

function createTodoRouter(service) {
  const router = express.Router();

  router.get('/', (_request, response) => response.json(service.listTodos()));
  router.get('/:todoId', (request, response) => {
    const todo = service.getTodo(request.params.todoId);
    return todo ? response.json(todo) : response.status(404).json({ error: 'Todo not found.' });
  });
  router.post('/', (request, response) => response.status(201).json(service.createTodo(request.body)));
  router.put('/:todoId', (request, response) => {
    const todo = service.updateTodo(request.params.todoId, request.body);
    return todo ? response.json(todo) : response.status(404).json({ error: 'Todo not found.' });
  });
  router.post('/:todoId/toggle', (request, response) => {
    const todo = service.toggleTodo(request.params.todoId);
    return todo ? response.json(todo) : response.status(404).json({ error: 'Todo not found.' });
  });
  router.delete('/:todoId', (request, response) => service.deleteTodo(request.params.todoId)
    ? response.status(204).end()
    : response.status(404).json({ error: 'Todo not found.' }));

  return router;
}

module.exports = { createTodoRouter };
