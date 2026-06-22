# Canonical Todo Assessment Prototype

This directory is the source-only canonical prototype used to define assessment starter files and contracts. It is strictly separate from the Next.js/.NET platform and runs only inside the sandbox.

Stack:

- UI: browser-safe HTML, CSS, and JavaScript adapted from the real Todo UI
- REST API: FastAPI
- ORM: Peewee
- Database: SQLite

The `frontend/`, `backend/`, and `database/` source files are packaged with the
API and copied directly into generated starter workspaces. The LLM supplies the
task description, tests, and optional task-specific files; it does not replace
the canonical base application.

Students do not install or start this project locally. Generated tasks may extend these files and contracts, but may not replace the base application with a different product.

API contract:

- `GET /api/todos`
- `GET /api/todos/{todo_id}`
- `POST /api/todos`
- `PUT /api/todos/{todo_id}`
- `POST /api/todos/{todo_id}/toggle`
- `DELETE /api/todos/{todo_id}`
