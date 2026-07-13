const express = require('express');
const { createTodoRouter } = require('./controllers.js');
const { loadEnvironment } = require('./environment.js');
const { TodoRepository } = require('./repositories.js');
const { TodoService } = require('./services.js');

function createApp(repository = new TodoRepository()) {
  const app = express();
  app.use(express.json());
  app.use('/api/todos', createTodoRouter(new TodoService(repository)));
  return app;
}

if (require.main === module) {
  createApp().listen(loadEnvironment().port);
}

module.exports = { createApp };
