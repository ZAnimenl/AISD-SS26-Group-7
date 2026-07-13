const path = require('path');

function loadEnvironment(environment = process.env, workingDirectory = process.cwd()) {
  const configuredPort = Number.parseInt(environment.PORT ?? '', 10);
  return {
    appTitle: 'Todo List API',
    port: Number.isInteger(configuredPort) && configuredPort > 0 ? configuredPort : 3000,
    databasePath: environment.TODO_DATABASE_PATH || path.join(workingDirectory, 'todos.json')
  };
}

module.exports = { loadEnvironment };
